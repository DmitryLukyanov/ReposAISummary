using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using ReportAISummary.API.Config;
using ReportAISummary.API.Models;

namespace ReportAISummary.API.Utils
{
    public class MongoDbUtils(
        AiUtils aiUtils,
        IMongoCollection<RepoProfileDto> collection)
    {
        public static void PrepareServices(IServiceCollection services)
        {
            services.AddSingleton<IMongoClient>(sp =>
            {
                var pack = new ConventionPack
                {
                    new CamelCaseElementNameConvention()
                };
                ConventionRegistry.Register(nameof(CamelCaseElementNameConvention), pack, _ => true);

                var aiSection = sp.GetRequiredService<IOptions<AISection>>()!.Value;
                return new MongoClient(aiSection.VECTOR_DB_ENDPOINT);
            });

            services.AddSingleton<IMongoCollection<RepoProfileDto>>(sp =>
            {
                var aiSection = sp.GetRequiredService<IOptions<AISection>>()!.Value;
                var mongoClient = sp.GetRequiredService<IMongoClient>();
                var indexCollection = CollectionNamespace.FromFullName(aiSection.VECTOR_DB_COLLECTION);
                var db = mongoClient.GetDatabase(indexCollection.DatabaseNamespace.DatabaseName);
                return db.GetCollection<RepoProfileDto>(indexCollection.CollectionName);
            });

            services.AddSingleton<MongoDbUtils>();
        }

        public async Task SaveRequest(UpdateReposStateRequest request)
        {
            var models = new List<WriteModel<RepoProfileDto>>(capacity: request.Items.Count());

            foreach (var i in request.Items)
            {
                var generalEmbedding = await aiUtils.GetEmbeddingAsync(i.BuildGeneralEmbedding());
                var docsEmbedding = await aiUtils.GetEmbeddingAsync(i.BuildDocummentationsEmbedding());

                var rp = new RepoProfileDto(
                    Repo: i.Repo,
                    Name: i.Name,
                    Team: i.Team,
                    Summary: i.Summary,
                    Owners: i.Owners,
                    Responsibilities: i.Responsibilities,
                    Dependencies: i.Dependencies,
                    Tags: i.Tags,
                    Docs: i.Docs)
                {
                    Embedding = generalEmbedding,
                    DocsEmbedding = docsEmbedding
                };

                var filter = Builders<RepoProfileDto>.Filter.And(
                    Builders<RepoProfileDto>.Filter.Eq(x => x.Repo, rp.Repo)
                );

                var updateDefinition = Builders<RepoProfileDto>.Update
                    .Set(x => x.Name, rp.Name)
                    .Set(x => x.Team, rp.Team)
                    .Set(x => x.Summary, rp.Summary)
                    .Set(x => x.Owners, rp.Owners)
                    .Set(x => x.Responsibilities, rp.Responsibilities)
                    .Set(x => x.Dependencies, rp.Dependencies)
                    .Set(x => x.Tags, rp.Tags)
                    .Set(x => x.Docs, rp.Docs)
                    .Set(x => x.Embedding, rp.Embedding)
                    .Set(x => x.DocsEmbedding, rp.DocsEmbedding);
                var update = new UpdateOneModel<RepoProfileDto>(filter, updateDefinition) { IsUpsert = true };
                models.Add(update);
            }

            var bulkResult = await collection.BulkWriteAsync(models, new BulkWriteOptions { IsOrdered = false });
            if (bulkResult.ProcessedRequests.Count != request.Items.Count())
            {
                // TODO: updating has been failed, do something
            }
        }

        public async Task<IEnumerable<RepoProfileResponse>> VectorSearch(
            string indexName,
            ReadOnlyMemory<float> searchVector,
            int limit,
            double minScore = 0.6,
            (
            // pre filtering
            string? Repository,
            string? Name,
            string? Team,
            // post filtering
            IEnumerable<string>? Owners,
            IEnumerable<string>? Tags)? filter = null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(minScore, 0, paramName: nameof(minScore));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(minScore, 1, paramName: nameof(minScore));

            var preFilter = Builders<RepoProfileDto>.Filter.Empty;
            if (filter.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(filter.Value.Name))
                {
                    preFilter &= Builders<RepoProfileDto>.Filter.Eq(x => x.Name, filter.Value.Name);
                }

                if (!string.IsNullOrWhiteSpace(filter.Value.Team))
                {
                    preFilter &= Builders<RepoProfileDto>.Filter.Eq(x => x.Team, filter.Value.Team);
                }

                if (!string.IsNullOrWhiteSpace(filter.Value.Repository))
                {
                    preFilter &= Builders<RepoProfileDto>.Filter.Eq(x => x.Repo, filter.Value.Repository);
                }
            }

            var vectorSearchPipeline = new EmptyPipelineDefinition<RepoProfileDto>()
                .VectorSearch(
                    field: e => e.Embedding,
                    queryVector: new QueryVector(searchVector),
                    limit: limit,
                    options: new VectorSearchOptions<RepoProfileDto>
                    {
                        NumberOfCandidates = 100,
                        IndexName = indexName,
                        Filter = preFilter
                    })
                .Project(Builders<RepoProfileDto>.Projection
                    .Include("_id")
                    .Include(x => x.Repo)
                    .Include(x => x.Name)
                    .Include(x => x.Summary)
                    .Include(x => x.Team)
                    .Include(x => x.Tags)
                    .Include(x => x.Owners)
                    .Include(x => x.Responsibilities)
                    .Include(x => x.Dependencies)
                    .Include(x => x.Docs)
                    .MetaVectorSearchScore("score"));

            var cursor = await collection.AggregateAsync(vectorSearchPipeline);
            var resultDocuments = await cursor.ToListAsync();

            var vectorDocumentationSearchPipeline = new EmptyPipelineDefinition<RepoProfileDto>()
                 .VectorSearch(
                     field: e => e.DocsEmbedding,
                     queryVector: new QueryVector(searchVector),
                     limit: limit,
                     options: new VectorSearchOptions<RepoProfileDto>
                     {
                         NumberOfCandidates = 100,
                         IndexName = indexName,
                         Filter = preFilter
                     })
                 .Project(Builders<RepoProfileDto>.Projection
                     .Include("_id")
                     .Include(x => x.Repo)
                     .Include(x => x.Name)
                     .Include(x => x.Summary)
                     .Include(x => x.Team)
                     .Include(x => x.Tags)
                     .Include(x => x.Owners)
                     .Include(x => x.Responsibilities)
                     .Include(x => x.Dependencies)
                     .Include(x => x.Docs)
                     .MetaVectorSearchScore("score"));

            cursor = await collection.AggregateAsync(vectorDocumentationSearchPipeline);
            // TODO: find a better way to merge two search results
            resultDocuments.AddRange(await cursor.ToListAsync());
            resultDocuments = [.. resultDocuments.DistinctBy(d => d.GetValue("_id"))];

            return [.. DeserializeBsonDocument(resultDocuments)
                // TODO: move this check on server side later
                .Where(i => i.Score >= minScore)
                .Where(i =>
                    filter == null ||
                    Enumerable.SequenceEqual(i.Tags!, filter.Value.Tags!, StringComparer.OrdinalIgnoreCase) ||
                    Enumerable.SequenceEqual(i.Owners!, filter.Value.Owners!, StringComparer.OrdinalIgnoreCase))];
        }

        public async Task<IEnumerable<string>> GetProcessedRepos(int? limit)
        {
            int take = Math.Clamp(limit ?? 50, 1, 200);

            var docs = await collection.Find(FilterDefinition<RepoProfileDto>.Empty)
                .Project<RepoProfileDto>(Builders<RepoProfileDto>.Projection.Exclude(x => x.Embedding))
                .Limit(take)
                .As<BsonDocument>()
                .ToListAsync();

            return DeserializeBsonDocument(docs).Select(i => i.Repo);
        }

        private static IEnumerable<RepoProfileResponse> DeserializeBsonDocument(IEnumerable<BsonDocument> resultDocuments)
        {
            return resultDocuments
                // TODO: fix ctors issue and use normal deserialization flow
                .Select(d => new RepoProfileResponse(
                    repo: d.GetValue("repo", string.Empty).AsString,
                    name: d.GetValue("name", string.Empty).AsString,
                    team: d.GetValue("team", string.Empty).AsString,
                    summary: d.GetValue("summary", string.Empty).AsString,
                    owners: [.. d.GetValue("owners", new BsonArray()).AsBsonArray.Select(x => x.AsString)],
                    responsibilities: [.. d.GetValue("responsibilities", new BsonArray()).AsBsonArray.Select(x => x.AsString)],
                    dependencies: [.. d.GetValue("dependencies", new BsonArray()).AsBsonArray.Select(x => x.AsString)],
                    tags: [.. d.GetValue("tags", new BsonArray()).AsBsonArray.Select(x => x.AsString)],
                    docs: [.. d.GetValue("docs", new BsonArray()).AsBsonArray.Select(x => x.AsString)],
                    score: d.GetValue("score", new BsonDouble(-1)).AsDouble));
        }
    }
}