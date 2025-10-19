using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace ReportAISummary.Mcp.Client
{
    internal class McpClientWrapper : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private readonly Lazy<Task<McpClient>> _client;
        private readonly HttpClient _httpClient;

        public McpClientWrapper(Uri endpoint)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            _httpClient = new HttpClient();

            _client = new Lazy<Task<McpClient>>(
                () =>
                {
                    return ConnectAsync(
                        endpoint: endpoint,
                        httpClient: _httpClient,
                        cancellationToken: _cancellationToken);
                });
        }

        public async Task<IList<McpClientTool>> GetTools()
        {
            var client = await _client.Value;
            return await client.ListToolsAsync(cancellationToken: _cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            try
            {
                _httpClient.Dispose();
            }
            catch
            { 
                // ignore for now
            }

            try
            {
                var client = await _client.Value;
                await client.DisposeAsync();
            }
            catch
            {
                // ignore for now
            }
        }

        private static async Task<McpClient> ConnectAsync(
            Uri endpoint,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = null,
            McpClientOptions? clientOptions = null,
            CancellationToken cancellationToken = default)
        {
            await using var transport = new HttpClientTransport(
                new HttpClientTransportOptions()
                {
                    Endpoint = endpoint,
                    TransportMode = HttpTransportMode.StreamableHttp,
                }, 
                httpClient, 
                loggerFactory);

            return await McpClient.CreateAsync(transport, clientOptions, loggerFactory, cancellationToken);
        }
    }
}
