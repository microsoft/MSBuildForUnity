// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    public abstract partial class FileTemplate
    {
        private const string TemplateExtension = ".template";

        public static bool TryParseTemplate(FileInfo path, out FileTemplate fileTemplate)
        {
            if (!(path?.Exists ?? throw new ArgumentNullException(nameof(path))))
            {
                throw new FileNotFoundException("Can't parse template because the file is missing.", path.FullName);
            }

            if (path.Extension != TemplateExtension)
            {
                throw new InvalidDataException($"The given file '{path.FullName}' is not a {TemplateExtension} file.");
            }

            int indexOfTemplateExtensionPeriod = path.FullName.Length - TemplateExtension.Length;
            int indexOfPreviousPeriod = path.FullName.LastIndexOf('.', indexOfTemplateExtensionPeriod - 1);
            string templateExtension = path.FullName.Substring(indexOfPreviousPeriod, indexOfTemplateExtensionPeriod - indexOfPreviousPeriod);

            switch (templateExtension)
            {
                case ".csproj":
                case ".props":
                case ".targets":
                    fileTemplate = new XmlFileTemplate(path);
                    break;
                case ".sln":
                    fileTemplate = new TextFileTemplate(path);
                    break;
                default:
                    fileTemplate = null;
                    return false;
            }

            fileTemplate.Parse();
            return true;
        }

        private readonly FileInfo templateFile;

        public ITemplatePart Root { get; protected set; }

        protected FileTemplate(FileInfo templateFile)
        {
            this.templateFile = templateFile;
        }

        protected abstract void Parse();

        public abstract void Write(string path, TemplateReplacementSet replacementSet);

        private partial class XmlFileTemplate : FileTemplate
        {

            private XDocument document;
            private XMLTemplatePart rootPart;

            public XmlFileTemplate(FileInfo templateFile) : base(templateFile)
            {
            }

            protected override void Parse()
            {
                document = XDocument.Load(templateFile.FullName);

                rootPart = new XMLTemplatePart(document.Root);
                rootPart.Parse();
                Root = rootPart;
            }

            public override void Write(string path, TemplateReplacementSet replacementSet)
            {
                using (StreamWriter writer = new StreamWriter(path))
                using (XmlTemplateWriter xmlWriter = new XmlTemplateWriter(writer, replacementSet) { Formatting = Formatting.Indented, IndentChar = ' ', Indentation = 4, Namespaces = false })
                {
                    rootPart.Write(xmlWriter);
                }
            }
        }

        private class TextFileTemplate : FileTemplate
        {
            public TextFileTemplate(FileInfo templateFile) : base(templateFile)
            {
            }

            protected override void Parse()
            {
                throw new NotImplementedException();
            }

            public override void Write(string path, TemplateReplacementSet replacementSet)
            {
                throw new NotImplementedException();
            }
        }
    }
}
#endif