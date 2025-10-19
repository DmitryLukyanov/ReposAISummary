using Microsoft.Extensions.Options;
using ReportAISummary.API.Config;
using ReportAISummary.API.Models;
using ReportAISummary.API.Utils;
using Scalar.AspNetCore;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddCors();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

AiUtils.PrepareServices(builder.Services, builder.Configuration);
MongoDbUtils.PrepareServices(builder.Services);
GitUtils.PrepareServices(builder.Services);
EndpointActions.PrepareServices(builder.Services);

var app = builder.Build();
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Repo Map API";
    options.Theme = ScalarTheme.Default;
});
app.UseCors(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

app.MapPost(
    pattern: "/refresh-repos-state",
    handler: async (EndpointActions endpointActions) =>
    {
        var readSupportedReposList = await File.ReadAllTextAsync("../supported-repos.json");
        var supportedRepos = JsonSerializer.Deserialize<List<string>>(readSupportedReposList);

        if (supportedRepos == null || supportedRepos.Count == 0)
        {
            return Results.Json(new { error = "the server config is empty" }, statusCode: (int)HttpStatusCode.InternalServerError);
        }

        await endpointActions.RefreshReposState([.. supportedRepos]);

        return Results.Ok(new { ok = true, cloned = supportedRepos });
    })
    .WithName("RefreshGithubReposState")
    .WithTags("repo-state");

app.MapPost(
    pattern: "/ask",
    handler: async (
        EndpointActions endpointActions,
        string question = "Return all repos where responsibility is 'refund'",
        AskFilterRequest? filter = null) =>
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return Results.BadRequest(new { error = "question is empty" });
        }

        var result = await endpointActions.AskQuestionAboutGithubRepos(question, filter);
        return Results.Ok(new { result });
    })
    .WithName(nameof(EndpointActions.AskQuestionAboutGithubRepos))
    // TODO: add example for question
    .WithTags("repo-state")
    .WithDescription("Asking is currently supported only about: ['repo', 'team', 'summary', 'owners', 'responsibilities']");

app.MapGet(
    pattern: "/repos",
    handler: async (EndpointActions endpointActions, int? limit = null) =>
    {
        var processedRepos = await endpointActions.GetProcessedGithubRepos(limit);
        return Results.Ok(processedRepos);
    })
    .WithName(nameof(EndpointActions.GetProcessedGithubRepos))
    .WithTags("repo-state");

app.MapMcp("mcp");

var aiSection = app.Services.GetRequiredService<IOptions<AISection>>();
app.Run(aiSection.Value.MCP_ENDPOINT);
