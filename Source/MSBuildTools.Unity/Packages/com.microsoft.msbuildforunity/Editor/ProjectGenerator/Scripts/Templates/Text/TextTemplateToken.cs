// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Text
{
    /// <summary>
    /// A simple text template token.
    /// </summary>
    internal class TextTemplateToken : ITemplateToken
    {
        private readonly Guid token = Guid.NewGuid();
        private readonly string tokenName;

        public TextTemplateToken(string tokenName)
        {
            this.tokenName = tokenName;
        }

        ///<inherit-doc/>
        public void AssignValue(TemplateReplacementSet replacementSet, object value)
        {
            replacementSet.ReplacementEntries[token] = value;
        }

        ///<inherit-doc/>
        public void PrepareForReplacement(TemplateReplacementSet replacementSet)
        {
            // DO nothing
        }

        internal object GetValue(TemplateReplacementSet replacementSet)
        {
            return replacementSet.ReplacementEntries[token];
        }

        public override string ToString()
        {
            return $"Text Token: {tokenName}";
        }
    }
}
#endif