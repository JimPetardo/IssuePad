using System.ComponentModel;
using IssuePad.Models;

namespace IssuePad.Forms;

public sealed partial class MainForm
{
    private void ReloadFromDisk(string? keepSelectionId = null)
    {
        _reloading = true;

        var all = _repository.LoadAllIssues()
            .OrderBy(x => x.Application, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.CreatedUtc)
            .ToList();

        UpdateFilterComboBoxes(all);

        var filtered = all.AsEnumerable();

        // Applica filtro per applicazione
        if (_currentAppFilter != null)
            filtered = filtered.Where(x => string.Equals(x.Application, _currentAppFilter, StringComparison.OrdinalIgnoreCase));

        // Applica filtro per titolo
        if (_currentTitleFilter != null)
            filtered = filtered.Where(x => string.Equals(x.Title, _currentTitleFilter, StringComparison.OrdinalIgnoreCase));

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

    private void UpdateFilterComboBoxes(List<IssueRow> allIssues)
    {
        var previousAppFilter = _currentAppFilter;
        var previousTitleFilter = _currentTitleFilter;

        var totalCount = allIssues.Count;
        var openCount = allIssues.Count(x => !x.Done);
        var closedCount = totalCount - openCount;

        // Filtra per app corrente prima di calcolare i titoli disponibili
        var issuesForTitleFilter = _currentAppFilter != null
            ? allIssues.Where(x => string.Equals(x.Application, _currentAppFilter, StringComparison.OrdinalIgnoreCase)).ToList()
            : allIssues;

        // Raggruppa per applicazione con conteggi
        var appStats = allIssues
            .GroupBy(r => r.Application, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                App = g.Key,
                Total = g.Count(),
                Open = g.Count(x => !x.Done)
            })
            .OrderBy(x => x.App)
            .ToList();

        // Raggruppa per titolo con conteggi (filtrato per app se selezionata)
        var titleStats = issuesForTitleFilter
            .GroupBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Title = g.Key,
                Total = g.Count(),
                Open = g.Count(x => !x.Done)
            })
            .OrderBy(x => x.Title)
            .ToList();

        // Aggiorna ComboBox Applicazione
        _appFilter.Items.Clear();
        var appOpenCount = allIssues.Count(x => !x.Done);
        _appFilter.Items.Add($"🔍 Tutte ({appOpenCount}/{totalCount})");

        foreach (var stat in appStats)
        {
            var appName = string.IsNullOrWhiteSpace(stat.App) ? "(nessuna)" : stat.App;
            var label = stat.Open > 0
                ? $"{appName} ({stat.Open}/{stat.Total})"
                : $"{appName} (✓ {stat.Total})";
            _appFilter.Items.Add(label);
        }

        // Ripristina selezione app precedente
        if (previousAppFilter != null)
        {
            var matchIndex = appStats.FindIndex(x =>
                string.Equals(x.App, previousAppFilter, StringComparison.OrdinalIgnoreCase));
            if (matchIndex >= 0)
                _appFilter.SelectedIndex = matchIndex + 1;
            else
                _appFilter.SelectedIndex = 0;
        }
        else
        {
            _appFilter.SelectedIndex = 0;
        }

        // Aggiorna ComboBox Titolo
        _titleFilter.Items.Clear();
        var titleOpenCount = issuesForTitleFilter.Count(x => !x.Done);
        var titleTotalCount = issuesForTitleFilter.Count;
        _titleFilter.Items.Add($"🔍 Tutti ({titleOpenCount}/{titleTotalCount})");

        foreach (var stat in titleStats)
        {
            var label = stat.Open > 0
                ? $"{stat.Title} ({stat.Open}/{stat.Total})"
                : $"{stat.Title} (✓ {stat.Total})";
            _titleFilter.Items.Add(label);
        }

        // Ripristina selezione titolo precedente
        if (previousTitleFilter != null)
        {
            var matchIndex = titleStats.FindIndex(x =>
                string.Equals(x.Title, previousTitleFilter, StringComparison.OrdinalIgnoreCase));
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

    private void ApplyAppFilter()
    {
        if (_reloading) return;

        if (_appFilter.SelectedIndex <= 0)
        {
            _currentAppFilter = null;
        }
        else
        {
            var selected = _appFilter.SelectedItem?.ToString() ?? "";
            var lastParen = selected.LastIndexOf(" (", StringComparison.Ordinal);
            var appName = lastParen > 0 ? selected[..lastParen] : selected;
            _currentAppFilter = appName == "(nessuna)" ? "" : appName;
        }

        // Reset filtro titolo quando cambia app
        _currentTitleFilter = null;
        ReloadFromDisk();
    }

    private void ApplyTitleFilter()
    {
        if (_reloading) return;

        if (_titleFilter.SelectedIndex <= 0)
        {
            _currentTitleFilter = null;
        }
        else
        {
            var selected = _titleFilter.SelectedItem?.ToString() ?? "";
            var lastParen = selected.LastIndexOf(" (", StringComparison.Ordinal);
            _currentTitleFilter = lastParen > 0 ? selected[..lastParen] : selected;
        }

        ReloadFromDisk();
    }
}
