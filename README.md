## Windows CI and native WPF

Native WPF adds nested document/tool-pane split trees, per-group layout persistence, live light/dark themes, tool-pane bottom tabs with group pin/unpin, auto-hide rails and flyouts, tab reorder, Visual Studio-style docking guides and drop previews, document shortcuts and a richer workbench sample. A separate Linux CI job tests the shared drop-target geometry.

This fork supports .NET Core 3.1 and .NET 6 for WinForms and adds an independent native WPF docking library, sample and tests targeting .NET Framework 4.8, .NET Core 3.1 and .NET 6. See [native WPF usage and compatibility](docs/native-wpf.md) for setup, API examples and current scope.

The [Windows build and tests workflow](.github/workflows/main.yml) builds the full solution, runs legacy and modern WinForms tests, and exercises native WPF on all three target runtimes. Download test reports, packages, screenshots and the WPF sample from its Actions artifacts.

DockPanel Suite
===============

[![Join the chat at https://gitter.im/dockpanelsuite/dockpanelsuite](https://img.shields.io/gitter/room/dockpanelsuite/dockpanelsuite.svg?style=flat-square)](https://gitter.im/dockpanelsuite/dockpanelsuite?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![NuGet Version](https://img.shields.io/nuget/v/DockPanelSuite.svg?style=flat-square)](https://www.nuget.org/packages/DockPanelSuite/)
[![Build status](https://img.shields.io/github/actions/workflow/status/dockpanelsuite/dockpanelsuite/main.yml?style=flat-square)](https://github.com/dockpanelsuite/dockpanelsuite/actions?query=workflow%3ACI)

> **This project is looking for new maintainers. Please read [this](https://github.com/dockpanelsuite/dockpanelsuite/issues/663) for more information.**

DockPanel Suite - The Visual Studio inspired docking library for .NET WinForms

For more details, check out [http://dockpanelsuite.com](http://dockpanelsuite.com).

Visual Studio 2019 Community edition and above is recommended to compile the code base.

> **If you look for alternative options, use a search engine to learn more about better components like Krypton Docking.**
