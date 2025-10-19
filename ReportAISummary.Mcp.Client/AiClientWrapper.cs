using Anthropic.SDK;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Text;

namespace ReportAISummary.Mcp.Client
{
    internal class AiClientWrapper : IDisposable
    {
        private readonly IChatClient _client;
        private readonly ChatOptions _options;
        private static readonly string SystemPrompt =
            @$"You are a helpful assistant. You can answer questions directly or use the available tools if needed.

IF you ask to call a tool available to you, do this immidiately.

Then just print output.
";

        private readonly List<ChatMessage> _messages =
        [
            new ChatMessage(ChatRole.System, SystemPrompt),
        ];

        public AiClientWrapper(string apiKey, IList<McpClientTool> tools)
        {
            _client = new AnthropicClient(new APIAuthentication(apiKey))
                .Messages
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

            _options = new ChatOptions
            {
                MaxOutputTokens = 8192, // max value for claude-3-5-sonnet-20241022 model
                // not really general purpose model
                ModelId = "claude-sonnet-4-20250514",//"claude-3-5-sonnet-20241022",
                Tools = [.. tools],
                AllowMultipleToolCalls = true,
                ToolMode = ChatToolMode.Auto,
                ResponseFormat = new ChatResponseFormatText()
            };
        }

        public void Dispose() => _client.Dispose();

        public async IAsyncEnumerable<ChatResponseUpdate> StreamResponse(string query)
        {
            _messages.Add(new ChatMessage(ChatRole.User, query));

            StringBuilder accumulator = new();
            await foreach (var message in _client.GetStreamingResponseAsync(_messages, _options))
            {
                accumulator.Append(message.Text);
                yield return message;
            }

            _messages.Add(new ChatMessage(ChatRole.Assistant, accumulator.ToString()));
        }
    }
}
