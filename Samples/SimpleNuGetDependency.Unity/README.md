# Simple NuGet Dependency

This sample shows how to make use of MSBuildForUnity to embed a NuGet dependency into your project with minimal other impact on your project.

## Declaring the Dependency

In order for you to declare the dependency, you need several key things:

- A folder for your dependency files
- A C# project file (.csproj) file that specifies the dependency.
- A .gitignore that ensures only the necessary files are seen by git.
- **Temporary:** A NuGet.config that contains the special feed of MSBuildForUnity NuGet package. This requirement will go away soon.

This sample has it set up under the following location `Assets/NewtonSoftDependency`.

### C# Project File Structure

The project file structure has several required parts to it:

- The `MSBuildForUnity.Common.props` import (if found).
  - This file contains some core properties that we require.
- Conditional declaration of `TargetFramework` that depends on whether `UnityCurrentTargetFramework` property is available from above import.
  - The target framework is what determines which DLL of a NuGet package is pulled in. MSBuildForUnity keeps the property up to date in `MSBuildForUnity.Common.props`.
- Setting the `BaseIntermediateOutputPath` (object directory) and `OutputPath` to be invisible by Unity, by prefixing it with a '.'
- `Sdk.props` and `Sdk.targets` import that doesn't produce a dll (`Microsoft.Build.NoTargets`), must be version `1.0.85`.

The `Assets/NewtonSoftDependency/NewtonSoftDependency.csproj` contains all of this, plus the NuGet reference to "NewtonSoft.Json" and is a total of 30 lines long.

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove(MSBuildForUnity.Common.props))" Condition="Exists('$([MSBuild]::GetPathOfFileAbove(MSBuildForUnity.Common.props))')" />
  
  <PropertyGroup Condition="'$(UnityCurrentTargetFramework)' == ''">
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
  
  <PropertyGroup Condition="'$(UnityCurrentTargetFramework)' != ''">
    <TargetFramework>$(UnityCurrentTargetFramework)</TargetFramework>
  </PropertyGroup>
  
  <PropertyGroup>
    <!-- Make sure Unity ignores the contents of the output path. -->
    <BaseIntermediateOutputPath>.obj\</BaseIntermediateOutputPath>
    <OutputPath>.bin\</OutputPath>
  </PropertyGroup>
  
  <!-- Note that this is the special "NoTarget" SDK to prevent this project from producing a dll. -->
  <Import Project="Sdk.props" Sdk="Microsoft.Build.NoTargets" Version="1.0.85" />
  
  <ItemGroup>
    <!-- Standard NuGet package -->
    <PackageReference Include="Newtonsoft.Json" Version="12.0.2" />

    <!-- A MSBuildForUnity enabled NuGet package that has additional behavior for Unity specific (no difference in import) -->
    <PackageReference Include="Microsoft.MixedReality.QR" Version="0.5.2085"/>
  </ItemGroup>

  <Import Project="Sdk.targets" Sdk="Microsoft.Build.NoTargets" Version="1.0.85" />
</Project>
```

## The Process

When MSBuildForUnity is brought into the project, the following happens:

1. Generates the small `MSBuildForUnity.Common.props` file.
2. Generates a top-level `{ProjectName}.Dependencies.msb4u.csproj` file.
2. Auto-processes all of the .csproj files in the repository, and builds them.

## Question & Answer

### Referencing External C# Projects

It is possible to reference external C# projects in the same manner as the NuGet packages. As long as the .csproj file contains the structure described above, you can include other tasks and references in it.

### Changing Target Scripting Subset

If you update the Unity project form .NET 4.6 to .NET Standard 2.0, the `MSBuildForUnity.Common.props` will be updated to reflect the change, and the project would get re-built.

> Today the project won't be cleaned, and the older NuGet artifacts will still be on disk. You would either have to do a `git clean` or manually delete that folder.
