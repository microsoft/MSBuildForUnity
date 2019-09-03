# MSBuildForUnity

MSBuildForUnity is a [collection of tools](#features) to help integrate MSBuild with Unity. This is helpful in leveraging more of the .NET ecosystem within Unity. Some things this project helps with are:
- Sharing code (via an MSBuild project) between Unity and other .NET projects (such as UWP XAML apps, Xamarin apps, etc.).
- Consuming existing .NET components (e.g. NuGet packages).

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

## Features

MSBuildForUnity has several features that can be used independently or in conjunction.

### MSBuild Project Builder

The MSBuild Project Builder provides a means of building MSBuild projects from within Unity, where the output is generally consumed by the Unity project.

[![MSBuild Project Builder Progress Bar](Documentation/MSBuildProjectBuilder/MSBuildProgressBar.gif)](Documentation/MSBuildProjectBuilder/MSBuildProjectBuilder.md)

For details, see the [documentation](Documentation/MSBuildProjectBuilder/MSBuildProjectBuilder.md), [source](Source/MSBuildProjectBuilder/Assets/MSBuildProjectBuilder/Editor/MSBuildProjectBuilder.cs), and [samples](Source/MSBuildProjectBuilder/Assets/Samples/Samples.sln).

### MSBuild Project Generator

The MSBuild Project Generator will generate a Visual Studio solution configured for building the Unity project into DLLs outside of Unity. This solution is configured for each of the platforms installed with Unity and the InEditor/Player variants of the assemblies.
