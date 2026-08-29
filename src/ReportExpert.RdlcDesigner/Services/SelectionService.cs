using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Services;

public sealed class SelectionService : ISelectionService
{
    private readonly List<IReportItem> _selected = [];
    private IReportItem? _primary;

    public IReportItem? Primary => _primary;
    public IReadOnlyList<IReportItem> Selected => _selected;

    public event EventHandler? SelectionChanged;

    public void Select(IReportItem? item)
    {
        if (item is null)
        {
            Clear();
            return;
        }

        if (_selected.Count == 1 && ReferenceEquals(_primary, item))
            return;

        _selected.Clear();
        _selected.Add(item);
        _primary = item;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectMany(IEnumerable<IReportItem> items, IReportItem? primary = null)
    {
        var list = items.Where(i => i is not null).Distinct().ToList();
        _selected.Clear();
        _selected.AddRange(list);
        _primary = primary ?? list.LastOrDefault();
        if (_primary is not null && !_selected.Contains(_primary))
            _selected.Add(_primary);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle(IReportItem item)
    {
        int index = _selected.FindIndex(i => ReferenceEquals(i, item));
        if (index >= 0)
        {
            _selected.RemoveAt(index);
            if (ReferenceEquals(_primary, item))
                _primary = _selected.LastOrDefault();
        }
        else
        {
            _selected.Add(item);
            _primary = item;
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_selected.Count == 0 && _primary is null)
            return;
        _selected.Clear();
        _primary = null;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
