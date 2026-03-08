using Godot;
using System.Threading.Tasks;

[Tool]
public partial class GrassChunk : Node3D
{
    [Export] public float LodSwitch = 10.0f;
    [Export] public float ImpostorFadeInStart = 5.0f;
    [Export] public float ImpostorFadeInEnd = 10.0f;
    [Export] public float GrassFadeOutStart = 10.0f;
    [Export] public float GrassFadeOutEnd = 20.0f;
    [Export] public float OverlapFactor = 1.3f;
    /// <summary>
    /// Resolution of the height/normal sampling grid (n×n queries per chunk).
    /// Higher = more accurate blade placement on steep/varied terrain.
    /// </summary>
    [Export] public int HeightGridResolution = 5;
    [Export] public bool UseImpostor = true;
    [Export] public Color ImpostorGroundColor = new Color(0.3f, 0.6f, 0.1f, 1f);
    [Export] public float ObjectRadius = 1.0f;
    /// <summary>Fraction of baked instances to spawn (0–1). 1 = full density from the .tres asset.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float Density = 1.0f;
    /// <summary>Minimum terrain slope (degrees) for grass to appear. 0 = flat.</summary>
    [Export(PropertyHint.Range, "0,90,0.1")] public float SlopeMin = 0f;
    /// <summary>Maximum terrain slope (degrees) for grass to appear. 90 = vertical cliff.</summary>
    [Export(PropertyHint.Range, "0,90,0.1")] public float SlopeMax = 90f;

    // Pre-baked fallbacks — lazy-loaded on first access.
    private static MultiMesh _bakedDetailed;
    private static MultiMesh _bakedSimple;
    private static MultiMesh BakedDetailed => _bakedDetailed ??= GD.Load<MultiMesh>("res://addons/grass_grid_editor/grass_multimesh_detailed.tres");
    private static MultiMesh BakedSimple   => _bakedSimple   ??= GD.Load<MultiMesh>("res://addons/grass_grid_editor/grass_multimesh_simple.tres");

    // Baked transforms read once on the main thread, then shared as read-only by background tasks.
    private static Transform3D[] _cachedDetailedTransforms;
    private static Transform3D[] _cachedSimpleTransforms;

    private MultiMeshInstance3D _grass;
    private MeshInstance3D _impostor;
    private bool _fullyHidden;
    private float _scaleX = 1f, _scaleZ = 1f; // grass XZ scale, stored for correct height sampling

    private MultiMesh _runtimeDetailed;
    private MultiMesh _runtimeSimple;

    // Build params stored at setup time so builds can be triggered lazily on first approach.
    private Vector3 _buildOrigin;
    private Basis   _buildBasis;
    private float   _buildChunkSize;
    private bool    _runtimeBuilt;

    // Active async build. Null when idle. Setting to null abandons the in-flight task
    // (background thread finishes but its result is discarded and GC'd).
    private Task<(Transform3D[] detailed, Transform3D[] simple)> _buildTask;

    // Per-frame budget counters — reset by GrassGrid at the start of each _Process.
    // Limits main-thread spike (applies) and memory/thread-pool pressure (starts).
    private static int _appliesThisFrame;
    private static int _startsThisFrame;
    private const  int MaxAppliesPerFrame = 2;
    private const  int MaxStartsPerFrame  = 4;

    public static void ResetFrameBudget()
    {
        _appliesThisFrame = 0;
        _startsThisFrame  = 0;
    }

    public override void _Ready()
    {
        _grass    = GetNode<MultiMeshInstance3D>("Grass");
        _impostor = GetNode<MeshInstance3D>("Impostor");

        float lenX = new Vector2(Basis.X.X, Basis.X.Z).Length();
        float lenZ = new Vector2(Basis.Z.X, Basis.Z.Z).Length();
        float sx = lenX > 0.01f ? OverlapFactor / lenX : OverlapFactor;
        float sz = lenZ > 0.01f ? OverlapFactor / lenZ : OverlapFactor;
        _scaleX = sx;
        _scaleZ = sz;
        _grass.Scale = new Vector3(sx, 1f, sz);

        // Duplicate materials so alpha can be set per-chunk without using the global
        // per-instance shader parameter buffer (which has a fixed 4096-slot limit).
        if (_grass.MaterialOverride != null)
            _grass.MaterialOverride = (ShaderMaterial)_grass.MaterialOverride.Duplicate();
        if (_impostor.MaterialOverride != null)
            _impostor.MaterialOverride = (ShaderMaterial)_impostor.MaterialOverride.Duplicate();

        (_grass.MaterialOverride as ShaderMaterial)
            ?.SetShaderParameter("object_radius", ObjectRadius);

        if (!UseImpostor)
        {
            _impostor.Visible = false;
        }
        else
        {
            (_impostor.MaterialOverride as ShaderMaterial)
                ?.SetShaderParameter("ground_color", ImpostorGroundColor);
        }
    }

    public override void _ExitTree() => FreeRuntimeData();

    private void FreeRuntimeData()
    {
        // Abandon any in-progress background task. The thread finishes on its own
        // but the result tuple is never read, so the Transform3D[] arrays are GC'd.
        _buildTask = null;

        if (_grass != null)
            _grass.Multimesh = null;

        _runtimeDetailed?.Dispose();
        _runtimeDetailed = null;
        _runtimeSimple?.Dispose();
        _runtimeSimple   = null;
        _runtimeBuilt    = false;
    }

    /// <summary>
    /// Store build parameters so the terrain-conformed multimesh can be built
    /// lazily on first approach rather than upfront for every chunk in the world.
    /// </summary>
    public void SetBuildParams(Vector3 worldOrigin, Basis worldBasis, float chunkSize)
    {
        _buildOrigin    = worldOrigin;
        _buildBasis     = worldBasis;
        _buildChunkSize = chunkSize;
        _runtimeBuilt   = false;
    }

    public void UpdateLod(Vector3 cameraPos, GodotObject terrainData)
    {
        // ── 1. Apply completed async build (main thread) ──────────────────────
        // CreateMultiMesh runs here — it calls Godot API (SetInstanceTransform) so
        // it must be on the main thread, but it's fast: no terrain queries, just
        // writing pre-computed floats into a new MultiMesh object.
        // Budget-gated: at most MaxAppliesPerFrame chunks per frame to avoid a
        // main-thread spike when many tasks finish simultaneously.
        if (_buildTask != null && _buildTask.IsCompleted && _appliesThisFrame < MaxAppliesPerFrame)
        {
            if (_buildTask.IsCompletedSuccessfully)
            {
                var (dt, st) = _buildTask.Result;
                _runtimeDetailed?.Dispose();
                _runtimeSimple?.Dispose();
                _runtimeDetailed = CreateMultiMesh(BakedDetailed, dt, Density);
                _runtimeSimple   = CreateMultiMesh(BakedSimple,   st, Density);
                _runtimeBuilt    = true;
            }
            _buildTask = null;
            _appliesThisFrame++;
        }

        float distance = GlobalPosition.DistanceTo(cameraPos);

        if (distance >= GrassFadeOutEnd)
        {
            if (_runtimeBuilt)
                FreeRuntimeData();

            if (!_fullyHidden)
            {
                _grass.Visible    = false;
                _impostor.Visible = false;
                _fullyHidden      = true;
            }
            return;
        }

        // ── 2. Kick off async build if not yet built and not already building ─
        // Budget-gated: at most MaxStartsPerFrame new tasks per frame to cap
        // memory usage (each task allocates ~48 KB of Transform3D arrays) and
        // avoid flooding the thread pool on large area transitions.
        if (!_runtimeBuilt && _buildTask == null && terrainData != null && _startsThisFrame < MaxStartsPerFrame)
        {
            _startsThisFrame++;
            int n = Mathf.Max(2, HeightGridResolution);

            // Terrain sampling MUST happen on the main thread (GDScript calls).
            // Cache baked transforms on main thread once; background tasks then
            // treat the arrays as read-only so no locking is needed.
            Transform3D[] dtSrc = EnsureCachedTransforms(BakedDetailed, ref _cachedDetailedTransforms);
            Transform3D[] stSrc = EnsureCachedTransforms(BakedSimple,   ref _cachedSimpleTransforms);

            Transform3D chunkTransform = new Transform3D(_buildBasis, _buildOrigin);
            Vector3     origin         = _buildOrigin;
            float       size           = _buildChunkSize;
            float       slopeMin       = SlopeMin;
            float       slopeMax       = SlopeMax;
            float       scaleX         = _scaleX;
            float       scaleZ         = _scaleZ;

            // Extend the sample area to cover the full overlap footprint so edge blades
            // get interpolated (not clamped) heights. Use enough grid points to keep
            // roughly the same step density as the original chunk-sized grid.
            float sampleSize = size * OverlapFactor;
            int   nSample    = Mathf.CeilToInt((float)(n - 1) * OverlapFactor) + 1;
            var (heightGrid, normalYGrid) = SampleTerrainGrids(terrainData, origin, sampleSize, nSample);

            // Pre-extract basis Y-row components for the correct local-Y formula.
            // For a tilted chunk the old inverseTransform approach gave wrong results.
            float bXY = _buildBasis.X.Y;
            float bYY = _buildBasis.Y.Y;
            float bZY = _buildBasis.Z.Y;

            // Heavy lifting (height correction + slope filtering) runs on a
            // thread-pool thread — pure float math, no Godot API calls.
            _buildTask = Task.Run(() =>
            (
                ComputeTransforms(dtSrc, heightGrid, normalYGrid, chunkTransform, origin, sampleSize, nSample, slopeMin, slopeMax, scaleX, scaleZ, bXY, bYY, bZY),
                ComputeTransforms(stSrc, heightGrid, normalYGrid, chunkTransform, origin, sampleSize, nSample, slopeMin, slopeMax, scaleX, scaleZ, bXY, bYY, bZY)
            ));
        }

        // ── 3. LOD visibility ─────────────────────────────────────────────────
        _fullyHidden = false;

        _grass.Multimesh = distance < LodSwitch
            ? (_runtimeDetailed ?? BakedDetailed)
            : (_runtimeSimple   ?? BakedSimple);

        float startToMid = Mathf.SmoothStep(ImpostorFadeInStart, ImpostorFadeInEnd, distance);
        float midToEnd   = Mathf.SmoothStep(GrassFadeOutStart,   GrassFadeOutEnd,   distance);

        _grass.Visible    = midToEnd < 1.0f;
        _impostor.Visible = UseImpostor && startToMid > 0.0f;

        (_impostor.MaterialOverride as ShaderMaterial)?.SetShaderParameter("alpha", startToMid);
        (_grass.MaterialOverride as ShaderMaterial)?.SetShaderParameter("alpha", 1.0f - midToEnd);
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Reads all instance transforms from a baked MultiMesh into a C# array.
    /// Called on the main thread the first time a chunk of each LOD type is built;
    /// subsequent calls return the cached array instantly.
    /// </summary>
    private static Transform3D[] EnsureCachedTransforms(MultiMesh mm, ref Transform3D[] cache)
    {
        if (cache != null) return cache;
        int count = mm.InstanceCount;
        var arr = new Transform3D[count];
        for (int i = 0; i < count; i++)
            arr[i] = mm.GetInstanceTransform(i);
        cache = arr;
        return cache;
    }

    /// <summary>
    /// Pure C# math — safe to run on any thread. Corrects each blade's Y via
    /// bilinear height interpolation and filters by slope range.
    /// </summary>
    private static Transform3D[] ComputeTransforms(Transform3D[] source,
        float[,] heightGrid, float[,] normalYGrid,
        Transform3D chunkTransform, Vector3 origin, float sampleSize, int n,
        float slopeMin, float slopeMax, float scaleX, float scaleZ,
        float bXY, float bYY, float bZY)
    {
        // normal.Y = cos(slope_angle): steeper slope → smaller normalY.
        float nyMin       = Mathf.Cos(Mathf.DegToRad(slopeMax));
        float nyMax       = Mathf.Cos(Mathf.DegToRad(slopeMin));
        bool  filterSlope = slopeMin > 0f || slopeMax < 90f;

        // Guard against degenerate tilt (chunk nearly vertical — shouldn't happen).
        float safeBYY = Mathf.Abs(bYY) > 0.001f ? bYY : 1f;

        var result = new System.Collections.Generic.List<Transform3D>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            var   t        = source[i];
            var   localPos = t.Origin;
            float scaledLX = localPos.X * scaleX;
            float scaledLZ = localPos.Z * scaleZ;

            // World XZ where the blade actually renders (node scale shifts its footprint).
            var worldPos = chunkTransform * new Vector3(scaledLX, 0f, scaledLZ);

            if (filterSlope)
            {
                float ny = BilinearSample(normalYGrid, worldPos.X, worldPos.Z, origin, sampleSize, n);
                if (ny < nyMin || ny > nyMax) continue;
            }

            float worldH = BilinearSample(heightGrid, worldPos.X, worldPos.Z, origin, sampleSize, n);

            // Solve for the local Y that makes the blade's world Y == worldH.
            // Derived from: chunkOrigin.Y + basis*(sx*lx, localY, sz*lz)).Y = worldH
            // → localY = (worldH - origin.Y - bXY*scaledLX - bZY*scaledLZ) / bYY
            // The old inverseTransform approach was wrong for tilted chunks because it
            // solved for a different vector than the one Godot actually renders.
            float localY = (worldH - origin.Y - bXY * scaledLX - bZY * scaledLZ) / safeBYY;

            t.Origin = new Vector3(localPos.X, localY, localPos.Z);
            result.Add(t);
        }
        return result.ToArray();
    }

    /// <summary>
    /// Creates a MultiMesh on the main thread from pre-computed transforms.
    /// Fast: no terrain queries, just bulk-writing floats to a new Godot object.
    /// </summary>
    private static MultiMesh CreateMultiMesh(MultiMesh source, Transform3D[] transforms, float density = 1f)
    {
        int count = density >= 1f
            ? transforms.Length
            : Mathf.Max(1, Mathf.RoundToInt(transforms.Length * Mathf.Clamp(density, 0f, 1f)));

        var mm = new MultiMesh();
        mm.Mesh            = source.Mesh;
        mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        mm.InstanceCount   = count;
        for (int i = 0; i < count; i++)
            mm.SetInstanceTransform(i, transforms[i]);
        return mm;
    }

    // Samples n×n grids of height and normal.Y in one pass — 2×n² terrain queries total.
    private static (float[,] heights, float[,] normalY) SampleTerrainGrids(
        GodotObject terrainData, Vector3 origin, float size, int n)
    {
        var   heights = new float[n, n];
        var   normalY = new float[n, n];
        float half    = size / 2f;
        float step    = size / (n - 1);

        for (int gx = 0; gx < n; gx++)
        {
            for (int gz = 0; gz < n; gz++)
            {
                var pos = new Vector3(origin.X - half + gx * step, 0f, origin.Z - half + gz * step);

                float h = terrainData.Call("get_height", pos).AsSingle();
                heights[gx, gz] = float.IsNaN(h) ? origin.Y : h;

                var normal = terrainData.Call("get_normal", pos).AsVector3();
                normalY[gx, gz] = float.IsNaN(normal.Y) ? 1f : normal.Y;
            }
        }

        return (heights, normalY);
    }

    // Bilinear interpolation of the height grid at a world (wx, wz) position.
    private static float BilinearSample(float[,] grid, float wx, float wz, Vector3 origin, float size, int n)
    {
        float half = size / 2f;
        float fx = (wx - (origin.X - half)) / size * (n - 1);
        float fz = (wz - (origin.Z - half)) / size * (n - 1);

        fx = Mathf.Clamp(fx, 0f, n - 1.001f);
        fz = Mathf.Clamp(fz, 0f, n - 1.001f);

        int x0 = (int)fx, x1 = x0 + 1;
        int z0 = (int)fz, z1 = z0 + 1;
        float tx = fx - x0, tz = fz - z0;

        return Mathf.Lerp(
            Mathf.Lerp(grid[x0, z0], grid[x1, z0], tx),
            Mathf.Lerp(grid[x0, z1], grid[x1, z1], tx),
            tz);
    }
}
