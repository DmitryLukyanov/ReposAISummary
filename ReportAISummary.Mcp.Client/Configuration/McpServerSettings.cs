namespace ReportAISummary.Mcp.Client.Configuration
{
    public record McpServerSettings(string Endpoint) { public static string SettingsKey = "McpServer"; }
}
