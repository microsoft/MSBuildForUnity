// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    /// <summary>
    /// A writer helper for templates and tokens.
    /// </summary>
    internal ref struct TemplatedWriter
    {
        private readonly FileTemplate fileTemplate;
        private readonly ITemplatePart template;
        private readonly TemplateReplacementSet replacementSet;

        /// <summary>
        /// Creates a new instance of the writer.
        /// </summary>
        internal TemplatedWriter(FileTemplate fileTemplate)
            : this(fileTemplate, fileTemplate.Root, fileTemplate.Root.CreateReplacementSet())
        {
        }

        private TemplatedWriter(FileTemplate fileTemplate, ITemplatePart template, TemplateReplacementSet replacementSet)
        {
            this.fileTemplate = fileTemplate;
            this.template = template;
            this.replacementSet = replacementSet;
        }

        /// <summary>
        /// Writes the a set of items for the token using a seperator. Use this to avoid the expensive string concat at this stage.
        /// </summary>
        /// <param name="token">The token key.</param>
        /// <param name="items">Items to write.</param>
        /// <param name="seperator">The seperator, defaulted to ';'.</param>
        /// <param name="optional">Whether this is an optional setting.</param>
        /// <returns>The same writer to allow chaining of writes.</returns>
        internal TemplatedWriter Write(string token, IEnumerable<string> items, string seperator = ";", bool optional = false)
        {
            return Write(token, new DelimitedStringSet(seperator, items), optional);
        }

        /// <summary>
        /// Updates the value of the field and the token (if the field has changed).
        /// </summary>
        /// <param name="token">The token key.</param>
        /// <param name="value">The value to update to.</param>
        /// <param name="optional">Whether this is an optional setting.</param>
        /// <returns>The same writer to allow chaining of writes.</returns>
        internal TemplatedWriter Write(string token, string value, bool optional = false)
        {
            return Write(token, (object)value, optional);
        }

        internal void Export(FileInfo exportPath)
        {
            if (fileTemplate == null)
            {
                throw new InvalidOperationException("Export must be called on the root templated writer.");
            }

            // Ensure the parent directories are created
            Directory.CreateDirectory(exportPath.Directory.FullName);

            fileTemplate.Write(exportPath.FullName, replacementSet);
        }

        /// <summary>
        /// Creates a writer for a sub-template.
        /// </summary>
        /// <param name="subTemplateName">The name of the sub-template.</param>
        /// <returns></returns>
        internal TemplatedWriter CreateWriterFor(string subTemplateName)
        {
            ITemplatePart subTemplate = template.Templates[subTemplateName];
            return new TemplatedWriter(null, subTemplate, subTemplate.CreateReplacementSet(replacementSet));
        }

        private TemplatedWriter Write(string token, object value, bool optional)
        {
            if (optional)
            {
                template.TryReplaceToken(token, replacementSet, value);
            }
            else
            {
                template.Tokens[token].AssignValue(replacementSet, value);
            }

            return this;
        }

    }
}
#endif