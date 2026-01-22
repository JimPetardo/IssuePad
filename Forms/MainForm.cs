using System.ComponentModel;
using IssuePad.Models;
using IssuePad.Services;

namespace IssuePad.Forms;

public sealed partial class MainForm : Form
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
                using var brush = new SolidBrush(Color.FromArgb(70, 130, 180));
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
}
