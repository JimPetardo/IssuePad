using System.ComponentModel;
using IssuePad.Models;

namespace IssuePad.Forms;

public sealed partial class MainForm
{
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
}
