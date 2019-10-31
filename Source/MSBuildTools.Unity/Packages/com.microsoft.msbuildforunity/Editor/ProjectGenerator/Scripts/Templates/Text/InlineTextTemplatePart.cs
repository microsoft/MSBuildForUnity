// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Text
{
    public class TextTemplatePart : ITemplatePart
    {
        private readonly Guid token = Guid.NewGuid();

        private readonly List<object> parts;

        public IReadOnlyDictionary<string, ITemplateToken> Tokens { get; }

        public IReadOnlyDictionary<string, ITemplatePart> Templates { get; }

        public TextTemplatePart(List<object> parts, IDictionary<string, ITemplateToken> tokens, IDictionary<string, ITemplatePart> templates)
        {
            this.parts = parts;
            Tokens = new ReadOnlyDictionary<string, ITemplateToken>(tokens);
            Templates = new ReadOnlyDictionary<string, ITemplatePart>(templates);
        }

        public TemplateReplacementSet CreateReplacementSet(TemplateReplacementSet parentReplacementSet = null)
        {
            return TemplateReplacementSet.Create(parentReplacementSet, token);
        }

        internal void Write(StreamWriter writer, TemplateReplacementSet replacementSet)
        {
            if (parts.Count > 0)
            {
                foreach (object part in parts)
                {
                    if (part is TextTemplatePart templatePart)
                    {
                        if (replacementSet.ReplacementEntries.TryGetValue(templatePart.token, out object value))
                        {
                            List<TemplateReplacementSet> replacementSets = (List<TemplateReplacementSet>)value;
                            foreach (TemplateReplacementSet set in replacementSets)
                            {
                                templatePart.Write(writer, set);
                            }
                        }
                    }
                    else if (part is TextTemplateToken templateToken)
                    {
                        writer.Write(templateToken.GetValue(replacementSet));
                    }
                    else
                    {
                        writer.Write(part);
                    }
                }

                writer.WriteLine();
            }
        }
    }
}
#endif