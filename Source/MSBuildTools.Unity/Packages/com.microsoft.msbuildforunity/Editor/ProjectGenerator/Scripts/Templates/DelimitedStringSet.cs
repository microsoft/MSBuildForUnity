// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    public struct DelimitedStringSet
    {
        public string Delimiter { get; }

        public IEnumerable<string> Items { get; }

        public DelimitedStringSet(string delimiter, IEnumerable<string> items)
        {
            Delimiter = delimiter;
            Items = items;
        }
    }
}
#endif