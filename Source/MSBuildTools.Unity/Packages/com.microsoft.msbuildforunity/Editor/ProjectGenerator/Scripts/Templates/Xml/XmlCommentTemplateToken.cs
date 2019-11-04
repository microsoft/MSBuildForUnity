// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Xml
{
    /// <summary>
    /// This token is encoded as a comment that should be replaced.
    /// </summary>
    internal class XmlCommentTemplateToken : XProcessingInstruction, ITemplateToken
    {
        private readonly Guid token = Guid.NewGuid();

        internal XmlCommentTemplateToken(string commentValue)
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
            // Do nothing
        }
    }
}
#endif
