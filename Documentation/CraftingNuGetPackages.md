# Crafting a NuGet Package for MSB4U

> MSBuildForUnity (MSB4U) consumes .NET based packages without any modifications to the package. This documentation describes how C++, WinRT or packages with Unity-specific contents can easily be adapted to work with MSBuildForUnity.

## Quick Start

A NuGet package is enabled for MSB4U through the use of a [Build Targets or Props](https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package#include-msbuild-props-and-targets-in-a-package) file inside a NuGet package, and a set of properties provided by MSB4U. The props/targets file is then responsible for declaring the contents that will get placed into the package consumer's Unity project. Essentially, the steps are as follows:

1. In your package root, add a `Unity` folder with content to be placed into the consumer's Unity project:
    - Assets - prefabs, meshes, textures, etc
    - Native DLLs for each platform you support.
    - Unity .meta files for the Assets and DLLs (with appropriate architecture marked)
2. Add a `build/<YourNuGetPackageName>.targets` file to your package, with the following contents:

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

    > This is a real example taken from the [Microsoft.Windows.MixedReality.DotNetWinRT](https://www.nuget.org/packages/Microsoft.Windows.MixedReality.DotNetWinRT/) NuGet package.

### What Happens

Those two steps are the basics for getting up to speed. When MSB4U is used to reference your NuGet package, the following will happen:

- Using the active Target Framework (.NET46, .NET Standard 2.0) configured in the consumer's Unity project, MSBuild will pick up the DLL from the libs folder and bring it into the Unity project.
- Furthermore, MSBuild will automatically reference the `.targets` file.
- MSB4U has a property `MSBuildForUnityVersion` declared, and thereby the condition `'$(MSBuildForUnityVersion)' != ''` will evaluate to `true`.
  - This will include the Unity folder in your NuGet package as Content, which will get put into the consumer's Unity project.

### Properties Provided Through MSBuildForUnity

The following properties are either passed through as the build CLI argument, or available in the Unity Project root's `MSBuildForUnity.Common.props` file.

- **UnityConfiguration:** Whether we are building for `InEditor` or `Player`. (**LIMITATION** we only support `InEditor` for now; [Issue #59](https://github.com/microsoft/MSBuildForUnity/issues/59))
- **UnityPlatform:** The platform that we are building for, the values match Unity's `BuildTarget` enum ([documentation](https://docs.unity3d.com/ScriptReference/BuildTarget.html))
- **MSBuildForUnityVersion:** The `{Major}.{Minor}.{Patch}` version of MSBuildForUnity, it can be used in comparison.
- **MSBuildForUnityDefaultOutputPath:** The default output path for the Unity project's dependencies.
  > **NOTE:** Don't rely on this unless you absolutely need to, rely on default MSBuild "copy-to-output" behavior.
- `(MSBuild)` **TargetFramework:** By default, MSBuild will make TargetFramework available.
  > MSBuild will also automatically bring in the DLLs from the NuGet packages' matching target framework folder.
- **UnityMajorVersion:** The major version of Unity (ie. 2018, 2019, etc)
- **UnityMinorVersion:** The minor version of Unity (ie. 1, 2, 3, 4)

### Limitations

Because all package resolution happens in the Unity Editor during edit-time compilation, we don't have access to the target architecture of the final device the code will live on. Because of this, we must include all flavors of Native binaries.
