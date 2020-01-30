// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    /// <summary>
    /// A helpe set of extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Updates the value of the field and the token (if the field has changed).
        /// </summary>
        /// <param name="writer">The "this" writer.</param>
        /// <param name="token">The token key.</param>
        /// <param name="value">The Guid to update to.</param>
        /// <param name="optional">Whether this is an optional setting.</param>
        /// <returns>The same writer to allow chaining of writes.</returns>
        internal static TemplatedWriter Write(this TemplatedWriter writer, string token, Guid guid, bool optional = false)
        {
            return writer.Write(token, guid.ToString().ToUpper(), optional: optional);
        }
    }
}
#endif
