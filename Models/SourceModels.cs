using System.Numerics;

namespace S1Radar.Models;

public sealed class VmfMap
{
    public string MapName { get; set; } = "untitled";
    public List<BrushSolid> Brushes { get; } = [];
    public List<VmfEntity> Entities { get; } = [];
    public Dictionary<int, string> VisGroups { get; } = new();
    public Bounds3 Bounds { get; set; } = Bounds3.Empty;
}

public sealed class BrushSolid
{
    public int Id { get; init; }
    public List<BrushFace> Faces { get; } = [];
    public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string ClassName { get; init; } = "worldspawn";
    public Vector3 ApproxCenter { get; set; }
}

public sealed class BrushFace
{
    public Vector3 A { get; init; }
    public Vector3 B { get; init; }
    public Vector3 C { get; init; }
    public string Material { get; init; } = "";
    public Vector3 Normal
    {
        get
        {
            var n = Vector3.Cross(B - A, C - A);
            var len = n.Length();
            return len < 1e-5f ? Vector3.Zero : n / len;
        }
    }
}

public sealed class VmfEntity
{
    public string ClassName { get; init; } = "";
    public Dictionary<string,string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<BrushSolid> Solids { get; } = [];
}

public readonly record struct Bounds3(Vector3 Min, Vector3 Max)
{
    public static Bounds3 Empty => new(new(float.PositiveInfinity), new(float.NegativeInfinity));
    public bool IsValid => float.IsFinite(Min.X) && float.IsFinite(Max.X) && Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;
    public Bounds3 Include(Vector3 p) => new(Vector3.Min(Min,p), Vector3.Max(Max,p));
    public float Width => Max.X - Min.X;
    public float Height => Max.Y - Min.Y;
    public float Depth => Max.Z - Min.Z;
    public Vector3 Center => (Min + Max) * 0.5f;
}

public enum SurfaceKind { Ground, Ramp, Stairs, Cover, Overlap, Wall, Detail, Objective, Spawn, BuyZone, Door, Unknown }

public sealed record Surface(
    IReadOnlyList<Vector2> Polygon,
    float Z,
    float MinZ,
    float MaxZ,
    float Slope,
    SurfaceKind Kind,
    int BrushId,
    int LevelId = 0,
    bool IsWalkable = false,
    float Confidence = 1f,
    string Tag = "",
    string Material = ""
)
{
    public float Area => MathF.Abs(GeometryArea(Polygon));
    public Vector2 Centroid => Polygon.Count == 0 ? Vector2.Zero : Polygon.Aggregate(Vector2.Zero, static (a,p) => a+p) / Polygon.Count;

    static float GeometryArea(IReadOnlyList<Vector2> p)
    {
        if (p.Count < 3) return 0;
        double a = 0;
        for (int i=0;i<p.Count;i++) { var q=p[(i+1)%p.Count]; a += (double)p[i].X*q.Y - (double)q.X*p[i].Y; }
        return (float)(a*0.5);
    }
}

public sealed record LevelInfo(int Id, float MinZ, float MaxZ, float RepresentativeZ, int SurfaceCount, float WalkableArea);

public sealed record TacticalMarker(string Kind, Vector2 Position, string Label, int Team = 0);

public sealed class RadarScene
{
    public IReadOnlyList<Surface> Surfaces { get; init; } = [];
    public IReadOnlyList<LevelInfo> Levels { get; init; } = [];
    public IReadOnlyList<TacticalMarker> Markers { get; init; } = [];
    public Bounds3 Bounds { get; init; } = Bounds3.Empty;
    public float ElevationMin { get; init; }
    public float ElevationMax { get; init; }
    public int ConnectedWalkableComponents { get; init; }
    public int IsolatedWalkableRegions { get; init; }
}
