namespace ReportAISummary.API.Config
{
    public sealed record AISection
    {
        internal const string AISectionConfiguration = "AI";

        public required string OPENAI_API_KEY { get; init; } = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
        public required string OPENAI_CHAT_MODEL { get; init; }
        public required string OPENAI_EMBED_MODEL { get; init; }
        public required string VECTOR_DB_ENDPOINT { get; init; }
        public required string VECTOR_DB_COLLECTION { get; init; }
        public required string VECTOR_DB_INDEX_NAME { get; init; }
        public required string MCP_ENDPOINT { get; init; }
    }
}
