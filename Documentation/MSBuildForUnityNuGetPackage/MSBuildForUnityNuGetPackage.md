# MSBuildForUnity NuGet Package

The MSBuildForUnity NuGet package augments the default MSBuild build logic to ensure the build output is suitable for Unity consumption.

While [MSBuild Project Builder](../MSBuildProjectBuilder/MSBuildProjectBuilder.md) enables building any MSBuild project, the common use cases for this are:

1. Pulling NuGet packages into the Unity project.
1. Pulling shared MSBuild projects into the Unity project (e.g. an MSBuild project that builds code that is used for both a Unity project and a non-Unity project such as Xamarin, UWP, etc.).

For these cases, the MSBuildForUnity NuGet package simplifies this through the following features.

## Unity Meta File Generator

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

## Dependency Resolution

Dependency graph resolution is provided by [`Publish.props`](../../Source/MSBuildTools.Unity.NuGet/Publish.props) and [`Publish.targets`](../../Source/MSBuildTools.Unity.NuGet/Publish.targets). This results in the project's full dependency graph (resolving `ProjectReferences` and `PackageReferences`) being copied to the build output directory. This includes assemblies as well as content.

## Debug Symbol Patching

TODO