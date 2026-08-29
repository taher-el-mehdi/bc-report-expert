using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Services;

public sealed class UndoService : IUndoService
{
    private readonly Stack<IDesignerCommand> _undo = new();
    private readonly Stack<IDesignerCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public event EventHandler? StateChanged;

    public void Execute(IDesignerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo)
            return;
        IDesignerCommand cmd = _undo.Pop();
        cmd.Unexecute();
        _redo.Push(cmd);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo)
            return;
        IDesignerCommand cmd = _redo.Pop();
        cmd.Execute();
        _undo.Push(cmd);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
