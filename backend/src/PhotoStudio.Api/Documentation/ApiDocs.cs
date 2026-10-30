using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PhotoStudio.Api.Documentation;

/// <summary>
/// The API documentation: the OpenAPI document and Swagger UI, a page to try every endpoint by hand. They show the whole surface
/// of the API and let anyone who pastes a token call it, so they are on where the developer works and off where real
/// customers' data lives: on in Development and wherever <c>API_DOCS_ENABLED=true</c> (the develop environment), off otherwise,
/// and <c>API_DOCS_ENABLED=false</c> turns them off even in Development. When they are off the routes do not exist.
/// </summary>
public static class ApiDocs
{
    /// <summary>
    /// Environment variable (or configuration key) that turns the documentation on or off, overriding the default.
    /// </summary>
    public const string EnabledVariable = "API_DOCS_ENABLED";

    /// <summary>
    /// Name of the security scheme that describes the access token of the photographer.
    /// </summary>
    public const string BearerScheme = "Bearer";

    /// <summary>
    /// Address of the OpenAPI document.
    /// </summary>
    public const string DocumentPath = "/openapi/v1.json";

    /// <summary>
    /// Decides whether the documentation is served.
    /// </summary>
    /// <param name="environment">Environment the API runs in.</param>
    /// <param name="configuration">Configuration, which includes the environment variables.</param>
    /// <returns><see langword="true"/> when the variable says "true"; <see langword="false"/> when it says "false"; otherwise
    /// only in Development. Any other text counts as off, so a typo never opens it.</returns>
    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[EnabledVariable];
        if (string.IsNullOrWhiteSpace(value))
        {
            return environment.IsDevelopment();
        }

        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registers the OpenAPI document with the description of how the endpoints are secured: a Bearer token with a JWT, on every
    /// operation that requires a signed-in user and on none that is public.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApiDocs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "PhotoSense Studio API";
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token of the photographer: the accessToken that POST /api/auth/login answers with. Paste only the token, without the word Bearer.",
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [] });
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>
    /// Serves Swagger UI at <c>/swagger</c> when the documentation is on, and nothing when it is off. It is middleware and it must
    /// come before authorization: the fallback policy asks for a signed-in user on every request, and the page that is used to
    /// get a token cannot ask for one. The UI is served from the package itself, with no script or style from another site, and
    /// it does not remember the token.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication UseApiDocsUi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!IsEnabled(app.Environment, app.Configuration))
        {
            return app;
        }

        app.UseSwaggerUI(options =>
        {
            options.DocumentTitle = "PhotoSense Studio API";
            options.SwaggerEndpoint(DocumentPath, "PhotoSense Studio API v1");
            options.DisplayRequestDuration();
        });

        return app;
    }

    /// <summary>
    /// Serves the OpenAPI document, without asking for a token, when the documentation is on, and nothing when it is off.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication MapApiDocument(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (IsEnabled(app.Environment, app.Configuration))
        {
            app.MapOpenApi().AllowAnonymous();
        }

        return app;
    }
}
