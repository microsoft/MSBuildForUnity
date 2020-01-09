// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    internal enum SolutionProjecSectionType
    {
        PreProject,
        PostProject
    }

    internal enum SolutionGlobalSectionType
    {
        PreSolution,
        PostSolution
    }

    internal class SolutionFileInfo
    {
        public Dictionary<Guid, Project> Projects { get; set; }

        public Dictionary<string, SolutionFileSection<SolutionGlobalSectionType>> SolutionSections { get; set; }

        public Dictionary<Guid, Guid> ChildToParentNestedMappings { get; set; }

        public HashSet<ConfigPlatformPair> ConfigPlatformPairs { get; set; }

        public Dictionary<string, string> Properties { get; set; }

        public Dictionary<string, string> ExtensibilityGlobals { get; set; }

        public Dictionary<string, string> SolutionNotes { get; set; }

        public Dictionary<Guid, List<ProjectConfigurationEntry>> ProjectConfigurationEntires { get; set; }

        public HashSet<Guid> MSB4UGeneratedItems { get; set; }
    }

    internal struct ConfigPlatformPair
    {
        public string Configuration { get; set; }

        public string Platform { get; set; }

        public ConfigPlatformPair(string configuration, string platform)
        {
            Configuration = configuration;
            Platform = platform;
        }

        public override bool Equals(object obj)
        {
            return obj is ConfigPlatformPair other
                && Equals(Configuration, other.Configuration)
                && Equals(Platform, other.Platform);
        }

        public override int GetHashCode()
        {
            return (Configuration?.GetHashCode() ?? 0)
                ^ (Platform?.GetHashCode() ?? 0);
        }

        internal struct Comparer : IComparer<ConfigPlatformPair>
        {
            internal static Comparer Instance { get; } = new Comparer();

            public int Compare(ConfigPlatformPair x, ConfigPlatformPair y)
            {
                int results = string.Compare(x.Configuration, y.Configuration);

                return results == 0
                    ? string.Compare(x.Platform, y.Platform)
                    : results;
            }
        }
    }

    internal struct ProjectConfigurationEntry
    {
        public ConfigPlatformPair SolutionConfig { get; set; }

        public ConfigPlatformPair ProjectConfig { get; set; }

        public string Property { get; set; }
    }

    internal struct SolutionFileSection<T>
    {
        public string Name { get; set; }

        public T Type { get; set; }

        public List<string> Lines { get; set; }
    }

    internal struct Project
    {
        public Guid TypeGuid { get; set; }

        public string Name { get; set; }

        public string RelativePath { get; set; }

        public Guid Guid { get; set; }

        public HashSet<Guid> Dependencies { get; set; }

        public Dictionary<string, SolutionFileSection<SolutionProjecSectionType>> Sections { get; set; }
    }

    internal static class TextSolutionFileParser
    {
        public static bool TryParseExistingSolutionFile(string path, out SolutionFileInfo solutionFileInfo)
        {
            try
            {
                solutionFileInfo = ParseExistingSolutionFile(path);
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

        private static SolutionFileInfo ParseExistingSolutionFile(string path)
        {
            Dictionary<Guid, Project> projects = new Dictionary<Guid, Project>();
            Dictionary<string, SolutionFileSection<SolutionGlobalSectionType>> globalSections = new Dictionary<string, SolutionFileSection<SolutionGlobalSectionType>>();
            HashSet<ConfigPlatformPair> configPlatPairs = new HashSet<ConfigPlatformPair>();
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
                                configPlatPairs.Add(new ConfigPlatformPair(configPlatPair[0], configPlatPair[1]));
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
                                    SolutionConfig = new ConfigPlatformPair(configMatch.Groups[2].Captures[0].Value, configMatch.Groups[3].Captures[0].Value),
                                    ProjectConfig = new ConfigPlatformPair(configMatch.Groups[5].Captures[0].Value, configMatch.Groups[6].Captures[0].Value),
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
                            globalSections.Add(sectionName, ReadSection(reader, sectionName, prePostSolution == "preSolution" ? SolutionGlobalSectionType.PreSolution : SolutionGlobalSectionType.PostSolution, "EndGlobalSection"));
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

            Dictionary<string, SolutionFileSection<SolutionProjecSectionType>> generalProjectSections = new Dictionary<string, SolutionFileSection<SolutionProjecSectionType>>();
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
                    generalProjectSections.Add(projectSectionName, ReadSection(reader, projectSectionName, prePostProject == "preProject" ? SolutionProjecSectionType.PreProject : SolutionProjecSectionType.PostProject, "EndProjectSection"));
                }

            } while (!reader.EndOfStream);

            throw new InvalidDataException("We should have already returned.");
        }

        private static SolutionFileSection<T> ReadSection<T>(StreamReader reader, string sectionName, T sessionType, string endSectionTag)
        {
            List<string> sectionLines = new List<string>();

            ReadLinesUntil(reader, endSectionTag, sectionLines.Add);

            return new SolutionFileSection<T>()
            {
                Lines = sectionLines,
                Name = sectionName,
                Type = sessionType
            };
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