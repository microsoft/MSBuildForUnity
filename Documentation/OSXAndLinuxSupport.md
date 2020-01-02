# OSX and Linux Support

MSBuildForUnity (MSB4U) has **beta** support for Unity on OSX and Linux platforms. If you encounter issues, please file a [new issue](https://github.com/microsoft/MSBuildForUnity/issues/new) with OSX or Linux label.

## Overview

MSB4U should behave the same on these platforms as on Windows, required software just needs to be installed. However, there is a special step that is needed when adding a dependency to a C# project with the TargetFramework of .NET 4.6 ([see below](OSXAndLinuxSupport.md#Targeting-.NET-4.6)).

### Required Software

The following software needs to be installed in addition to Unity:

- [.NET Core](https://dotnet.microsoft.com/download) - Select Linux or macOS
- Mono [Linux](https://www.mono-project.com/download/stable/#download-lin) or [macOS](https://www.mono-project.com/download/stable/#download-mac); should have been installed with Unity.

## Targeting .NET 4.6

.NET Framework is not available for macOS or Linux, however, with Unity you can configure your project to target .NET 4.6. Once you do this, if you have a dependency on an external C# project, it will also need to build as a .NET 4.6 project. To enable that project to successfully build, you will need to add the following NuGet package:

```xml
<PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.0">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```
