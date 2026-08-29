using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using S1Radar.Models;

namespace S1Radar.Parsing;

public static class VmfParser
{
    private static readonly Regex P = new(@"\((-?[0-9.+-Ee]+)\s+(-?[0-9.+-Ee]+)\s+(-?[0-9.+-Ee]+)\)", RegexOptions.Compiled);

    public static VmfMap Parse(string path)
    {
        var text = File.ReadAllText(path);
        var map = new VmfMap { MapName = Path.GetFileNameWithoutExtension(path) };
        var root = KeyValuesParser.Parse(text);

        var visRoot = root.Children.FirstOrDefault(x => x.Name.Equals("visgroups", StringComparison.OrdinalIgnoreCase));
        if (visRoot is not null)
        {
            foreach (var vg in visRoot.Children.Where(x => x.Name.Equals("visgroup", StringComparison.OrdinalIgnoreCase)))
            {
                if (int.TryParse(vg.Values.GetValueOrDefault("visgroupid"), out var id))
                    map.VisGroups[id] = vg.Values.GetValueOrDefault("name", "");
            }
        }

        foreach (var n in root.Children.Where(x => x.Name.Equals("world", StringComparison.OrdinalIgnoreCase)))
            ReadSolids(n, map, null, "worldspawn");

        foreach (var e in root.Children.Where(x => x.Name.Equals("entity", StringComparison.OrdinalIgnoreCase)))
            ReadEntity(e, map);

        return map;
    }

    static void ReadEntity(KvNode e, VmfMap map)
    {
        var ent = new VmfEntity { ClassName = e.Values.GetValueOrDefault("classname", "") };
        foreach (var kv in e.Values) ent.Properties[kv.Key] = kv.Value;
        ReadSolids(e, map, ent, ent.ClassName);
        map.Entities.Add(ent);
    }

    static void ReadSolids(KvNode n, VmfMap map, VmfEntity? ent, string className)
    {
        foreach (var s in n.Children.Where(x => x.Name.Equals("solid", StringComparison.OrdinalIgnoreCase)))
        {
            var b = new BrushSolid
            {
                Id = int.TryParse(s.Values.GetValueOrDefault("id"), out var id) ? id : map.Brushes.Count + (ent?.Solids.Count ?? 0) + 1,
                ClassName = className
            };

            var editor = s.Children.FirstOrDefault(x => x.Name.Equals("editor", StringComparison.OrdinalIgnoreCase));
            if (editor is not null)
            {
                foreach (var v in editor.Values)
                {
                    if (v.Key.Contains("visgroup", StringComparison.OrdinalIgnoreCase) && int.TryParse(v.Value, out var vgId) && map.VisGroups.TryGetValue(vgId, out var vgName))
                        b.Tags.Add(NormalizeTag(vgName));
                    else if (v.Key.Equals("visgroups", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var token in v.Value.Split(new[]{',',';',' '}, StringSplitOptions.RemoveEmptyEntries)) b.Tags.Add(NormalizeTag(token));
                    }
                }
            }

            foreach (var side in s.Children.Where(x => x.Name.Equals("side", StringComparison.OrdinalIgnoreCase)))
            {
                if (!side.Values.TryGetValue("plane", out var plane)) continue;
                var m = P.Matches(plane);
                if (m.Count < 3) continue;
                try
                {
                    var a = V(m[0]); var b2 = V(m[1]); var c = V(m[2]);
                    b.Faces.Add(new BrushFace { A=a, B=b2, C=c, Material=side.Values.GetValueOrDefault("material", "") });
                    map.Bounds = map.Bounds.IsValid ? map.Bounds.Include(a).Include(b2).Include(c) : new Bounds3(Vector3.Min(a, Vector3.Min(b2,c)), Vector3.Max(a, Vector3.Max(b2,c)));
                }
                catch { }
            }

            if (b.Faces.Count == 0) continue;
            b.ApproxCenter = b.Faces.SelectMany(f => new[]{f.A,f.B,f.C}).Aggregate(Vector3.Zero, (v,p)=>v+p) / (b.Faces.Count*3f);
            if (ent is null) map.Brushes.Add(b); else ent.Solids.Add(b);
        }
    }

    static string NormalizeTag(string s) => s.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
    static Vector3 V(Match m) => new(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
}
