// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    /// <summary>
    /// A writer helper for templates and tokens.
    /// </summary>
    internal ref struct TemplatedWriter
    {
        private readonly ITemplatePart template;
        private readonly TemplateReplacementSet replacementSet;

        /// <summary>
        /// Creates a new instance of the writer.
        /// </summary>
        internal TemplatedWriter(ITemplatePart template, TemplateReplacementSet replacementSet)
        {
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
        internal void Write(string token, IEnumerable<string> items, string seperator = ";", bool optional = false)
        {
            Write(token, new DelimitedStringSet(seperator, items), optional);
        }

        /// <summary>
        /// Updates the value of the field and the token (if the field has changed).
        /// </summary>
        /// <param name="token">The token key.</param>
        /// <param name="value">The value to update to.</param>
        /// <param name="optional">Whether this is an optional setting.</param>
        internal void Write(string token, string value, bool optional = false)
        {
            Write(token, (object)value, optional);
        }

        /// <summary>
        /// Creates a writer for a sub-template.
        /// </summary>
        /// <param name="subTemplateName">The name of the sub-template.</param>
        /// <returns></returns>
        internal TemplatedWriter CreateWriterFor(string subTemplateName)
        {
            ITemplatePart subTemplate = template.Templates[subTemplateName];
            return new TemplatedWriter(subTemplate, subTemplate.CreateReplacementSet(replacementSet));
        }

        private void Write(string token, object value, bool optional)
        {
            if (optional)
            {
                template.TryReplaceToken(token, replacementSet, value);
            }
            else
            {
                template.Tokens[token].AssignValue(replacementSet, value);
            }
        }

    }
}
#endif