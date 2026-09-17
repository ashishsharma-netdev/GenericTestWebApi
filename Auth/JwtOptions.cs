namespace GenericTestWebApi.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "GenericTestWebApi";
    public string Audience { get; set; } = "GenericTestWebApp";
    public string Key { get; set; } = "CHANGE_THIS_DEVELOPMENT_KEY_TO_A_LONG_RANDOM_SECRET_123456789";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
}