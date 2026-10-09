using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using PhotoStudio.Api.Endpoints;
using PhotoStudio.Api.Errors;
using PhotoStudio.Api.RateLimiting;
using PhotoStudio.Application;
using PhotoStudio.Infrastructure;
using PhotoStudio.Infrastructure.Identity;

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
builder.Services.AddInfrastructure();
builder.Services.AddPhotographerAuthentication(builder.Environment.IsDevelopment());
builder.Services.AddProblemDetails();
builder.Services.AddApiRateLimiting();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

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
app.MapBookingEndpoints();
app.MapNotificationEndpoints();
app.MapMaintenanceEndpoints();

await app.RunAsync();

/// <summary>
/// Entry point type, made visible so integration tests can start the API in memory.
/// </summary>
public partial class Program;
