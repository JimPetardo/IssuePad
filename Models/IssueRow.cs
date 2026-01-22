namespace IssuePad.Models;

public sealed record IssueRow(
    string Id,
    bool Done,
    string Title,
    string Description,
    string Notes,
    DateTime CreatedUtc
);
