using SkiaSharp;
using S1Radar.Models;
using S1Radar.Rendering;

namespace S1Radar.Export;

public static class OverviewExporter
{
    public static void Export(RadarScene scene,string dir,string mapName,int size,RadarStyle style,bool showMarkers=true)
    {
        Directory.CreateDirectory(dir);
        using var bmp=RadarRenderer.Render(scene,size,style,showMarkers);
        using var data=bmp.Encode(SKEncodedImageFormat.Png,100);
        File.WriteAllBytes(Path.Combine(dir,mapName+".png"),data.ToArray());
        var worldWidth=Math.Max(1e-3f,scene.Bounds.Width); var posX=scene.Bounds.Min.X; var posY=scene.Bounds.Max.Y; var scale=worldWidth/size;
        File.WriteAllText(Path.Combine(dir,mapName+".txt"),$"\"{mapName}\"\n{{\n    \"material\" \"overviews/{mapName}\"\n    \"pos_x\" \"{posX:0.###}\"\n    \"pos_y\" \"{posY:0.###}\"\n    \"scale\" \"{scale:0.######}\"\n    \"rotate\" \"0\"\n    \"zoom\" \"1\"\n}}\n");
        File.WriteAllText(Path.Combine(dir,mapName+".vmt"),$"\"UnlitGeneric\"\n{{\n    \"$basetexture\" \"overviews/{mapName}\"\n    \"$translucent\" \"1\"\n    \"$vertexalpha\" \"1\"\n    \"$no_fullbright\" \"1\"\n}}\n");
    }
}
