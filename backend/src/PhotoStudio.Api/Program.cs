using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using PhotoStudio.Api.Cors;
using PhotoStudio.Api.Endpoints;
using PhotoStudio.Api.Errors;
using PhotoStudio.Api.Maintenance;
using PhotoStudio.Api.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhotoStudio.Application;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Infrastructure;
using PhotoStudio.Infrastructure.Identity;
using PhotoStudio.Infrastructure.Portal;
using PhotoStudio.Infrastructure.Storage;

// Composition root of the PhotoStudio API. Configuration comes from environment variables (Railway service variables).
var builder = WebApplication.CreateBuilder(args);

// Railway injects the port to listen on through the PORT variable; the container default is 8080.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Enums travel as strings ("Tentative", "Cash") so the TypeScript types can mirror them as string unions.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Behind Railway's proxy the socket address is the proxy's, so the real client address (used by the per-address rate limit
// on sign-in) comes from X-Forwarded-For. Only the last proxy hop is trusted.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddApplication();
builder.Services.AddApplicationAuthentication();
builder.Services.AddApplicationPortal();
builder.Services.AddInfrastructure();
builder.Services.AddObjectStorage();
// This process delivers the outbox soon after a booking changes (see OutboxWakeUpService); the worker keeps doing it on a schedule.
builder.Services.AddSingleton<OutboxWakeUpService>();
builder.Services.Replace(ServiceDescriptor.Singleton<IOutboxWakeUp>(provider => provider.GetRequiredService<OutboxWakeUpService>()));
builder.Services.AddHostedService(provider => provider.GetRequiredService<OutboxWakeUpService>());
builder.Services.AddPhotographerAuthentication(builder.Environment.IsDevelopment());
builder.Services.AddClientPortal(builder.Environment.IsDevelopment());
builder.Services.AddPortalCors(builder.Environment.IsDevelopment());
builder.Services.AddProblemDetails();
builder.Services.AddApiRateLimiting();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseForwardedHeaders();

// A booking that changed may have raised events: ask for a delivery pass once the response is final (so a failed request, whose
// change was not saved, asks for nothing). Only the booking endpoints change bookings; sign-in and the like are left out.
app.Use(async (context, next) =>
{
    await next(context);

    var changesBookings = context.Request.Path.StartsWithSegments("/api/bookings") || context.Request.Path.StartsWithSegments("/public/bookings");
    if (changesBookings && !HttpMethods.IsGet(context.Request.Method) && context.Response.StatusCode < StatusCodes.Status400BadRequest)
    {
        context.RequestServices.GetRequiredService<IOutboxWakeUp>().Signal();
    }
});

// Before everything that can answer: a browser asks (preflight) before sending a request, and the answer must not need a
// token. The policy itself is attached only to the portal endpoints.
app.UseCors();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// After authorization on purpose: a request that is refused (no token, no system role) must not use up the shared permit of
// an endpoint such as the maintenance pass, or any user could lock the operator out of it.
app.UseRateLimiter();

// Every endpoint requires a signed-in photographer by default (fallback policy); the ones below opt out explicitly.
// The OpenAPI document is the source for the generated TypeScript types of the mobile app and the client portal.
app.MapOpenApi().AllowAnonymous();

// Liveness: the process answers. Readiness: dependencies (MongoDB) answer. Railway probes them without credentials.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(PhotoStudio.Infrastructure.DependencyInjection.ReadinessTag),
}).AllowAnonymous();

app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapBookingEndpoints();
app.MapBookingPortalEndpoints();
app.MapPortalEndpoints();
app.MapNotificationEndpoints();
app.MapPushDeviceEndpoints();
app.MapBillingEndpoints();
app.MapGalleryEndpoints();
app.MapMaintenanceEndpoints();

await app.RunAsync();

/// <summary>
/// Entry point type, made visible so integration tests can start the API in memory.
/// </summary>
public partial class Program;
