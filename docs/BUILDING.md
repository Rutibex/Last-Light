# Building and distributing Lastlight

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Internet access **during initial restore** for NuGet packages (MonoGame DesktopGL 3.8.4.1)
- An OpenGL/SDL compatible desktop environment **for running the client**
- Windows x64 for the documented distributable target

## Build and run from source

From the repository root:

```sh
dotnet restore Lastlight.sln
dotnet build Lastlight.sln -c Release
dotnet run --project src/Lastlight.Desktop -c Release
```

On Windows, `Run-Lastlight-Source.bat` wraps `dotnet run`. A freshly built SDK application is distinct from the packaged self-contained player release.

## Publish a self-contained Windows build

```sh
dotnet publish src/Lastlight.Desktop/Lastlight.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/Windows-x64
```

On Windows, `build-windows.bat` performs the same publish step. The produced `Lastlight.exe`, DLLs, native dependencies, runtime configuration and `Assets/` folder are required **as a set**. Do not manually delete .NET runtime DLLs from a self-contained publish. Supply the third-party notices and a player README with the distributable. No .NET installation is required for the resulting player archive.

## Automated GitHub workflows

- `.github/workflows/ci.yml` restores and builds the solution and runs the core's release regression subset.
- `.github/workflows/windows-release.yml` publishes the Windows executable. Push a `v*` tag to create a GitHub release with a fresh Windows ZIP; manual workflow dispatch creates an artifact without releasing it.

The provided standalone Windows archive is from the **supplied release candidate**, not rebuilt in this environment. Its native Windows runtime behavior remains to be checked on Windows before wide distribution. The workflows are provided for repeatable future builds, not evidence that they have already passed.

## What not to commit

No `bin/`, `obj/`, `artifacts/`, player saves, transient logs, diagnostic screenshots or published runtimes belong in the source repository. The only `.lastlight` files retained are intentional, immutable compatibility fixtures in `tests/fixtures`. Use GitHub Releases for compiled downloads.
