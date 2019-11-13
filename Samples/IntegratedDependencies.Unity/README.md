# Integrated Dependencies

This project shows a considerably more robust usage of MSBuildForUnity for your dependency management needs. Here, we rely on project generation to create .csproj files for each Assembly Definition file declared in the project, as well as, the top-level Dependencies project. Furthermore, we include an "External (to Unity)" .csproj and establish a dependency relationship between two of our components and that code. And finally, these dependency relationships are actually established depending on the platform that is currently selected in Unity.

## Generated Projects

When you open this sample in Unity, you will note that project generation is enabled:

![Project Generation Enabled](docs\ProjectGenerationEnabled.png)

> This is persisted in a checked-in file at `IntegratedDependencies.Unity\MSBuild\settings.json`.



## Establishing Dependency Relationship

### Platform Specific Dependency

## Building Dependencies

## Known Limitations








## Declaring the Dependency

In order for you to declare the dependency, you need several key things:

- A folder for your dependency files
- A C# project file (.csproj) file that specifies the dependency.
- A .gitignore that ensures only the necessary files are seen by git.
- **Temporary:** A NuGet.config that contains the special feed of MSBuildForUnity NuGet package. This requirement will go away soon.

This sample has it set up under the following location `Assets\NewtonSoftDependency`.

### C# Project File Structure

The project file structure has several required parts to it:

- The `MSBuildForUnity.Common.props` import (if found).
  - This file contains some core properties that we require.
- Conditional declaration of `TargetFramework` that depends on whether `UnityCurrentTargetFramework` property is available from above import.
  - The target framework is what determines which DLL of a NuGet package is pulled in. MSBuildForUnity keeps the property up to date in `MSBuildForUnity.Common.props`.
- Setting the `BaseIntermediateOutputPath` (object directory) to be invisible by Unity, by prefixing it with a '.'
- Naming the `OutputPath` that will be seen by Unity, this is where the NuGet output will be dumped into.
- Package reference to `MSBuildForUnity` NuGet package that enables the correct resolution of dependencies.
- `Sdk.props` and `Sdk.targets` import that doesn't produce a dll (`Microsoft.Build.NoTargets`).

The `Assets\NewtonSoftDependency\NewtonSoftDependency.csproj` contains all of this, plus the NuGet reference to "NewtonSoft.Json" and is a total of 33 lines long.

![C# Project File Contents](docs\CSProjectContents.png)

## The Process

When MSBuildForUnity is brought into the project, the following happens:

1. Generates the small `MSBuildForUnity.Common.props` file.
2. Auto-processes all of the .csproj files in the repository, and builds them.

## Question & Answer

### Referencing External C# Projects

It is possible to reference external C# projects in the same manner as the NuGet packages. As long as the .csproj file contains the structure described above, you can include other tasks and references in it.

### Changing Target Scripting Subset

If you update the Unity project form .NET 4.6 to .NET Standard 2.0, the `MSBuildForUnity.Common.props` will be updated to reflect the change, and the project would get re-built.

> Today the project won't be cleaned, and the older NuGet artifacts will still be on disk. You would either have to do a `git clean` or manually delete that folder.
