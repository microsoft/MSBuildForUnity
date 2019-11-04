// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Xml;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Xml
{
    /// <summary>
    /// Helper XmlWriter class for the template.
    /// </summary>
    internal class XmlTemplateWriter : XmlTextWriter
    {
        private readonly XmlWriterSettings settings = new XmlWriterSettings()
        {
            NewLineChars = Environment.NewLine,
            Indent = true,
            IndentChars = " "
        };

        public TemplateReplacementSet ReplacementSet { get; set; }

        public TextWriter Writer { get; }

        public override XmlWriterSettings Settings => settings;

        internal XmlTemplateWriter(TextWriter w, TemplateReplacementSet replacementSet) : base(w)
        {
            Writer = w;
            ReplacementSet = replacementSet;

            Formatting = Formatting.Indented;
            Indentation = 4;
            Namespaces = false;
        }

        public override void WriteComment(string text)
        {
            base.WriteComment(text);
        }
    }
}
#endif