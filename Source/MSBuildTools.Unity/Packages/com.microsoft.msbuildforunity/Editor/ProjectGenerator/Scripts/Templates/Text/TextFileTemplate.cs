// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates.Text
{
    /// <summary>
    /// This is the implementation of the simple text based file template.
    /// </summary>
    internal class TextFileTemplate : FileTemplate
    {
        private const string TemplateSuffix = "_TEMPLATE";
        private const string TemplateStartSuffix = "_TEMPLATE_START";
        private const string TokenSuffix = "_TOKEN";

        internal TextFileTemplate(FileInfo templateFile)
            : base(templateFile)
        {
        }

        protected override void Parse()
        {
            using (StreamReader reader = new StreamReader(templateFile.FullName))
            {
                Root = ParseMultilineTemplate(reader, null);
            }
        }

        private void ParseLineForTokens(string line, List<object> parts, Dictionary<string, ITemplateToken> tokenMap)
        {
            int startOfPreviousPart = 0;
            int nextIndexOfOpenBracket = 0;
            while ((nextIndexOfOpenBracket = line.IndexOf('<', nextIndexOfOpenBracket)) > -1)
            {
                int i;
                char lastC = '\0';
                for (i = nextIndexOfOpenBracket + 1; i < line.Length; i++)
                {
                    lastC = line[i];
                    if (lastC != '_' && !char.IsLetterOrDigit(lastC))
                    {
                        break;
                    }
                }

                if (lastC == '>')
                {
                    string potentialToken = line.Substring(nextIndexOfOpenBracket + 1, i - nextIndexOfOpenBracket - 1);
                    if (potentialToken.EndsWith(TokenSuffix))
                    {
                        string tokenName = potentialToken.Substring(0, potentialToken.Length - TokenSuffix.Length);
                        if (!tokenMap.TryGetValue(tokenName, out ITemplateToken token))
                        {
                            token = new TextTemplateToken(tokenName);
                            tokenMap.Add(tokenName, token);
                        }

                        parts.Add(line.Substring(startOfPreviousPart, nextIndexOfOpenBracket - startOfPreviousPart));
                        parts.Add(token);

                        startOfPreviousPart = i + 1;
                    }
                }

                nextIndexOfOpenBracket = i;
            }

            if (startOfPreviousPart < line.Length)
            {
                if (startOfPreviousPart == 0)
                {
                    parts.Add(line);
                }
                else
                {
                    parts.Add(line.Substring(startOfPreviousPart, line.Length - startOfPreviousPart));
                }
            }
        }

        private TextTemplatePart ParseInlineTemplate(string inlineTemplate)
        {
            List<object> templateParts = new List<object>();
            Dictionary<string, ITemplateToken> subTokens = new Dictionary<string, ITemplateToken>();

            ParseLineForTokens(inlineTemplate, templateParts, subTokens);
            templateParts.Add(Environment.NewLine);
            return new TextTemplatePart(templateParts, subTokens, new Dictionary<string, ITemplatePart>());
        }

        private TextTemplatePart ParseMultilineTemplate(StreamReader reader, string parentTemplateName = null)
        {
            List<object> templateParts = new List<object>();
            Dictionary<string, ITemplatePart> subTemplates = new Dictionary<string, ITemplatePart>();
            Dictionary<string, ITemplateToken> subTokens = new Dictionary<string, ITemplateToken>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (parentTemplateName != null && line.StartsWith($"#{parentTemplateName}_TEMPLATE_END"))
                {
                    break;
                }
                else if (!string.IsNullOrWhiteSpace(line) &&
                    line[0] == '#')
                {
                    // Possible start of an in-line template
                    int indexOfSpace = line.IndexOf(' ');
                    if (indexOfSpace > TemplateSuffix.Length && line.Substring(indexOfSpace - TemplateSuffix.Length, TemplateSuffix.Length) == TemplateSuffix)
                    {
                        // Got an in-line template here
                        string templateName = line.Substring(1, indexOfSpace - TemplateSuffix.Length - 1);

                        TextTemplatePart template = ParseInlineTemplate(line.Substring(indexOfSpace + 1));
                        subTemplates.Add(templateName, template);
                        templateParts.Add(template);
                        continue;
                    }
                    else if (line.EndsWith(TemplateStartSuffix))
                    {
                        // Got a multiline template here
                        string templateName = line.Substring(1, line.Length - TemplateStartSuffix.Length - 1);

                        TextTemplatePart template = ParseMultilineTemplate(reader, templateName);
                        subTemplates.Add(templateName, template);
                        templateParts.Add(template);
                    }
                    else
                    {
                        ParseLineForTokens(line, templateParts, subTokens);
                        templateParts.Add(Environment.NewLine);
                    }
                }
                else
                {
                    ParseLineForTokens(line, templateParts, subTokens);
                    templateParts.Add(Environment.NewLine);
                }
            }
            return new TextTemplatePart(templateParts, subTokens, subTemplates);
        }

        public override void Write(string path, TemplateReplacementSet replacementSet)
        {
            using (StreamWriter writer = new StreamWriter(path))
            {
                ((TextTemplatePart)Root).Write(writer, replacementSet);
            }
        }
    }
}
#endif