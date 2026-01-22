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

    private readonly Button _new = new() { Text = "📄 Nuovo", Dock = DockStyle.Top, Height = 36 };
    private readonly Button _add = new() { Text = "➕ Aggiungi", Dock = DockStyle.Fill, Height = 36, BackColor = Color.FromArgb(220, 255, 220) };
    private readonly Button _update = new() { Text = "💾 Salva", Dock = DockStyle.Fill, Height = 36, BackColor = Color.FromArgb(220, 235, 255) };
    private readonly Button _toggleDone = new() { Text = "✅ Cambia stato", Dock = DockStyle.Top, Height = 36 };
    private readonly Button _delete = new() { Text = "🗑️ Elimina", Dock = DockStyle.Top, Height = 36, ForeColor = Color.DarkRed, BackColor = Color.FromArgb(255, 230, 230) };

    private readonly LinkLabel _pathLabel = new() { Dock = DockStyle.Top, Height = 20, AutoEllipsis = true };

    private readonly ComboBox _titleFilter = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList
    };
    private readonly CheckBox _showOnlyOpen = new()
    {
        Text = "Solo aperte",
        Dock = DockStyle.Right,
        Width = 100,
        Appearance = Appearance.Button,
        TextAlign = ContentAlignment.MiddleCenter,
        FlatStyle = FlatStyle.Flat
    };
    private string? _currentFilter = null;
    private bool _onlyOpen = false;

    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusTotal = new() { Spring = false, BorderSides = ToolStripStatusLabelBorderSides.Right };
    private readonly ToolStripStatusLabel _statusOpen = new() { Spring = false, BorderSides = ToolStripStatusLabelBorderSides.Right, ForeColor = Color.DarkGreen };
    private readonly ToolStripStatusLabel _statusClosed = new() { Spring = false, ForeColor = Color.Gray };
    private readonly ToolStripStatusLabel _statusSpacer = new() { Spring = true };

    private Font? _fontRegular;
    private Font? _fontBold;
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
        _fontBold = new Font(Font, FontStyle.Bold);
        _fontStrike = new Font(Font, FontStyle.Strikeout);

        _split.FixedPanel = FixedPanel.Panel2;

        SetupGridColumns();
        SetupLeftPanel();
        SetupRightPanel();
        SetupStatusStrip();

        Controls.Add(_statusStrip);
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
            if (_fontRegular == null || _fontStrike == null || _fontBold == null) return;
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;

            var r = _rows[e.RowIndex];
            var colName = _grid.Columns[e.ColumnIndex].Name;

            e.CellStyle!.ForeColor = r.Done ? Color.Gray : Color.Black;

            if (r.Done)
            {
                e.CellStyle.Font = _fontStrike;
            }
            else if (colName == "title")
            {
                e.CellStyle.Font = _fontBold;
            }
            else
            {
                e.CellStyle.Font = _fontRegular;
            }
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
            _fontBold?.Dispose();
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
            Width = 300,
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

    private void SetupLeftPanel()
    {
        var leftPanel = new Panel { Dock = DockStyle.Fill };

        // Area filtri stilizzata
        var filterGroup = new GroupBox
        {
            Text = "🔎 Filtri",
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(4, 2, 4, 4),
            ForeColor = Color.FromArgb(70, 130, 180)
        };

        var filterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        filterTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _titleFilter.Dock = DockStyle.Fill;
        _titleFilter.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;

        _showOnlyOpen.Dock = DockStyle.Fill;
        _showOnlyOpen.Width = 100;
        _showOnlyOpen.Margin = new Padding(4, 0, 0, 0);

        _showOnlyOpen.CheckedChanged += (_, _) =>
        {
            _onlyOpen = _showOnlyOpen.Checked;
            _showOnlyOpen.BackColor = _onlyOpen ? Color.FromArgb(200, 230, 200) : SystemColors.Control;
            ReloadFromDisk();
        };

        _titleFilter.SelectedIndexChanged += (_, _) => ApplyFilter();

        filterTable.Controls.Add(_titleFilter, 0, 0);
        filterTable.Controls.Add(_showOnlyOpen, 1, 0);
        filterGroup.Controls.Add(filterTable);

        leftPanel.Controls.Add(_grid);
        leftPanel.Controls.Add(filterGroup);

        _split.Panel1.Controls.Add(leftPanel);
    }

    private void SetupStatusStrip()
    {
        _statusStrip.Items.Add(_statusSpacer);
        _statusStrip.Items.Add(_statusTotal);
        _statusStrip.Items.Add(_statusOpen);
        _statusStrip.Items.Add(_statusClosed);
    }

    private static Label CreateSeparator() => new()
    {
        Dock = DockStyle.Top,
        Height = 12,
        BorderStyle = BorderStyle.Fixed3D
    };

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

        // Pannello con pulsanti Aggiungi e Salva affiancati
        var addUpdatePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        addUpdatePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        addUpdatePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        addUpdatePanel.Controls.Add(_add, 0, 0);
        addUpdatePanel.Controls.Add(_update, 1, 0);

        // Aggiungo in ordine inverso perché Dock = Top li impila dal basso verso l'alto
        // Sezione: Azioni su issue esistenti (in basso)
        right.Controls.Add(_delete);
        right.Controls.Add(_toggleDone);
        right.Controls.Add(CreateSeparator());
        // Sezione: Aggiungi/Salva affiancati
        right.Controls.Add(addUpdatePanel);
        right.Controls.Add(CreateSeparator());
        // Sezione: Campi editor
        right.Controls.Add(notesPanel);
        right.Controls.Add(descPanel);
        right.Controls.Add(titlePanel);
        right.Controls.Add(CreateSeparator());
        // Sezione: Pulsante nuovo e path DB (in alto)
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

        UpdateFilterComboBox(all);

        var filtered = all.AsEnumerable();

        // Applica filtro per titolo
        if (_currentFilter != null)
            filtered = filtered.Where(x => string.Equals(x.Title, _currentFilter, StringComparison.OrdinalIgnoreCase));

        // Applica filtro solo aperte
        if (_onlyOpen)
            filtered = filtered.Where(x => !x.Done);

        var filteredList = filtered.ToList();

        _rows = new BindingList<IssueRow>(filteredList);
        _grid.DataSource = _rows;

        if (!string.IsNullOrWhiteSpace(keepSelectionId))
        {
            var idx = filteredList.FindIndex(x => x.Id == keepSelectionId);
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

    private void UpdateFilterComboBox(List<IssueRow> allIssues)
    {
        var previousFilter = _currentFilter;

        var totalCount = allIssues.Count;
        var openCount = allIssues.Count(x => !x.Done);
        var closedCount = totalCount - openCount;

        // Raggruppa per titolo con conteggi
        var titleStats = allIssues
            .GroupBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Title = g.Key,
                Total = g.Count(),
                Open = g.Count(x => !x.Done)
            })
            .OrderBy(x => x.Title)
            .ToList();

        _titleFilter.Items.Clear();
        _titleFilter.Items.Add($"🔍 Tutti ({openCount}/{totalCount})");

        foreach (var stat in titleStats)
        {
            var label = stat.Open > 0
                ? $"{stat.Title} ({stat.Open}/{stat.Total})"
                : $"{stat.Title} (✓ {stat.Total})";
            _titleFilter.Items.Add(label);
        }

        // Ripristina selezione precedente
        if (previousFilter != null)
        {
            var matchIndex = titleStats.FindIndex(x =>
                string.Equals(x.Title, previousFilter, StringComparison.OrdinalIgnoreCase));
            if (matchIndex >= 0)
                _titleFilter.SelectedIndex = matchIndex + 1;
            else
                _titleFilter.SelectedIndex = 0;
        }
        else
        {
            _titleFilter.SelectedIndex = 0;
        }

        // Aggiorna StatusStrip
        UpdateStatusStrip(totalCount, openCount, closedCount);
    }

    private void UpdateStatusStrip(int total, int open, int closed)
    {
        _statusTotal.Text = $"📋 Totale: {total}";
        _statusOpen.Text = $"🔓 Aperte: {open}";
        _statusClosed.Text = $"✅ Chiuse: {closed}";
    }

    private void ApplyFilter()
    {
        if (_reloading) return;

        if (_titleFilter.SelectedIndex <= 0)
        {
            _currentFilter = null;
        }
        else
        {
            // Estrae il titolo rimuovendo il suffisso con contatori " (X/Y)" o " (✓ Y)"
            var selected = _titleFilter.SelectedItem?.ToString() ?? "";
            var lastParen = selected.LastIndexOf(" (", StringComparison.Ordinal);
            _currentFilter = lastParen > 0 ? selected[..lastParen] : selected;
        }

        ReloadFromDisk();
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
