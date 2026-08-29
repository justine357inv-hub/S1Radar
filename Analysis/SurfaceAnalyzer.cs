using System.Numerics;
using S1Radar.Models;

namespace S1Radar.Analysis;

public static class SurfaceAnalyzer
{
    public static RadarScene Analyze(VmfMap map, IEnumerable<Surface> input)
    {
        var surfaces = input.Where(s=>s.Area>=2).ToList();
        var levelBands = BuildLevelBands(surfaces);
        var taggedBrushes = map.Brushes.Concat(map.Entities.SelectMany(e=>e.Solids)).ToDictionary(b=>b.Id);
        for (int i=0;i<surfaces.Count;i++)
        {
            var s=surfaces[i];
            var level=levelBands.OrderBy(l=>MathF.Abs(l.RepresentativeZ-s.Z)).FirstOrDefault();
            var kind=Classify(s, taggedBrushes.TryGetValue(s.BrushId,out var b) ? b.Tags : null, level?.Id ?? 0, map.Bounds);
            var walkable = kind is SurfaceKind.Ground or SurfaceKind.Ramp or SurfaceKind.Stairs;
            surfaces[i]=s with { Kind=kind, LevelId=level?.Id ?? 0, IsWalkable=walkable };
        }

        // Reduce redundant fragments on the same level by discarding deeply-contained tiny islands.
        surfaces = RemoveRedundantFragments(surfaces).Where(s=>s.Kind!=SurfaceKind.Unknown).ToList();
        var levels = BuildLevelInfos(surfaces);
        var markers = DetectMarkers(map);
        var components = CountWalkableComponents(surfaces);
        var bounds = map.Bounds;
        return new RadarScene
        {
            Surfaces=surfaces,
            Levels=levels,
            Markers=markers,
            Bounds=bounds,
            ElevationMin=surfaces.Count==0?0:surfaces.Min(s=>s.Z),
            ElevationMax=surfaces.Count==0?0:surfaces.Max(s=>s.Z),
            ConnectedWalkableComponents=components,
            IsolatedWalkableRegions=Math.Max(0,components-1)
        };
    }

    private static List<LevelBand> BuildLevelBands(IReadOnlyList<Surface> surfaces)
    {
        if(surfaces.Count==0)return [];
        var z=surfaces.Select(s=>s.Z).OrderBy(x=>x).ToList();
        var clusters=new List<List<float>>();
        foreach(var value in z)
        {
            if(clusters.Count==0 || value-clusters[^1].Average()>72f) clusters.Add([value]);
            else clusters[^1].Add(value);
        }
        return clusters.Select((c,i)=>new LevelBand(i,c.Min(),c.Max(),c.Average())).ToList();
    }

    private static List<LevelInfo> BuildLevelInfos(IReadOnlyList<Surface> surfaces)
    {
        var bands=BuildLevelBands(surfaces);
        return bands.Select(b=>new LevelInfo(b.Id,b.MinZ,b.MaxZ,b.RepresentativeZ,
            surfaces.Count(s=>s.LevelId==b.Id), surfaces.Where(s=>s.IsWalkable&&s.LevelId==b.Id).Sum(s=>s.Area))).ToList();
    }

    private static SurfaceKind Classify(Surface s, HashSet<string>? tags, int level, Bounds3 mapBounds)
    {
        if(tags is not null)
        {
            if(tags.Contains("s1_remove")) return SurfaceKind.Unknown;
            if(tags.Contains("s1_overlap")) return SurfaceKind.Overlap;
            if(tags.Contains("s1_cover")) return SurfaceKind.Cover;
            if(tags.Contains("s1_wall")) return SurfaceKind.Wall;
            if(tags.Contains("s1_detail")) return SurfaceKind.Detail;
            if(tags.Contains("s1_ramp")) return SurfaceKind.Ramp;
            if(tags.Contains("s1_stairs")) return SurfaceKind.Stairs;
            if(tags.Contains("s1_objective")) return SurfaceKind.Objective;
            if(tags.Contains("s1_door")) return SurfaceKind.Door;
            if(tags.Contains("s1_path")) return s.Slope>6 ? SurfaceKind.Ramp : SurfaceKind.Ground;
        }
        if(s.Slope > 48f) return SurfaceKind.Wall;
        if(s.Slope > 14f) return SurfaceKind.Ramp;
        if(level>0 && s.Z-mapBounds.Min.Z>96f) return SurfaceKind.Overlap;
        return SurfaceKind.Ground;
    }

    private readonly record struct LevelBand(int Id,float MinZ,float MaxZ,float RepresentativeZ);

    private static List<Surface> RemoveRedundantFragments(List<Surface> surfaces)
    {
        return surfaces.Where((s,i)=>s.Area>=4 || !surfaces.Any((o,j)=>j!=i && o.LevelId==s.LevelId && o.Area>=s.Area*8f && PointInPolygon(s.Centroid,o.Polygon))).ToList();
    }

    private static int CountWalkableComponents(IReadOnlyList<Surface> surfaces)
    {
        var w=surfaces.Where(s=>s.IsWalkable).ToList(); if(w.Count==0)return 0;
        var seen=new bool[w.Count]; int components=0;
        for(int i=0;i<w.Count;i++) if(!seen[i])
        {
            components++; var q=new Queue<int>(); q.Enqueue(i); seen[i]=true;
            while(q.Count>0){var a=q.Dequeue(); for(int b=0;b<w.Count;b++) if(!seen[b]&&w[a].LevelId==w[b].LevelId&&OverlapsOrNear(w[a],w[b],8f)){seen[b]=true;q.Enqueue(b);}}
        }
        return components;
    }

    private static bool OverlapsOrNear(Surface a,Surface b,float margin)
    {
        var aa=Bounds2(a.Polygon); var bb=Bounds2(b.Polygon);
        if(aa.Max.X+margin<bb.Min.X||bb.Max.X+margin<aa.Min.X||aa.Max.Y+margin<bb.Min.Y||bb.Max.Y+margin<aa.Min.Y)return false;
        return PointInPolygon(a.Centroid,b.Polygon)||PointInPolygon(b.Centroid,a.Polygon)||Distance(a.Centroid,b.Centroid)<margin*6;
    }

    private static List<TacticalMarker> DetectMarkers(VmfMap map)
    {
        var list=new List<TacticalMarker>();
        foreach(var e in map.Entities)
        {
            if(e.ClassName.Equals("func_bomb_target",StringComparison.OrdinalIgnoreCase)) list.Add(new("bombsite",EntityOrigin(e),e.Properties.GetValueOrDefault("targetname","Bombsite")));
            else if(e.ClassName is "info_player_start" or "info_player_terrorist" || e.ClassName.Equals("info_player_counterterrorist",StringComparison.OrdinalIgnoreCase)) list.Add(new("spawn",EntityOrigin(e),e.ClassName));
            else if(e.ClassName.Equals("func_buyzone",StringComparison.OrdinalIgnoreCase)) list.Add(new("buyzone",EntityOrigin(e),"Buy Zone"));
        }
        return list;
    }

    private static Vector2 EntityOrigin(VmfEntity e)
    {
        var o=e.Properties.GetValueOrDefault("origin","0 0 0").Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(o.Length>=2 && float.TryParse(o[0],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var x) && float.TryParse(o[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var y)) return new Vector2(x,y);
        return Vector2.Zero;
    }

    private static (Vector2 Min,Vector2 Max) Bounds2(IReadOnlyList<Vector2> p)=> (new(p.Min(x=>x.X),p.Min(x=>x.Y)),new(p.Max(x=>x.X),p.Max(x=>x.Y)));
    private static float Distance(Vector2 a,Vector2 b)=>Vector2.Distance(a,b);
    private static bool PointInPolygon(Vector2 p,IReadOnlyList<Vector2> poly){bool inside=false;for(int i=0,j=poly.Count-1;i<poly.Count;j=i++){var a=poly[i];var b=poly[j];if(((a.Y>p.Y)!=(b.Y>p.Y))&&(p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y+1e-8f)+a.X))inside=!inside;}return inside;}
}
