using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Xml.Linq;

namespace MRW.Shared.Build.UnityApp.Tasks
{
    /// <summary>
    /// This task searches up from the given filepath to find a nuspec file. If one is found
    /// it will append metadata properties with information about the NuGet package.
    /// </summary>
    public sealed class AppendNuGetInformation : Task
    {
        [Required]
        [Output]
        public ITaskItem[] Items { get; set; }

        private const string NUSPEC_EXTENSION_SEARCH = "*.nuspec";
        private const string NUSPEC_XML_NAMESPACE_ATTRIBUTE = "xmlns";
        private const string NUSPEC_XML_METADATA_ELEMENT = "metadata";
        private const string NUSPEC_XML_ID_ELEMENT = "id";
        private const string NUSPEC_XML_VERSION_ELEMENT = "version";

        public override bool Execute()
        {
            foreach (ITaskItem item in Items)
            {
                if (!File.Exists(item.ItemSpec) && !Directory.Exists(item.ItemSpec))
                {
                    // The given file or directory doesn't exist (or can't be accessed). Nothing can be done, move onto the next item.
                    continue;
                }

                // Find the nearest nuspec file. If the given item is a directory then start from there, if it's a file start from the parent folder.
                string nuspecFile = LocateNuspecFile(Path.HasExtension(item.ItemSpec) ? Path.GetDirectoryName(item.ItemSpec) : item.ItemSpec);

                if (!File.Exists(nuspecFile))
                {
                    // The nuspec file could not be located. Move onto the next item.
                    continue;
                }

                // Read the PackageId and PackageVersion from the nuspec file
                XElement nuspecContent = XElement.Load(nuspecFile);
                XNamespace nuspecNamespace = nuspecContent.Attribute(NUSPEC_XML_NAMESPACE_ATTRIBUTE)?.Value ?? "";
                string packageID = nuspecContent.Element(nuspecNamespace + NUSPEC_XML_METADATA_ELEMENT)?.Element(nuspecNamespace + NUSPEC_XML_ID_ELEMENT)?.Value;
                string packageVersion = nuspecContent.Element(nuspecNamespace + NUSPEC_XML_METADATA_ELEMENT)?.Element(nuspecNamespace + NUSPEC_XML_VERSION_ELEMENT)?.Value;
                string packageRootPath = Path.GetDirectoryName(nuspecFile) + Path.DirectorySeparatorChar;

                item.SetMetadata("NuGetPackageId", packageID);
                item.SetMetadata("NuGetPackageVersion", packageVersion);
                item.SetMetadata("NuGetPackagePath", packageRootPath);
            }

            // We've succeeded if we didn't hit any exceptions
            return true;
        }

        /// <summary>
        /// Recursively look up through the folder path until a nuspec file is found or the root of the path is reached
        /// </summary>
        private string LocateNuspecFile(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }

            string[] nuspecFilesInFolder = Directory.GetFiles(folder, NUSPEC_EXTENSION_SEARCH);

            if (nuspecFilesInFolder != null && nuspecFilesInFolder.Length > 0)
            {
                // A valid NuGet package cannot contain any nuspec file in it other than it's main one. If we've found multiple
                // throw a warning that the behavior is unexpected and then use the first nuspec found.
                if (nuspecFilesInFolder.Length > 1)
                {
                    Log.LogWarning($"Found multiple .nuspec files at \"{folder}\". It is not expected for a NuGet package to have multiple .nuspec files. The first one found \"{nuspecFilesInFolder[0]}\" will be used.");
                }

                return Path.GetFullPath(nuspecFilesInFolder[0]);
            }

            // Recurse into the parent folder. Path.GetDirectoryName will work on files as well as directories to get the parent.
            return LocateNuspecFile(Path.GetDirectoryName(folder));
        }
    }
}
