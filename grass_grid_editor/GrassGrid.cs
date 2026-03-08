using Godot;

[Tool]
public partial class GrassGrid : Node3D
{
    private int _extent = 10;

    [Export]
    public int Extent
    {
        get => _extent;
        set
        {
            _extent = value;
            Reload();
        }
    }

    [Export] public float ChunkSize = 5.0f;
    [Export] public NodePath TerrainPath;
    [Export] public NodePath PlayerPath;
    [Export] public float ObjectRadius = 1.0f;

    private bool _useImpostor = true;
    [Export]
    public bool UseImpostor
    {
        get => _useImpostor;
        set { _useImpostor = value; Reload(); }
    }

    private Color _impostorGroundColor = new Color(0.3f, 0.6f, 0.1f, 1f);
    [Export]
    public Color ImpostorGroundColor
    {
        get => _impostorGroundColor;
        set { _impostorGroundColor = value; Reload(); }
    }

    private float _density = 1.0f;
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Density
    {
        get => _density;
        set { _density = value; Reload(); }
    }

    private float _slopeMin = 0f;
    [Export(PropertyHint.Range, "0,90,0.1")]
    public float SlopeMin
    {
        get => _slopeMin;
        set { _slopeMin = value; Reload(); }
    }

    private float _slopeMax = 90f;
    [Export(PropertyHint.Range, "0,90,0.1")]
    public float SlopeMax
    {
        get => _slopeMax;
        set { _slopeMax = value; Reload(); }
    }

    private static PackedScene _chunkScene;
    private static PackedScene ChunkScene => _chunkScene ??= GD.Load<PackedScene>("res://addons/grass_grid_editor/GrassChunk.tscn");

    private GrassChunk[] _chunks = System.Array.Empty<GrassChunk>();
    private GodotObject  _terrainData;

    public override void _Ready()
    {
        Reload();
    }

    public override void _ExitTree()
    {
        _chunks = System.Array.Empty<GrassChunk>();
    }

    public override void _Process(double delta)
    {
        if (_chunks.Length == 0)
            return;

        Vector3 cameraPos;
#if TOOLS
        if (Engine.IsEditorHint())
            cameraPos = EditorInterface.Singleton.GetEditorViewport3D().GetCamera3D().GlobalPosition;
        else
#endif
            cameraPos = GetViewport().GetCamera3D().GlobalPosition;

        GrassChunk.ResetFrameBudget();
        foreach (var chunk in _chunks)
            chunk.UpdateLod(cameraPos, _terrainData);

        // Update player position for grass bending
        if (!Engine.IsEditorHint() && PlayerPath != null && !PlayerPath.IsEmpty)
        {
            var player = GetNodeOrNull<Node3D>(PlayerPath);
            if (player != null)
                RenderingServer.GlobalShaderParameterSet("grass_object_position", player.GlobalPosition);
        }
    }

    public void Reload()
    {
        if (!IsInsideTree())
            return;

        _chunks = System.Array.Empty<GrassChunk>();

        foreach (Node child in GetChildren())
            child.Free();

        if (Engine.IsEditorHint())
        {
            UpdateEditorGizmo();
            return;
        }

        _terrainData = null;
        if (TerrainPath != null && !TerrainPath.IsEmpty)
        {
            var terrain = GetNodeOrNull<Node3D>(TerrainPath);
            _terrainData = terrain?.Get("data").AsGodotObject();
        }

        Vector3 origin = GlobalPosition;
        int count     = _extent + _extent / 2;
        int halfCount = count / 2;
        for (int x = -halfCount; x < count - halfCount; x++)
        {
            for (int z = -halfCount; z < count - halfCount; z++)
            {
                var chunk = ChunkScene.Instantiate<Node3D>();
                var gc = chunk as GrassChunk;
                if (gc != null)
                {
                    gc.UseImpostor = _useImpostor;
                    gc.ImpostorGroundColor = _impostorGroundColor;
                    gc.ObjectRadius = ObjectRadius;
                    gc.Density   = _density;
                    gc.SlopeMin  = _slopeMin;
                    gc.SlopeMax  = _slopeMax;
                }

                float localX = ChunkSize * x;
                float localZ = ChunkSize * z;
                float height = 0.0f;

                Basis chunkBasis = chunk.Basis;

                if (_terrainData != null)
                {
                    var worldPos = new Vector3(origin.X + localX, 0f, origin.Z + localZ);
                    float sampled = _terrainData.Call("get_height", worldPos).AsSingle();
                    if (!float.IsNaN(sampled))
                        height = sampled;

                    var normal = _terrainData.Call("get_normal", worldPos).AsVector3();
                    if (!float.IsNaN(normal.X))
                    {
                        var basis = chunk.Basis;
                        basis.Y = normal;
                        basis.X = -basis.Z.Cross(normal);
                        chunkBasis = basis.Orthonormalized();
                        chunk.Basis = chunkBasis;
                    }
                }

                chunk.Position = new Vector3(localX, height - origin.Y, localZ);
                AddChild(chunk);

                // Store build params for lazy streaming — the multimesh is built on
                // first approach rather than upfront, so only nearby chunks consume memory.
                if (gc != null && _terrainData != null)
                {
                    var chunkWorldOrigin = new Vector3(origin.X + localX, height, origin.Z + localZ);
                    gc.SetBuildParams(chunkWorldOrigin, chunkBasis, ChunkSize);
                }
            }
        }

        _chunks = new GrassChunk[GetChildCount()];
        for (int i = 0; i < _chunks.Length; i++)
            _chunks[i] = GetChild<GrassChunk>(i);

        UpdateEditorGizmo();
    }

    private void UpdateEditorGizmo()
    {
        if (!Engine.IsEditorHint()) return;

        int count     = _extent + _extent / 2;
        int halfCount = count / 2;
        float start = (-halfCount - 0.5f) * ChunkSize;
        float end   = (count - halfCount - 0.5f) * ChunkSize;

        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

        for (int i = 0; i <= count; i++)
        {
            float pos = start + i * ChunkSize;
            var color = (i == 0 || i == count)
                ? new Color(0f, 1f, 0.3f)
                : new Color(0f, 0.45f, 0.15f);

            mesh.SurfaceSetColor(color); mesh.SurfaceAddVertex(new Vector3(pos, 0f, start));
            mesh.SurfaceSetColor(color); mesh.SurfaceAddVertex(new Vector3(pos, 0f, end));
            mesh.SurfaceSetColor(color); mesh.SurfaceAddVertex(new Vector3(start, 0f, pos));
            mesh.SurfaceSetColor(color); mesh.SurfaceAddVertex(new Vector3(end, 0f, pos));
        }

        mesh.SurfaceEnd();

        var mat = new StandardMaterial3D();
        mat.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.NoDepthTest = true;
        mat.RenderPriority = 1;
        mesh.SurfaceSetMaterial(0, mat);

        var gizmo = new MeshInstance3D();
        gizmo.Name = "__GrassGizmo";
        gizmo.Mesh = mesh;
        gizmo.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(gizmo);
    }
}
