using System.Numerics;
using SkiaSharp;
using S1Radar.Models;

namespace S1Radar.Rendering;

public sealed class LevelStyle
{
    public SKColor LowColor { get; set; }
    public SKColor HighColor { get; set; }
    public byte Opacity { get; set; } = 255;
    public bool Visible { get; set; } = true;
}

public sealed class RadarStyle
{
    public SKColor Background { get; set; } = new(12,14,17);
    public SKColor CoverColor { get; set; } = new(105,112,120);
    public SKColor OutlineColor { get; set; } = new(20,22,25);
    public SKColor MarkerColor { get; set; } = new(245,245,245);
    public byte OverlapAlpha { get; set; } = 153;
    public Dictionary<int,LevelStyle> Levels { get; } = new();

    public LevelStyle GetLevelStyle(int level)
    {
        if (Levels.TryGetValue(level, out var style)) return style;
        var styles = new[] {
            new LevelStyle { LowColor=new(55,65,78), HighColor=new(220,225,230) },
            new LevelStyle { LowColor=new(48,58,72), HighColor=new(190,205,220), Opacity=235 },
            new LevelStyle { LowColor=new(62,58,78), HighColor=new(205,195,220), Opacity=210 }
        };
        var s=styles[Math.Min(level,styles.Length-1)]; Levels[level]=s; return s;
    }
}

public static class RadarRenderer
{
    public static SKBitmap Render(RadarScene scene,int size,RadarStyle style,bool showMarkers=true)
    {
        var bmp=new SKBitmap(size,size); using var canvas=new SKCanvas(bmp); canvas.Clear(style.Background);
        if(!scene.Bounds.IsValid||scene.Bounds.Width<=0||scene.Bounds.Height<=0)return bmp;
        var pad=size*.045f;
        var scale=MathF.Min((size-2*pad)/scene.Bounds.Width,(size-2*pad)/scene.Bounds.Height);
        SKPoint T(Vector2 p)=>new(pad+(p.X-scene.Bounds.Min.X)*scale,size-(pad+(p.Y-scene.Bounds.Min.Y)*scale));
        var zMin=scene.ElevationMin; var zMax=scene.ElevationMax;

        var drawable=scene.Surfaces.Where(s=>style.GetLevelStyle(s.LevelId).Visible && s.Kind!=SurfaceKind.Unknown).OrderBy(s=>Layer(s.Kind)).ToList();
        foreach(var group in drawable.Where(s=>s.Kind is not (SurfaceKind.Ramp or SurfaceKind.Stairs)).GroupBy(s=>(s.LevelId,s.Kind)))
        {
            using var combined=new SKPath();
            foreach(var s in group)
            {
                var pts=s.Polygon.Select(T).ToArray(); if(pts.Length<3)continue;
                using var p=new SKPath(); p.MoveTo(pts[0]); for(int i=1;i<pts.Length;i++)p.LineTo(pts[i]); p.Close();
                if(combined.CountPoints == 0) combined.AddPath(p); else if(!combined.Op(p,SKPathOp.Union)) combined.AddPath(p);
            }
            DrawGroup(canvas,combined,group.Key.LevelId,group.Key.Kind,style,zMin,zMax,group.First());
        }
        foreach(var s in drawable.Where(s=>s.Kind is SurfaceKind.Ramp or SurfaceKind.Stairs))
        {
            var pts=s.Polygon.Select(T).ToArray(); if(pts.Length<3)continue;
            using var path=new SKPath(); path.MoveTo(pts[0]); for(int i=1;i<pts.Length;i++)path.LineTo(pts[i]); path.Close();
            DrawGroup(canvas,path,s.LevelId,s.Kind,style,zMin,zMax,s);
        }

        // Tactical markers stay above all geometry.
        if (!showMarkers) return bmp;
        foreach(var marker in scene.Markers)
        {
            var p=T(marker.Position); using var paint=new SKPaint{Style=SKPaintStyle.Fill,Color=style.MarkerColor,IsAntialias=true};
            var radius=Math.Max(3,size/180f);
            if(marker.Kind.Equals("bombsite",StringComparison.OrdinalIgnoreCase))
            { canvas.DrawCircle(p,radius*1.7f,paint); paint.Color=style.Background; canvas.DrawCircle(p,radius*.7f,paint); }
            else { canvas.DrawCircle(p,radius,paint); }
            using var text=new SKPaint{Color=style.MarkerColor,TextSize=Math.Max(10,size/90f),IsAntialias=true,Typeface=SKTypeface.Default};
            if(!string.IsNullOrWhiteSpace(marker.Label))canvas.DrawText(marker.Label,p.X+radius+3,p.Y+text.TextSize*.35f,text);
        }
        return bmp;
    }

    private static void DrawGroup(SKCanvas canvas,SKPath path,int level,SurfaceKind kind,RadarStyle style,float zMin,float zMax,Surface sample)
    {
        using var fill=new SKPaint{Style=SKPaintStyle.Fill,IsAntialias=true};
        var ls=style.GetLevelStyle(level);
        if(kind==SurfaceKind.Cover || kind==SurfaceKind.Wall || kind==SurfaceKind.Door) fill.Color=style.CoverColor.WithAlpha(Math.Min((byte)255,ls.Opacity));
        else if(kind==SurfaceKind.Overlap) fill.Color=ColorAt(ls,sample.Z,zMin,zMax).WithAlpha(Math.Min(style.OverlapAlpha,ls.Opacity));
        else if(kind==SurfaceKind.Detail) fill.Color=style.CoverColor.WithAlpha(80);
        else fill.Color=ColorAt(ls,sample.Z,zMin,zMax).WithAlpha(ls.Opacity);
        canvas.DrawPath(path,fill);
        using var stroke=new SKPaint{Style=SKPaintStyle.Stroke,StrokeWidth=Math.Max(1,canvas.LocalClipBounds.Width/1024f*1.35f),Color=style.OutlineColor.WithAlpha(kind==SurfaceKind.Detail?80:210),IsAntialias=true};
        canvas.DrawPath(path,stroke);
    }

    private static int Layer(SurfaceKind k)=>k switch{SurfaceKind.Detail=>1,SurfaceKind.Ground=>2,SurfaceKind.Ramp=>2,SurfaceKind.Stairs=>2,SurfaceKind.Wall=>3,SurfaceKind.Door=>4,SurfaceKind.Cover=>5,SurfaceKind.Overlap=>6,_=>7};
    private static SKColor ColorAt(LevelStyle ls,float z,float min,float max){var t=max<=min?.5f:Math.Clamp((z-min)/(max-min),0,1);return Lerp(ls.LowColor,ls.HighColor,t);}
    private static SKColor Lerp(SKColor a,SKColor b,float t)=>new((byte)(a.Red+(b.Red-a.Red)*t),(byte)(a.Green+(b.Green-a.Green)*t),(byte)(a.Blue+(b.Blue-a.Blue)*t));
}
