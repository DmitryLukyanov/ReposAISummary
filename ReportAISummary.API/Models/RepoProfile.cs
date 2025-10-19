using MongoDB.Bson;

namespace ReportAISummary.API.Models
{
    public sealed class RepoProfileDto(
        string Repo,
        string? Name,
        string? Team,
        string? Summary,
        IEnumerable<string>? Owners,
        IEnumerable<string>? Responsibilities,
        IEnumerable<string>? Dependencies,
        IEnumerable<string>? Tags,
        IEnumerable<string>? Docs) : RepoProfileRequest(
            Repo,
            Name,
            Team,
            Summary,
            Owners,
            Responsibilities,
            Dependencies,
            Tags,
            Docs)
    {
        public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
        public IEnumerable<double>? Embedding { get; init; }
        public IEnumerable<double>? DocsEmbedding { get; init; }
    }

    public class RepoProfileRequest
    {
        private readonly string repo;
        private readonly string? name;
        private readonly string? team;
        private readonly string? summary;
        private readonly IEnumerable<string>? owners;
        private readonly IEnumerable<string>? responsibilities;
        private readonly IEnumerable<string>? dependencies;
        private readonly IEnumerable<string>? tags;
        private readonly IEnumerable<string>? docs;

        public RepoProfileRequest(
            string repo,
            string? name,
            string? team,
            string? summary,
            IEnumerable<string>? owners,
            IEnumerable<string>? responsibilities,
            IEnumerable<string>? dependencies,
            IEnumerable<string>? tags,
            IEnumerable<string>? docs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repo);
            this.repo = repo;
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            this.name = name;
            ArgumentException.ThrowIfNullOrWhiteSpace(team);
            this.team = team;
            ArgumentException.ThrowIfNullOrWhiteSpace(summary);
            this.summary = summary;

            ArgumentNullException.ThrowIfNull(owners);
            ArgumentOutOfRangeException.ThrowIfZero(owners.Count(), paramName: nameof(owners));
            this.owners = owners;

            ArgumentNullException.ThrowIfNull(responsibilities);
            ArgumentOutOfRangeException.ThrowIfZero(responsibilities.Count(), paramName: nameof(responsibilities));
            
            this.responsibilities = responsibilities;

            this.dependencies = EnsureCollectionIsValid(dependencies, nameof(dependencies));
            this.tags = EnsureCollectionIsValid(tags, nameof(tags));
            this.docs = EnsureCollectionIsValid(docs, nameof(docs));

            static IEnumerable<string>? EnsureCollectionIsValid(IEnumerable<string>? collection, string paramName)
            {
                ArgumentNullException.ThrowIfNull(collection);
                ArgumentOutOfRangeException.ThrowIfZero(collection.Count(), paramName: nameof(responsibilities));
                foreach (var item in collection)
                {
                    ArgumentNullException.ThrowIfNullOrWhiteSpace(item, paramName);
                }
                return collection;
            }
        }

        public string Repo => repo;
        public string? Name => name;
        public string? Team => team;
        public string? Summary => summary;
        public IEnumerable<string>? Owners => owners;
        public IEnumerable<string>? Docs => docs;
        public IEnumerable<string>? Responsibilities => responsibilities;
        public IEnumerable<string>? Dependencies => dependencies;
        public IEnumerable<string>? Tags => tags;

        public string BuildGeneralEmbedding()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"repo: {{{Repo}}}");
            if (!string.IsNullOrWhiteSpace(Name))
            {
                sb.AppendLine($"application name: {{{Name}}}");
            }
            if (!string.IsNullOrWhiteSpace(Team)) 
            {
                sb.AppendLine($"team: {{{Team}}}");
            }
            if (!string.IsNullOrWhiteSpace(Summary))
            {
                sb.AppendLine($"summary: {{{Summary}}}");
            }
            if (Owners != null && Owners.Any())
            {
                sb.AppendLine($"owners: [{{{string.Join(",", Owners)}}}]");
            }
            if (Responsibilities != null && Responsibilities.Any())
            {
                sb.AppendLine($"responsibilities: [{{{string.Join(",", Responsibilities)}}}]");
            }
            if (Tags != null && Tags.Any())
            {
                sb.AppendLine($"tags: [{{{string.Join(",", Tags)}}}]");
            }
            if (Dependencies != null && Dependencies.Any())
            {
                sb.AppendLine($"dependencies: [{{{string.Join(",", Dependencies)}}}]");
            }
            return sb.ToString();
        }

        public string BuildDocummentationsEmbedding()
        {
            var sb = new System.Text.StringBuilder();
            if (Docs != null)
            {
                foreach (var doc in Docs)
                {
                    sb.AppendLine(doc);
                }
            }
            return sb.ToString();
        }
    }

    public class RepoProfileResponse(
        string repo,
        string? name,
        string? team,
        string? summary,
        IEnumerable<string>? owners,
        IEnumerable<string>? responsibilities,
        IEnumerable<string>? dependencies,
        IEnumerable<string>? tags,
        IEnumerable<string>? docs,
        double score) : RepoProfileRequest(repo, name, team, summary, owners, responsibilities, dependencies, tags, docs)
    {
        public double Score { get; init; } = score;
    }

    public record UpdateReposStateRequest(IEnumerable<RepoProfileRequest> Items);
}
