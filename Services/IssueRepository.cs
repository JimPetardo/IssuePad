using System.Text;
using System.Text.Json;
using IssuePad.Models;

namespace IssuePad.Services;

public sealed class IssueRepository
{
    private readonly string _dbPath;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string DbPath => _dbPath;

    public IssueRepository()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IssuePad");
        Directory.CreateDirectory(baseDir);
        _dbPath = Path.Combine(baseDir, "issues.jsonl");
    }

    public List<IssueRow> LoadAllIssues()
    {
        var dict = new Dictionary<string, IssueRow>();

        if (!File.Exists(_dbPath))
            return new List<IssueRow>();

        foreach (var raw in File.ReadLines(_dbPath, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            DbLine? e;
            try { e = JsonSerializer.Deserialize<DbLine>(line, _jsonOpts); }
            catch { continue; }

            if (e == null || string.IsNullOrWhiteSpace(e.Type) || string.IsNullOrWhiteSpace(e.Id))
                continue;

            if (e.Type == "add")
            {
                dict[e.Id] = new IssueRow(
                    Id: e.Id,
                    Done: false,
                    Application: e.Application ?? "",
                    Title: e.Title ?? "(senza titolo)",
                    Description: e.Description ?? "",
                    Notes: e.Notes ?? "",
                    CreatedUtc: e.WhenUtc ?? DateTime.UtcNow
                );
            }
            else if (e.Type == "resolve" && dict.TryGetValue(e.Id, out var cur1))
            {
                dict[e.Id] = cur1 with { Done = true };
            }
            else if (e.Type == "reopen" && dict.TryGetValue(e.Id, out var cur2))
            {
                dict[e.Id] = cur2 with { Done = false };
            }
            else if (e.Type == "delete")
            {
                dict.Remove(e.Id);
            }
            else if (e.Type == "update" && dict.TryGetValue(e.Id, out var cur3))
            {
                dict[e.Id] = cur3 with
                {
                    Application = e.Application ?? cur3.Application,
                    Title = e.Title ?? cur3.Title,
                    Description = e.Description ?? cur3.Description,
                    Notes = e.Notes ?? cur3.Notes
                };
            }
        }

        return dict.Values.ToList();
    }

    public void AppendEvent(DbLine line)
    {
        var json = JsonSerializer.Serialize(line, _jsonOpts);
        File.AppendAllText(_dbPath, json + Environment.NewLine, Encoding.UTF8);
    }
}
