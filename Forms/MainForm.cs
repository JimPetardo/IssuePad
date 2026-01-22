using System.ComponentModel;
using IssuePad.Models;
using IssuePad.Services;

namespace IssuePad.Forms;

public sealed class MainForm : Form
{
    private readonly IssueRepository _repository;

    private readonly SplitContainer _split = new()
    {
        Dock = DockStyle.Fill,
        SplitterWidth = 6
    };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        ReadOnly = false
    };

    private BindingList<IssueRow> _rows = new();
    private bool _reloading;

    private readonly TextBox _title = new() { Dock = DockStyle.Top };
    private readonly TextBox _desc = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _notes = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };

    private readonly Button _new = new() { Text = "Nuovo (pulisci campi)", Dock = DockStyle.Top, Height = 36 };
    private readonly Button _add = new() { Text = "Aggiungi", Dock = DockStyle.Top, Height = 36 };
    private readonly Button _toggleDone = new() { Text = "Toggle completato (riga selezionata)", Dock = DockStyle.Top, Height = 36 };
    private readonly Button _update = new() { Text = "Aggiorna", Dock = DockStyle.Top, Height = 36 };
    private readonly Button _delete = new() { Text = "Rimuovi selezionato", Dock = DockStyle.Top, Height = 36, ForeColor = Color.DarkRed };

    private readonly LinkLabel _pathLabel = new() { Dock = DockStyle.Top, Height = 20, AutoEllipsis = true };

    private Font? _fontRegular;
    private Font? _fontStrike;

    public MainForm(IssueRepository repository)
    {
        _repository = repository;

        Text = "IssuePad";
        Width = 1080;
        Height = 680;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        _pathLabel.Text = $"DB: {_repository.DbPath}";
        _pathLabel.LinkArea = new LinkArea(4, _repository.DbPath.Length);
        _pathLabel.LinkClicked += (_, _) =>
        {
            System.Diagnostics.Process.Start("explorer.exe", Path.GetDirectoryName(_repository.DbPath)!);
        };

        _fontRegular = new Font(Font, FontStyle.Regular);
        _fontStrike = new Font(Font, FontStyle.Strikeout);

        _split.FixedPanel = FixedPanel.Panel2;

        SetupGridColumns();
        SetupRightPanel();

        _split.Panel1.Controls.Add(_grid);
        Controls.Add(_split);

        Shown += (_, _) =>
        {
            _split.Panel1MinSize = 500;
            _split.Panel2MinSize = 300;
            EnsureSplitterDistanceSafe();
        };

        Resize += (_, _) => EnsureSplitterDistanceSafe();

        _new.Click += (_, _) => ClearEditor();
        _add.Click += (_, _) => AddIssue();
        _update.Click += (_, _) => UpdateSelectedIssue();
        _toggleDone.Click += (_, _) => ToggleDoneForSelected();
        _delete.Click += (_, _) => DeleteSelectedIssue();

        _grid.SelectionChanged += (_, _) => LoadSelectedToEditor();

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_reloading) return;
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        _grid.CellValueChanged += (_, e) =>
        {
            if (_reloading) return;
            if (e.RowIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "done") return;
            if (e.RowIndex >= _rows.Count) return;

            var row = _rows[e.RowIndex];

            _repository.AppendEvent(new DbLine
            {
                Type = row.Done ? "resolve" : "reopen",
                Id = row.Id,
                WhenUtc = DateTime.UtcNow
            });

            ReloadFromDisk(keepSelectionId: row.Id);
        };

        _grid.CellFormatting += (_, e) =>
        {
            if (_fontRegular == null || _fontStrike == null) return;
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;

            var r = _rows[e.RowIndex];
            var style = _grid.Rows[e.RowIndex].DefaultCellStyle;
            style.ForeColor = r.Done ? Color.Gray : Color.Black;
            style.Font = r.Done ? _fontStrike : _fontRegular;
        };

        _grid.RowPrePaint += (_, e) =>
        {
            if (e.RowIndex <= 0 || e.RowIndex >= _rows.Count) return;

            var currentTitle = _rows[e.RowIndex].Title;
            var previousTitle = _rows[e.RowIndex - 1].Title;

            if (!string.Equals(currentTitle, previousTitle, StringComparison.OrdinalIgnoreCase))
            {
                var bounds = e.RowBounds;
                using var brush = new SolidBrush(Color.FromArgb(70, 130, 180)); // Steel Blue
                e.Graphics!.FillRectangle(brush, bounds.Left, bounds.Top - 2, bounds.Width, 4);
            }
        };

        FormClosed += (_, _) =>
        {
            _fontRegular?.Dispose();
            _fontStrike?.Dispose();
        };

        ReloadFromDisk();
    }

    private void SetupGridColumns()
    {
        var colDone = new DataGridViewCheckBoxColumn
        {
            Name = "done",
            HeaderText = "",
            Width = 30,
            DataPropertyName = nameof(IssueRow.Done)
        };

        var colTitle = new DataGridViewTextBoxColumn
        {
            Name = "title",
            HeaderText = "Titolo",
            Width = 260,
            DataPropertyName = nameof(IssueRow.Title),
            ReadOnly = true
        };

        var colDesc = new DataGridViewTextBoxColumn
        {
            Name = "description",
            HeaderText = "Descrizione",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DataPropertyName = nameof(IssueRow.Description),
            ReadOnly = true
        };

        _grid.Columns.AddRange(colDone, colTitle, colDesc);
    }

    private void SetupRightPanel()
    {
        var right = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };

        var titleLabel = new Label { Text = "Titolo", Dock = DockStyle.Top, AutoSize = true };
        var descLabel = new Label { Text = "Descrizione", Dock = DockStyle.Top, AutoSize = true };
        var notesLabel = new Label { Text = "Note aggiuntive", Dock = DockStyle.Top, AutoSize = true };

        var titlePanel = new Panel { Dock = DockStyle.Top, Height = 42 };
        titlePanel.Controls.Add(_title);
        titlePanel.Controls.Add(titleLabel);

        var descPanel = new Panel { Dock = DockStyle.Top, Height = 160 };
        descPanel.Controls.Add(_desc);
        descPanel.Controls.Add(descLabel);

        var notesPanel = new Panel { Dock = DockStyle.Top, Height = 160 };
        notesPanel.Controls.Add(_notes);
        notesPanel.Controls.Add(notesLabel);

        // Aggiungo in ordine inverso perché Dock = Top li impila dal basso verso l'alto
        right.Controls.Add(_delete);
        right.Controls.Add(_toggleDone);
        right.Controls.Add(_update);
        right.Controls.Add(_add);
        right.Controls.Add(notesPanel);
        right.Controls.Add(descPanel);
        right.Controls.Add(titlePanel);
        right.Controls.Add(_new);
        right.Controls.Add(_pathLabel);

        _split.Panel2.Controls.Add(right);
    }

    private void EnsureSplitterDistanceSafe()
    {
        if (!IsHandleCreated) return;

        var width = _split.ClientSize.Width;
        if (width <= 0) return;

        var minLeft = _split.Panel1MinSize;
        var maxLeft = width - _split.Panel2MinSize;
        if (maxLeft < minLeft) return;

        var desired = (width * 2) / 3;
        if (desired < minLeft) desired = minLeft;
        if (desired > maxLeft) desired = maxLeft;

        try
        {
            _split.SplitterDistance = desired;
        }
        catch
        {
        }
    }

    private void ClearEditor()
    {
        _title.Text = "";
        _desc.Text = "";
        _notes.Text = "";
        _title.Focus();
    }

    private void ReloadFromDisk(string? keepSelectionId = null)
    {
        _reloading = true;

        var all = _repository.LoadAllIssues()
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.CreatedUtc)
            .ToList();

        _rows = new BindingList<IssueRow>(all);
        _grid.DataSource = _rows;

        if (!string.IsNullOrWhiteSpace(keepSelectionId))
        {
            var idx = all.FindIndex(x => x.Id == keepSelectionId);
            if (idx >= 0 && idx < _grid.Rows.Count)
            {
                _grid.ClearSelection();
                _grid.Rows[idx].Selected = true;
                _grid.CurrentCell = _grid.Rows[idx].Cells["title"];
            }
        }

        _reloading = false;
        LoadSelectedToEditor();
    }

    private void AddIssue()
    {
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
            Title = title,
            Description = desc,
            Notes = notes,
            WhenUtc = when
        });

        ClearEditor();
        ReloadFromDisk(keepSelectionId: id);
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

        _title.Text = row.Title;
        _desc.Text = row.Description;
        _notes.Text = row.Notes;
    }

    private void UpdateSelectedIssue()
    {
        var row = GetSelectedRow();
        if (row == null)
        {
            MessageBox.Show("Seleziona una riga da aggiornare.", "IssuePad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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
            Title = title,
            Description = desc,
            Notes = notes,
            WhenUtc = DateTime.UtcNow
        });

        ReloadFromDisk(keepSelectionId: row.Id);
    }
}
