# AntennaReader

AntennaReader is a Windows WPF application for digitizing antenna diagram images. It lets you load a diagram image, align a measurement ellipse over it, manually or automatically extract dB values by angle, save diagrams to a local SQLite database, and export the results for downstream tools.

<video src="docs/demo.mp4" controls width="800"></video>

## Features

- Load antenna diagram images in PNG, JPG, or JPEG format.
- Draw, move, resize, lock, rotate, and zoom a diagram overlay.
- Manually measure antenna values by clicking points on the locked diagram.
- Automatically extract curves with two algorithms:
  - Dynamic Programming / shortest-path style extraction.
  - Fourier-based smooth curve fitting.
- Generate debug images for the image-processing pipeline and curve detection output.
- Interpolate measured points across the full 360 degrees.
- Choose interpolation mode: Monotone, Linear, Catmull-Rom, or Lagrange.
- Save antenna diagrams, metadata, raw measurements, and interpolated measurements to SQLite.
- Browse, search, edit, delete, and import saved diagrams.
- Export selected diagrams to CSV or PAT files.
- Tune diagram scale, dB range, export precision, and algorithm parameters.
- Save and manage named diagram preferences.
- Export ML label JSON files for diagram center/radius training data.

## Tech Stack

- .NET 8
- WPF
- Entity Framework Core 8
- SQLite
- OpenCvSharp

## Requirements

- Windows
- .NET 8 SDK
- Visual Studio 2022 or another editor that supports .NET desktop projects

## Getting Started

Clone the repository and restore dependencies:

```powershell
dotnet restore AntennaReader.sln
```

Build the solution:

```powershell
dotnet build AntennaReader.sln
```

Run the application:

```powershell
dotnet run --project AntennaReader/AntennaReader.csproj
```

You can also open `AntennaReader.sln` in Visual Studio and run the `AntennaReader` project.

## Basic Workflow

1. Open an image with `File > Open Image` or `Ctrl+O`.
2. Draw a rectangle around the antenna diagram.
3. Move or resize the overlay until the ellipse matches the diagram.
4. Lock the diagram with `Ctrl+L`.
5. Add measurements manually by clicking on the locked diagram, or run automatic extraction:
   - `Ctrl+H` for the dynamic-programming extraction.
   - `Ctrl+J` for the Fourier extraction.
6. Adjust interpolation or diagram settings if needed.
7. Save the diagram to the database with `Ctrl+Shift+S`.
8. Open the database browser with `Ctrl+Shift+O` to search, import, edit, delete, or export diagrams.

## Useful Shortcuts
+
| Shortcut | Action |
| --- | --- |
| `Ctrl+O` | Open image |
| `Ctrl+D` | Delete background image |
| `Ctrl+L` | Lock/unlock diagram |
| `Ctrl+F` | Toggle scale mode |
| `Ctrl+H` | Auto extract curve with dynamic programming |
| `Ctrl+Alt+H` | Dynamic-programming extraction with debug output |
| `Ctrl+J` | Auto extract curve with Fourier fitting |
| `Ctrl+Alt+J` | Fourier extraction with debug output |
| `Ctrl+M` | Open diagram settings |
| `Ctrl+Shift+M` | Manage saved preferences |
| `Ctrl+Shift+S` | Save current diagram to database |
| `Ctrl+Shift+O` | Open database browser |
| `Ctrl+Shift+D` | Delete current diagram |
| `Ctrl+Shift+P` | Delete all measurement points |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Q` / `E` | Rotate background image counter-clockwise / clockwise while locked |
| `A` / `D` | Rotate measurements counter-clockwise / clockwise while locked |
| Arrow keys | Nudge locked diagram/background position |

## Data Locations

On startup, the app creates its working folders under:

```text
%LOCALAPPDATA%\AntennaReader
```

Important paths:

| Path | Purpose |
| --- | --- |
| `%LOCALAPPDATA%\AntennaReader\antenna.db` | SQLite database |
| `%LOCALAPPDATA%\AntennaReader\Images` | Default image folder |
| `%LOCALAPPDATA%\AntennaReader\Images\ML` | ML label JSON output |
| `%LOCALAPPDATA%\AntennaReader\Exports` | CSV and PAT exports |
| `%LOCALAPPDATA%\AntennaReader\Debug` | Debug images from extraction runs |

Database migrations run automatically when the application starts.

## Automatic Extraction

Both extraction modes use the shared image-processing pipeline in `Services/ImageProcessingPipeline.cs`:

1. Read the loaded image from the canvas.
2. Apply background rotation if needed.
3. Transform the diagram into polar coordinates.
4. Build an HSV-based mask and cost map.
5. Ignore the configured center dead zone.

The dynamic-programming extractor then searches for a low-cost closed path through the polar image and simplifies the result with Ramer-Douglas-Peucker. The Fourier extractor estimates a smooth fitted path using iterative weighted Fourier fitting.

Use the debug variants when tuning settings. They write intermediate images to the debug folder so you can inspect the pipeline output.

## Database and Exports

Saved diagrams include:

- Antenna name
- Owner
- State
- City
- Creation date
- Raw measured angle/dB values
- Interpolated angle/dB values

The database browser supports multi-select exports:

- CSV export writes one combined `AntennaDiagrams.csv`. and other formats

Export precision is configurable in the diagram settings window.

## Project Layout

```text
AntennaReader/
  Infrastructure/
    AppDbContext.cs       # EF Core SQLite context
    AppPaths.cs           # Local app data paths
  Migrations/             # EF Core database migrations
  Models/                 # Diagram, measurement, and settings models
  Services/
    ImageProcessingPipeline.cs
    DiagramDetectionServiceDP.cs
    DiagramDetectionServiceFA.cs
  DrawingCanvas.cs        # Main interactive diagram canvas
  Interpolator.cs         # 360-degree interpolation modes
  MainWindow.xaml(.cs)    # Main application window and commands
  DatabaseBrowser.xaml(.cs)
  SettingsWindow.xaml(.cs)
  Prefrences.xaml(.cs)
```

## Notes

- This is a Windows desktop application because it uses WPF and targets `net8.0-windows`.
- The local SQLite database is stored outside the repository, so user data is not committed with source code.
- Automatic extraction works best when the diagram boundary is accurately placed and locked before running an algorithm.
