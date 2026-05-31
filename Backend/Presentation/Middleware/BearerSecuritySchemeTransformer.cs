using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Presentation.Middleware;

public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT access token",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = scheme;

        document.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                [new OpenApiSecuritySchemeReference("Bearer")] = new List<string>()
            }
        };

        return Task.CompletedTask;
    }
}
