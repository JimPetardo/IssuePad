namespace IssuePad.Models;

public sealed class DbLine
{
    public string? Type { get; set; } // "add" | "resolve" | "reopen"
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateTime? WhenUtc { get; set; }
}
