using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using MongoDB.Bson;
using ReportAISummary.API.Config;
using ReportAISummary.API.Models;
using System.ComponentModel;

namespace ReportAISummary.API.Utils
{
    [McpServerToolType]
    public sealed class EndpointActions(
        MongoDbUtils mongoDbUtils, 
        GitUtils gitUtils,
        AiUtils aiUtils,
        IOptions<AISection> aiSection)
    {
        public static void PrepareServices(IServiceCollection services)
        {
            services.AddScoped<EndpointActions>();
        }

        // TODO: make it logically async
        public async Task RefreshReposState(string[] supportedRepos)
        {
            //var requests = new List<RepoProfileRequest>();
            var workingPath = Path.GetFullPath(
                Path.Combine(
                    [
                    Directory.GetCurrentDirectory(),
                    "ClonedRepos",
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
                    ]));

            List<Task<RepoProfileRequest>> tasks = [];
            foreach (var repo in supportedRepos)
            {
                tasks.Add(Task.Run<RepoProfileRequest>(async () =>
                {
                    var cloneOutput = gitUtils.CloneRepo(repo, workingPath: workingPath);
                    var codeownersString = await GetFileContent(Path.Combine(cloneOutput, "CODEOWNERS"));

                    /*
                        "name": "payments-service",
                        "summary": "Payment service for Adyen",
                        "team": "payments-platform",
                        "responsibilities": ["create-payment", "refund"],
                        "dependencies": ["ledger-service", "fx-rate-service"],
                        "tags": ["dotnet", "kafka"],
                        "docs": ["docs/overview.md"]
                     */
                    var repoInfoString = await GetFileContent(Path.Combine(cloneOutput, "RepoInfo.json"));
                    string? summary = string.Empty;
                    string? team = string.Empty;
                    string? name = string.Empty;
                    string[]? responsibilities = null;
                    string[]? dependencies = null;
                    string[]? tags = null;
                    List<string>? docs = [];
                    if (!string.IsNullOrWhiteSpace(repoInfoString))
                    {
                        var deserialized = BsonDocument.Parse(repoInfoString);
                        summary = deserialized?.GetValue("summary", defaultValue: null)?.AsString;
                        team = deserialized?.GetValue("team", defaultValue: null)?.AsString;
                        name = deserialized?.GetValue("name", defaultValue: null)?.AsString;
                        responsibilities = deserialized?.GetValue("responsibilities", defaultValue: null)?.AsBsonArray?.Select(x => x.AsString).ToArray();
                        dependencies = deserialized?.GetValue("dependencies", defaultValue: null)?.AsBsonArray?.Select(x => x.AsString).ToArray();
                        tags = deserialized?.GetValue("tags", defaultValue: null)?.AsBsonArray?.Select(x => x.AsString).ToArray();
                        var docsPaths = deserialized?.GetValue("docs", defaultValue: null)?.AsBsonArray?.Select(x => x.AsString).ToArray();
                        if (docsPaths != null && docsPaths.Length != 0)
                        {
                            foreach (var docPath in docsPaths)
                            {
                                var docContent = await GetFileContent(Path.Combine(cloneOutput, docPath));
                                var docSummary = await aiUtils.SummarizeContent(docContent!);
                                docs.Add(docSummary);
                            }
                        }
                    }

                    var repoInfo = new RepoProfileRequest(
                        repo: repo,
                        name: name,
                        team: team,
                        summary: summary,
                        owners: codeownersString?.Split('\n', StringSplitOptions.RemoveEmptyEntries),
                        responsibilities: responsibilities,
                        dependencies: dependencies,
                        tags: tags,
                        docs: docs);
                    return repoInfo;
                }));
            }

            await Task.WhenAll(tasks);

            var repoInfos = new UpdateReposStateRequest(tasks.Select(t => t.Result));
            await mongoDbUtils.SaveRequest(repoInfos);

            static async Task<string?> GetFileContent(string path)
            {
                if (File.Exists(path))
                {
                    return await File.ReadAllTextAsync(path);
                }

                return null;
            }
        }

        [McpServerTool(Name = nameof(AskQuestionAboutGithubRepos))]
        [Description("Answers on the question about github repositories via vector search approach.")]
        [return: Description("The list of answers on the question that consists of details about github repositoes sorted by vector search score")]
        public async Task<IEnumerable<RepoProfileResponse>> AskQuestionAboutGithubRepos(
            [Description("The question about github repositories")] string question, 
            [Description("The pre-filter that will be applied before vector search. Can be null")] AskFilterRequest? filter)
        {
            var questionVector = await aiUtils.GetEmbeddingAsync(question);
            var result = await mongoDbUtils.VectorSearch(
                aiSection.Value.VECTOR_DB_INDEX_NAME,
                questionVector,
                limit: filter != null && filter.K is > 0 and <= 100 ? filter.K : 8);

            return result.OrderByDescending(i => i.Score);
        }

        [McpServerTool(Name = nameof(GetProcessedGithubRepos))]
        [Description("Get the list of github repositories that were previously processed and which state there is information about.")]
        [return: Description("The list of github repositories that were previously processed and which state there is information about.")]
        public async Task<IEnumerable<string>> GetProcessedGithubRepos([Description("The limit github repositories to return state for")] int? limit)
        {
            var processedRepos = await mongoDbUtils.GetProcessedRepos(limit);
            return processedRepos;
        }
    }
}
