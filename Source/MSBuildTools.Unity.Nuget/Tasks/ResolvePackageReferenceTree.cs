// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MRW.Shared.Build.UnityApp.Tasks
{
    /// <summary>
    /// This task gathers additional information about all of the PackageReferences that will be included
    /// from the ProjectAssetFile by finding the NuSpec and parsing it.
    /// </summary>
    public sealed class ResolvePackageReferenceTree : Task
    {
        /// <summary>
        /// The path to the ProjectAssetFile. If you're operating in the context of the app you wish to
        /// run the task for the value should be $(ProjectAssetFile). It is the generated project.asset.json
        /// file created during the NuGet restore of the project.
        /// </summary>
        [Required]
        public string ProjectAssetsFile { get; set; }

        [Output]
        public ITaskItem[] Packages { get; set; }

        private const char ASSET_FILE_LIBRARIES_SEPERATOR = '/';
        private const string ASSET_FILE_LIBRARIES = "libraries";
        private const string ASSET_FILE_LIBRARIES_PATH = "path";
        private const string ASSET_FILE_LIBRARIES_TYPE = "type";
        private const string ASSET_FILE_LIBRARIES_TYPE_PACKAGE = "package";
        private const string ASSET_FILE_PACKAGE_FOLDERS = "packageFolders";

        private const string NUSPEC_XML_NAMESPACE_ATTRIBUTE = "xmlns";
        private const string NUSPEC_XML_METADATA_ELEMENT = "metadata";
        private const string NUSPEC_XML_VERSION_ELEMENT = "version";
        private const string NUSPEC_XML_PROJECT_URL_ELEMENT = "projectUrl";
        private const string NUSPEC_XML_REPOSITORY_ELEMENT = "repository";
        private const string NUSPEC_XML_REPOSITORY_ATTRIBUTE_TYPE = "type";
        private const string NUSPEC_XML_REPOSITORY_ATTRIBUTE_URL = "url";
        private const string NUSPEC_XML_REPOSITORY_ATTRIBUTE_COMMIT = "commit";

        private const string NUSPEC_FILE_EXTENSION = ".nuspec";

        private const string REPOSITORY_TYPE_ADO = "TfsGit";

        private const string FILE_A_BUG_FORMAT = @"https://aka.ms/MRWSharedTechBug?%5BSystem.Title%5D=%5B{0}%5D+Description+of+issue";
        private const string REQUEST_A_FEATURE_FORMAT = @"https://aka.ms/MRWSharedTechFeature?%5BSystem.Title%5D=%5B{0}%5D+Description+of+request";

        public override bool Execute()
        {
            JObject assetFileJson = JObject.Parse(File.ReadAllText(ProjectAssetsFile));

            // Get both the libraries object (which is the resolved list of all dependencies) and the
            // packageFolders list which indicates where installed packages came from.
            IEnumerable<JProperty> libraries = ((JObject)assetFileJson[ASSET_FILE_LIBRARIES]).Properties();
            IEnumerable<JProperty> packageFolders = ((JObject)assetFileJson[ASSET_FILE_PACKAGE_FOLDERS]).Properties();

            List<TaskItem> packages = new List<TaskItem>();

            foreach (JProperty library in libraries)
            {
                if (!ASSET_FILE_LIBRARIES_TYPE_PACKAGE.Equals(library.Value.Value<string>(ASSET_FILE_LIBRARIES_TYPE)))
                {
                    // This library is not of the type "package". It's most likely a project reference. Skip processing it.
                    break;
                }

                // The library property in the asset file is in the format of <Package_Id>/<Version_Number>. Split
                // on the '/' character and take the first part to get the Package_Id
                string packageId = library.Name.Split(ASSET_FILE_LIBRARIES_SEPERATOR)[0];

                // Read the packagePath from the asset file. The path is the install location relative to the package folders
                string packagePath = library.Value.Value<string>(ASSET_FILE_LIBRARIES_PATH);

                string nuspecPath = null;

                // Search through each of the package folders specified to locate the installed packages nuspec
                foreach(string packageFolder in packageFolders.Select(pkgFolder => pkgFolder.Name))
                {
                    string potentialNuspecPath = Path.Combine(packageFolder, packagePath, $"{packageId.ToLower()}{NUSPEC_FILE_EXTENSION}");
                    if (File.Exists(potentialNuspecPath))
                    {
                        nuspecPath = potentialNuspecPath;
                        break;
                    }
                }

                if (nuspecPath == null)
                {
                    // If the nuspecPath is null it means we couldn't find a matching nuspec for the library, just move on.
                    Log.LogMessage($"Couldn't find a nuspec file for '{packageId}'");
                    break;
                }

                // Parse the contents of the NuSpec
                XElement nuspecContent = XElement.Load(nuspecPath);
                XNamespace nuspecNamespace = nuspecContent.Attribute(NUSPEC_XML_NAMESPACE_ATTRIBUTE)?.Value ?? String.Empty;
                XElement nuspecMetadata = nuspecContent.Element(nuspecNamespace + NUSPEC_XML_METADATA_ELEMENT);

                if (nuspecMetadata == null)
                {
                    Log.LogMessage($"Couldn't find a {NUSPEC_XML_METADATA_ELEMENT} element in the nuspec file {nuspecPath}");
                    break;
                }

                string version = nuspecMetadata.Element(nuspecNamespace + NUSPEC_XML_VERSION_ELEMENT)?.Value;
                string projectUrl = nuspecMetadata.Element(nuspecNamespace + NUSPEC_XML_PROJECT_URL_ELEMENT)?.Value;
                string installPath = Path.GetDirectoryName(nuspecPath) + Path.DirectorySeparatorChar;

                string repositoryUrl = null;
                string repositoryCommit = null;
                string repositoryCommitUrl = null;
                string repositoryReadMeUrl = null;

                XElement repositoryElement = nuspecMetadata.Element(nuspecNamespace + NUSPEC_XML_REPOSITORY_ELEMENT);

                if (repositoryElement != null)
                {
                    repositoryUrl = repositoryElement.Attribute(NUSPEC_XML_REPOSITORY_ATTRIBUTE_URL)?.Value;
                    repositoryCommit = repositoryElement.Attribute(NUSPEC_XML_REPOSITORY_ATTRIBUTE_COMMIT)?.Value;

                    // If the repositoryUrl and repositoryCommit are known then we can attempt to form some more helpful urls

                    if (!String.IsNullOrEmpty(repositoryUrl) && !String.IsNullOrEmpty(repositoryCommit))
                    {
                        string repositoryType = repositoryElement.Attribute(NUSPEC_XML_REPOSITORY_ATTRIBUTE_TYPE)?.Value;

                        // Check for an ADO repository type (it has it's own repository type so it's easy)
                        if (REPOSITORY_TYPE_ADO.Equals(repositoryType, StringComparison.OrdinalIgnoreCase))
                        {
                            repositoryCommitUrl = $"{repositoryUrl}?version=GC{repositoryCommit}";
                            repositoryReadMeUrl = $"{repositoryUrl}?path=README.md&version=GBmaster";
                        }

                        // Check if the repository url has github.com as the host
                        if ("github.com".Equals(new Uri(repositoryUrl).Host, StringComparison.OrdinalIgnoreCase))
                        {
                            repositoryCommitUrl = $"{repositoryUrl}/tree/{repositoryCommit}";
                            repositoryReadMeUrl = $"{repositoryUrl}/blob/master/README.md";
                        }

                    }
                }

                string fileABugURL = null;
                string requestAFeatureUrl = null;

                // For MRW packages add in the links to file a bug and request a feature
                if (packageId.StartsWith("MRW.", StringComparison.OrdinalIgnoreCase))
                {
                    fileABugURL = String.Format(FILE_A_BUG_FORMAT, packageId);
                    requestAFeatureUrl = String.Format(REQUEST_A_FEATURE_FORMAT, packageId);
                }

                // Create a TaskItem to return to the caller. Null values getting into the object are fine.
                TaskItem libraryItem = new TaskItem(packageId);
                libraryItem.SetMetadata("VersionNumber", version);
                libraryItem.SetMetadata("ProjectUrl", projectUrl);
                libraryItem.SetMetadata("NuSpecPath", nuspecPath);
                libraryItem.SetMetadata("InstallPath", installPath);
                libraryItem.SetMetadata("RepositoryUrl", repositoryUrl);
                libraryItem.SetMetadata("RepositoryCommit", repositoryCommit);
                libraryItem.SetMetadata("RepositoryCommitUrl", repositoryCommitUrl);
                libraryItem.SetMetadata("RepositoryReadMeUrl", repositoryReadMeUrl);
                libraryItem.SetMetadata("FileABugUrl", fileABugURL);
                libraryItem.SetMetadata("RequestAFeatureUrl", requestAFeatureUrl);

                packages.Add(libraryItem);
            }

            // Sort the packages alphabetically then convert to an array and assign to the output property
            Packages = packages.OrderBy(package => package.ItemSpec).ToArray();

            // If we made it this far without an exception then everything has passed
            return true;
        }
    }
}
