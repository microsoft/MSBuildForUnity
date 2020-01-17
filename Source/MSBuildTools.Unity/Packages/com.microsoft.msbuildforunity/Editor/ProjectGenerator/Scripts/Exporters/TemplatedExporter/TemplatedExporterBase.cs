// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    /// <summary>
    /// Base class for file based exporters.
    /// </summary>
    internal class TemplatedExporterBase : TemplatedExporterPart
    {
        private readonly FileTemplate templateFile;
        private readonly string exportPath;

        protected TemplatedExporterBase(FileTemplate templateFile, string exportPath)
            : base(templateFile.Root, templateFile.Root.CreateReplacementSet())
        {
            this.templateFile = templateFile;
            this.exportPath = exportPath;
        }

        /// <summary>
        /// Writes out the template data.
        /// </summary>
        public void Write()
        {
            templateFile.Write(exportPath, replacementSet);
        }
    }
}
#endif