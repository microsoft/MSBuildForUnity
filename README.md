# MSBuildForUnity

MSBuildForUnity solves the problem of establishing clear dependency relationships between Unity project and other .NET components such as external (to Unity) C# projects, or NuGet packages. It creates a familiar to .NET developers project structure and ensures that the dependencies are resolved and brought into the Unity project as appropriate. With this component, you can:

- Share code between Unity and other .NET projects (such as UWP XAML apps, Xamarin apps, etc.).
- Consuming existing .NET components (e.g. NuGet packages).

The samples included in this repository best convey the simplicity and value of this component:

- [Simple NuGet Dependency Sample](Samples/SimpleNuGetDependency.Unity/README.md) - Showcases the simplest and most minimal usage of this component to pull in a dependency.
- [Integrated Dependencies Sample](Samples/IntegratedDependencies.Unity/README.md) - Showcases the power of this component by relying on project generation.
- [Cross Unity Dependencies Sample](Samples/CrossUnityDependencies.Unity/README.md) - Showcases how to establish a dependency between two Unity projects (almost like AsmDef-to-AsmDef).

### Builds and Packages

| Build | Build Status                                         | Package Feed                                                                                                          |
|-------|------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------|
| UPM   | [![UPM Build Status][UPMBuildBadge]][UPMBuild]       | [Azure DevOps](https://dev.azure.com/UnityDeveloperTools/MSBuildForUnity/_packaging?_a=feed&feed=UnityDeveloperTools) |
| NuGet | [![NuGet Build Status][NuGetBuildBadge]][NuGetBuild] | [![NuGet Package][NuGetPackageBadge]][NuGetPackage]                                                                   |

## Quick Start

The following are basic instructions for taking advantage of MSBuildForUnity for some common scenarios.

### Bring NuGet packages and MSBuild projects into a Unity project

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
          "com.microsoft.msbuildforunity": "0.9.2-20200131.11"
        ```

1. MSBuildForUnity will create a top-level project in your `Assets` folder named after your Unity project name: `{UnityProjectName}.Dependencies.msb4u.csproj`, edit this project file to add additional references to any NuGet packages or C# projects you want to use in your Unity project.

    ```xml
    <Project ToolsVersion="15.0">
    <!--GENERATED FILE-->
    <!--
        This file can be modified and checked in.
        
        It is different from the other generated C# Projects in that it will be the one gathering all dependencies and placing them into the Unity asset folder.
        
        You can add project level dependencies to this file, by placing them below:
        - <Import Project="$(MSBuildForUnityGeneratedProjectDirectory)\$(MSBuildProjectName).g.props" />
        and before:
        - <Import Project="$(MSBuildForUnityGeneratedProjectDirectory)\$(MSBuildProjectName).g.targets" />
        
        Do not add any source or compilation items.
        
        Examples of how you can modify this file:
        - Add NuGet package references:
            <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="11.0.1" />
            </ItemGroup>
        - Add external C# project references:
        <ItemGroup>
            <ProjectReference Include="..\..\..\ExternalLib\ExternalLib.csproj" />
        </ItemGroup>
    -->

    <Import Project="$([MSBuild]::GetPathOfFileAbove(MSBuildForUnity.Common.props))" Condition="Exists('$([MSBuild]::GetPathOfFileAbove(MSBuildForUnity.Common.props))')" />

    <PropertyGroup>
        <TargetFramework>$(UnityCurrentTargetFramework)</TargetFramework>
    </PropertyGroup>

    <!-- SDK.props is imported inside this props file -->
    <Import Project="$(MSBuildForUnityGeneratedProjectDirectory)\$(MSBuildProjectName).g.props" />

    <ItemGroup>
        <!--Add NuGet or Project references here-->
    </ItemGroup>

    <!-- SDK.targets is imported inside this props file -->
    <Import Project="$(MSBuildForUnityGeneratedProjectDirectory)\$(MSBuildProjectName).g.targets" />
    </Project>
    ```

1. For additional instructions, see [Core Scenarios](Documentation/CoreScenarios.md).

## Extended Instructions

Here you can find additional instructions for various things you may want to accomplish:

- [Add Support MSB4U to a NuGet Package](Documentation/CraftingNuGetPackages.md)
- [OSX and Linux Support](Documentation/OSXAndLinuxSupport.md)

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

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com>.

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
