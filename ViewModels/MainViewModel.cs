using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using S1Radar.Analysis;
using S1Radar.Export;
using S1Radar.Geometry;
using S1Radar.Models;
using S1Radar.Parsing;
using S1Radar.Rendering;

namespace S1Radar.ViewModels;

public sealed class LevelRow
{
    public int Id { get; init; }
    public string Title => $"Level {Id}";
    public string Range { get; init; } = "";
    public int SurfaceCount { get; init; }
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    string _status="Open a .vmf to begin."; string _file=""; RadarScene? _scene;
    void Set<T>(ref T f,T v,[CallerMemberName]string? n=null){f=v;PropertyChanged?.Invoke(this,new(n));}
    public string Status{get=>_status;set=>Set(ref _status,value);}
    public string FilePath{get=>_file;private set=>Set(ref _file,value);}
    public string Stats{get;private set="";} = "";
    public RadarScene? Scene{get=>_scene;private set=>Set(ref _scene,value);}
    public ObservableCollection<LevelRow> Levels { get; } = [];
    public RadarStyle Style { get; } = new();

    public void SetFile(string path) => FilePath=path;

    public void Analyze()
    {
        try
        {
            Status="Parsing VMF…";
            var map=VmfParser.Parse(FilePath);
            Status=$"Reconstructing Source solids… {map.Brushes.Count} world brushes, {map.Entities.Count} entities";
            var surfaces=CsgEngine.ReconstructSurfaces(map).ToList();
            Status=$"Analyzing {surfaces.Count} reconstructed surfaces…";
            Scene=SurfaceAnalyzer.Analyze(map,surfaces);
            Levels.Clear(); foreach(var l in Scene.Levels) Levels.Add(new LevelRow{Id=l.Id,Range=$"Z {l.MinZ:0.#} → {l.MaxZ:0.#}",SurfaceCount=l.SurfaceCount});
            Stats=$"Surfaces: {Scene.Surfaces.Count}\nLevels: {Scene.Levels.Count}\nWalkable components: {Scene.ConnectedWalkableComponents}\nElevation: {Scene.ElevationMin:0.#} → {Scene.ElevationMax:0.#}\nMarkers: {Scene.Markers.Count}";
            Status="Ready — multi-level scene generated.";
        }
        catch(Exception ex){Scene=null;Levels.Clear();Status="Error: "+ex.Message;Stats="";}
    }

    public void Export(string dir,int size,bool showMarkers)
    {
        if(Scene is null)return;
        var name=Path.GetFileNameWithoutExtension(FilePath);
        OverviewExporter.Export(Scene,dir,name,size,Style,showMarkers);
        Status=$"Exported {name}.png / .txt / .vmt";
    }
}
