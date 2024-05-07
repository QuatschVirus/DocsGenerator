using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;


namespace DocsGenerator
{
    public class Program
    {
        public static List<ClassRecord> classRecords = new();
        static SyntaxTree[] trees;
        static SemanticModel[] models;

        static void Main(string[] args)
        {
            string[] htmlExtensions = new string[] { ".html", ".htm", ".php", ".aspx", "asp", ".jsp" };

            string inPath = args[0] ?? "./docs.xml";
            string sourcePath = args[1] ?? "./src";
            string outPath = args[2] ?? "";
            List<string> classes = args.Where(a => a.StartsWith('-')).ToList();
            bool outputHTML = htmlExtensions.Contains(Path.GetExtension(outPath)) || args.Any(a => a.ToLower() == "/h");

            string[] sourceFiles = Directory.GetFiles(sourcePath, "*.cs");
            trees = sourceFiles.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p))).ToArray();

            var comp = CSharpCompilation.Create(
                "DocsGeneration",
                trees,
                new MetadataReference[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }
                );

            models = trees.Select(t => comp.GetSemanticModel(t)).ToArray();

            XDocument doc = XDocument.Load(inPath);
            XElement[] members = doc.Root.Descendants("members").Descendants("member").ToArray();

            foreach (XElement member in members.Where(m => m.Attribute("name").Value.StartsWith('T')))
            {
                string name = member.Attribute("name").Value[2..];
                var rawNode = GetNode(trees, name);
                if (rawNode == null) continue;
                if (rawNode is not ClassDeclarationSyntax) continue;
                var node = rawNode as ClassDeclarationSyntax;

                ClassRecord r = new()
                {
                    FullQualifier = name,
                    Name = name.Split('.').Last(),
                    ClassType = GetClassType(node),
                };
            }
        }

        public static ClassType GetClassType(this ClassDeclarationSyntax s)
        {
            if (s.Identifier.Text.EndsWith("Attribute")) return ClassType.Attribute;



            return ClassType.Class;
        }

        public static SyntaxNode GetNodeAt(this SyntaxNode node, string fullName)
        {
            string primary = string.Concat(fullName.TakeWhile(c => c != '.'));
            string secondary = fullName[(primary.Length + 1)..];
            foreach (var child in node.ChildNodes())
            {
                if (child is MemberDeclarationSyntax m)
                {
                    if (GetName(m) == primary)
                    {
                        if (secondary != "")
                        {
                            return GetNodeAt(m, secondary);
                        } else
                        {
                            return m;
                        }
                    }
                }
                
            }
            return null;
        }

        public static SyntaxNode GetNode(this SyntaxTree[] trees, string name)
        {
            SyntaxNode node;
            foreach (var tree in trees)
            {
                node = GetNodeAt(tree.GetRoot(), name);
                if (node != null) return node;
            }
            return null;
        }

        public static string GetName(this MemberDeclarationSyntax member)
        {
            return member switch
            {
                NamespaceDeclarationSyntax s => s.Name.ToString(),
                TypeDeclarationSyntax s => s.Identifier.Text,
                FieldDeclarationSyntax s => s.Declaration.Variables.First().Identifier.Text,
                PropertyDeclarationSyntax s => s.Identifier.Text,
                MethodDeclarationSyntax s => s.Identifier.Text,
                _ => null
            };
        }
    }

    public class Record
    {
        public string FullQualifier;
        public string Name;
    }

    public enum ClassType
    {
        Class,
        Struct,
        Enum,
        Abstract,
        Interface,
        Static,
        Attribute,
        Unknown
    }

    public class ClassRecord : Record
    {
        public ClassType ClassType;
        public List<Record> Members = new();
    }

    public class FieldRecord : Record
    {
        public string Type;
    }
}