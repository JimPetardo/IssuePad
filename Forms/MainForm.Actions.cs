using IssuePad.Models;

namespace IssuePad.Forms;

public sealed partial class MainForm
{
    private void ClearEditor()
    {
        _application.Text = "";
        _title.Text = "";
        _desc.Text = "";
        _notes.Text = "";
        _application.Focus();
    }

    private void AddIssue()
    {
        var app = (_application.Text ?? "").Trim();
        var title = (_title.Text ?? "").Trim();
        var desc = (_desc.Text ?? "").Trim();
        var notes = (_notes.Text ?? "").Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Titolo obbligatorio.", "IssuePad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var when = DateTime.UtcNow;

        _repository.AppendEvent(new DbLine
        {
            Type = "add",
            Id = id,
            Application = app,
            Title = title,
            Description = desc,
            Notes = notes,
            WhenUtc = when
        });

        ClearEditor();
        ReloadFromDisk(keepSelectionId: id);
    }

    private void UpdateSelectedIssue()
    {
        var row = GetSelectedRow();
        if (row == null)
        {
            MessageBox.Show("Seleziona una riga da aggiornare.", "IssuePad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var app = (_application.Text ?? "").Trim();
        var title = (_title.Text ?? "").Trim();
        var desc = (_desc.Text ?? "").Trim();
        var notes = (_notes.Text ?? "").Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Titolo obbligatorio.", "IssuePad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _repository.AppendEvent(new DbLine
        {
            Type = "update",
            Id = row.Id,
            Application = app,
            Title = title,
            Description = desc,
            Notes = notes,
            WhenUtc = DateTime.UtcNow
        });

        ReloadFromDisk(keepSelectionId: row.Id);
    }

    private void ToggleDoneForSelected()
    {
        var row = GetSelectedRow();
        if (row == null)
        {
            MessageBox.Show("Seleziona una riga.", "IssuePad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var newDone = !row.Done;

        _repository.AppendEvent(new DbLine
        {
            Type = newDone ? "resolve" : "reopen",
            Id = row.Id,
            WhenUtc = DateTime.UtcNow
        });

        ReloadFromDisk(keepSelectionId: row.Id);
    }

    private void DeleteSelectedIssue()
    {
        var row = GetSelectedRow();
        if (row == null)
        {
            MessageBox.Show("Seleziona una riga.", "IssuePad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Vuoi davvero eliminare l'issue \"{row.Title}\"?",
            "IssuePad - Conferma eliminazione",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        _repository.AppendEvent(new DbLine
        {
            Type = "delete",
            Id = row.Id,
            WhenUtc = DateTime.UtcNow
        });

        ClearEditor();
        ReloadFromDisk();
    }

    private IssueRow? GetSelectedRow()
    {
        if (_grid.CurrentRow == null) return null;
        var idx = _grid.CurrentRow.Index;
        if (idx < 0 || idx >= _rows.Count) return null;
        return _rows[idx];
    }

    private void LoadSelectedToEditor()
    {
        var row = GetSelectedRow();
        if (row == null) return;

        _application.Text = row.Application;
        _title.Text = row.Title;
        _desc.Text = row.Description;
        _notes.Text = row.Notes;
    }
}
