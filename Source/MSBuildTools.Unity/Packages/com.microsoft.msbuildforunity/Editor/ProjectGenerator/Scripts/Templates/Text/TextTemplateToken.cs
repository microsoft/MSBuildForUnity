// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Text
{
    public class TextTemplateToken : ITemplateToken
    {
        private readonly Guid token = Guid.NewGuid();

        public void AssignValue(TemplateReplacementSet replacementSet, object value)
        {
            replacementSet.ReplacementEntries[token] = value;
        }

        public void PrepareForReplacement(TemplateReplacementSet replacementSet)
        {
            // DO nothing
        }

        public object GetValue(TemplateReplacementSet replacementSet)
        {
            return replacementSet.ReplacementEntries[token];
        }
    }
}
#endif