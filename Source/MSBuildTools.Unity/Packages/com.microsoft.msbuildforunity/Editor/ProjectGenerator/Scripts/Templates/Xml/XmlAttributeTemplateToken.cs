// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Xml
{
    /// <summary>
    /// Represents a token that is part of a Xml attribute.
    /// </summary>
    internal class XmlAttributeTemplateToken : XAttribute, ITemplateToken
    {
        private readonly Guid tokenGuid;

        private readonly string attributeValue;
        private readonly string tokenToReplace;

        internal XmlAttributeTemplateToken(Guid tokenGuid, XName attributeName, string attributeValue, string tokenToReplace)
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