# Native WPF docking (0.2)

An independent native WPF library for `netcoreapp3.1` and `net6.0-windows`.
WinForms core and all themes continue to retain the existing targets. WPF uses
`DockPanelSuite.Wpf`, assembly `WpfUI`, package `DockPanelSuite.Wpf`.

## What's in 0.2

| Capability | Behavior |
| --- | --- |
| Docking | Document tabs, four tool-pane regions, resize splitters with minimum sizes |
| Drag/drop | Shared hit testing for the translucent preview and final dock region |
| Tab order | Drop a title before another title, including across dock regions |
| Floating | Owned native WPF windows; double-click a title to float or redock |
| Auto-hide | Four edge rails, click-to-open flyouts, pin/unpin, click-outside or Escape dismissal |
| Themes | Live light/dark palettes, themed docking chrome, opt-in content styles |
| Keyboard | Ctrl+Tab / Ctrl+Shift+Tab cycles visible documents; Ctrl+W / Ctrl+F4 hides the active item |
| Application integration | `ActiveItemChanged`, cancelable `ItemClosing`, content preserved while moving |
| Persistence | Version 2 XML stores pane state, geometry, tab order, selections and active item; version 1 remains readable |

The sample is a Chinese industrial-vision-style workbench: native vector preview,
editable JSON document, project tree, properties, output, and auto-hidden history.
All measurement values and logs are explicitly sample data. There is no hardware
connection or image-processing implementation in this docking demo.

## Add a workspace

Reference `WpfUI/WpfUI.csproj`:

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:docking="clr-namespace:DockPanelSuite.Wpf;assembly=WpfUI">
  <docking:DockWorkspace x:Name="Workspace" Theme="Dark" />
</Window>
```

```csharp
var editor = new DockItem("editor", "Editor", new TextBox { AcceptsReturn = true });
var tool = new DockItem("tools", "Tools", new TreeView());
Workspace.Add(editor);
Workspace.Add(tool, DockState.Left);
Workspace.AutoHide(tool);           // returns to Left when pinned
Workspace.OpenAutoHide(tool);       // expands without changing its AutoHide state
Workspace.DismissAutoHide();
Workspace.Pin(tool);
Workspace.Float(editor);
Workspace.Show(editor, DockState.Document);
Workspace.Activate(editor);
Workspace.Hide(editor);             // checks CanClose and ItemClosing
Workspace.Show(editor);             // returns to its last dock region
```

`AutoHide(item, edge)` assigns an explicit tool edge. `AutoHide(item)` requires
an existing tool edge; documents cannot auto-hide without choosing an edge.
`MoveBefore(item, before)` reorders items within a region. Moving an item to a
new region uses `Show` first. A header drop does both operations.

Items have stable unique IDs, immutable content, and one owning workspace. All
access belongs on the workspace's UI dispatcher. `Items` is read-only; use `Add`
and `Remove`. `Title` and `CanClose` notify property changes. Content may be a WPF
control or a view model resolved by an implicit `DataTemplate`. Applications own
content resource disposal after permanent removal.

```csharp
Workspace.ItemClosing += (sender, e) =>
{
    if (e.Item.Id == "editor" && HasUnsavedChanges()) e.Cancel = true;
};
Workspace.ActiveItemChanged += (sender, e) => UpdateInspector(Workspace.ActiveItem);
```

User-facing close buttons, close shortcuts and the floating-window close command
honor cancellation. Explicit `Show(item, DockState.Hidden)` is a programmatic
state change and bypasses closing policy. `Remove` permanently unregisters an item.

## Theme integration

```csharp
Workspace.Theme = DockTheme.Dark;
var resources = DockThemeResources.Create(DockTheme.Dark);
window.Resources.MergedDictionaries.Add(resources);
editorControl.SetResourceReference(FrameworkElement.StyleProperty, "Dps.TextBox");
```

The docking theme styles the chrome, not arbitrary application content. Consumers
can opt into `Dps.TextBox`, `Dps.TreeView`, `Dps.Button`, and `Dps.IconButton`.
Other keys include `Dps.TabControl`, `Dps.TabItem` and `Dps.Menu`.

Palette brush keys: `Dps.Background`, `Dps.Surface`, `Dps.Header`, `Dps.Foreground`,
`Dps.Muted`, `Dps.Border`, `Dps.Hover`, `Dps.Accent`, `Dps.Selected`, `Dps.Preview`,
and `Dps.Success`. Override these in workspace resources to match your app.
Theme changes preserve documents and existing floating-window instances. Context
menus and floating chrome share the live workspace palette.

## Layout snapshots

```csharp
using (var stream = File.Create("layout.xml")) Workspace.SaveLayout(stream);
using (var stream = File.OpenRead("layout.xml")) Workspace.LoadLayout(stream);
```

Register application items before restoring. Snapshots serialize identities and
layout metadata, never controls, view models, document text, or arbitrary types.
Unknown IDs are skipped. Registered items absent from a snapshot become hidden.
Invalid snapshots are validated before live states change. Caller streams stay
open. Auto-hide flyouts restore collapsed, and the most recent active visible
pane is restored. Theme is an application preference outside the layout snapshot.

WPF XML is independent of WinForms XML. Version 2 readers accept version 1
snapshots. Old readers do not support the new auto-hide state. The sample's reset
button restores its initial snapshot, including tab order and tool placement;
new documents are retained as hidden items and can be found through the pane menu.

## Windows builds and tests

The repository pins the .NET 6 SDK with `latestPatch` roll-forward. CI installs
6.0.428 and 3.1.426, including the 3.1 desktop runtime. Use Visual Studio 2022
MSBuild for the full legacy solution. Required framework reference assemblies
and desktop resource dependencies restore from NuGet.

```powershell
msbuild WinFormsUI.Docking.sln /restore /m /p:Configuration=Release
dotnet test WpfTests/WpfTests.csproj -c Release -f net6.0-windows
dotnet test WpfTests/WpfTests.csproj -c Release -f netcoreapp3.1
dotnet test Tests.Modern/Tests.Modern.csproj -c Release -f net6.0-windows
dotnet test Tests.Modern/Tests.Modern.csproj -c Release -f netcoreapp3.1
dotnet run --project WpfSample/WpfSample.csproj -f net6.0-windows
```

The original `Tests`, `Tests2`, and `Tests3` suites remain in the compatibility
job. WPF tests run in STA and exercise actual floating-window lifecycle,
auto-hide transitions, order persistence, cancellation and live theme changes.
The sample's `--smoke` path produces `wpf-sample-light.png`,
`wpf-sample-dark.png`, `wpf-auto-hide.png`, and the compatibility filename
`wpf-sample.png`. Smoke errors are also saved to `smoke-error.txt`.

## Linux-testable logic

`DockDropTargetResolver` has no WPF/runtime dependency. `WpfLogicTests` links the
same production geometry source and runs on Linux or Windows:

```sh
dotnet test WpfLogicTests/WpfLogicTests.csproj -c Release
```

CI has a separate Ubuntu job for these tests. Local verification for this update:
25 geometry cases passed on Linux using .NET 6 and NUnitLite. Native WPF and WPF
regression sources compiled against the official 3.1 and 6 reference assemblies.
XAML/project XML and workflow YAML parse successfully. These checks do not verify
BAML generation, a full solution build, or Windows UI execution. The GitHub
repository had no Actions runs at the time this update was prepared; Windows
results and rendered screenshots still require the Windows workflow to run.

## Remaining scope

There is one tab group per region. Arbitrary nested pane trees, multiple-tab
floating groups, animated docking diamonds, old MDI behavior and exact WinForms
Visual Studio theme parity are not implemented. Auto-hide flyouts are click based,
not hover/timer based. Keyboard navigation, real mouse-drag behavior and
multi-monitor/DPI behavior still require Windows acceptance testing.

The requested runtime versions remain outside Microsoft's current support window.
