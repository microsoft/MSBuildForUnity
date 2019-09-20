# MSBuildForUnity NuGet Package

The MSBuildForUnity NuGet package augments the default MSBuild build logic to ensure the build output is suitable for Unity consumption.

While [MSBuild Project Builder](../MSBuildProjectBuilder/MSBuildProjectBuilder.md) enables building any MSBuild project, the common use cases for this are:

1. Pulling NuGet packages into the Unity project.
1. Pulling shared MSBuild projects into the Unity project (e.g. an MSBuild project that builds code that is used for both a Unity project and a non-Unity project such as Xamarin, UWP, etc.).

The MSBuildForUnity NuGet package simplifies these scenarios.

## Features

The MSBuildForUnity NuGet package includes the following features:

- [Meta file generation](#unity-meta-file-generator) - generates .meta files for build output such as .dlls.
- [Dependency resolution](#dependency-resolution) - all dependencies (through `PackageReference`s or `ProjectReference`s) are resolved and sent to the output directory (which is typically under the Unity project's Assets directory).
- [Debug symbol patching](#debug-symbol-patching) - enables debugging pre-built dlls (e.g. from NuGet packages) while running in the Unity Editor.

### Unity Meta File Generator

Unity meta file generation is provided by [`UnityMetaFileGenerator.props`](../../Source/MSBuildTools.Unity.NuGet/UnityMetaFileGenerator.props) and [`UnityMetaFileGenerator.targets`](../../Source/MSBuildTools.Unity.NuGet/UnityMetaFileGenerator.targets). This allows the build to generate .meta files for built files that Unity would expect to have a Player-specific associated .meta file.

When the `$(UnityPlayer)` MSBuild property is set, then the generated .meta file targets the platforms associated with that player. By default, this is defined as:

| `$(UnityPlayer)` | Player Meta Entries                                      |
|------------------|----------------------------------------------------------|
| Editor           | Editor                                                   |
| Standalone       | Win, Win64, Linux, Linux64, LinuxUniversal, OSXUniversal |
| UAP              | WindowsStoreApps                                         |
| iOS              | iOS                                                      |
| Android          | Android                                                  |

When `$(UnityPlayer)` is not set, then `$(ExcludedUnityPlayers)` can optionally be defined. In this case, the generated .meta file targets any platform, excluding any platforms listed in `$(ExcludedUnityPlayers)`. You should always start with a "default" project that defines neither `$(UnityPlayer)` nor `$(ExcludedUnityPlayers)`. When you need a player specific variation, you should create a new project that defines `$(UnityPlayer)`, and add that player to the `$(ExcludedUnityPlayers)` of the "default" project.

These properties should be defined in the MSBuild project being built by [MSBuild Project Builder](../MSBuildProjectBuilder/MSBuildProjectBuilder.md). Examples of how this might look are as follows:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>

        <!-- Define $(UnityPlayer) if pulling in NuGet packages and/or MSBuild projects for a specific Unity player, such as iOS. -->
        <UnityPlayer>iOS</UnityPlayer>

        <!-- Define $(ExcludedUnityPlayers) if pulling in NuGet packages and/or MSBuild projects for all Unity players except specific Unity players, such as iOS and Android. -->
        <ExcludedUnityPlayers>iOS;Android</ExcludedUnityPlayers>
    </PropertyGroup>
    ...
</Project>
```

Generated .meta files also include a "stable" asset id (guid). This is generated from the file name and either the `$(UnityPlayer)` property.

### Dependency Resolution

Dependency graph resolution is provided by [`Publish.props`](../../Source/MSBuildTools.Unity.NuGet/Publish.props) and [`Publish.targets`](../../Source/MSBuildTools.Unity.NuGet/Publish.targets). This results in the project's full dependency graph (resolving `ProjectReferences` and `PackageReferences`) being copied to the build output directory. This includes assemblies as well as content.

### Debug Symbol Patching

TODO

## MSBuild Project Configuration

In the simplest case, a single MSBuild project using the `netstandard2.0` "target framework moniker" (TFM) (e.g. `<TargetFramework>netstandard2.0</TargetFramework>`) will be sufficient. However, there are a number of scenarios where this breaks down and multiple MSBuild projects are required.

*Note* - an alternative to multiple MSBuild projects is a single MSBuild project that is parameterized on custom MSBuild properties that are passed in on the command line, but this is not well supported in Visual Studio is currently not supported by [MSBuild Project Builder](../MSBuildProjectBuilder/MSBuildProjectBuilder.md).

### .NET Scripting Backend

When using the IL2CPP scripting backend, Unity supports .NET Standard 2.0. However, when using the .NET scripting backend, this is not available. It may be possible to still have a single project that uses the `net471` TFM, but this depends on Unity's assembly rewriter and generally requires that all assemblies being consumed only use .NET APIs that are compatible with the .NET runtimes being used for each player (e.g. Editor=Mono, iOS=Mono, Android=Mono, UAP=UWP .NET Core, etc.).

If you are targeting multiple Unity players, and the players use different .NET runtimes and/or only some involve ahead-of-time compilation (e.g. IL2CPP), then you likely will need multiple MSBuild projects with different configurations (e.g. the `$(TargetFramework)` may differ between the projects, as well as the referenced NuGet packages).

### Platform Specific Implementations

Even when IL2CPP is being used for all target Unity players and consumed packages are `netstandard2.0`, multiple projects may be required. For example, a library implementation may be different depending on the platform it is targeting because it needs to *platform invoke* into native platform APIs. Such platform specific implementations could still all be targeting `netstandard2.0`, but could live in different NuGet packages (e.g. MyLibrary.iOS and MyLibrary.Android). In this case, multiple projects would be needed, where each project targets a specific Unity player and has a different set of NuGet package references.

## Building Source

The primary goal of bringing MSBuild projects into a Unity project is to make it easy to consume both NuGet packages and shared MSBuild projects. However, these projects can also be used to build source code. This can be advantageous if you want to take advantage of more of the Visual Studio features such as code analyzers, unit tests, Unity agnostic automated builds, etc.

To do this, you need to ensure Unity will not also try to build the source (since the MSBuild project will build it and place the compiled dll into the Unity project). The easiest way to do this is to simply put all the source code under a folder starting with a period (`.`), which Unity will then ignore. See the [sample code](../../Source/MSBuildTools.Unity/Assets/Samples/.Source/NetStandardProject/Class1.cs) for an example of this, where the source code is all under a `.Source` folder.