# S1Radar

S1Radar is a .NET 8 desktop application for generating Counter-Strike: Source overview radars from Hammer/Hammer++ VMF files. It reconstructs Source brush geometry with native C# math, analyzes surfaces and elevation, classifies tactical geometry, and rasterizes a clean 2D radar with SkiaSharp.

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

`S1Radar` does not require these tags for basic automatic analysis. When present, they act as explicit classification overrides. `s1_remove` is an explicit exclusion.

## Geometry pipeline

```text
VMF
 ↓
KeyValues + Hammer metadata
 ↓
Convex brush CSG reconstruction
 ↓
Surface extraction
 ↓
Slope / ramp / stair classification
 ↓
Elevation and floor analysis
 ↓
Walkability + connectivity analysis
 ↓
Tactical entity extraction
 ↓
Level-aware vector scene
 ↓
SkiaSharp rendering
 ↓
PNG + CS:S TXT + VMT
```

The current renderer keeps elevation and level information in the scene model so geometry can be composed intelligently while still producing a single overview image.

## Dependencies

The project uses:

- Avalonia 11.3.2
- Avalonia.Skia 11.3.2
- SkiaSharp 3.119.0
- .NET 8

The project intentionally does **not** reference `SkiaSharp.Views.Avalonia`; S1Radar renders directly into `SKBitmap` and feeds the encoded PNG into the Avalonia image control.

## Build on Windows

Double-click `build-windows.bat` for an x86 self-contained Windows build. The builder checks common .NET locations first, including the x86 installation directory, and can fall back to Microsoft's official .NET install script.

For GitHub Actions, the repository includes:

```text
.github/workflows/build-windows.yml
```

Run it from GitHub Actions to produce `S1Radar-Windows-x86.zip`.
