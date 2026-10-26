# Native WPF docking

An independent native WPF library for `net48`, `netcoreapp3.1` and `net6.0-windows`.
WinForms core and all themes continue to retain the existing targets. WPF uses
`DockPanelSuite.Wpf`, assembly `WpfUI`, package `DockPanelSuite.Wpf`.

## Capabilities

| Capability | Behavior |
| --- | --- |
| Docking | Recursive horizontal/vertical splits and tab groups inside the document area and all four tool-pane regions |
| Drag/drop | Visual Studio-style central and outer docking guides, hover highlight, translucent preview and shared final-drop hit testing |
| Tab order | Drop a title before another title, including across dock regions |
| Floating | Owned native WPF windows; double-click a title to float or redock |
| Tool groups | Shared top caption, bottom tabs, group pin/unpin and single-tab close |
| Auto-hide | Four edge rails, grouped click-to-open flyouts, click-outside or Escape dismissal |
| Themes | Live light/dark palettes, themed docking chrome, opt-in content styles |
| Keyboard | Ctrl+Tab / Ctrl+Shift+Tab cycles visible documents; Ctrl+F4 closes the active item; Ctrl+W is opt-in via `IsCtrlWEnabled` |
| Application integration | `ActiveItemChanged`, cancelable `ItemClosing`, content preserved while moving |
| Persistence | Version 3 XML stores split trees, ratios, per-group tab order and selections, pane state, floating bounds and active item; versions 1 and 2 remain readable |

The sample is a Chinese industrial-vision-style workbench: native vector preview,
editable JSON document, project tree, properties, output, and auto-hidden history.
All measurement values and logs are explicitly sample data. There is no hardware
connection or image-processing implementation in this docking demo.

## Add a workspace

Reference `WpfUI/WpfUI.csproj` from a WPF application targeting .NET Framework 4.8,
.NET Core 3.1 or .NET 6. The `DockPanelSuite.Wpf` NuGet package includes a separate
assembly for each target, including `lib/net48/WpfUI.dll`. The same docking API
and XAML themes are available on all three frameworks.

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
`MoveBefore(item, before)` reorders tabs and moves the item into `before`'s group
when both items share a region. A title-to-title drop can also merge and reorder
across regions.

## Ctrl+W close shortcut

`DockWorkspace.IsCtrlWEnabled` is a Boolean dependency property and defaults to
`false`. Disabled Ctrl+W is not handled by the workspace, so content controls or
the host application can use that gesture. Enable it in XAML or code:

```xml
<docking:DockWorkspace IsCtrlWEnabled="True" />
```

```csharp
Workspace.IsCtrlWEnabled = true;
```

The property supports binding and runtime changes for both docked and floating
panes. Enabled Ctrl+W closes only the active item and honors `CanClose` and
`ItemClosing`. Ctrl+F4 remains enabled independently. Close buttons and explicit
`Hide` calls retain their normal behavior. This application preference is outside
the saved layout and is preserved when restoring a layout or switching themes.

## Nested docking

Each document or tool region owns a binary tree. `DockSplit` nodes divide space
horizontally or vertically; `DockTabGroup` leaves hold ordered tabs and their own
selection. Use `Dock` to target a specific visible pane:

```csharp
// All five items are registered before arranging them.
Workspace.Dock(doc5, doc1, DockPosition.Right);
Workspace.Dock(doc3, doc1, DockPosition.Bottom);
Workspace.Dock(doc4, doc5, DockPosition.Bottom);
Workspace.Dock(doc2, doc1, DockPosition.Center); // joins doc1's tabs
Workspace.Dock(output, properties, DockPosition.Bottom);
```

This creates a 2-by-2 document area with `doc1`/`doc2` at top left, `doc5` at top
right, `doc3` at bottom left and `doc4` at bottom right. The same operations stack
or split tool windows. `proportion` specifies the new pane's share (default 0.5,
valid range 0.05–0.95). Each split has an independent resize handle. Directional
splits inherit the target's `DockState`: a document split remains a document,
and a split in the right tool area remains `DockState.Right`.

While dragging a title or tab, the five-way cross follows the hovered **tab
group**. Its arrows split that group; its center merges tabs into that group.
The four outer targets dock into the corresponding workspace tool region.
The interaction follows the [Visual Studio docking guide](https://learn.microsoft.com/en-us/visualstudio/ide/customizing-window-layouts-in-visual-studio?view=vs-2022#dock-windows).
Guides use WPF vectors, live light/dark colors and the same bounds for hover,
preview and final drop. Narrow groups scale the cross and omit conflicting outer
tiles. Guides disappear on drop, cancellation, workspace exit or unload.
Preview event handlers prevent text editors swallowing workspace-owned drags.

Floating, hiding or auto-hiding an item temporarily collapses its empty group in
the view and remembers its tree position. `Show`/`Pin` restores that position.
Moving or permanently removing the last member prunes the empty branch. Layout
snapshots retain the full tree, including dormant groups and their split ratios.
The public split operations and snapshot validation enforce a maximum tree depth
of 32 and 1,024 total nodes.

The sample's **多窗格示例** button recreates four document groups, two tabs at the
top left, a left toolbox and three stacked tools at the right. **重置布局** returns
to the original workbench without discarding registered content.
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

## Tabbed tool panes and group auto-hide

Tool windows in the same `DockTabGroup` share one top caption and use bottom
labels to switch content, matching the WinForms tool-pane arrangement. The
caption follows the selected tab. A single-tab tool pane hides the bottom strip.
Documents keep their top tabs.

```csharp
Workspace.Dock(output, solutionExplorer, DockPosition.Center);
Workspace.AutoHideGroup(solutionExplorer); // both tool tabs move to the edge rail
Workspace.OpenAutoHide(output);            // opens the group with Output selected
Workspace.PinGroup(output);                // restores both at their saved position
```

The caption's pin button and pin context-menu command act on the current tool
**group**, including when pinning an open flyout. Separate split panes remain
independent. Closing a tab only hides that item and respects `CanClose` and
`ItemClosing`; a later group pin does not reopen closed tabs. Hidden and floating
group members are also left alone by group auto-hide.

Rail labels retain their group order, and an expanded flyout keeps the same
bottom tab switcher. Layout snapshots preserve group membership, selection,
auto-hide state and the original split position; flyouts restore collapsed.
The existing `AutoHide(item)` and `Pin(item)` APIs continue to affect only one
item. Use `AutoHideGroup` and `PinGroup` for the caption behavior.

The sample's **工具组示例** button builds the four document panes and a full-height
right tool group with **Output / Solution Explorer** tabs. Its pin button folds
both tools into the right rail; opening either label and pinning restores both.

## Theme integration

```csharp
Workspace.Theme = DockTheme.Dark;
var resources = DockThemeResources.Create(DockTheme.Dark);
window.Resources.MergedDictionaries.Add(resources);
editorControl.SetResourceReference(FrameworkElement.StyleProperty, "Dps.TextBox");
```

Theme resources are split by purpose:

| File under `WpfUI/Themes` | Customize |
| --- | --- |
| `Dark.xaml` | Dark palette, including docking-guide colors |
| `Light.xaml` | Light palette with the same resource keys |
| `GreenDark.xaml` | MasterGo-inspired neutral charcoal palette with emerald accents |
| `Headers.xaml` | `Dps.DockItemHeader` template, title spacing/width, pin geometry and button order |
| `Metrics.xaml` | Fonts, pane/window minimums, splitter thickness, preview inset, initial tool sizes and guide layout parameters |
| `Chrome.xaml` | Workspace, buttons, rails, flyouts, floating frames/windows and drop-preview styles |
| `Guides.xaml` | Guide plate and tile templates, arrow geometry, shadow, padding and highlighted states |
| `Controls.xaml` | Shared document/tool tab templates, splitters, menus, trees and scrollbars |

All three palette dictionaries merge `Controls.xaml`, which merges the shared metric,
chrome, header and guide dictionaries.
`DockThemeResources.Create` loads the requested palette; `Workspace.Theme`
continues to switch colors live in docked panes, open flyouts, floating windows
and menus. Floating windows and menus share workspace resources, so application
overrides in `DockWorkspace.Resources` take precedence
and survive theme switching.

The **GreenDark** palette follows the shared color tokens in the supplied MasterGo
[channel-split design](https://mastergo.com/goto/X8lltNym?page_id=429:22199&layer_id=429:39626&file=193180413336859)
and [image-processing design](https://mastergo.com/goto/X8lu8z2l?page_id=429:22199&layer_id=575:34515&file=193180413336859):

| Role | Color / source token |
| --- | --- |
| Workspace and headers | `#262626`, white/15 |
| Pane surface | `#1F1F1F`, white/12 |
| Hover | `#333333`, white/20 |
| Border | `#4F4F4F`, white/35 |
| Main / secondary text | `#FFFFFF` / `#A4A4A4`, white/100 / white/60 |
| Accent and success | `#12C785`, green/100 |
| Selection and docking preview | `rgba(18,199,133,0.3)`, encoded as WPF ARGB `#4D12C785` |

Guide colors adapt the same tokens to the docking targets. Shared templates,
fonts and sizing continue to come from the common dictionaries. The sample's
theme menu offers **Light / Dark / GreenDark**. Select it programmatically with:

```csharp
Workspace.Theme = DockTheme.GreenDark;
```

For application content using the shared opt-in styles, load
`DockThemeResources.Create(DockTheme.GreenDark)` or merge the dictionary directly:

```xml
<ResourceDictionary Source="/WpfUI;component/Themes/GreenDark.xaml" />
```

`Headers.xaml` uses `DockItemHeader`, with `Item`, `IsToolTab` and
`IsToolPaneHeader` properties to select the appropriate chrome. Change the
`Grid.Column` assignments in its template to reorder the title, pin and close.
Pin visibility, rotation and tooltips are also defined in this XAML. C# retains
only behavior and command wiring.

For small adjustments, override the shared metrics without copying the template
(`sys` is `clr-namespace:System;assembly=mscorlib`):

```xml
<docking:DockWorkspace.Resources>
  <Thickness x:Key="Dps.HeaderTitleMargin">8,3,8,3</Thickness>
  <Thickness x:Key="Dps.ToolPaneHeaderMargin">4,2,4,2</Thickness>
  <sys:Double x:Key="Dps.TabTitleMaxWidth">240</sys:Double>
  <sys:Double x:Key="Dps.ToolPaneTitleMaxWidth">Infinity</sys:Double>
  <Geometry x:Key="Dps.PinGeometry">M4,1 L12,1 M6,1 L6,6 L3,9 L3,10 L13,10 L13,9 L10,6 L10,1 M8,10 L8,15</Geometry>
</docking:DockWorkspace.Resources>
```

For a completely custom header, override the `Dps.DockItemHeader` style with a
`ControlTemplate` targeting `DockItemHeader`. Bind title text to `Item.Title`,
close enablement to `Item.CanClose`, and buttons to
`DockItemHeader.CloseCommand` / `DockItemHeader.TogglePinCommand`, with the header
as their `CommandTarget`. Mark the draggable title area with
`DockItemHeader.IsDragHandle="True"` to retain drag and double-click behavior.
Template element names are for styling convenience; commands and the drag-handle
property do not require specific part names. Keep buttons collapsed for
`IsToolTab`, and show pin only for tool-pane/auto-hide states, as in the default
template.

The remaining fixed chrome also lives in XAML. Common metric overrides can be
placed alongside the header overrides above:

```xml
<sys:Double x:Key="Dps.FontSize">13</sys:Double>
<FontFamily x:Key="Dps.FontFamily">Segoe UI</FontFamily>
<GridLength x:Key="Dps.SplitterLength">8</GridLength>
<sys:Double x:Key="Dps.PaneMinWidth">96</sys:Double>
<sys:Double x:Key="Dps.PaneMinHeight">72</sys:Double>
<Thickness x:Key="Dps.DropPreviewInset">3</Thickness>
<docking:DockGuideMetrics x:Key="Dps.GuideMetrics" TileSize="40" Step="46"
                         PlateSize="140" ScaleThreshold="280" OuterMargin="20" />
```

`Dps.SplitterLength` is an absolute `GridLength`; both outer and nested splitters
use it, and preview rectangles read the same length. Pane and document minimums
are separate keys. `Dps.FloatingMinWidth` / `Dps.FloatingMinHeight` control floating
window minimums; `Dps.FlyoutMinWidth` / `Dps.FlyoutMinHeight` provide the preferred
minimum expansion size, still clamped to the host. `Dps.DefaultToolWidth` /
`Dps.DefaultToolHeight` are applied at initial load and never replace restored
layout sizes. The layout protocol retains its validation limits, including
floating bounds of at least 100 by 80 and saved tool sizes of at least 40 DIPs.

Rail spacing, rotation and group corners are defined by `Dps.RailButton`,
`Dps.RailPanel` and `Dps.RailGroup` in `Chrome.xaml`. `DockChrome.Edge` exposes the
current edge to their triggers and to `Dps.AutoHideFlyout`. `Dps.DropPreview`
controls preview border/corners, while the inset comes from `Metrics.xaml`.
`Dps.FloatingWindow`, `Dps.FloatingContent`, `Dps.Workspace` and `Dps.DockSurface`
control the other shared surfaces.

`Guides.xaml` supplies `Dps.GuideTile` (a `DockGuideTile` template with `State` and
`IsHighlighted` properties) and `Dps.GuidePlateStyle`. The five guide arrows and
plate outline are geometry resources. Icons use normalized drawing coordinates;
their actual bounds come from `Dps.GuideMetrics`. Rendering, hover hit testing,
preview and final drop all consume those same parameters. Replacing or changing
the metrics rebuilds guides on the next pointer update. Tile spacing must be at
least the tile size; the plate must contain the complete cross; the scale
threshold must contain the plate and outer margins. Invalid metrics are rejected.

Most style/brush/font/minimum overrides update live. Flyout size parameters are
read when the flyout is built, and initial sizes only during initial load. Tree
creation, resizing ratios, preview positions and host clamps remain C# layout
logic. Persistence defaults and the standalone four-argument
`DockDropTargetResolver.Resolve` keep their compatibility values; the workspace
always reads its guide configuration from XAML. Standalone consumers can use the
five-argument resolver overload with the same `DockGuideMetrics` instance.

The docking theme styles the chrome, not arbitrary application content. Consumers
can opt into `Dps.TextBox`, `Dps.TreeView`, `Dps.ScrollViewer`, `Dps.Button`, and `Dps.IconButton`.
Other keys include `Dps.TabControl`, `Dps.TabItem`, `Dps.ToolTabControl`,
`Dps.ToolTabItem`, `Dps.DockItemHeader`, `Dps.TreeViewItem`,
`Dps.ScrollBar`, `Dps.Splitter` and `Dps.Menu`. The opt-in tree, editor and scroll
viewer styles include themed scrollbars; tree nodes have themed selection,
expansion arrows and keyboard focus. Long docking titles truncate with a full
text tooltip, and splitters highlight on hover or keyboard focus.

Palette brush keys: `Dps.Background`, `Dps.Surface`, `Dps.Header`, `Dps.Foreground`,
`Dps.Muted`, `Dps.Border`, `Dps.Hover`, `Dps.Accent`, `Dps.Selected`, `Dps.Preview`,
and `Dps.Success`. Override these in workspace resources to match your app.
Docking guide brush keys are `Dps.GuideBackground`, `Dps.GuidePlate`,
`Dps.GuideBorder`, `Dps.GuideGlyph`, `Dps.GuideHover`, and `Dps.GuideHoverGlyph`.
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

WPF XML is independent of WinForms XML. Version 3 readers migrate version 1/2
snapshots into one group per region. Older readers cannot read version 3 trees. The sample's reset
button restores its initial snapshot, including tab order and tool placement;
new documents are retained as hidden items and can be found through the pane menu.

## Windows builds and tests

CI installs SDKs 6.0.428 and 3.1.426, including the 3.1 desktop runtime. Use Visual Studio 2022
MSBuild for the full legacy solution. Required framework reference assemblies
and desktop resource dependencies restore from NuGet. To build the WPF projects
for `net48` locally, install the .NET Framework 4.8 Developer Pack; running the
sample or tests requires the .NET Framework 4.8 runtime. WPF CI runs the same
regressions and screenshot smoke checks on all three frameworks.

```powershell
msbuild WinFormsUI.Docking.sln /restore /m /p:Configuration=Release
dotnet test WpfTests/WpfTests.csproj -c Release -f net48
dotnet test WpfTests/WpfTests.csproj -c Release -f net6.0-windows
dotnet test WpfTests/WpfTests.csproj -c Release -f netcoreapp3.1
dotnet test Tests.Modern/Tests.Modern.csproj -c Release -f net6.0-windows
dotnet test Tests.Modern/Tests.Modern.csproj -c Release -f netcoreapp3.1
dotnet run --project WpfSample/WpfSample.csproj -f net48
dotnet run --project WpfSample/WpfSample.csproj -f net6.0-windows
```

The original `Tests`, `Tests2`, and `Tests3` suites remain in the compatibility
job. WPF tests run in STA and exercise actual floating-window lifecycle,
auto-hide transitions, order persistence, cancellation and live theme changes.
The sample's `--smoke` path produces `wpf-sample-light.png`,
`wpf-sample-dark.png`, `wpf-auto-hide.png`, `wpf-sample-compact.png`,
`wpf-sample-compact-scrolled.png`, `wpf-docking-guides-light.png`,
`wpf-docking-guides-dark.png`, `wpf-nested-layout-dark.png`,
`wpf-nested-layout-light.png`, `wpf-nested-docking-guides.png`,
`wpf-tool-group-dark.png`, `wpf-tool-group-light.png`,
`wpf-tool-group-autohide.png`, `wpf-tool-group-flyout.png`,
`wpf-sample-greendark.png`, `wpf-docking-guides-greendark.png`,
`wpf-tool-group-greendark.png`, `wpf-tool-group-flyout-greendark.png`, and the compatibility filename
`wpf-sample.png`. Screenshots capture the client content using its current DPI.
The smoke path checks that the normal preview fits, and that the minimum-size
preview scrolls to its measurement cards. It also checks group auto-hide, snapshot
restoration and pinning of both tool tabs. Smoke errors are saved to `smoke-error.txt`.

## Linux-testable logic

`DockDropTargetResolver` has no WPF/runtime dependency. `WpfLogicTests` links the
same production geometry source and runs on Linux or Windows:

```sh
dotnet test WpfLogicTests/WpfLogicTests.csproj -c Release
```

CI has a separate Ubuntu job for these tests.

## Windows verification (2026-10-26)

| Check | Result |
| --- | --- |
| Native WPF regressions, .NET Framework 4.8 | 131 passed |
| Native WPF regressions, .NET 6 | 131 passed |
| Native WPF regressions, .NET Core 3.1 | 131 passed |
| Shared drop-target geometry, .NET 6 | 53 passed |
| Sample build and smoke, all three desktop runtimes | Passed; nineteen screenshots per runtime |
| WPF NuGet package | Contains assemblies for all three target frameworks |

Verification used the installed SDK 9.0.316 through its explicit CLI entry point.
The WPF target frameworks are .NET Framework 4.8, .NET Core 3.1 and .NET 6. Test reports
and screenshots are under the ignored `artifacts/` directory.

Regressions cover the latest floating bounds after a user close, focus transfer
between floating and docked content, document focus after dismissing an auto-hide
flyout, dismissal on document activation, all four flyout edges while shrinking
and expanding the host, live tree/menu foreground colors, and horizontal/vertical
scrollbar paging and offset synchronization. Docking-guide tests cover all nine
targets, guide/preview/drop agreement, icon bounds and gaps, live highlighted
colors, cross-region tab reordering, routed drag events over text editors,
child-to-child drag leave, cancellation/unload cleanup and small-host resizing.
Nested docking regressions cover the reference 2-by-2 document and three-tool
layout, local splitting/merging, branch collapse, dormant group restoration,
per-group selection and ratios, actual splitter resizing, legacy migrations,
unknown plugin pruning, rejected malformed trees without changing live floating
windows, selected-tab fallback after closing a group, and saved active-tab focus
when a floating branch is restored. Tool-group regressions click the real pin
buttons on all four edges, verify shared caption and bottom-tab placement,
switch flyout tabs, close or cancel a single tab, preserve separate split panes,
restore grouped snapshots and retain the single-item API behavior. Header/theme
regressions load all three XAML palettes, override live metrics and pin geometry,
replace the complete header template while using its commands and double-click
float/redock handle, and verify title,
close-state and live theme bindings on an open flyout. Chrome regressions override
font/minimum resources, nested and outer splitter widths, preview styling/insets,
guide dimensions and arrow geometry, all four rail/flyout edges, popup/floating
resource inheritance and initial versus restored sizes. Portable geometry tests
also cover custom metrics, overlap prevention and invalid configurations.
GreenDark regressions cycle through all three themes with a floating window,
open tool flyout and highlighted docking target, checking color restoration,
content/state preservation and local metric overrides. Smoke also selects
GreenDark through the sample theme menu before rendering its four screenshots.
Close-shortcut regressions check disabled/unconsumed Ctrl+W by default, runtime
enable/disable and binding updates, close cancellation, and independent Ctrl+F4
behavior for docked and floating panes.
The guide smoke screenshots use the same production preview path with synthetic
pointer positions; they do not represent a physical mouse-drag acceptance test.
The sample's compact layout keeps
its preview and measurement cards reachable by scrolling.

This validation does not include the full legacy WinForms solution, physical
mouse dragging, or multi-monitor/DPI acceptance testing.

## Remaining scope

Floating windows currently contain one item. Multiple-tab floating groups,
animated docking transitions, old MDI behavior and exact WinForms
Visual Studio theme parity are not implemented. Auto-hide flyouts are click based,
not hover/timer based. Keyboard navigation, real mouse-drag behavior and
multi-monitor/DPI behavior still require Windows acceptance testing.

.NET Core 3.1 and .NET 6 remain outside Microsoft's current support window.
