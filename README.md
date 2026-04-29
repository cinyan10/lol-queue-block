# QueueCutoff

QueueCutoff is a Windows tray app that lets you confirm a daily League of Legends queue cutoff and then blocks new queue attempts after the cutoff while avoiding interruption during active play.

## Projects

- `QueueCutoff.App`: WPF tray app, settings, confirmation popup, and background controller.
- `QueueCutoff.Core`: persistence models, state machine, LCU/process/firewall abstractions, and reusable services.
- `QueueCutoff.Elevated`: UAC-launched firewall helper that owns `QueueCutoff` Windows Firewall rules.
- `QueueCutoff.Tests`: unit tests for daily lock and enforcement behavior.

## Requirements

- Windows
- .NET 8 SDK

This machine currently has .NET 8 runtimes installed, but no SDK. Install the SDK before building.

## Build

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"

dotnet test .\tests\QueueCutoff.Tests\QueueCutoff.Tests.csproj
dotnet build .\src\QueueCutoff.App\QueueCutoff.App.csproj
dotnet build .\src\QueueCutoff.Elevated\QueueCutoff.Elevated.csproj
```

## Run

For local testing, publish both executables into the same folder so the app can launch `QueueCutoff.Elevated.exe` when firewall changes require elevation.

```powershell
$out="$PWD\artifacts\debug-run"
dotnet publish .\src\QueueCutoff.App\QueueCutoff.App.csproj -c Debug -r win-x64 --self-contained false -o $out
dotnet publish .\src\QueueCutoff.Elevated\QueueCutoff.Elevated.csproj -c Debug -r win-x64 --self-contained false -o $out
Start-Process "$out\QueueCutoff.App.exe"
```

For a portable build:

```powershell
$out="$PWD\artifacts\release-win-x64"
dotnet publish .\src\QueueCutoff.App\QueueCutoff.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o $out
dotnet publish .\src\QueueCutoff.Elevated\QueueCutoff.Elevated.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o $out
Start-Process "$out\QueueCutoff.App.exe"
```
