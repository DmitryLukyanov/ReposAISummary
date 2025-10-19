namespace ReportAISummary.API.Models
{
    public record AskFilterRequest(
        string? Repository = null,
        string? Name = null,
        string? Team = null,
        List<string>? Tags = null,
        int K = 8
    );
}
