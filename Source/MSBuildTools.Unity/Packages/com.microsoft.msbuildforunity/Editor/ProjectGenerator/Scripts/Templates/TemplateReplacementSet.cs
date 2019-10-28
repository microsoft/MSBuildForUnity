// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    public class TemplateReplacementSet
    {
        public Dictionary<Guid, object> ReplacementEntries { get; } = new Dictionary<Guid, object>();
    }
}
#endif