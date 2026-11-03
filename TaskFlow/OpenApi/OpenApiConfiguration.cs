using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using TaskFlow.DTOs.Projects;
using TaskFlow.DTOs.Tasks;

namespace TaskFlow.OpenApi;

/// <summary>
/// OpenAPI document configuration, kept out of <c>Program.cs</c> so the composition root
/// stays readable.
/// </summary>
public static class OpenApiConfiguration
{
    /// <summary>
    /// Example request bodies shown in Swagger UI. .NET 10 maps XML <c>&lt;example&gt;</c> tags
    /// onto query parameters but not onto schema properties, so the bodies are supplied here.
    /// </summary>
    private static readonly Dictionary<Type, string> RequestExamples = new()
    {
        [typeof(CreateTaskRequest)] = """
            {
              "title": "Finish the database assignment",
              "description": "Normalise the schema and hand in the ER diagram",
              "status": "Pending",
              "priority": "High",
              "dueDate": "2026-12-01T09:00:00Z",
              "projectId": 1
            }
            """,
        [typeof(UpdateTaskRequest)] = """
            {
              "title": "Finish the database assignment",
              "description": "Schema handed in, diagram left",
              "status": "InProgress",
              "priority": "Urgent",
              "dueDate": "2026-12-05T17:00:00Z",
              "projectId": 1
            }
            """,
        [typeof(CreateProjectRequest)] = """
            {
              "name": "University",
              "description": "Coursework, assignments and exams"
            }
            """,
        [typeof(UpdateProjectRequest)] = """
            {
              "name": "University 2026",
              "description": "Final year"
            }
            """
    };

    public static IServiceCollection AddTaskFlowOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "TaskFlow API",
                    Version = "v1",
                    Description = "A personal productivity REST API for managing tasks, projects "
                                  + "and productivity statistics."
                };
                return Task.CompletedTask;
            });

            options.AddSchemaTransformer((schema, context, _) =>
            {
                if (RequestExamples.TryGetValue(context.JsonTypeInfo.Type, out var example))
                {
                    // OpenAPI 3.1 carries a list of examples rather than a single one.
                    schema.Examples = [JsonNode.Parse(example)!];
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
