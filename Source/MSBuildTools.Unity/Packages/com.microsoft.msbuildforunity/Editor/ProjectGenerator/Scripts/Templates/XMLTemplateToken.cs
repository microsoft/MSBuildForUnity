// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR


using System;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    public class XmlCommentTemplateToken : XProcessingInstruction, ITemplateToken
    {
        private readonly Guid token = Guid.NewGuid();

        public XmlCommentTemplateToken(string commentValue)
            : base("somename", commentValue)
        {
        }

        public override void WriteTo(XmlWriter writer)
        {
            XmlTemplateWriter xmlTemplateWriter = (XmlTemplateWriter)writer;
            writer.WriteRaw((string)xmlTemplateWriter.ReplacementSet.ReplacementEntries[token]);
        }

        public void AssignValue(TemplateReplacementSet replacementSet, string value)
        {
            replacementSet.ReplacementEntries[token] = value;
        }

        public void PrepareForReplacement(TemplateReplacementSet replacementSet)
        {
            // DO nothing
        }
    }

    public class XmlAttributeTemplateToken : XAttribute, ITemplateToken
    {
        private readonly Guid token = Guid.NewGuid();

        private readonly XName attributeName;
        private readonly string prefix;
        private readonly string suffix;

        public XmlAttributeTemplateToken(XName attributeName, string prefix, string suffix)
            : base(attributeName, string.Empty)
        {
            this.attributeName = attributeName;
            this.prefix = prefix;
            this.suffix = suffix;
        }

        public void AssignValue(TemplateReplacementSet replacementSet, string value)
        {
            replacementSet.ReplacementEntries[token] = $"{prefix}{value}{suffix}";
        }

        public void PrepareForReplacement(TemplateReplacementSet replacementSet)
        {
            SetValue(replacementSet.ReplacementEntries[token]);
        }
    }
}
#endif