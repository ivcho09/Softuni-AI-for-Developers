using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// --- Services -------------------------------------------------------------
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
