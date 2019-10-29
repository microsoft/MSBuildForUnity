// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR


using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    public interface ITemplateToken
    {
        void AssignValue(TemplateReplacementSet replacementSet, object value);

        void PrepareForReplacement(TemplateReplacementSet replacementSet);
    }

    public class MultipleTemplateToken : ITemplateToken
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