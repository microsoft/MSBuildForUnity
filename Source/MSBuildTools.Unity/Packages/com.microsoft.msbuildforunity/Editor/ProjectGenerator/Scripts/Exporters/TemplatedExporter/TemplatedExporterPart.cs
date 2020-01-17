// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    /// <summary>
    /// Base class for template based exporters.
    /// </summary>
    internal class TemplatedExporterPart
    {
        private readonly ITemplatePart template;
        protected readonly TemplateReplacementSet replacementSet;

        protected TemplatedExporterPart(ITemplatePart template, TemplateReplacementSet replacementSet)
        {
            this.template = template;
            this.replacementSet = replacementSet;
        }

        /// <summary>
        /// Updates the value of the field and the token (if the field has changed).
        /// </summary>
        /// <param name="field">The ref field to update.</param>
        /// <param name="value">The value to update to.</param>
        /// <param name="token">The token key.</param>
        protected void UpdateToken(ref string field, string value, string token)
        {
            UpdateToken(ref field, value, token, t => t);
        }

        /// <summary>
        /// Updates the value of the field and the token (if the field has changed).
        /// </summary>
        /// <typeparam name="T">The generic type of the field and value.</typeparam>
        /// <param name="field">The ref field to update.</param>
        /// <param name="value">The value to update to.</param>
        /// <param name="token">The token key.</param>
        /// <param name="toStringFunc">The conversion to string value function.</param>
        protected void UpdateToken<T>(ref T field, T value, string token, Func<T, string> toStringFunc)
        {
            if (!Equals(field, value))
            {
                field = value;
                template.Tokens[token].AssignValue(replacementSet, toStringFunc(value));
            }
        }
    }
}
#endif