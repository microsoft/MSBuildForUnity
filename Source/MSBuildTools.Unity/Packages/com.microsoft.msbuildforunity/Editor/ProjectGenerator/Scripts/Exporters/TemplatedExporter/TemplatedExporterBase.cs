// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    /// <summary>
    /// Base class for file based exporters.
    /// </summary>
    internal abstract class TemplatedExporterBase
    {
        private readonly FileTemplate templateFile;
        private readonly FileInfo exportPath;

        protected TemplatedExporterBase(FileTemplate templateFile, FileInfo exportPath)
        {
            this.templateFile = templateFile;
            this.exportPath = exportPath;
        }

        /// <summary>
        /// Writes out the template data.
        /// </summary>
        public void Write()
        {
            // Ensure the parent directories are created
            Directory.CreateDirectory(exportPath.Directory.FullName);

            TemplateReplacementSet replacementSet = templateFile.Root.CreateReplacementSet();

            Export(new TemplatedWriter(templateFile.Root, replacementSet));

            templateFile.Write(exportPath.FullName, replacementSet);
        }

        /// <summary>
        /// Override this method in a derived class to perform the export.
        /// </summary>
        /// <param name="writer">The writer to use to export.</param>
        protected abstract void Export(TemplatedWriter writer);
    }
}
#endif