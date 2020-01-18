// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    /// <summary>
    /// This represents a template that has it's own sub-templates and replaceable tokens.
    /// </summary>
    public interface ITemplatePart
    {
        /// <summary>
        /// A dictionary of tokens that can be replaced as part of this template.
        /// </summary>
        IReadOnlyDictionary<string, ITemplateToken> Tokens { get; }

        /// <summary>
        /// A dictionary of sub-templates that can be instanced as part of this template.
        /// </summary>
        IReadOnlyDictionary<string, ITemplatePart> Templates { get; }

        /// <summary>
        /// Creates a new replacement set for holding the replacement values, possibly given a parent replacement set (if this is a sub-template).
        /// </summary>
        /// <param name="parentReplacementSet">Parent replacement set that may govern this one.</param>
        /// <returns>A new replacement set to use.</returns>
        TemplateReplacementSet CreateReplacementSet(TemplateReplacementSet parentReplacementSet = default);
    }

    /// <summary>
    /// Just a helpful extension to the ITemplatePart.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Attempts to replace the token if it's available and returns true, otherwise returns false.
        /// </summary>
        /// <param name="templatePart">The template on which to replace the token.</param>
        /// <param name="tokenName">The name of the token.</param>
        /// <param name="replacementSet">The replacement set where to encode the value.</param>
        /// <param name="value">The value to encode.</param>
        /// <returns>True if was able to locate the token.</returns>
        public static bool TryReplaceToken(this ITemplatePart templatePart, string tokenName, TemplateReplacementSet replacementSet, object value)
        {
            if (templatePart.Tokens.TryGetValue(tokenName, out ITemplateToken templateToken))
            {
                templateToken.AssignValue(replacementSet, value);
                return true;
            }

            return false;
        }
    }
}
#endif
