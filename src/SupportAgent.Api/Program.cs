using System.Reflection;
using SupportAgent.Api.Configuration;
using SupportAgent.Api.Endpoints;
using SupportAgent.Infrastructure;
using SupportAgent.Infrastructure.AI;

// Application entry point.
// Registers services, maps API endpoints, and starts the web server.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "SupportAgent.NET",
        Version = "v1",
        Description = "Open-source AI customer support agent starter kit API."
    });

    var xmlFiles = new[]
    {
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml",
        $"{typeof(SupportAgent.Core.DTOs.HealthResponse).Assembly.GetName().Name}.xml",
        $"{typeof(SupportAgent.Infrastructure.DependencyInjection).Assembly.GetName().Name}.xml"
    };

    foreach (var xmlFile in xmlFiles)
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    }
});
builder.Services.AddDevelopmentCors();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAI(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SupportAgent.NET v1");
        options.RoutePrefix = "swagger";
    });
    app.MapOpenApi();
    app.UseCors(CorsConfiguration.DevelopmentPolicyName);
    await app.Services.SeedDevelopmentDataAsync();
}

// Map all HTTP endpoints.
app.MapHealthEndpoints();
app.MapCustomerEndpoints();
app.MapOrderEndpoints();
app.MapTicketEndpoints();
app.MapKnowledgeEndpoints();
app.MapAIEndpoints();
app.MapCopilotEndpoints();

app.Run();
