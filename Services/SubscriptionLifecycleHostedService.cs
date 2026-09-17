using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GenericTestWebApi.Services;

public class SubscriptionLifecycleHostedService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionLifecycleHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var lifecycle = scope.ServiceProvider.GetRequiredService<SubscriptionLifecycleService>();
            var count = await lifecycle.ExpireSubscriptionsAsync(cancellationToken);
            if (count > 0) logger.LogInformation("Expired {Count} subscription(s) during lifecycle reconciliation.", count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Subscription lifecycle reconciliation failed.");
        }
    }
}
