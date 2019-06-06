﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;

namespace MatBlazor.DevUtils.Core
{
    public class MatDocumenationGenerator
    {
        public Assembly Assembly;
        public string XmlPath;
        public string OutputPath;


        public void Generate()
        {
            var xml = XDocument.Load(XmlPath);

            foreach (var fileInfo in new DirectoryInfo(OutputPath).GetFiles("*.razor", SearchOption.TopDirectoryOnly))
            {
                fileInfo.Delete();
            }


            foreach (var type in Assembly.ExportedTypes)
            {
//                if (!type.IsSubclassOf(typeof(ComponentBase)))
//                {
//                    continue;
//                }


                if (type.Name == "MatTheme")
                {
                }

                if (type.Name.StartsWith("Base"))
                {
                    continue;
                }

                var typeName = GetTypeName(type, true);

                Console.WriteLine(typeName);

                var outFilePath = Path.Combine(OutputPath, $"Doc{GetFileName(typeName)}.razor");

                var sb = new StringBuilder();

                sb.AppendLine($"@inherits MatBlazor.Demo.Components.BaseDocComponent");
                sb.AppendLine();
                sb.AppendLine("@* THIS FILE IS AUTOGENERATED FROM C# XML Comments! *@");
                sb.AppendLine("@* ALL MANUAL CHANGES WILL BE REMOVED! *@");
                sb.AppendLine();
                sb.AppendLine();
                //@if (Secondary) { <h3 class="mat-h3">MatProgressBar</h3> } else { <h3 class="mat-h3">MatProgressBar</h3> }
                sb.AppendLine(
                    $"@if (!Secondary) {{<h3 class=\"mat-h3\">{HtmlEncode(typeName)}</h3> }} else {{ <h5 class=\"mat-h5\">{HtmlEncode(typeName)}</h5> }}");
                sb.AppendLine();
                var typeXml = FindDocXml(xml, type);
                if (typeXml != null)
                {
                    sb.AppendLine($"<p>{HtmlEncode(ParseXmlMember(typeXml))}</p>");
                    sb.AppendLine();
                }


                var includeFields = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    nameof(BaseMatComponent.Ref)
                };


                var parameters = type
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(prop =>
                        (type.IsSubclassOf(typeof(ComponentBase)) &&
                        prop.GetCustomAttributes(typeof(ParameterAttribute)).Any())

                        ||

                        (!type.IsSubclassOf(typeof(ComponentBase)) &&
                         prop.DeclaringType.Assembly == Assembly)

                    )
                    .OrderBy(i => i.Name)
                    .Union(
                        type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                            .Where(i => includeFields.Contains(i.Name))
                            .OrderBy(i => i.Name)
                    )
                    .ToArray();


//                sb.AppendLine($"<h5 class=\"mat-h5\">Documentation</h5>");

                sb.AppendLine($"<div><table class=\"article-table mat-elevation-z5\">");
                sb.AppendLine($"\t<tr>");
                sb.AppendLine($"\t\t<th>Name</th>");
                sb.AppendLine($"\t\t<th>Type</th>");
                sb.AppendLine($"\t\t<th>Description</th>");
                sb.AppendLine($"\t</tr>");


                if (type.IsGenericType)
                {
                    foreach (var genericArgument in type.GetGenericArguments())
                    {
                        var propXml = FindDocXml(xml, type);

                        sb.AppendLine($"\t<tr>");
                        sb.AppendLine($"\t\t<td style=\"font-weight: bold;\">{HtmlEncode(genericArgument.Name)}</td>");
                        sb.AppendLine($"\t\t<td>Generic argument</td>");
                        sb.AppendLine(
                            $"\t\t<td>{HtmlEncode(ParseXmlMember(typeXml, "typeparam", genericArgument.Name))}</td>");
                        sb.AppendLine($"\t</tr>");
                    }
                }


                foreach (var prop in parameters)
                {
                    var propXml = FindDocXml(xml, prop);
                    var propText = ParseXmlMember(propXml);

                    if (prop.Name == "ChildContent" && string.IsNullOrEmpty(propText))
                    {
                        propText = "Child content of " + typeName;
                    }

                    sb.AppendLine($"\t<tr>");
                    sb.AppendLine($"\t\t<td>{HtmlEncode(prop.Name)}</td>");
                    sb.AppendLine($"\t\t<td>{HtmlEncode(GetTypeName(prop.PropertyType, false))}</td>");
                    sb.AppendLine($"\t\t<td>{HtmlEncode(propText)}</td>");
                    sb.AppendLine($"\t</tr>");
                }

                sb.AppendLine($"</table></div>");


                File.WriteAllText(outFilePath, sb.ToString());
            }
        }


        private string HtmlEncode(string v)
        {
            if (v == null)
            {
                return "";
            }

            return string.Join("<br/>",
                v.Trim().Split("\n").Select(i => i.Trim()).Select(i => HttpUtility.HtmlEncode(i)));
        }

        private XElement FindDocXml(XDocument xml, Type type)
        {
            if (xml.Root != null)
            {
                var membersEl = xml.Root.Element("members");
                if (membersEl != null)
                {
                    while (type != null && type.Assembly == Assembly)
                    {
                        var key = $"T:{type.FullName}";
                        var el = membersEl.Elements("member").FirstOrDefault(i => i.Attribute("name").Value == key);
                        if (el != null)
                        {
                            return el;
                        }

                        if (type.IsGenericType)
                        {
                            type = type.BaseType;
                        }
                        else
                        {
                            type = type.BaseType;
                        }
                    }
                }
            }

            return null;
        }

        private XElement FindDocXml(XDocument xml, MemberInfo member)
        {
            if (xml.Root != null)
            {
                var membersEl = xml.Root.Element("members");
                if (membersEl != null)
                {
                    string key;
                    if (member.DeclaringType.IsGenericType)
                    {
                        key = $"P:{member.DeclaringType.GetGenericTypeDefinition().FullName}.{member.Name}";
                    }
                    else
                    {
                        key = $"P:{member.DeclaringType}.{member.Name}";
                    }

                    var el = membersEl.Elements("member").FirstOrDefault(i => i.Attribute("name").Value == key);
                    return el;
                }
            }

            return null;
        }

        private string ParseXmlMember(XElement xmlEl, string element = "summary", string name = null)
        {
            if (xmlEl != null)
            {
                var summaryXmlEls = xmlEl.Elements(element);

                if (name != null)
                {
                    summaryXmlEls = summaryXmlEls.Where(i => i.Attribute("name").Value == name);
                }

                return summaryXmlEls.FirstOrDefault()?.Value;
            }

            return null;
        }

        private string GetFileName(string n)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
            {
                n = n.Replace("" + ch, "");
            }

            return n;
        }

        private string GetTypeName(Type t, bool disableGeneric)
        {
            if (!t.IsGenericType)
            {
                return t.Name;
            }

            string genericTypeName = t.GetGenericTypeDefinition().Name;
            genericTypeName = genericTypeName.Substring(0,
                genericTypeName.IndexOf('`'));

            if (disableGeneric)
            {
                return genericTypeName;
            }
            else
            {
                string genericArgs = string.Join(",",
                    t.GetGenericArguments()
                        .Select(ta => GetTypeName(ta, disableGeneric)).ToArray());
                return genericTypeName + "<" + genericArgs + ">";
            }
        }

        public MatDocumenationGenerator(Assembly assembly, string outputPath)
        {
            Assembly = assembly;
            XmlPath = Path.ChangeExtension(assembly.Location, ".xml");
            if (!File.Exists(XmlPath))
            {
                throw new Exception("Xml not found");
            }

            OutputPath = outputPath;
            if (!Directory.Exists(OutputPath))
            {
                throw new Exception("OutputPath not exists");
            }
        }
    }
}