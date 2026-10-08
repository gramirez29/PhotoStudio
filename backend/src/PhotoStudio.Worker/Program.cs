using PhotoStudio.Application;
using PhotoStudio.Infrastructure;
using PhotoStudio.Worker;

// Composition root of the PhotoStudio worker. Configuration comes from environment variables (Railway service variables).
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHostedService<OutboxDispatcherService>();
builder.Services.AddHostedService<BookingExpirationService>();

await builder.Build().RunAsync();
