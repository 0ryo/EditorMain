using System.Collections.Generic;

public interface IEditorCommand {
    void Do();
    void Undo();
    string Label { get; }
}

public class CommandStack {
    Stack<IEditorCommand> undo = new();
    Stack<IEditorCommand> redo = new();
    public void Execute(IEditorCommand cmd) { cmd.Do(); undo.Push(cmd); redo.Clear(); }
    public void Undo() { if (undo.Count>0){ var c=undo.Pop(); c.Undo(); redo.Push(c);} }
    public void Redo() { if (redo.Count>0){ var c=redo.Pop(); c.Do(); undo.Push(c);} }
}