// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System.IO;
using System.Xml;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Xml
{
    public class XmlTemplateWriter : XmlTextWriter
    {
        public TemplateReplacementSet ReplacementSet { get; set; }

        public TextWriter Writer { get; }

        public XmlTemplateWriter(TextWriter w, TemplateReplacementSet replacementSet) : base(w)
        {
            Writer = w;
            ReplacementSet = replacementSet;
        }
    }
}
#endif