// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    /// <summary>
    /// This class is used to hold the values for token and template replacements that will be used when the template is written out.
    /// This way the template structure can be reused multiple times.
    /// </summary>
    public class TemplateReplacementSet
    {
        /// <summary>
        /// Internal helper to create a replacement set based on the parent replacement set and a given entry identifier.
        /// </summary>
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

        /// <summary>
        /// A dictionary of encoded values as replacement entries.
        /// </summary>
        public Dictionary<Guid, object> ReplacementEntries { get; } = new Dictionary<Guid, object>();

        private TemplateReplacementSet() { }
    }
}
#endif
