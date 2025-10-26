using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using ReportAISummary.API.Config;
using System.Text;

namespace ReportAISummary.API.Utils
{
    public class AiUtils(
        ChatClient chatClient,
        EmbeddingClient embeddingClient)
    {
        private readonly ChatClient _chatClient = chatClient;
        private readonly EmbeddingClient _embeddingClient = embeddingClient;

        public static void PrepareServices(IServiceCollection services, IConfiguration config)
        {
            services.Configure<AISection>(config.GetSection(AISection.AISectionConfiguration));

            services.AddSingleton(sp =>
            {
                var aiSection = sp.GetRequiredService<IOptions<AISection>>().Value;
                return new OpenAIClient(aiSection.OPENAI_API_KEY);
            });

            services.AddSingleton<ChatClient>(sp =>
            {
                var aiSection = sp.GetRequiredService<IOptions<AISection>>().Value;
                var rootClient = sp.GetRequiredService<OpenAIClient>();
                return rootClient.GetChatClient(aiSection.OPENAI_CHAT_MODEL);
            });

            services.AddSingleton<EmbeddingClient>(sp =>
            {
                var aiSection = sp.GetRequiredService<IOptions<AISection>>().Value;
                var rootClient = sp.GetRequiredService<OpenAIClient>();
                return rootClient.GetEmbeddingClient(aiSection.OPENAI_EMBED_MODEL);
            });

            services.AddSingleton<AiUtils>();
        }

        public async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var embeddingResponse = await _embeddingClient.GenerateEmbeddingAsync(
                text,
                new EmbeddingGenerationOptions 
                {
                    Dimensions = 1536
                },
                cancellationToken: cancellationToken);

            return embeddingResponse.Value.ToFloats();
        }

        public async Task<string> SummarizeContent(
            string content,
            CancellationToken cancellationToken = default)
        {
            var systemInstruction = $$$"""
                You are a helpful assistant.
                You summarize documents concisely and preserve key facts.
                """;

            // TODO: move content in lazy render flow?
            var userPrompt = $$$"""
                "Summarize the following document in a concise manner:
                {{{content}}}
                """;

            // Build the conversation messages
            List<ChatMessage> messages =
            [
                new SystemChatMessage(systemInstruction),
                new UserChatMessage(userPrompt)
            ];

            var options = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                ToolChoice = ChatToolChoice.CreateNoneChoice()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(
                messages,
                options,
                cancellationToken);

            var sb = new StringBuilder();
            foreach (var part in completion.Content)
            {
                if (!string.IsNullOrWhiteSpace(part.Text))
                {
                    sb.AppendLine(part.Text.Trim());
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
