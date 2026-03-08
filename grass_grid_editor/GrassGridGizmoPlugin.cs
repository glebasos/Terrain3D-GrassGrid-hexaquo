#if TOOLS
using Godot;
using System.Collections.Generic;
using System.Linq;

[Tool]
[GlobalClass]
public partial class GrassGridGizmoPlugin : EditorNode3DGizmoPlugin
{
    public GrassGridGizmoPlugin()
    {
        CreateMaterial("cell",     new Color(0f,    1f,   0f));
        CreateMaterial("adjacent", new Color(1f,    0.8f, 0f));
    }

    public override bool _HasGizmo(Node3D forNode) => forNode is GrassGridManager;
    public override string _GetGizmoName() => "GrassGridManager";

    public override void _Redraw(EditorNode3DGizmo gizmo)
    {
        gizmo.Clear();

        if (gizmo.GetNode3D() is not GrassGridManager manager) return;

        float gs   = manager.GridSpacing;
        float half = gs / 2f;
        const float y = 0.2f;

        var cellMat = GetMaterial("cell",     gizmo);
        var adjMat  = GetMaterial("adjacent", gizmo);

        var occupied = manager.GetOccupiedCells();

        // Occupied cells — green rectangles
        foreach (var cell in occupied)
        {
            float cx = cell.X * gs;
            float cz = cell.Y * gs;

            gizmo.AddLines(new Vector3[]
            {
                new(cx - half, y, cz - half), new(cx + half, y, cz - half),
                new(cx + half, y, cz - half), new(cx + half, y, cz + half),
                new(cx + half, y, cz + half), new(cx - half, y, cz + half),
                new(cx - half, y, cz + half), new(cx - half, y, cz - half),
            }, cellMat);
        }

        // Adjacent cells — yellow ghost-cell squares + cross
        float s   = gs * 0.4f;  // half-size of the ghost square
        float arm = gs * 0.2f;  // cross arm length

        foreach (var cell in GetAdjacentCells(occupied))
        {
            float cx = cell.X * gs;
            float cz = cell.Y * gs;

            gizmo.AddLines(new Vector3[]
            {
                // Square outline
                new(cx - s, y, cz - s), new(cx + s, y, cz - s),
                new(cx + s, y, cz - s), new(cx + s, y, cz + s),
                new(cx + s, y, cz + s), new(cx - s, y, cz + s),
                new(cx - s, y, cz + s), new(cx - s, y, cz - s),
                // Cross
                new(cx - arm, y, cz), new(cx + arm, y, cz),
                new(cx, y, cz - arm), new(cx, y, cz + arm),
            }, adjMat);
        }
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static List<Vector2I> GetAdjacentCells(IEnumerable<Vector2I> occupied)
    {
        var occupiedList = occupied.ToList();
        if (occupiedList.Count == 0)
            return new List<Vector2I> { Vector2I.Zero };

        var occupiedSet = new HashSet<Vector2I>(occupiedList);
        var adjacent    = new HashSet<Vector2I>();

        foreach (var cell in occupiedList)
        {
            foreach (var offset in new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
            {
                var neighbor = cell + offset;
                if (!occupiedSet.Contains(neighbor))
                    adjacent.Add(neighbor);
            }
        }

        return adjacent.OrderBy(c => c.X).ThenBy(c => c.Y).ToList();
    }
}
#endif
