# Native WPF docking (0.1)

This branch adds a separate native WPF library targeting `netcoreapp3.1` and
`net6.0-windows`. The existing WinForms library and every theme now also target
`net6.0-windows`, while retaining `netcoreapp3.1`, `net35`, and `net40`.

## WPF API

Add a project reference to `WpfUI/WpfUI.csproj`. The namespace is
`DockPanelSuite.Wpf`; the assembly is `WpfUI` and the NuGet package ID is
`DockPanelSuite.Wpf`. No WinForms reference or interop host is used.

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:docking="clr-namespace:DockPanelSuite.Wpf;assembly=WpfUI">
  <docking:DockWorkspace x:Name="Workspace" />
</Window>
```

```csharp
var editor = new DockItem("editor", "Editor", new TextBox { AcceptsReturn = true });
Workspace.Add(editor); // document tab
Workspace.Add(new DockItem("tools", "Tools", new TreeView()), DockState.Left);
Workspace.Float(editor);
Workspace.Show(editor, DockState.Document);
Workspace.Hide(editor); // retains the content instance
Workspace.Show(editor); // returns to its previous dock region
```

`Content` can be a WPF control or a view model resolved by an implicit WPF
`DataTemplate`. `Title` and `CanClose` implement change notifications. Items have
stable, unique IDs and one owning workspace. Content instances cannot be shared
between items. Use the owning UI dispatcher for every operation. `Items` is
read-only; use `Add` and `Remove`. Applications own the lifetime of content and
must dispose any resources when they permanently remove an item.

## Implemented interactions

- Center document tabs and tabbed tool panes at all four edges.
- Native WPF grid splitters to resize the edge panes.
- Drag tab titles to an outer edge or the center to move an item.
- Right-click a title to select a dock region or float the item.
- Native owned WPF floating windows, including size/position persistence.
- Close buttons hide items; `CanClose = false` blocks user closing.
- Unloading the workspace closes floating windows; loading it restores them.
- Repeated docking/floating keeps the same content/control instance.

The sample provides New document, Save/Restore layout, and Show hidden panes.
It uses an editable WPF `TextBox`, a `TreeView`, properties and output panes.

## Layout snapshots

```csharp
using (var stream = File.Create("layout.xml")) Workspace.SaveLayout(stream);
using (var stream = File.OpenRead("layout.xml")) Workspace.LoadLayout(stream);
```

Register the application's items before restoring. Snapshots store identities,
states, selected tabs, pane sizes, and floating geometry; they do not serialize
controls, arbitrary types, document text, or view models. Unknown IDs are skipped.
Registered items missing from the snapshot are hidden. Invalid snapshots are
validated before any live states are changed. Caller-owned streams stay open.
The WPF layout schema is independent of the WinForms XML schema.

## Windows build and test

Install .NET SDKs 6.0.428 and 3.1.426 (the latter supplies the 3.1 desktop runtime).
`global.json` selects the .NET 6 SDK for builds. For the full legacy solution,
use Visual Studio 2022 MSBuild. Framework reference assemblies are restored from
NuGet, including the older .NET Framework targets.

```powershell
msbuild WinFormsUI.Docking.sln /restore /m /p:Configuration=Release
dotnet test WpfTests/WpfTests.csproj -c Release -f net6.0-windows
dotnet test WpfTests/WpfTests.csproj -c Release -f netcoreapp3.1
dotnet test Tests.Modern/Tests.Modern.csproj -c Release -f net6.0-windows
dotnet test Tests.Modern/Tests.Modern.csproj -c Release -f netcoreapp3.1
dotnet test Tests/Tests.csproj -c Release --no-build
dotnet test Tests2/Tests2.csproj -c Release --no-build
dotnet test Tests3/Tests3.csproj -c Release --no-build
dotnet run --project WpfSample/WpfSample.csproj -f net6.0-windows
```

The `Windows build and tests` GitHub Actions workflow runs on `windows-2022`.
It builds the complete solution, runs the original framework configuration
tests, and tests both desktop runtimes separately. The modern WinForms tests
reuse the existing default/registry/environment and theme tests. WPF tests run
in STA and exercise real native floating windows with dispatcher pumping.

Each modern job also launches the sample with `--smoke`, moves a document out
and back, saves/restores its layout, and renders `wpf-sample.png`. Artifacts include
TRX results, screenshots and the sample binaries. The compatibility job uploads
WinForms/theme packages and the native WPF package. There is no signing or
publishing to a package registry in CI.

## Compatibility boundary

This is the initial native WPF implementation, not a source-compatible port of
`WeifenLuo.WinFormsUI.Docking`. WPF callers use `DockWorkspace` and `DockItem`;
WinForms callers continue to use the existing `DockPanel` and `DockContent`.
The old WinForms Visual Studio theme classes do not style WPF controls.

The first WPF version has one tab group per region. Arbitrary nested pane splits,
multiple floating tab groups, tab reorder, auto-hide flyouts, animated docking
indicators, legacy MDI, and complete old-theme parity are not implemented.
Drag/drop uses region hit testing without a preview overlay. UI tests verify
programmatic transitions; manual mouse-drag, keyboard accessibility and
multi-monitor/DPI testing remain necessary for a production desktop release.

Runtime versions are intentionally those requested by this project, despite
their being outside Microsoft's current support window.
