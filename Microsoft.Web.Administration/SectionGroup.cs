﻿// Copyright (c) Lex Li. All rights reserved.
// 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Web.Administration
{
    [DebuggerDisplay("{Name} [{_core.FileName}]")]
    public sealed class SectionGroup
    {
        internal const string KEYWORD_TRUE = "true";
        internal const string KEYWORD_FALSE = "false";
        internal const string KEYWORD_SECTION = "section";
        internal const string KEYWORD_SECTION_NAME = "name";
        internal const string KEYWORD_SECTION_ALLOWLOCATION = "allowLocation";
        internal const string KEYWORD_SECTION_ALLOWDEFINITION = "allowDefinition";
        internal const string KEYWORD_SECTION_ALLOWDEFINITION_APPHOSTONLY = "AppHostOnly";
        internal const string KEYWORD_SECTION_ALLOWDEFINITION_EVERYWHERE = "Everywhere";
        internal const string KEYWORD_SECTION_ALLOWDEFINITION_MACHINEONLY = "MachineOnly";
        internal const string KEYWORD_SECTION_ALLOWDEFINITION_MACHINETOAPPLICATION = "MachineToApplication";
        internal const string KEYWORD_SECTION_ALLOWDEFINITION_MACHINETOWEBROOT = "MachineToWebRoot";
        internal const string KEYWORD_SECTION_TYPE = "type";
        internal const string KEYWORD_SECTIONGROUP = "sectionGroup";
        internal const string KEYWORD_SECTIONGROUP_NAME = "name";
        internal const string KEYWORD_SECTION_OVERRIDEMODEDEFAULT = "overrideModeDefault";
        internal const string KEYWORD_LOCATION_OVERRIDEMODE = "overrideMode";
        internal const string KEYWORD_OVERRIDEMODE_INHERIT = "Inherit";
        internal const string KEYWORD_OVERRIDEMODE_ALLOW = "Allow";
        internal const string KEYWORD_OVERRIDEMODE_DENY = "Deny";

        private readonly FileContext _core;

        internal SectionGroup(FileContext core)
        {
            _core = core;
            SectionGroups = new SectionGroupCollection(core, this);
            Sections = new SectionDefinitionCollection(core);
            Name = string.Empty;
            Type = string.Empty;
            Path = string.Empty;
        }

        public string Name { get; internal set; }

        public SectionGroupCollection SectionGroups { get; }
        public SectionDefinitionCollection Sections { get; }
        public string Type { get; set; }

        internal void ParseSectionDefinitions(XElement root, Dictionary<string, SectionSchema> sectionSchemas)
        {
            foreach (var node in root.Nodes())
            {
                if (!(node is XElement element))
                {
                    continue;
                }

                if (element.Name.LocalName == KEYWORD_SECTIONGROUP)
                {
                    XAttribute groupName = element.Attribute(KEYWORD_SECTIONGROUP_NAME);
                    Debug.Assert(groupName != null, nameof(groupName) + " != null");
                    var group = SectionGroups.Add(groupName.Value);
                    group.Path = Name == string.Empty ? group.Name : $"{Path}/{@group.Name}";
                    group.ParseSectionDefinitions(element, sectionSchemas);
                    continue;
                }

                if (element.Name.LocalName == KEYWORD_SECTION)
                {
                    XAttribute sectionName = element.Attribute(KEYWORD_SECTION_NAME);
                    Debug.Assert(sectionName != null, nameof(sectionName) + " != null");
                    var sectionDefinition = Sections.Add(sectionName.Value);
                    sectionDefinition.OverrideModeDefault = element.Attribute(KEYWORD_SECTION_OVERRIDEMODEDEFAULT).LoadString(KEYWORD_OVERRIDEMODE_ALLOW);
                    sectionDefinition.AllowLocation = element.Attribute(KEYWORD_SECTION_ALLOWLOCATION).LoadString(KEYWORD_TRUE);
                    sectionDefinition.AllowDefinition = element.Attribute(KEYWORD_SECTION_ALLOWDEFINITION).LoadString(KEYWORD_SECTION_ALLOWDEFINITION_EVERYWHERE);
                    sectionDefinition.Path = Name == string.Empty ? sectionDefinition.Name : $"{Path}/{sectionDefinition.Name}";
                    sectionDefinition.Type = element.Attribute(KEYWORD_SECTION_TYPE).LoadString(string.Empty);

                    sectionSchemas.TryGetValue(sectionDefinition.Path, out SectionSchema schema);
                    sectionDefinition.Schema = schema;
                    sectionDefinition.Entity = element;
                }
            }
        }

        internal string Path { get; set; }

        internal static ConfigurationAllowDefinition AllowDefinitionToEnum(string allowDefinition)
        {
            switch (allowDefinition)
            {
                case KEYWORD_SECTION_ALLOWDEFINITION_EVERYWHERE:
                    return ConfigurationAllowDefinition.Everywhere;

                case KEYWORD_SECTION_ALLOWDEFINITION_MACHINEONLY:
                    return ConfigurationAllowDefinition.MachineOnly;

                case KEYWORD_SECTION_ALLOWDEFINITION_MACHINETOAPPLICATION:
                    return ConfigurationAllowDefinition.MachineToApplication;

                case KEYWORD_SECTION_ALLOWDEFINITION_MACHINETOWEBROOT:
                    return ConfigurationAllowDefinition.MachineToWebRoot;

                case KEYWORD_SECTION_ALLOWDEFINITION_APPHOSTONLY:
                    return ConfigurationAllowDefinition.AppHostOnly;

                default:
                    throw new ServerManagerException("Invalid allow definition string");
            }
        }

        internal void GetAllDefinitions(IList<SectionDefinition> result)
        {
            foreach (SectionDefinition item in Sections)
            {
                if (result.Any(_ => _.Path == item.Path))
                {
                    throw new COMException($"Line number: {(item.Entity as IXmlLineInfo).LineNumber}\r\nError: There is a duplicate '{item.Path}' section defined\r\n");
                }

                result.Add(item);
            }

            foreach (SectionGroup child in SectionGroups)
            {
                child.GetAllDefinitions(result);
            }
        }

        internal void AddSpecial()
        {
            Sections.Add(new SectionDefinition()
            {
                AllowDefinition = KEYWORD_SECTION_ALLOWDEFINITION_EVERYWHERE,
                AllowLocation = KEYWORD_FALSE,
                Name = "configProtectedData",
                OverrideModeDefault = KEYWORD_OVERRIDEMODE_ALLOW,
                RequirePermission = true,
                Type = string.Empty,
                Path = "configProtectedData",
                FileContext = _core
            });
        }
    }
}
