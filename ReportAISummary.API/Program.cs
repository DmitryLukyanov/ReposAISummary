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
app.UseHttpsRedirection();
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

        var result = await endpointActions.AskQuestion(question, filter);
        return Results.Ok(new { result });
    })
    // TODO: add example for question
    .WithTags("repo-state")
    .WithDescription("Asking is currently supported about: ['repo', 'team', 'summary', 'owners', 'responsibilities']");

app.MapGet(
    pattern: "/repos",
    handler: async (int? limit, MongoDbUtils mongoDbUtils) =>
    {
        var processedRepos = await mongoDbUtils.GetProcessedRepos(limit);
        return Results.Ok(processedRepos);
    })
    .WithTags("repo-state");

app.Run();
