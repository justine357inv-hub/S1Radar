using System.Numerics;
using S1Radar.Models;

namespace S1Radar.Geometry;

/// <summary>Source brush reconstruction using plane/plane/plane intersections.</summary>
public static class CsgEngine
{
    private const float ParallelEpsilon = 1e-5f;
    private const float PlaneTolerance = 1.25f;

    public static IEnumerable<Surface> ReconstructSurfaces(VmfMap map, CancellationToken ct = default)
    {
        foreach (var brush in map.Brushes)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var item in ReconstructBrush(brush, ct)) yield return item;
        }
        foreach (var entity in map.Entities)
        {
            foreach (var brush in entity.Solids)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var item in ReconstructBrush(brush, ct)) yield return item;
            }
        }
    }

    // Kept as a compatibility entry point for older callers.
    public static IEnumerable<Surface> ExtractTopSurfaces(VmfMap map, CancellationToken ct = default) => ReconstructSurfaces(map, ct);

    private static IEnumerable<Surface> ReconstructBrush(BrushSolid brush, CancellationToken ct)
    {
        if (brush.Faces.Count < 4) yield break;

        var planes = new List<PlaneData>(brush.Faces.Count);
        foreach (var f in brush.Faces)
        {
            var n = f.Normal;
            if (n.LengthSquared() < 1e-8f || !float.IsFinite(n.X+n.Y+n.Z)) continue;
            var d = -Vector3.Dot(n, f.A);
            // Source brush interior is always the side containing an interior sample.
            // Orient each plane so the approximate brush center is behind the plane.
            if (Vector3.Dot(n, brush.ApproxCenter) + d > 0)
            {
                n = -n; d = -d;
            }
            planes.Add(new PlaneData(n, d, f));
        }
        if (planes.Count < 4) yield break;

        var vertices = new List<Vector3>();
        for (int i=0;i<planes.Count;i++) for (int j=i+1;j<planes.Count;j++)
        for (int k=j+1;k<planes.Count;k++)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryIntersect(planes[i], planes[j], planes[k], out var p)) continue;
            var inside = true;
            foreach (var pl in planes)
            {
                if (Vector3.Dot(pl.N,p)+pl.D > PlaneTolerance) { inside=false; break; }
            }
            if (inside && !vertices.Any(v => Vector3.DistanceSquared(v,p) < 0.25f)) vertices.Add(p);
        }
        if (vertices.Count < 4) yield break;

        foreach (var facePlane in planes)
        {
            var onFace = vertices.Where(v => MathF.Abs(Vector3.Dot(facePlane.N,v)+facePlane.D) <= PlaneTolerance).ToList();
            if (onFace.Count < 3) continue;
            var poly = OrderCoplanar(onFace, facePlane.N);
            if (poly.Count < 3) continue;
            var area = PolygonArea(poly);
            if (area < 2f) continue;

            var sourceNormal = facePlane.N;
            var slope = MathF.Acos(Math.Clamp(sourceNormal.Z,-1,1))*180f/MathF.PI;
            var upward = sourceNormal.Z > 0.10f;
            if (!upward) continue;

            var zMin = onFace.Min(v=>v.Z); var zMax = onFace.Max(v=>v.Z); var z = onFace.Average(v=>v.Z);
            yield return new Surface(poly.Select(v=>new Vector2(v.X,v.Y)).ToArray(), z, zMin, zMax,
                slope, SurfaceKind.Ground, brush.Id, 0, slope <= 48f, 1f, brush.Tags.FirstOrDefault() ?? "", facePlane.Original.Material);
        }
    }

    private static List<Vector3> OrderCoplanar(List<Vector3> points, Vector3 normal)
    {
        var center = points.Aggregate(Vector3.Zero,(v,p)=>v+p)/points.Count;
        var u = Vector3.Cross(MathF.Abs(normal.Z) > .8f ? Vector3.UnitX : Vector3.UnitZ, normal);
        if (u.LengthSquared() < 1e-8f) u = Vector3.UnitY;
        u = Vector3.Normalize(u);
        var v = Vector3.Normalize(Vector3.Cross(normal,u));
        return points.OrderBy(p => MathF.Atan2(Vector3.Dot(p-center,v), Vector3.Dot(p-center,u))).ToList();
    }

    private static bool TryIntersect(PlaneData a, PlaneData b, PlaneData c, out Vector3 point)
    {
        var n1=a.N; var n2=b.N; var n3=c.N;
        var det=Vector3.Dot(n1,Vector3.Cross(n2,n3));
        if(MathF.Abs(det)<ParallelEpsilon){point=default;return false;}
        point=(Vector3.Cross(n2,n3)*(-a.D)+Vector3.Cross(n3,n1)*(-b.D)+Vector3.Cross(n1,n2)*(-c.D))/det;
        return float.IsFinite(point.X+point.Y+point.Z);
    }

    private static float PolygonArea(IReadOnlyList<Vector3> p)
    {
        var n = p.Count; if(n<3)return 0;
        var normal=Vector3.Normalize(Vector3.Cross(p[1]-p[0],p[2]-p[0]));
        double area=0;
        for(int i=1;i<n-1;i++) area += Vector3.Cross(p[i]-p[0],p[i+1]-p[0]).Length()/2.0;
        return (float)Math.Abs(area);
    }

    private readonly record struct PlaneData(Vector3 N,float D,BrushFace Original);
}
