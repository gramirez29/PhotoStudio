using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PhotoStudio.Api.Endpoints;
using PhotoStudio.Api.Errors;
using PhotoStudio.Application;
using PhotoStudio.Infrastructure;

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

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

// The OpenAPI document is the source for the generated TypeScript types of the mobile app and the client portal.
app.MapOpenApi();

// Liveness: the process answers. Readiness: dependencies (MongoDB) answer.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(PhotoStudio.Infrastructure.DependencyInjection.ReadinessTag),
});

app.MapBookingEndpoints();

await app.RunAsync();
