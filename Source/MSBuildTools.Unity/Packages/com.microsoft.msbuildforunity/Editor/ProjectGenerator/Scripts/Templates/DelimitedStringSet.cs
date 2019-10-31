// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    /// <summary>
    /// This class is a small optimization to avoid string Join operation when intending to store a set of values seperated by a delimiter.
    /// </summary>
    public struct DelimitedStringSet
    {
        /// <summary>
        /// Delimiter to be applied in-between the values.
        /// </summary>
        public string Delimiter { get; }

        /// <summary>
        /// The list of replacement values.
        /// </summary>
        public IEnumerable<string> Items { get; }

        /// <summary>
        /// Creates a new instance of the set by providing the delimiter and items.
        /// </summary>
        public DelimitedStringSet(string delimiter, IEnumerable<string> items)
        {
            Delimiter = delimiter;
            Items = items;
        }
    }
}
#endif