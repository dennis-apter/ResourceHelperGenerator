using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using Microsoft.CSharp;

namespace ResourceHelperGenerator
{
    internal static class TemplateRenderer
    {
        private static readonly string[] Separators = { Environment.NewLine };

        public static void RenderTemplate(TextWriter writer, TemplateModel model)
        {
            using (var indentedWriter = new IndentedTextWriter(writer))
            {
                indentedWriter.WriteLine("// <auto-generated />");
                indentedWriter.WriteEmptyLine();
                indentedWriter.WriteNamespace(model.ProjectName);
                indentedWriter.WriteLine("{");
                indentedWriter.WriteClass(model);
                indentedWriter.WriteLine("}");
            }
        }

        private static void WriteUsings(this TextWriter writer, params string[] usings)
        {
            foreach (var @using in usings)
            {
                writer.WriteLine("using {0};", @using);
            }
        }

        private static void WriteNamespace(this TextWriter writer, string @namespace)
        {
            writer.WriteLine("namespace {0}", GetValidIdentifier(@namespace));
        }

        private static void WriteClass(this IndentedTextWriter writer, TemplateModel model)
        {
            writer.Indent++;
            writer.WriteUsings("System.Diagnostics", "System.Globalization", "System.Reflection", "System.Resources");
            writer.WriteEmptyLine();
            writer.WriteLine("{0} static class {1}", model.Internalize ? "internal" : "public", GetValidIdentifier(model.FileName));
            writer.WriteLine("{");
            writer.WriteClassContent(model);
            writer.WriteLine("}");
            writer.Indent--;
        }

        private static void WriteClassContent(this IndentedTextWriter writer, TemplateModel model)
        {
            writer.Indent++;
            writer.WriteFields(model);
            foreach (var resourceData in model.ResourceData)
            {
                writer.WriteResource(model, resourceData);
            }
            writer.WriteGetStringMethod();
            writer.Indent--;
        }

        private static void WriteFields(this TextWriter writer, TemplateModel model)
        {
            writer.WriteLine(@"private static readonly ResourceManager ResourceManager
            = new ResourceManager(""{0}.{1}"", typeof({1}).Assembly);", model.ProjectName, model.FileName);
        }

        private static void WriteResource(this IndentedTextWriter writer, TemplateModel model, ResourceData data)
        {
            writer.WriteEmptyLine();

            var isFormatMethod = data.Arguments.Any();

            writer.WriteHeader(data, isFormatMethod);

            if (isFormatMethod)
            {
                writer.WriteFormatMethod(model, data);
            }
            else
            {
                writer.WriteProperty(model, data);
            }
        }

        private static void WriteHeader(this TextWriter writer, ResourceData data, bool isFormatMethod)
        {
            if (isFormatMethod)
            {
                writer.WriteSummaryTag(data.Value, data.Comment);
            }
            else
            {
                writer.WriteSummaryTag(data.Value);

                if (!string.IsNullOrWhiteSpace(data.Comment))
                {
                    writer.WriteValueTag(data.Comment);
                }
            }
        }

        private static void WriteSummaryTag(this TextWriter writer, params string[] summaryParts)
        {
            writer.WriteLine("/// <summary>");

            foreach (var summaryPart in summaryParts)
            {
                writer.WriteMultiLine(summaryPart, line =>
                    string.Format("/// {0}", line.Replace("<", "&lt;").Replace(">", "&gt;")));
            }

            writer.WriteLine("/// </summary>");
        }

        private static void WriteValueTag(this TextWriter writer, string value)
        {
            writer.WriteLine("/// <value>");

            writer.WriteMultiLine(value, line =>
                string.Format("/// {0}", line.Replace("<", "&lt;").Replace(">", "&gt;")));

            writer.WriteLine("/// </value>");
        }

        private static void WriteMultiLine(this TextWriter writer, string value, Func<string, string> transformer)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (var line in value.Split(Separators, StringSplitOptions.None))
            {
                writer.WriteLine(transformer.Invoke(line));
            }
        }

        private static void WriteFormatMethod(this IndentedTextWriter writer, TemplateModel model, ResourceData data)
        {
            var formatArguments = data.UsingNamedArgs ? string.Concat(", ", data.FormatArguments) : null;

            writer.WriteLine("{0} static string {1}({2})", model.Internalize ? "internal" : "public", GetValidIdentifier(data.Name), data.Parameters);
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine(@"return string.Format(CultureInfo.CurrentCulture, GetString(""{0}""{1}), {2});", data.Name, formatArguments, data.ArgumentNames);
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteProperty(this IndentedTextWriter writer, TemplateModel model, ResourceData data)
        {
            writer.WriteLine("{0} static string {1}", model.Internalize ? "internal" : "public", GetValidIdentifier(data.Name));
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine(@"get {{ return GetString(""{0}""); }}", data.Name);
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteGetStringMethod(this IndentedTextWriter writer)
        {
            writer.WriteEmptyLine();
            writer.WriteLine("private static string GetString(string name, params string[] formatterNames)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var value = ResourceManager.GetString(name);");
            writer.WriteEmptyLine();
            writer.WriteLine("Debug.Assert(value != null);");
            writer.WriteEmptyLine();
            writer.WriteLine("if (formatterNames != null)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("for (var i = 0; i < formatterNames.Length; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine(@"value = value.Replace(""{"" + formatterNames[i] + ""}"", ""{"" + i + ""}"");");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteEmptyLine();
            writer.WriteLine("return value;");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteEmptyLine(this IndentedTextWriter writer)
        {
            var temp = writer.Indent;

            writer.Indent = 0;

            writer.WriteLine();

            writer.Indent = temp;
        }

        private static string GetValidIdentifier(string value)
        {
            var codeProvider = new CSharpCodeProvider();

            var isValidIdentifier = value.Split('.')
                .Select(codeProvider.CreateValidIdentifier)
                .All(codeProvider.IsValidIdentifier);

            if (isValidIdentifier)
            {
                return value;
            }

            throw new InvalidOperationException(string.Format("'{0}' is not a valid identifier.", value));
        }
    }
}