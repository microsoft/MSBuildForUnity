// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Xml
{
    /// <summary>
    /// An Xml based implementation of a template part.
    /// </summary>
    internal class XMLTemplatePart : XProcessingInstruction, ITemplatePart
    {
        private readonly Guid token = Guid.NewGuid();

        private const string commentTokenSuffixKeyword = "_TOKEN";
        private const string commentTemplateStartSuffixKeyword = "_TEMPLATE_START";
        private const string commentTempalteEndSuffixKeyword = "_TEMPLATE_END";

        private readonly Dictionary<string, ITemplateToken> tokens = new Dictionary<string, ITemplateToken>();
        private readonly Dictionary<string, ITemplatePart> templates = new Dictionary<string, ITemplatePart>();

        private readonly XElement element;

        public IReadOnlyDictionary<string, ITemplateToken> Tokens { get; }

        public IReadOnlyDictionary<string, ITemplatePart> Templates { get; }


        internal XMLTemplatePart(XElement element)
            : base("templatepart", string.Empty)
        {
            Tokens = new ReadOnlyDictionary<string, ITemplateToken>(tokens);
            Templates = new ReadOnlyDictionary<string, ITemplatePart>(templates);
            this.element = new XElement(element);
        }

        public void Parse()
        {
            ParseElement(element);
        }

        public TemplateReplacementSet CreateReplacementSet(TemplateReplacementSet parentReplacementSet = null)
        {
            return TemplateReplacementSet.Create(parentReplacementSet, token);
        }

        public override void WriteTo(XmlWriter writer)
        {
            XmlTemplateWriter parentWriter = ((XmlTemplateWriter)writer);
            TemplateReplacementSet parentReplacementSet = parentWriter.ReplacementSet;
            // Don't dispose
            if (parentReplacementSet.ReplacementEntries.TryGetValue(token, out object value))
            {
                List<TemplateReplacementSet> replacementSets = (List<TemplateReplacementSet>)value;
                foreach (TemplateReplacementSet replacementSet in replacementSets)
                {
                    parentWriter.ReplacementSet = replacementSet;
                    Write(parentWriter);
                }
            }
            parentWriter.ReplacementSet = parentReplacementSet;
        }

        public void Write(XmlTemplateWriter xmlTemplateWriter)
        {
            foreach (KeyValuePair<string, ITemplateToken> pair in Tokens)
            {
                pair.Value.PrepareForReplacement(xmlTemplateWriter.ReplacementSet);
            }
            element.WriteTo(xmlTemplateWriter);
        }

        private bool TryParseAttributeValueForTokens(XName attributeName, string value, out XmlAttributeTemplateToken attributeToken)
        {
            attributeToken = null;

            int nextTokenSeperator = value.IndexOf("##");
            if (nextTokenSeperator == -1)
            {
                return false;

            }

            Guid tokenGuid = Guid.NewGuid();
            int previousTokenSeperator = 0;
            while (nextTokenSeperator != -1)
            {
                // We have potential token, if we don't return false
                previousTokenSeperator = nextTokenSeperator + 2;
                if (previousTokenSeperator >= value.Length)
                {
                    return false;
                }

                nextTokenSeperator = value.IndexOf("##", previousTokenSeperator);
                if (nextTokenSeperator == -1)
                {
                    return false;
                }

                string potentialToken = value.Substring(previousTokenSeperator, nextTokenSeperator - previousTokenSeperator);
                if (potentialToken.EndsWith(commentTokenSuffixKeyword))
                {
                    string token = potentialToken.Substring(0, potentialToken.Length - commentTokenSuffixKeyword.Length);
                    // We only really need to return 1, even if multiple are created, this will all properly be handled
                    attributeToken = new XmlAttributeTemplateToken(tokenGuid, attributeName, value, $"##{potentialToken}##");
                    AddReplacementToken(token, attributeToken);
                }

                previousTokenSeperator = nextTokenSeperator + 2;
                nextTokenSeperator = previousTokenSeperator >= value.Length ? -1 : value.IndexOf("##", previousTokenSeperator);
            }
            if (attributeToken != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ParseElement(XElement element)
        {
            // Scan for comments
            // For each token comment, add it to token collection
            // For each start of template, next node is the template
            // - There should only be one between start and end of a template
            // Then also check the attributes for token replacements

            bool atleastOneReplacement = false;
            List<XAttribute> attributes = new List<XAttribute>();
            foreach (XAttribute attribute in element.Attributes())
            {
                if (TryParseAttributeValueForTokens(attribute.Name, attribute.Value, out XmlAttributeTemplateToken toReplace))
                {
                    attributes.Add(toReplace);

                    atleastOneReplacement = true;
                }
                else
                {
                    attributes.Add(attribute);
                }
            }

            if (atleastOneReplacement)
            {
                element.ReplaceAttributes(attributes);
            }

            List<Action> replacementActions = new List<Action>();
            string templateStartKey = null;
            bool seenTemplateBody = false;
            foreach (XNode node in element.Nodes())
            {
                if (node is XComment comment)
                {
                    switch (GetTokenType(comment, out string tokenKey))
                    {
                        case TokenType.ReplacementToken:
                            XmlCommentTemplateToken xmlCommentTemplateToken = new XmlCommentTemplateToken(comment.Value);
                            AddReplacementToken(tokenKey, xmlCommentTemplateToken);
                            replacementActions.Add(() => comment.ReplaceWith(xmlCommentTemplateToken));
                            break;
                        case TokenType.TemplateStart:
                            templateStartKey = tokenKey;
                            seenTemplateBody = false;
                            break;
                        case TokenType.TemplateEnd:
                            if (templateStartKey == null)
                            {
                                throw new InvalidOperationException($"We have template end '{tokenKey}' without equivalent start.");
                            }
                            else if (!Equals(templateStartKey, tokenKey))
                            {
                                throw new InvalidOperationException($"We have template end '{tokenKey}' but the template start was different '{templateStartKey}'.");
                            }
                            else if (!seenTemplateBody)
                            {
                                throw new InvalidOperationException($"We have template '{tokenKey}' without a body.");
                            }
                            else
                            {
                                templateStartKey = null;
                                seenTemplateBody = false;
                            }
                            break;
                    }
                }
                else if (node is XElement childElement)
                {
                    if (templateStartKey != null)
                    {
                        if (seenTemplateBody)
                        {
                            throw new InvalidOperationException($"Template '{templateStartKey}' can only have one body node.");
                        }
                        else
                        {
                            XMLTemplatePart templatePart = new XMLTemplatePart(childElement);
                            templatePart.Parse();
                            templates.Add(templateStartKey, templatePart);
                            replacementActions.Add(() => childElement.ReplaceWith(templatePart));
                            seenTemplateBody = true;
                        }
                    }
                    else
                    {
                        ParseElement(childElement);
                    }
                }
            }

            replacementActions.ForEach(t => t());
        }

        private void AddReplacementToken(string tokenKey, ITemplateToken token)
        {
            if (tokens.TryGetValue(tokenKey, out ITemplateToken existingToken))
            {
                if (!(existingToken is MultipleTemplateToken multipleTemplateToken))
                {
                    multipleTemplateToken = new MultipleTemplateToken();
                    multipleTemplateToken.Tokens.Add(existingToken);

                    tokens[tokenKey] = multipleTemplateToken;
                }

                multipleTemplateToken.Tokens.Add(token);
            }
            else
            {
                tokens[tokenKey] = token;
            }
        }

        private TokenType GetTokenType(XComment comment, out string tokenKey)
        {
            string contents = comment.Value.Trim();
            if (contents.EndsWith(commentTokenSuffixKeyword))
            {
                tokenKey = contents.Substring(0, contents.Length - commentTokenSuffixKeyword.Length);
                return TokenType.ReplacementToken;
            }

            if (contents.EndsWith(commentTemplateStartSuffixKeyword))
            {
                tokenKey = contents.Substring(0, contents.Length - commentTemplateStartSuffixKeyword.Length);
                return TokenType.TemplateStart;
            }

            if (contents.EndsWith(commentTempalteEndSuffixKeyword))
            {
                tokenKey = contents.Substring(0, contents.Length - commentTempalteEndSuffixKeyword.Length);
                return TokenType.TemplateEnd;
            }

            tokenKey = null;
            return TokenType.None;
        }

        private enum TokenType
        {
            None,
            ReplacementToken,
            TemplateStart,
            TemplateEnd
        }
    }
}
#endif