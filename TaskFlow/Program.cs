using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using TaskFlow.Data;

var builder = WebApplication.CreateBuilder(args);

// --- Services -------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("TaskFlowDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'TaskFlowDatabase' was not found in configuration.");

builder.Services.AddDbContext<TaskFlowDbContext>(options =>
    options.UseSqlite(connectionString));

// Injected into services so that "now" can be controlled in unit tests.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddControllers();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "TaskFlow API",
            Version = "v1",
            Description = "A personal productivity REST API for managing tasks, projects and productivity statistics."
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// --- HTTP pipeline --------------------------------------------------------
app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "TaskFlow API v1");
    options.DocumentTitle = "TaskFlow API";
});

app.MapControllers();

app.Run();
