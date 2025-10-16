using Microsoft.Extensions.Options;
using MongoDB.Bson;
using ReportAISummary.API.Config;
using ReportAISummary.API.Models;

namespace ReportAISummary.API.Utils
{
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

        public async Task RefreshReposState(string[] supportedRepos)
        {
            var requests = new List<RepoProfileRequest>();
            var workingPath = Path.GetFullPath(
                Path.Combine(
                    [
                    Directory.GetCurrentDirectory(),
                    "ClonedRepos",
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
                    ]));
            foreach (var repo in supportedRepos)
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
                requests.Add(repoInfo);
            }
            var repoInfos = new UpdateReposStateRequest(requests);
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

        public async Task<IEnumerable<RepoProfileResponse>> AskQuestion(string question, AskFilterRequest? askRequest)
        {
            var questionVector = await aiUtils.GetEmbeddingAsync(question);
            var result = await mongoDbUtils.VectorSearch(
                aiSection.Value.VECTOR_DB_INDEX_NAME,
                questionVector,
                limit: askRequest != null && askRequest.K is > 0 and <= 100 ? askRequest.K : 8);

            return result.OrderByDescending(i => i.Score);
        }
    }
}
