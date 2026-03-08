#if TOOLS
using Godot;

[Tool]
public partial class GrassGridEditorPlugin111 : EditorPlugin
{
    private GrassGridGizmoPlugin _gizmoPlugin;

    public override void _EnterTree()
    {
        _gizmoPlugin = new GrassGridGizmoPlugin();
        AddNode3DGizmoPlugin(_gizmoPlugin);
    }

    public override void _ExitTree()
    {
        RemoveNode3DGizmoPlugin(_gizmoPlugin);
        _gizmoPlugin = null;
    }
}
#endif
