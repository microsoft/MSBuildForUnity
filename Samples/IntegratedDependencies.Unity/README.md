# Integrated Dependencies

This project shows a considerably more robust usage of MSBuildForUnity for your dependency management needs. Here, we rely on project generation to create .csproj files for each Assembly Definition file declared in the project, as well as, the top-level Dependencies project. Furthermore, we include an "External (to Unity)" .csproj and establish a dependency relationship between two of our components and that code. And finally, these dependency relationships are actually established depending on the platform that is currently selected in Unity.

## Generated Projects

When you open this sample in Unity, you will note that project generation is enabled:

![Project Generation Enabled](docs/ProjectGenerationEnabled.png)

> This is persisted in a checked-in file at `IntegratedDependencies.Unity/MSBuild/settings.json`.

Generation is split into two types of files being created:

- Persistent files that can be checked-in:
  - C# Project files that are found under `Assets/` or `Packages/` folder.
  - Solution file in the Assets folder
- Transient files Under `MSBuild/` that will be regenerated, and shouldn't be checked in.
- MSBuildForUnity.Common.props that will be regenerated and shouldn't be checked-in.

There is a limited set of times when the projects would be automatically regenerated:

- When you enable `Auto Generation Enabled`, it will go ahead and generate.
- When you launch Unity
- When you switch platforms
- Every time a rebuild happens, we do a super fast regeneration of MSBuildForUnity.common.props.

## Establishing Dependency Relationship

In order to add dependencies to your project, you can open any C# project under `Assets/` or `Packages/` folder and modify it. For example, here is the `Assembly-CSharp.msb4u.csproj`.

```xml
<Project ToolsVersion="15.0">
  <!--GENERATED FILE-->
  <!--
    This file can be modified and checked in as long as the following rules are met:
    - Both the imports are present:
    - - <Import Project="$(MSBuildProjectName).g.props" />
    - - <Import Project="$(MSBuildProjectName).g.targets" />
    - Nothing above the props import or below the targets import is modified
    - No C# source files are added for compilation
    
    You can modify this file as follows:
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
    <!--anborod: This is a weird thing, it is a required property (even if commented)-->
    <!--<TargetFrameworks>netstandard2.0;uap10.0;net46</TargetFrameworks> -->
  </PropertyGroup>

  <Import Project="$(MSBuildForUnityGeneratedOutputDirectory)\$(MSBuildProjectName).g.props" />

  <ItemGroup>
  <!-- Add your references here -->
    <ProjectReference Include="..\External\CommonLibrary\CommonLibrary.csproj" />
  </ItemGroup>

  <Import Project="$(MSBuildForUnityGeneratedOutputDirectory)\$(MSBuildProjectName).g.targets" />
</Project>
```

This project has been modified to depend on an external C# project that is unrelated to Unity. Thus when this project is built, it will build that dependency and bring it into the Unity Assets folder.

### Platform Specific Dependency

Furthermore, this sample includes an example of how to specify a dependency for a specific platform. The Assembly Definition at `Assets/WSASpecific/Component.WSA.asmdef` is marked to be built for `Universal Windows Platform` and `Editor` platforms; additionally it specifies a `UNITY_WSA` define constraint that only allows it to be compiled when UWP platform is selected.

![Dependencies.msb4u.csproj](docs/UWPAsmDef.png)

The generated project `Assets/WSASpecific/Component.WSA.msb4u.csproj` contains a dependency on `CommonLibrary.WSA.csproj` that would get built and pulled in only when the Editor is set to UWP platform.

```xml
<Project ToolsVersion="15.0">
  <!--GENERATED FILE-->
  <!--
    This file can be modified and checked in.
    
    It is different from the other generated C# Projects in that it will be the one gathering all dependencies and placing them into the Unity asset folder.
    
    You can add project level dependencies to this file, by placing them below:
    - <Import Project="$(MSBuildForUnityGeneratedOutputDirectory)\$(MSBuildProjectName).g.props" />
    and before:
    - <Import Project="$(MSBuildForUnityGeneratedOutputDirectory)\$(MSBuildProjectName).g.targets" />
    
    Do not add any source or compliation items.
    
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
    <!--anborod: This is a weird thing, it is a required property (even if commented)-->
    <!--<TargetFrameworks>netstandard2.0;uap10.0;net46</TargetFrameworks> -->
  </PropertyGroup>

  <Import Project="$(MSBuildForUnityGeneratedOutputDirectory)\$(MSBuildProjectName).g.props" />
  
  <ItemGroup>

  </ItemGroup>

  <Import Project="$(MSBuildForUnityGeneratedOutputDirectory)\$(MSBuildProjectName).g.targets" />
</Project>
```

> As you can see, this project also incudes the `NewtonSoft.Json` NuGet package reference that will be brought in.

## Building Dependencies

MSBuild Tools for Unity auto builds the `Dependencies.msb4u.csproj` at various times in order for these dependencies to be pulled in. If for some reason this doesn't happen, you can select the `Dependencies.msb4u` item under `Assets/` folder and press `Build` yourself.

![Dependencies.msb4u.csproj](docs/CSProjectBuild.png)

## Known Limitations

### Unity Specific NuGet Packages

A NuGet package that was built for NuGet like [Microsoft.MixedReality.Toolkit.Foundation](https://www.nuget.org/packages/Microsoft.MixedReality.Toolkit.Foundation/) won't yet work with this method. Support for that is coming soon.

### Player vs Editor Dependencies

There is no supported way to pull in one dependency for the editor and a different one for when the Player is being built. You can only do it on a platform level.

### When Regeneration and Rebuilding Happens

There are still fine tuning and optimizations that must happen to make sure we regenerate and build the minimum number of times but sufficiently often. Today we err on the side of less.
