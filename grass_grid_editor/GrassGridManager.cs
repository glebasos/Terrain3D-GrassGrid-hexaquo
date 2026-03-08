using Godot;
using System.Collections.Generic;

[Tool]
[GlobalClass]
public partial class GrassGridManager : Node3D
{
    [Export] public NodePath TerrainPath;
    [Export] public NodePath PlayerPath;

    private int _extent = 10;
    [Export]
    public int Extent
    {
        get => _extent;
        set { _extent = value; PropagateToGrids(); }
    }

    private float _chunkSize = 5.0f;
    [Export]
    public float ChunkSize
    {
        get => _chunkSize;
        set { _chunkSize = value; PropagateToGrids(); }
    }

    [Export] public float ObjectRadius = 1.0f;

    private bool _useImpostor = true;
    [Export]
    public bool UseImpostor
    {
        get => _useImpostor;
        set { _useImpostor = value; PropagateToGrids(); }
    }

    private Color _impostorGroundColor = new Color(0.3f, 0.6f, 0.1f, 1f);
    [Export]
    public Color ImpostorGroundColor
    {
        get => _impostorGroundColor;
        set { _impostorGroundColor = value; PropagateToGrids(); }
    }

    private float _density = 1.0f;
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Density
    {
        get => _density;
        set { _density = value; PropagateToGrids(); }
    }

    private float _slopeMin = 0f;
    [Export(PropertyHint.Range, "0,90,0.1")]
    public float SlopeMin
    {
        get => _slopeMin;
        set { _slopeMin = value; PropagateToGrids(); }
    }

    private float _slopeMax = 90f;
    [Export(PropertyHint.Range, "0,90,0.1")]
    public float SlopeMax
    {
        get => _slopeMax;
        set { _slopeMax = value; PropagateToGrids(); }
    }

    /// <summary>
    /// Distance between adjacent GrassGrid origins so their chunk extents tile without overlap or gap.
    /// = (Extent + Extent/2) * ChunkSize = count * ChunkSize  (same as GrassGrid's footprint width)
    /// </summary>
    public float GridSpacing => (_extent + _extent / 2) * _chunkSize;

    /// <summary>Creates a new GrassGrid at the given grid cell and adds it as a child.</summary>
    public void AddGrid(Vector2I cell)
    {
        var grid = new GrassGrid();
        grid.Name = $"GrassGrid_{cell.X}_{cell.Y}";
        grid.ChunkSize = _chunkSize;
        grid.ObjectRadius = ObjectRadius;
        grid.UseImpostor = _useImpostor;
        grid.ImpostorGroundColor = _impostorGroundColor;
        grid.Density  = _density;
        grid.SlopeMin = _slopeMin;
        grid.SlopeMax = _slopeMax;
        grid.Position = new Vector3(cell.X * GridSpacing, 0f, cell.Y * GridSpacing);

        // Set Extent last — its setter triggers Reload, which needs ChunkSize already set.
        grid.Extent = _extent;

        AddChild(grid);
        ResolveGridPaths(grid);

        if (Engine.IsEditorHint())
        {
            grid.Owner = GetTree().EditedSceneRoot;
            UpdateGizmos();
        }
    }

    /// <summary>Parses child node names to derive occupied grid cells.</summary>
    public Godot.Collections.Array<Vector2I> GetOccupiedCells()
    {
        var cells = new Godot.Collections.Array<Vector2I>();
        foreach (Node child in GetChildren())
        {
            var cell = GetCellFromName(child.Name);
            if (cell.HasValue)
                cells.Add(cell.Value);
        }
        return cells;
    }

    /// <summary>Returns the GrassGrid at a given cell, or null.</summary>
    public GrassGrid GetGridAtCell(Vector2I cell)
        => GetNodeOrNull<GrassGrid>($"GrassGrid_{cell.X}_{cell.Y}");

    /// <summary>Propagates Extent/ChunkSize changes to all child grids and repositions them.</summary>
    public void PropagateToGrids()
    {
        if (!IsInsideTree()) return;

        foreach (Node child in GetChildren())
        {
            if (child is not GrassGrid grid) continue;

            var cell = GetCellFromName(child.Name);
            if (!cell.HasValue) continue;

            // Set ChunkSize first (plain field, no Reload), then Extent (triggers one Reload).
            grid.ChunkSize = _chunkSize;
            grid.UseImpostor = _useImpostor;
            grid.ImpostorGroundColor = _impostorGroundColor;
            grid.Density  = _density;
            grid.SlopeMin = _slopeMin;
            grid.SlopeMax = _slopeMax;
            grid.Extent   = _extent;
            grid.Position = new Vector3(cell.Value.X * GridSpacing, 0f, cell.Value.Y * GridSpacing);

            ResolveGridPaths(grid);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Vector2I? GetCellFromName(StringName name)
    {
        var s = name.ToString();
        if (!s.StartsWith("GrassGrid_")) return null;
        var parts = s.Split('_');
        if (parts.Length == 3 && int.TryParse(parts[1], out int cx) && int.TryParse(parts[2], out int cz))
            return new Vector2I(cx, cz);
        return null;
    }

    /// <summary>
    /// Resolves TerrainPath and PlayerPath from the manager's perspective,
    /// then writes grid-relative paths onto the child GrassGrid.
    /// </summary>
    private void ResolveGridPaths(GrassGrid grid)
    {
        if (TerrainPath != null && !TerrainPath.IsEmpty)
        {
            var terrain = GetNodeOrNull<Node3D>(TerrainPath);
            if (terrain != null)
                grid.TerrainPath = grid.GetPathTo(terrain);
        }

        if (PlayerPath != null && !PlayerPath.IsEmpty)
        {
            var player = GetNodeOrNull<Node3D>(PlayerPath);
            if (player != null)
                grid.PlayerPath = grid.GetPathTo(player);
        }
    }
}
