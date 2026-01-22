using IssuePad.Models;

namespace IssuePad.Forms;

public sealed partial class MainForm
{
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
}
