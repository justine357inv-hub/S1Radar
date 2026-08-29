# S1Radar

S1Radar is a .NET 8 desktop application for generating Counter-Strike: Source overview radars from Hammer/Hammer++ VMF files. The core is a native Source geometry pipeline that reconstructs convex brushes, analyzes surfaces by elevation, preserves multi-level overlap, and rasterizes a clean tactical vector scene.

## S1 VisGroups

Mapper-facing naming is S1-only:

```text
s1_path
s1_cover
s1_overlap
s1_remove
s1_wall
s1_detail
s1_ramp
s1_stairs
s1_door
s1_objective
s1_spawn
s1_buyzone
```

`S1Radar` never requires these tags for basic automatic analysis. When present, they act as explicit classification overrides. `s1_remove` is an explicit exclusion.

## Multi-level analysis

The integrated pipeline is:

```text
VMF
 ↓
KeyValues + Hammer VisGroup model
 ↓
Convex brush CSG reconstruction
 ↓
Surface extraction
 ↓
Slope / ramp / stair classification
 ↓
Elevation clustering into independent levels
 ↓
Walkability + connectivity analysis
 ↓
Tactical marker extraction
 ↓
Level-aware vector scene
 ↓
Compatible polygon union during rasterization
 ↓
SkiaSharp rendering
 ↓
PNG + CS:S TXT + VMT
```

Overlapping XY geometry is not merged solely by XY position. Each surface retains its level assignment and elevation range. Upper levels can therefore be rendered independently over lower levels. Every detected level has its own `LevelStyle` entry, so low/high colors, opacity, and visibility can be changed independently in code and future UI controls.

## Build on Windows

Install the .NET 8 SDK or simply run `build-windows.bat`. The builder first looks for any installed .NET 8 SDK, including SDKs such as `8.0.424`. If it cannot find one, it downloads Microsoft's official `dotnet-install.ps1` and installs a private x64 .NET 8 SDK under `.dotnet` next to the project. No system-wide .NET install is required for that fallback.

Normal development:

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

Self-contained Windows build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## GitHub Actions build

This repository includes `.github/workflows/build-windows.yml`. It builds the project on a GitHub-hosted Windows runner and publishes a self-contained `win-x86` single-file executable.

In GitHub:

1. Upload the contents of the `S1Radar` folder to a repository.
2. Commit the `.github/workflows/build-windows.yml` file.
3. Open **Actions** → **Build S1Radar Windows x86** → **Run workflow**.
4. When the run finishes, download the **S1Radar-Windows-x86** artifact.

The artifact is a ZIP containing `S1Radar.exe` for 32-bit Windows.
