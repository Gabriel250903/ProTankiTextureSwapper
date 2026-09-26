# ProTanki Texture Swapper

A Windows desktop utility for customizing and swapping textures in ProTanki.

## Overview

ProTanki Texture Swapper is a WPF-based desktop app designed to help players manage custom textures, skins, paints, and shot effects in ProTanki. It provides a user-friendly interface for applying visual changes while keeping backups of original assets so they can be restored when needed.

The project is built with:
- .NET 8
- WPF
- WPF-UI
- Microsoft.Extensions.DependencyInjection
- Serilog

## Features

- Manage hull, turret, and supply skins
- Apply custom paint and texture variants
- Manage shot effects
- View and restore backup history
- Save local app settings
- Modern Fluent UI interface
- GitHub-based update checks
- Logging and cache support

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK
- Visual Studio 2022 with .NET desktop development workload
- Local access to the ProTanki game installation

## Getting Started

### Clone the repository

```bash
git clone https://github.com/Gabriel250903/ProTankiTextureSwapper.git
cd ProTankiTextureSwapper
```

### Restore dependencies

```bash
dotnet restore
```

### Build the project

```bash
dotnet build TextureSwapper.csproj
```

### Run the application

```bash
dotnet run --project TextureSwapper.csproj
```

You can also open the project in Visual Studio and run it from the IDE.

## Project Structure

```text
.
├── Core/                     # Shared constants and core values
├── Helpers/                  # Helper utilities
├── Models/                   # Data models for skins, backups, and metadata
├── Services/                 # Settings, updates, swapping, sync, notification, and cache services
├── Textures/                 # Texture assets and UI resources
├── ViewModels/               # View models for the WPF UI
├── Views/                    # UI views and tab content
├── App.xaml                  # Application startup config
├── App.xaml.cs               # App bootstrap logic
├── MainWindow.xaml           # Main application layout
├── MainWindow.xaml.cs        # Main window behavior and notifications
├── SettingsWindow.xaml       # Settings UI
├── SettingsWindow.xaml.cs    # Settings logic
├── TextureSwapper.csproj     # Project configuration
├── ingame_paints.json        # In-game paint metadata
├── shot_effects.json          # Shot effect metadata
├── skins_hulls.json          # Hull skin metadata
├── skins_paints.json         # Paint skin metadata
├── skins_supplies.json       # Supply skin metadata
├── skins_turrets.json        # Turret skin metadata
├── .gitignore                # Git ignore file
├── README.md                 # Project documentation
└── ...
```

## Usage

1. Launch the app.
2. Open the relevant tab for skins, paints, or shot effects.
3. Choose the texture or visual item you want to apply.
4. Use the backup history to restore earlier states if needed.
5. Adjust settings through the app’s settings window.

## Backup and Restore

The app includes backup features intended to protect original assets and allow users to return to a previous state after applying custom textures.

## Notes

- This project is intended for use on Windows systems.
- It is designed around local game asset management and backup workflows.
- Some functionality depends on the presence of local ProTanki files and directories.

## License

This repository does not currently declare a license. If you plan to redistribute or reuse the project publicly, check with the repository owner before doing so.

## Contributing

Contributions are welcome. If you want to improve the project:

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Open a pull request

## Repository

- Owner: Gabriel250903
- Repository: ProTankiTextureSwapper
- URL: https://github.com/Gabriel250903/ProTankiTextureSwapper