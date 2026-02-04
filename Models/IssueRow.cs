namespace IssuePad.Models;

public sealed record IssueRow(
    string Id,
    bool Done,
    string Application,
    string Title,
    string Description,
    string Notes,
    DateTime CreatedUtc
);
