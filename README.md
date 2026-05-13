# Windrose Server Control

Professional dedicated server management tool for Windrose servers.

Built with a focus on:

* Simple server deployment
* Persistent world management
* Automatic updates
* Portable/self-contained installs
* Profile-based server presets
* Tailscale remote access integration
* Safe upgrades that preserve worlds/backups/profiles

---

# Features

## Dedicated Server Management

* Start/stop Windrose dedicated server
* Visible console or hidden background launch
* Auto restart support
* Live player monitoring
* Session join/leave tracking
* Server status + tick monitoring

---

## World Management

* Import existing worlds
* World backup handling
* World profile presets
* Persistent world settings
* Difficulty customization
* World restore defaults

---

## Profile System

Profiles allow you to:

* Store server configurations
* Store world difficulty presets
* Reuse settings across worlds
* Create different server styles for the same world

Examples:

* Casual Easy profile
* Hardcore PvE profile
* Co-op exploration profile
* Combat-heavy profile

Profiles are stored with the world itself for portability.

---

# Automatic App Updates

Windrose Server Control includes a built-in updater.

The updater:

* Checks GitHub Releases automatically
* Detects newer versions
* Downloads installer updates
* Preserves all user data
* Relaunches automatically after update

The updater NEVER overwrites:

* Worlds
* Backups
* Profiles
* Server files
* Tools
* SteamCMD data

---

# Portable-Friendly Design

The application is intentionally designed to be self-contained.

You can:

* Move the entire folder to another machine
* Copy worlds directly
* Transfer profiles with worlds
* Keep backups portable

Recommended folder structure:

```text
Windrose Server Control/
├── Elka_windrose_server_control.exe
├── Assets/
├── Data/
├── Profiles/
├── Backups/
├── WorldBackups/
├── Tools/
├── ServerFiles/
└── Updates/
```

---

# Installation

## Installer Version

1. Download latest installer from Releases
2. Run installer
3. Choose install location
4. Launch application

The installer is upgrade-safe.

Reinstalling or updating does NOT erase:

* Worlds
* Profiles
* Backups
* Server settings

---

# Updating

Updates are automatic.

On launch:

1. App checks GitHub Releases
2. Detects latest version
3. Downloads installer silently
4. Installs update
5. Relaunches app automatically

You can also manually check updates from:

```text
Update → Check App Update
```

---

# Remote Access

Windrose Server Control supports:

* Tailscale
* Tailscale Funnel
* Direct remote management

This allows hosting without complicated router forwarding.

---

# World Difficulty Presets

The app supports full editing of:

* Combat difficulty
* Enemy health scaling
* Enemy damage scaling
* Ship scaling
* Boarding difficulty
* Shared quests
* Immersive exploration
* Multiplayer scaling

Supported presets:

* Easy
* Normal
* Hard
* Custom

---

# World Preset Storage

Each world stores:

* Original default settings
* Custom profile presets
* World preset backups

This means worlds remain portable between:

* Different PCs
* Different servers
* Different users

---

# GitHub Releases

Latest releases:

```text
https://github.com/torment78/WindroseServerControl-Releases/releases
```

---

# Known Notes

## Windows SmartScreen

Unsigned builds may trigger Windows SmartScreen until signing reputation is established.

Code signing support is planned.

---

# Planned Features

## Planned

* Signed installers
* Signed executables
* Multi-server management
* Advanced world editors
* Web-based remote dashboard
* Dedicated server analytics
* Player statistics
* Resource session tracking
* Discord integration
* Automatic server deployments
* Remote cluster management

---

# Troubleshooting

## Update check fails

Ensure:

* Internet connection is available
* GitHub Releases repository is reachable
* Firewall is not blocking GitHub API access

---

## Installer does not update

Ensure:

* App is closed fully
* Antivirus is not blocking installer
* Installer has permissions to write into install folder

---

## Tailscale issues

Ensure:

* Tailscale is installed
* Tailnet is connected
* Funnel is enabled if using external access

---

# Development

## Build Pipeline

The project includes:

* Auto version incrementing
* Automated Inno Setup builds
* GitHub release upload automation
* Installer branding
* Silent upgrade support

---

# License

All rights reserved.

This repository is used for release distribution only.

Source code is not publicly distributed.

---

# Credits

Created by ElkaSoft.

Windrose Server Control is designed to provide a professional-grade dedicated server management experience with safe updates, portable worlds, and streamlined hosting workflows.
