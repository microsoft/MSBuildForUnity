# Crafting a NuGet Package for MSB4U

> MSBuildForUnity consumes .NET based packages without any modifications to the package. This documentation describes how C++, WinRT or packages with Unity-specific contents can easily be adapted to work with MSBuildForUnity.

## Quickstart Summary

Though the use of a [Build Targets or Props](https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package#include-msbuild-props-and-targets-in-a-package) file inside a NuGet package, we can leverage properties provided by MSBuildForUnity to appropriately select the contents to bring into the Unity project. The following snippet is taken from [Microsoft.Windows.MixedReality.DotNetWinRT](https://www.nuget.org/packages/Microsoft.Windows.MixedReality.DotNetWinRT/) NuGet package. It brings in the `Unity` folder packaged in the root of the NuGet package to the output folder inside Unity.

```xml
<Project>
  <PropertyGroup>
    <!--All extra resources for this package will be placed under this folder in the output directory.-->
    <PackageDestinationFolder>$(MSBuildThisFileName)</PackageDestinationFolder>
  </PropertyGroup>
  
  <ItemGroup Condition="'$(MSBuildForUnityVersion)' != ''">
    <Content Include="$(MSBuildThisFileDirectory)..\Unity\**">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <!-- Don't show .meta files in Solution Explorer - it's not useful. -->
      <Visible Condition="'%(Extension)' == '.meta'">false</Visible>
      <Link>$(PackageDestinationFolder)\%(RecursiveDir)%(Filename)%(Extension)</Link>
    </Content>
  </ItemGroup>
</Project>
```

## Understanding MSBuildForUnity Behavior

MSBuildForUnity is based on MSBuild and thereby leverages the build rules including package and project reference resoluton of MSBuild. These steps are invoked at Editor Compilation stage with a set of MSBuildForUnity provided variables. At this stage, we have the following information available:

- Unity Version (ie Major.Minor)
- Platform being targeted (UWP, Android, Standalone, etc).
- .NET Target Framework (4.6, .NET Standard 2.0)
- Compilation define symbols specified
- For UWP Platform we know Min and Target UWP SDK

Because this stage is not aware of the target device, and happens before we build the player output (to Android Studio, XCode or Visual Studio) we don't have the following information:

- The target platform of the device (ARM, x86, x64)

This means that we don't know the target platform of the device and thereby can't bring in a specific native DLL (ARM, x86, x64). Thus for NuGet packages with Native code, we must add additional logic to bring in every DLL as well as Unity specific .meta files.

### Meta Files

MSBuildForUnity performs meta file generation for .NET libraries, and there are plans to support processing Native DLLs as well, however this is not possible yet. For this reasons, packages with Native DLLs that must be brought into Unity must be accompanied with .meta files configured for those packages.

### Properties Provided Through MSBuildForUnity

The following properties are either passed through as the build CLI argument, or available in the Unity Project root's `MSBuildForUnity.Common.props` file.

- **UnityConfiguration:** Whether we are building for `InEditor` or `Player`. (**LIMITATION** we only support `InEditor` for now; [Issue #59](https://github.com/microsoft/MSBuildForUnity/issues/59))
- **UnityPlatform:** The platform that we are building for, the values match Unity's `BuildTarget` enum ([documentation](https://docs.unity3d.com/ScriptReference/BuildTarget.html))
- **MSBuildForUnityVersion:** The `{Major}.{Minor}.{Patch}` version of MSBuildForUnity, it can be used in comparison.
- **MSBuildForUnityDefaultOutputPath:** The default output path for the Unity project's dependencies.
  > **NOTE:** Don't rely on this unless you absolutely need to, rely on default MSBuild "copy-to-output" behavior.
- **TargetFramework:** By default, MSBuild will make TargetFramework available.
  > MSBuild will also automatically bring in the DLLs from the NuGet packages' matching target framework folder.
- **UnityMajorVersion:** The major version of Unity (ie. 2018, 2019, etc)
- **UnityMinorVersion:** The minor version of Unity (ie. 1, 2, 3, 4)

## Supporting MSBuildForUnity in your NuGet Package

As mentioned in the [Quickstart Summary](##Quickstart%20Summary), to augment your NuGet package, you should do the following:

1. Add [Build Targets or Props](https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package#include-msbuild-props-and-targets-in-a-package) file to your package.
2. Add necessary .meta files for your Native libraries.
3. Add additional content files (prefabs, etc) that is only Unity specific.
4. Specify your Native DLLs, .Meta and other files as `Content` in the build targets or props file.
5. Make sure to condition your properties/inclusions on the presence of `'$(MSBuildForUnityVersion)' != ''"` property. (If required, you can condition on specific MSBuild or Unity version).
