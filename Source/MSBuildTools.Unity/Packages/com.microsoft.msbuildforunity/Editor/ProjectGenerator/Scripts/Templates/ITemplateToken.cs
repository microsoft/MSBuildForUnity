// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    /// <summary>
    /// This represents a parsed token of a template that can be replaced with a value.
    /// </summary>
    public interface ITemplateToken
    {
        /// <summary>
        /// Encodes the value in the replacement set for the current token.
        /// </summary>
        /// <param name="replacementSet"><see cref="TemplateReplacementSet"/> where the value should be stored.</param>
        /// <param name="value">The value to store.</param>
        void AssignValue(TemplateReplacementSet replacementSet, object value);

        /// <summary>
        /// A helper to prepare for replacement in case some prep is needed for the token.
        /// </summary>
        /// <param name="replacementSet"></param>
        void PrepareForReplacement(TemplateReplacementSet replacementSet);
    }

    /// <summary>
    /// This helper class allows the template to contain multiple instances of the same token to be replaced.
    /// </summary>
    internal class MultipleTemplateToken : ITemplateToken
    {
        public List<ITemplateToken> Tokens { get; } = new List<ITemplateToken>();

        public void AssignValue(TemplateReplacementSet replacementSet, object value)
        {
            Tokens.ForEach(t => t.AssignValue(replacementSet, value));
        }

        public void PrepareForReplacement(TemplateReplacementSet replacementSet)
        {
            Tokens.ForEach(t => t.PrepareForReplacement(replacementSet));
        }
    }
}
#endif