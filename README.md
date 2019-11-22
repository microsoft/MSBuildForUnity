# MSBuildForUnity

MSBuildForUnity solves the problem of establishing clear dependency relationships between Unity project and other .NET components such as external (to Unity) C# projects, or NuGet packages. It creates a familiar to .NET developers project structure and ensures that the dependencies are resolved and brought into the Unity project as appropriate. With this component, you can:

- Share code between Unity and other .NET projects (such as UWP XAML apps, Xamarin apps, etc.).
- Consuming existing .NET components (e.g. NuGet packages).

The samples included in this repository best convey the simplicity and value of this component:

- [Simple NuGet Dependency Sample](Samples/SimpleNuGetDependency.Unity/README.md) - Showcases the simplest and most minimal usage of this component to pull in a dependency.
- [Integrated Dependencies Sample](Samples/IntegratedDependencies.Unity/README.md) - Showcases the power of this component by relying on project generation.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

### Prerequisites

The following tools are required to contribute to this project:
- [Visual Studio 2017+](https://visualstudio.microsoft.com/downloads)
- [Unity 2018+](https://unity3d.com/get-unity/download)

To get started, clone the repo, and then run `git submodule update --init` to initialize submodules.

### Builds and Packages

| Build | Status                                               |
|-------|------------------------------------------------------|
| UPM   | [![UPM Build Status][UPMBuildBadge]][UPMBuild]       |
| NuGet | [![NuGet Build Status][NuGetBuildBadge]][NuGetBuild] |

| Package | Feed                                                                                                                  |
|---------|-----------------------------------------------------------------------------------------------------------------------|
| UPM     | [Azure DevOps](https://dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_packaging?_a=feed&feed=UnityDeveloperTools) |
| NuGet   | [![NuGet Package][NuGetPackageBadge]][NuGetPackage]                                                                   |

## Quick Start

Following are basic instructions for taking advantage of MSBuildForUnity for some common scenarios.

### Scenario 1: Bring NuGet packages and MSBuild projects into a Unity project

This scenario leverages the MSBuildForUnity [Project Builder](#msbuild-project-builder) and the MSBuildForUnity [NuGet Package](#msbuildforunity-nuget-package).

1. Add the `com.microsoft.msbuildforunity` UPM (Unity Package Manager) package.
    - Edit the `Packages/manifest.json` file in your Unity project.
    - Add the following near the top of the file:
        ```json
        "scopedRegistries": [
            {
                "name": "Microsoft",
                "url": "https://pkgs.dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_packaging/UnityDeveloperTools/npm/registry/",
                "scopes": [
                    "com.microsoft"
                ]
            }
        ],
        ```
    - Add the following to the `dependencies` section of the file:
        ```json
          "com.microsoft.msbuildforunity": "0.8.1"
        ```
1. Create a "SDK style" MSBuild project (e.g. csproj) somewhere under your `Assets` directory of your Unity project that references the `MSBuildForUnity` NuGet package. Here is an example:
    ```xml
    <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
            <TargetFramework>netstandard2.0</TargetFramework>
        </PropertyGroup>
        <ItemGroup>
            <PackageReference Include="MSBuildForUnity" Version="0.8.1">
                <PrivateAssets>all</PrivateAssets>
                <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
            </PackageReference>
        </ItemGroup>
    </Project>
    ```
1. Add additional references to any NuGet packages you want to use in your Unity project.

## Features

MSBuildForUnity has several features that can be used independently or in conjunction.

### MSBuild Project Builder

The MSBuild Project Builder provides a means of building MSBuild projects from within Unity, where the output is generally consumed by the Unity project.

[![MSBuild Project Builder Progress Bar](Documentation/MSBuildProjectBuilder/MSBuildProgressBar.gif)](Documentation/MSBuildProjectBuilder/MSBuildProjectBuilder.md)

For details, see the [documentation](Documentation/MSBuildProjectBuilder/MSBuildProjectBuilder.md), [source](Source/MSBuildTools.Unity/Packages/com.microsoft.msbuildforunity/Editor/ProjectBuilder/MSBuildProjectBuilder.cs), and [samples](Source/MSBuildTools.Unity/Assets/Samples/Samples.sln).

### MSBuild Project Generator

The MSBuild Project Generator will generate a Visual Studio solution configured for building the Unity project into DLLs outside of Unity. This solution is configured for each of the platforms installed with Unity and the InEditor/Player variants of the assemblies.

### MSBuildForUnity NuGet Package

The `MSBuildForUnity` NuGet package augments the default MSBuild build logic to ensure the build output is suitable for Unity consumption. This package can be referenced from MSBuild projects that are built by the [MSBuild Project Builder](#msbuild-project-builder) to add these features:

- Meta file generation - generates .meta files for build output such as .dlls.
- Dependency resolution - all dependencies (through `PackageReference`s or `ProjectReference`s) are resolved and sent to the output directory (which is typically under the Unity project's Assets directory).
- Debug symbol patching - enables debugging pre-built dlls (e.g. from NuGet packages) while running in the Unity Editor.

For details, see the [documentation](Documentation/MSBuildForUnityNuGetPackage/MSBuildForUnityNuGetPackage.md), [source](Source/MSBuildTools.Unity.NuGet/MSBuildForUnity.csproj), and [samples](Source/MSBuildTools.Unity/Assets/Samples/Samples.sln).


[PRBuildBadge]: https://dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_apis/build/status/MSBuildForUnity.PRGate?branchName=master
[PRBuild]: https://dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_build/latest?definitionId=2&branchName=master

[UPMBuildBadge]: https://dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_apis/build/status/MSBuildForUnity.Publish.UPM?branchName=master
[UPMBuild]: https://dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_build/latest?definitionId=1&branchName=master

[NuGetBuildBadge]: https://dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_apis/build/status/MSBuildForUnity.Publish.NuGet?branchName=master
[NuGetBuild]: https://dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_build/latest?definitionId=3&branchName=master

[NuGetPackageBadge]: https://feeds.dev.azure.com/UnityDeveloperTools/0cb95e25-9194-4ccd-9afb-439b25ecb93a/_apis/public/Packaging/Feeds/a3d1c3cc-6042-4e05-b699-39a947e75639/Packages/bdf78d31-dd97-4f6b-befb-75bb6185172e/Badge
[NuGetPackage]: https://dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_packaging?_a=package&feed=a3d1c3cc-6042-4e05-b699-39a947e75639&package=bdf78d31-dd97-4f6b-befb-75bb6185172e&preferRelease=true
