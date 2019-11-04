// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Xml
{
    /// <summary>
    /// Represents a template file that is Xml based.
    /// </summary>
    internal class XmlFileTemplate : FileTemplate
    {
        private XDocument document;
        private XMLTemplatePart rootPart;

        public XmlFileTemplate(FileInfo templateFile) : base(templateFile)
        {
        }

        protected override void Parse()
        {
            document = XDocument.Load(templateFile.FullName, LoadOptions.PreserveWhitespace);

            rootPart = new XMLTemplatePart(document.Root);
            rootPart.Parse();
            Root = rootPart;
        }

        public override void Write(string path, TemplateReplacementSet replacementSet)
        {
            using (StreamWriter writer = new StreamWriter(path))
            using (XmlTemplateWriter xmlWriter = new XmlTemplateWriter(writer, replacementSet))
            {
                rootPart.Write(xmlWriter);
            }
        }
    }
}
#endif
