using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GenericTestWebApi.Payments;

public sealed class RazorpayService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
{
    private string KeyId => configuration["Razorpay:KeyId"] ?? "";
    private string KeySecret => configuration["Razorpay:KeySecret"] ?? "";
    private string BaseUrl => configuration["Razorpay:BaseUrl"] ?? "https://api.razorpay.com/v1";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(KeySecret);
    public string PublicKeyId => KeyId;

    public async Task<RazorpayOrderResult> CreateOrderAsync(decimal amount, string currency, string receipt, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Razorpay is not configured on the server.");
        var client = httpClientFactory.CreateClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{KeyId}:{KeySecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        var payload = new { amount = (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero), currency, receipt };
        using var response = await client.PostAsJsonAsync($"{BaseUrl.TrimEnd('/')}/orders", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay order creation failed: {body}");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        return new RazorpayOrderResult(root.GetProperty("id").GetString()!, root.GetProperty("amount").GetInt64(), root.GetProperty("currency").GetString()!, root.GetProperty("status").GetString()!);
    }

    public bool VerifyPaymentSignature(string orderId, string paymentId, string signature) => FixedTimeEquals(HmacSha256Hex($"{orderId}|{paymentId}", KeySecret), signature);
    public bool VerifyWebhookSignature(string rawBody, string signature, string webhookSecret) => FixedTimeEquals(HmacSha256Hex(rawBody, webhookSecret), signature);

    private static string HmacSha256Hex(string message, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    }
    private static bool FixedTimeEquals(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(actual)) return false;
        var a = Encoding.UTF8.GetBytes(expected); var b = Encoding.UTF8.GetBytes(actual.Trim());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

public record RazorpayOrderResult(string Id, long Amount, string Currency, string Status);
