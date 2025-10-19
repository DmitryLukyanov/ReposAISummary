using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ReportAISummary.Mcp.Client;
using ReportAISummary.Mcp.Client.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddEnvironmentVariables()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>();
var mcpSettings = builder.Configuration.GetSection(McpServerSettings.SettingsKey).Get<McpServerSettings>()!;

// setup client
await using var mcpClient = new McpClientWrapper(endpoint: new Uri(mcpSettings.Endpoint));

var availableTools = await mcpClient.GetTools();
foreach (var tool in availableTools)
{
    Console.WriteLine($"Available tool: {tool}");
}

var apiKey = builder.Configuration["ANTHROPIC_API_KEY"]!;
using var aiClient = new AiClientWrapper(apiKey, availableTools);

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("MCP Client Started!");
Console.ResetColor();

do
{
    PromptForInput();
    var prompt = Console.ReadLine();
    if ("exit".Equals(prompt, StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (string.IsNullOrWhiteSpace(prompt))
    {
        continue;
    }

    await foreach (var response in aiClient.StreamResponse(prompt))
    {
        Console.Write(response);
    }
}
while (true);

Console.WriteLine("Completed");

static void PromptForInput()
{
    Console.WriteLine("\n\nEnter a command (or 'exit' to quit):");
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("> ");
    Console.ResetColor();
}
