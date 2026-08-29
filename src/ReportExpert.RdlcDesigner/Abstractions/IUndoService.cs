namespace ReportExpert.RdlcDesigner.Abstractions;

public interface IDesignerCommand
{
    string Name { get; }
    void Execute();
    void Unexecute();
}

public interface IUndoService
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    event EventHandler? StateChanged;
    void Execute(IDesignerCommand command);
    void Undo();
    void Redo();
    void Clear();
}
