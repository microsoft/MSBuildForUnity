// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR


using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

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
            object value = xmlTemplateWriter.ReplacementSet.ReplacementEntries[token];
            if (value is string stringValue)
            {
                writer.WriteRaw(stringValue);
            }
            else if (value is IEnumerable<string> valueSet)
            {
                foreach (string item in valueSet)
                {
                    writer.WriteRaw(item);
                }
            }
            else if (value is DelimitedStringSet delimitedStringSet)
            {
                bool firstWritten = false;
                foreach (string item in delimitedStringSet.Items)
                {
                    if (firstWritten)
                    {
                        writer.WriteRaw(delimitedStringSet.Delimiter);
                    }

                    writer.WriteRaw(item);
                    firstWritten = true;
                }
            }
            else
            {
                throw new InvalidCastException($"Can't treat {value} as string or IEnumerable<string>");
            }
        }

        public void AssignValue(TemplateReplacementSet replacementSet, object value)
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
        private readonly Guid tokenGuid;

        private readonly string attributeValue;
        private readonly string tokenToReplace;

        public XmlAttributeTemplateToken(Guid tokenGuid, XName attributeName, string attributeValue, string tokenToReplace)
            : base(attributeName, string.Empty)
        {
            this.tokenGuid = tokenGuid;
            this.attributeValue = attributeValue;
            this.tokenToReplace = tokenToReplace;
        }

        public void AssignValue(TemplateReplacementSet replacementSet, object value)
        {
            string toUseForReplace;
            if (replacementSet.ReplacementEntries.TryGetValue(tokenGuid, out object obj))
            {
                toUseForReplace = (string)obj;
            }
            else
            {
                toUseForReplace = attributeValue;
            }

            if (value is string strValue)
            {
                replacementSet.ReplacementEntries[tokenGuid] = toUseForReplace.Replace(tokenToReplace, strValue);
            }
            else if (value is IEnumerable<string> valueSet)
            {
                replacementSet.ReplacementEntries[tokenGuid] = toUseForReplace.Replace(tokenToReplace, string.Join(string.Empty, valueSet));
            }
            else if (value is DelimitedStringSet delimitedStringSet)
            {
                replacementSet.ReplacementEntries[tokenGuid] = toUseForReplace.Replace(tokenToReplace, string.Join(delimitedStringSet.Delimiter, delimitedStringSet.Items));
            }
            else
            {
                throw new InvalidCastException($"Can't treat {value} as string or IEnumerable<string>");
            }
        }

        public void PrepareForReplacement(TemplateReplacementSet replacementSet)
        {
            SetValue(replacementSet.ReplacementEntries[tokenGuid]);
        }
    }
}
#endif