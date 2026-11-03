using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.DTOs.Common;
using TaskFlow.Interfaces;
using TaskFlow.Middleware;
using TaskFlow.OpenApi;
using TaskFlow.Services;
using TaskFlow.Validators;

var builder = WebApplication.CreateBuilder(args);

// --- Database -------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("TaskFlowDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'TaskFlowDatabase' was not found in configuration.");

builder.Services.AddDbContext<TaskFlowDbContext>(options =>
    options.UseSqlite(connectionString));

// --- Application services -------------------------------------------------
// Injected instead of calling DateTime.UtcNow directly, so overdue and statistics
// logic can be tested against a fixed clock.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

builder.Services.AddValidatorsFromAssemblyContaining<CreateTaskRequestValidator>();

// --- MVC ------------------------------------------------------------------
builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Enums travel as readable names ("Pending") rather than integers, and unknown
        // names are rejected with a message that lists the supported values.
        options.JsonSerializerOptions.Converters.Add(new StrictEnumConverterFactory());
    });

// Model binding and JSON parsing failures are reported in the same shape as
// FluentValidation failures, so clients only ever see one error contract.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ErrorResponse
    {
        Status = StatusCodes.Status400BadRequest,
        Message = ValidationErrorFormatter.ValidationFailedMessage,
        Errors = ValidationErrorFormatter.FromModelState(context.ModelState),
        TraceId = context.HttpContext.TraceIdentifier
    });
});

// --- OpenAPI --------------------------------------------------------------
builder.Services.AddTaskFlowOpenApi();

var app = builder.Build();

// --- Database migration ---------------------------------------------------
// Applying migrations at startup means a fresh clone just runs: no manual step is
// required before the first request. The equivalent CLI command is in the README.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
    await dbContext.Database.MigrateAsync();
}

// --- HTTP pipeline --------------------------------------------------------
// Registered first so that it also catches failures from everything downstream.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "TaskFlow API v1");
    options.DocumentTitle = "TaskFlow API";
});

// Browsing the root is more useful than a 404.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapControllers();

await app.RunAsync();
