namespace SupportAgent.Api.Configuration;

/// <summary>
/// CORS configuration helpers for local frontend development.
/// </summary>
public static class CorsConfiguration
{
    /// <summary>
    /// The CORS policy name used during local React development.
    /// </summary>
    public const string DevelopmentPolicyName = "DevelopmentCors";

    /// <summary>
    /// Registers the development CORS policy so the React app on port 5173 can call the API.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddDevelopmentCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(DevelopmentPolicyName, policy =>
            {
                policy
                    .WithOrigins("http://localhost:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }
}
