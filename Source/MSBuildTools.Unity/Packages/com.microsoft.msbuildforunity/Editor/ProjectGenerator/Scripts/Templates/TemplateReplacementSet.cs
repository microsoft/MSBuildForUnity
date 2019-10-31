// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    public class TemplateReplacementSet
    {
        internal static TemplateReplacementSet Create(TemplateReplacementSet parentReplacementSet, Guid entryIdentifier)
        {
            if (parentReplacementSet == null)
            {
                return new TemplateReplacementSet();
            }

            TemplateReplacementSet toReturn = new TemplateReplacementSet();
            List<TemplateReplacementSet> templateInstances;
            if (parentReplacementSet.ReplacementEntries.TryGetValue(entryIdentifier, out object list))
            {
                templateInstances = (List<TemplateReplacementSet>)list;
            }
            else
            {
                templateInstances = new List<TemplateReplacementSet>();
                parentReplacementSet.ReplacementEntries[entryIdentifier] = templateInstances;
            }
            templateInstances.Add(toReturn);
            return toReturn;
        }

        public Dictionary<Guid, object> ReplacementEntries { get; } = new Dictionary<Guid, object>();
    }
}
#endif