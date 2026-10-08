using PhotoStudio.Application;
using PhotoStudio.Infrastructure;
using PhotoStudio.Worker;

// Composition root of the PhotoStudio worker. Configuration comes from environment variables (Railway service variables).
// Two modes: with WORKER_RUN_ONCE=true it performs one maintenance pass and exits (scheduled job); otherwise it stays
// alive and polls the outbox and the expiration job.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var runOnce = RunOnceJob.IsEnabled();
if (!runOnce)
{
    builder.Services.AddHostedService<OutboxDispatcherService>();
    builder.Services.AddHostedService<BookingExpirationService>();
}

using var host = builder.Build();

if (!runOnce)
{
    await host.RunAsync();
    return RunOnceJob.SuccessExitCode;
}

// Starting the host makes the hosted services run once, which ensures the MongoDB indexes before the pass.
await host.StartAsync();
var exitCode = await RunOnceJob.RunAsync(host.Services, host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
await host.StopAsync();
return exitCode;
