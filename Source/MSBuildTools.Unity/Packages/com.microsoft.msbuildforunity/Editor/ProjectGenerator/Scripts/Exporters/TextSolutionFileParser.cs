// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    internal class SolutionFileInfo
    {
        public static SolutionFileInfo Empty { get; } = new SolutionFileInfo()
        {
            ChildToParentNestedMappings = new Dictionary<Guid, Guid>(),
            ConfigPlatformPairs = new HashSet<ConfigurationPlatformPair>(),
            ExtensibilityGlobals = new Dictionary<string, string>(),
            MSB4UGeneratedItems = new HashSet<Guid>(),
            ProjectConfigurationEntires = new Dictionary<Guid, List<ProjectConfigurationEntry>>(),
            Projects = new Dictionary<Guid, Project>(),
            Properties = new Dictionary<string, string>(),
            SolutionNotes = new Dictionary<string, string>(),
            SolutionSections = new Dictionary<string, SolutionSection>()
        };

        public bool IsEmpty { get; }

        public Dictionary<Guid, Project> Projects { get; set; }

        public Dictionary<string, SolutionSection> SolutionSections { get; set; }

        public Dictionary<Guid, Guid> ChildToParentNestedMappings { get; set; }

        public HashSet<ConfigurationPlatformPair> ConfigPlatformPairs { get; set; }

        public Dictionary<string, string> Properties { get; set; }

        public Dictionary<string, string> ExtensibilityGlobals { get; set; }

        public Dictionary<string, string> SolutionNotes { get; set; }

        public Dictionary<Guid, List<ProjectConfigurationEntry>> ProjectConfigurationEntires { get; set; }

        public HashSet<Guid> MSB4UGeneratedItems { get; set; }

        private SolutionFileInfo(bool isEmpty)
        {
            IsEmpty = isEmpty;
        }

        public SolutionFileInfo() : this(false) { }
    }

    internal struct ProjectConfigurationEntry
    {
        public ConfigurationPlatformPair SolutionConfig { get; set; }

        public ConfigurationPlatformPair ProjectConfig { get; set; }

        public string Property { get; set; }
    }

    internal struct Project
    {
        public Guid TypeGuid { get; set; }

        public string Name { get; set; }

        public string RelativePath { get; set; }

        public Guid Guid { get; set; }

        public HashSet<Guid> Dependencies { get; set; }

        public HashSet<SolutionSection> Sections { get; set; }
    }

    internal static class TextSolutionFileParser
    {
        public static bool TryParseExistingSolutionFile(string path, out SolutionFileInfo solutionFileInfo)
        {
            try
            {
                solutionFileInfo = ParseExistingSolutionFileInner(path);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to parse existing solution file.");
                UnityEngine.Debug.LogException(ex);

                solutionFileInfo = default;
                return false;
            }
        }

        public static SolutionFileInfo ParseExistingSolutionFile(ILogger logger, string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    return ParseExistingSolutionFileInner(path);
                }
                catch (Exception ex)
                {
                    logger?.LogError(nameof(TextSolutionFileParser), "Failed to parse existing solution file.");
                    logger?.LogException(ex);
                }
            }

            // Return a default/empty one
            return SolutionFileInfo.Empty;
        }

        private static SolutionFileInfo ParseExistingSolutionFileInner(string path)
        {
            Dictionary<Guid, Project> projects = new Dictionary<Guid, Project>();
            Dictionary<string, SolutionSection> globalSections = new Dictionary<string, SolutionSection>();
            HashSet<ConfigurationPlatformPair> configPlatPairs = new HashSet<ConfigurationPlatformPair>();
            Dictionary<string, string> solutionProperties = new Dictionary<string, string>();
            Dictionary<string, string> extensibilityGlobals = new Dictionary<string, string>();
            Dictionary<string, string> solutionNotes = new Dictionary<string, string>();
            Dictionary<Guid, Guid> childToParentNestedMapping = new Dictionary<Guid, Guid>();
            Dictionary<Guid, List<ProjectConfigurationEntry>> projectConfigurationEntries = new Dictionary<Guid, List<ProjectConfigurationEntry>>();

            HashSet<Guid> msb4uGeneratedItems = new HashSet<Guid>();

            using (StreamReader reader = new StreamReader(path))
            {
                string line = string.Empty;
                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine();
                    if (line.StartsWith("Project"))
                    {
                        // Entered Projects section
                        break;
                    }
                }

                // Read Projects
                while (!reader.EndOfStream)
                {
                    if (!line.StartsWith("Project"))
                    {
                        throw new InvalidDataException("Unexpected top level section.");
                    }

                    Project project = ParseProjectDefinitionSection(reader, line);
                    projects.Add(project.Guid, project);

                    line = reader.ReadLine() ?? string.Empty;

                    if (line.StartsWith("Global") && line.Trim().Length == "Global".Length)
                    {
                        // Entered next part of the Global sections
                        break;
                    }
                }

                // Parse Global Sections
                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine().TrimStart();

                    if (line.StartsWith("EndGlobal"))
                    {
                        // Done with Global (and file)
                        break;
                    }

                    // SAMPLE: GlobalSection(SolutionConfigurationPlatforms) = preSolution
                    string globalSectionHeaderRegex = @"GlobalSection\(([^\)]+)\)\s+=\s+(preSolution|postSolution)";
                    Match match = Regex.Match(line, globalSectionHeaderRegex);

                    if (!match.Success)
                    {
                        throw new InvalidDataException("Expecting Global sections.");
                    }
                    string sectionName = match.Groups[1].Captures[0].Value;
                    string prePostSolution = match.Groups[2].Captures[0].Value;
                    switch (sectionName)
                    {
                        case "SolutionConfigurationPlatforms":
                            ReadLinesUntil(reader, "EndGlobalSection", l =>
                            {
                                string[] configPlatPair = l.Split('=')[0].Trim().Split('|');
                                configPlatPairs.Add(new ConfigurationPlatformPair(configPlatPair[0], configPlatPair[1]));
                            });
                            break;
                        case "ProjectConfigurationPlatforms":
                            ReadLinesUntil(reader, "EndGlobalSection", l =>
                            {
                                // SAMPLE: {<PROJECT_GUID_TOKEN>}.<SOLUTION_CONFIGURATION_TOKEN>|<SOLUTION_PLATFORM_TOKEN>.ActiveCfg = <PROJECT_CONFIGURATION_TOKEN>|<PROJECT_PLATFORM_TOKEN>
                                // SAMPLE: {<PROJECT_GUID_TOKEN>}.<SOLUTION_CONFIGURATION_TOKEN>|<SOLUTION_PLATFORM_TOKEN>.Build.0 = <PROJECT_CONFIGURATION_TOKEN>|<PROJECT_PLATFORM_TOKEN>
                                string regex = @"{([^}]+)}\.([^\|]+)\|([^\.]+)\.([^\s]+)\s+=\s+([^\|]+)\|(.+)";
                                Match configMatch = Regex.Match(l, regex);
                                Guid projectGuid = Guid.Parse(configMatch.Groups[1].Captures[0].Value);
                                if (!projectConfigurationEntries.TryGetValue(projectGuid, out List<ProjectConfigurationEntry> list))
                                {
                                    projectConfigurationEntries.Add(projectGuid, list = new List<ProjectConfigurationEntry>());
                                }

                                list.Add(new ProjectConfigurationEntry()
                                {
                                    SolutionConfig = new ConfigurationPlatformPair(configMatch.Groups[2].Captures[0].Value, configMatch.Groups[3].Captures[0].Value),
                                    ProjectConfig = new ConfigurationPlatformPair(configMatch.Groups[5].Captures[0].Value, configMatch.Groups[6].Captures[0].Value),
                                    Property = configMatch.Groups[4].Captures[0].Value
                                });
                            });
                            break;
                        case "SolutionProperties":
                            ReadLinesUntil(reader, "EndGlobalSection", l =>
                            {
                                string[] property = l.Split('=');
                                solutionProperties[property[0].Trim()] = property[1].Trim();
                            });
                            break;
                        case "NestedProjects":
                            ReadLinesUntil(reader, "EndGlobalSection", l =>
                            {
                                string[] nestedMapping = l.Split('=');
                                nestedMapping[0] = nestedMapping[0].Trim();
                                nestedMapping[1] = nestedMapping[1].Trim();

                                string childProject = nestedMapping[0].Substring(1, nestedMapping[0].Length - 2);
                                string parentProject = nestedMapping[1].Substring(1, nestedMapping[1].Length - 2);

                                childToParentNestedMapping[Guid.Parse(childProject)] = Guid.Parse(parentProject);
                            });
                            break;
                        case "ExtensibilityGlobals":
                            ReadLinesUntil(reader, "EndGlobalSection", l =>
                            {
                                string[] property = l.Split('=');
                                extensibilityGlobals[property[0].Trim()] = property[1].Trim();
                            });
                            break;
                        case "SolutionNotes":
                            ReadLinesUntil(reader, "EndGlobalSection", l =>
                            {
                                string[] property = l.Split('=');
                                string key = property[0].Trim();
                                string value = property[1].Trim();

                                if (value == "msb4u.generated")
                                {
                                    msb4uGeneratedItems.Add(Guid.Parse(key.Substring(1, key.Length - 2)));
                                }
                                else
                                {
                                    solutionNotes[key] = value;
                                }
                            });
                            break;
                        default:
                            globalSections.Add(sectionName, ReadSection(reader, sectionName, prePostSolution == "preSolution" ? SectionType.PreSection : SectionType.PostSection, "EndGlobalSection"));
                            break;
                    }
                }
            }

            return new SolutionFileInfo()
            {
                Projects = projects,
                ChildToParentNestedMappings = childToParentNestedMapping,
                ConfigPlatformPairs = configPlatPairs,
                ProjectConfigurationEntires = projectConfigurationEntries,
                Properties = solutionProperties,
                ExtensibilityGlobals = extensibilityGlobals,
                SolutionNotes = solutionNotes,
                SolutionSections = globalSections,
                MSB4UGeneratedItems = msb4uGeneratedItems
            };
        }

        private static Project ParseProjectDefinitionSection(StreamReader reader, string line)
        {
            // Line is currently the "Project" line, so parse the project until EndProject

            //Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "<PROJECT_NAME_TOKEN>", "<PROJECT_RELATIVE_PATH_TOKEN>", "{<PROJECT_GUID_TOKEN>}"
            string projectLineRegex = "Project\\(\"{" + "([^}]+)" + "}\"\\)\\s*=\\s*\"" + "([^\"]+)" + "\",\\s*\"" + "([^\"]+)" + "\",\\s*\"" + "([^\"]+)" + "\"";
            Match match = Regex.Match(line, projectLineRegex);

            if (!match.Success)
            {
                throw new InvalidDataException("Failed to parse the project line.");
            }

            string projectType = match.Groups[1].Captures[0].Value;
            string projectName = match.Groups[2].Captures[0].Value;
            string projectPath = match.Groups[3].Captures[0].Value;
            string projectGuid = match.Groups[4].Captures[0].Value;

            HashSet<SolutionSection> generalProjectSections = new HashSet<SolutionSection>();
            HashSet<Guid> dependencies = new HashSet<Guid>();
            do
            {
                string projectSectionRegex = @"ProjectSection\(([^\)]+)\)\s*=\s*(preProject|postProject)";

                line = reader.ReadLine().TrimStart();
                if (line.StartsWith("EndProject"))
                {
                    // We reached end of project section, read next line and return
                    return new Project()
                    {
                        Dependencies = dependencies,
                        Guid = Guid.Parse(projectGuid),
                        TypeGuid = Guid.Parse(projectType),
                        Name = projectName,
                        RelativePath = projectPath,
                        Sections = generalProjectSections
                    };
                }

                match = Regex.Match(line, projectSectionRegex);
                if (!match.Success)
                {
                    throw new InvalidDataException("Invalid data in the Project, expecting a ProjectSection");
                }

                string projectSectionName = match.Groups[1].Captures[0].Value;
                string prePostProject = match.Groups[2].Captures[0].Value;

                if (projectSectionName == "ProjectDependencies")
                {
                    ReadLinesUntil(reader, "EndProjectSection", l =>
                    {
                        string guid = l.Split('=')[0].Trim();
                        dependencies.Add(Guid.Parse(guid.Substring(1, guid.Length - 2)));
                    });
                }
                else
                {
                    generalProjectSections.Add(ReadSection(reader, projectSectionName, prePostProject == "preProject" ? SectionType.PreSection : SectionType.PostSection, "EndProjectSection"));
                }

            } while (!reader.EndOfStream);

            throw new InvalidDataException("We should have already returned.");
        }

        private static SolutionSection ReadSection(StreamReader reader, string sectionName, SectionType sessionType, string endSectionTag)
        {
            SolutionSection toReturn = new SolutionSection()
            {
                Name = sectionName,
                Type = sessionType
            };

            ReadLinesUntil(reader, endSectionTag, toReturn.SectionLines.Add);

            return toReturn;
        }

        private static void ReadLinesUntil(StreamReader reader, string endLine, Action<string> processLineCallback)
        {
            do
            {
                string line = reader.ReadLine().Trim();
                if (line == endLine)
                {
                    break;
                }

                processLineCallback(line);
            } while (!reader.EndOfStream);
        }
    }
}
#endif