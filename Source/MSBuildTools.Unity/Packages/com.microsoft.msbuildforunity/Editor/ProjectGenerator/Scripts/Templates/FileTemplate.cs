// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates.Text;
using Microsoft.Build.Unity.ProjectGeneration.Templates.Xml;
using System;
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    /// <summary>
    /// This is the base class for the types of template files used by the <see cref="Exporters.TemplatedProjectExporter"/>.
    /// </summary>
    public abstract class FileTemplate
    {
        private const string TemplateExtension = ".template";

        /// <summary>
        /// Attempts to parse the templtae file.
        /// </summary>
        /// <param name="path">The path to the template file.</param>
        /// <param name="fileTemplate">The instance of the parsed template file, null if failed.</param>
        /// <returns>True if was able to parse succesfully.</returns>
        public static bool TryParseTemplate(FileInfo path, out FileTemplate fileTemplate)
        {
            if (!(path?.Exists ?? throw new ArgumentNullException(nameof(path))))
            {
                throw new FileNotFoundException("Can't parse template because the file is missing.", path.FullName);
            }

            if (path.Extension != TemplateExtension)
            {
                throw new InvalidDataException($"The given file '{path.FullName}' is not a {TemplateExtension} file.");
            }

            int indexOfTemplateExtensionPeriod = path.FullName.Length - TemplateExtension.Length;
            int indexOfPreviousPeriod = path.FullName.LastIndexOf('.', indexOfTemplateExtensionPeriod - 1);
            string templateExtension = path.FullName.Substring(indexOfPreviousPeriod, indexOfTemplateExtensionPeriod - indexOfPreviousPeriod);

            switch (templateExtension)
            {
                case ".csproj":
                case ".props":
                case ".targets":
                    fileTemplate = new XmlFileTemplate(path);
                    break;
                case ".sln":
                case ".meta":
                    fileTemplate = new TextFileTemplate(path);
                    break;
                default:
                    fileTemplate = null;
                    return false;
            }

            fileTemplate.Parse();
            return true;
        }

        protected readonly FileInfo templateFile;

        public ITemplatePart Root { get; protected set; }

        protected FileTemplate(FileInfo templateFile)
        {
            this.templateFile = templateFile;
        }

        protected abstract void Parse();

        public abstract void Write(string path, TemplateReplacementSet replacementSet);
    }
}
#endif