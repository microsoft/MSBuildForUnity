// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    public interface ITemplatePart
    {
        IReadOnlyDictionary<string, ITemplateToken> Tokens { get; }

        IReadOnlyDictionary<string, ITemplatePart> Templates { get; }

        TemplateReplacementSet CreateReplacementSet(TemplateReplacementSet parentReplacementSet = null);
    }
}
#endif