﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Frozen;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.IO;
using System.Runtime.CompilerServices;

namespace DocsGenerator
{
    public static class Program
    {
        public const string credits = "\n---\n*Generated by [DocsGenerator](https://github.com/QuatschVirus/DocsGenerator)*";
        const string configPath = "dg-cfg.json";

        static JsonElement config;

        public static SyntaxTree[] trees;
        public static UnifiedTree<Record> records = new("Code Dcoumentation");
        public static UnifiedTree<Record> pathedRecords;

        static string docsPath;

        static void Main(string[] args)
        {
            string basepath = (args.Length > 0) ? args[0] : ".";
            string cp = Path.Combine(basepath, configPath);
            docsPath = Path.Combine(basepath, "docs");

            Console.WriteLine(basepath);
            Console.WriteLine(docsPath);

            if (File.Exists(cp)) {
                config = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(cp));
            }
            Console.WriteLine("Welcome to DocsGenerator! Config loaded, beginning import...");

            string[] files = Directory.GetFiles(basepath, "*.cs", SearchOption.AllDirectories);
            Console.WriteLine($"Found {files.Length} files to index");
            trees = files.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p))).ToArray();
            Console.WriteLine($"Import complete, imported {trees.Length} syntax tress. Beginning indexing");

            DocumentationWalker walker = new();
            foreach (var t in trees)
            {
                walker.Visit(t.GetRoot());
            }
            Console.WriteLine($"Indexing complete, records:");
            Console.WriteLine(records.ToString());

            pathedRecords = records.WithFilePathSettings();
            if (Directory.Exists(Path.Combine(docsPath, pathedRecords.Root.Name))) Directory.Delete(Path.Combine(docsPath, pathedRecords.Root.Name), true);
            pathedRecords.Traverse<object, object>(TransformTree, null, out var _);

            
        }

        public static (object, bool) TransformTree(object _, string path, UnifiedTreeNode<Record> node)
        {
            if (node.HasValue)
            {
                var r = node.Value;
                if ((int)r.Kind < 7)
                {
                    var docMembers = from c in node.Children()
                                     where c.HasValue
                                     let v = c.Value
                                     where (int)v.Kind > 6
                                     select v;
                    string fileContent = r.GetMarkdown(docMembers.ToList()) + credits;
                    string p = Path.Combine(docsPath, node.Path);

                    Directory.CreateDirectory(p);
                    File.WriteAllText(Path.Combine(p, "index.md"), fileContent);
                }
            }
            return (null, false);
        }

        public static (string, bool) SearchTree(string search, string path, UnifiedTreeNode<Record> node)
        {
            if (path.EndsWith(search))
            {
                if (node.HasValue)
                {
                    if ((int)node.Value.Kind > 6)
                    {
                        int lastSegment = path.LastIndexOf(node.Tree.seperator);
                        string fixedPath = path[..lastSegment] + "#" + path[lastSegment..];
                        return (fixedPath, false);
                    }
                }
                return (path, false);
            }
            return (null, false);
        }

        public static string XMLtoMD(XElement xml)
        {
            if (xml == null) return null;

            foreach (var e in xml.Descendants("c")) e.Value = "`" + e.Value + "`";
            foreach (var e in xml.Descendants("code"))
            {
                string lang = e.Attribute("lang")?.Value ?? "cs";
                e.Value = "```" + lang + "\n" + e.Value + "\n```";
            }
            foreach (var e in xml.Descendants("see"))
            {
                string r;
                if ((r = e.Attribute("cref")?.Value) != null)
                {
                    var f = pathedRecords.Traverse(SearchTree, r, out var _);
                    if (f.Count < 1)
                    {
                        e.Value += "<sup>!</sup>";
                    } else if (f.Count > 1)
                    {
                        e.Value += "<sup>?</sup>";
                    } else
                    {
                        e.Value = $"[{e.Value}]({f.First()})";
                    }
                } else if ((r = e.Attribute("href")?.Value) != null)
                {
                    e.Value = $"[{e.Value}]({r})";
                }
            }

            return xml.Value;
        }
    }

    public class DocumentationWalker : CSharpSyntaxWalker
    {
        public override void VisitDocumentationCommentTrivia(DocumentationCommentTriviaSyntax node)
        {
            var n = node.ParentTrivia.Token.Parent;
            if (n is not MemberDeclarationSyntax) n = n.Parent;
            var m = n as MemberDeclarationSyntax;
            Scope s = GetScope(m);
            RecordKind k = GetKind(m);
            Identifier i = GetIdentifier(m);
            Flags f = GetFlags(m);
            string signature = GetSignature(m);

            Console.WriteLine($"{s} {k} {i} ({f}) [{signature}]");
            string xml = node.GetText().ToString().Replace("///", "");
            XDocument doc = XDocument.Parse("<root>" + xml + "</root>");

            Record r = new(i, k, s, f, signature, doc.Root);
            Program.records.AddNode(i.Qualifier, r);
        }

        public DocumentationWalker() : base(SyntaxWalkerDepth.StructuredTrivia) { }

        public static string GetSignature(MemberDeclarationSyntax m)
        {
            string basic = m.WithAttributeLists(new SyntaxList<AttributeListSyntax>()).ToString();

            int semiColonIndex = basic.IndexOf(';');
            int equalsIndex = basic.IndexOf("=");
            int bracketIndex = basic.IndexOf("{");
            int stopIndex = Math.Min(Math.Min(semiColonIndex > 0 ? semiColonIndex : basic.Length, equalsIndex > 0 ? equalsIndex : basic.Length), bracketIndex > 0 ? bracketIndex : basic.Length);

            return basic[..stopIndex].Trim();
        }
        public static string GetQualifiedName(SyntaxNode n)
        {
            string name = n switch
            {
                CompilationUnitSyntax => "",
                NamespaceDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.Name,
                ClassDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.Identifier,
                EnumDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.Identifier,
                InterfaceDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.Identifier,
                DelegateDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.Identifier,
                EventDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.Identifier,
                MethodDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.Identifier,
                PropertyDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.Identifier,
                FieldDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)).First(),
                EnumMemberDeclarationSyntax s => GetQualifiedName(s.Parent) + "." + s.Identifier,
                _ => GetQualifiedName(n.Parent)
            };
            return name.Trim().Trim('.');
        }

        public static Identifier GetIdentifier(SyntaxNode n) => new(GetQualifiedName(n));

        public static RecordKind GetKind(MemberDeclarationSyntax m)
        {
            return m switch
            {
                NamespaceDeclarationSyntax => RecordKind.Namespace,
                ClassDeclarationSyntax => RecordKind.Class,
                EnumDeclarationSyntax => RecordKind.Enum,
                InterfaceDeclarationSyntax   => RecordKind.Interface,
                DelegateDeclarationSyntax => RecordKind.Delegate,
                EventDeclarationSyntax => RecordKind.Event,
                MethodDeclarationSyntax => RecordKind.Method,
                PropertyDeclarationSyntax => RecordKind.Property,
                FieldDeclarationSyntax => RecordKind.Field,
                EnumMemberDeclarationSyntax => RecordKind.EnumMember,
                _ => RecordKind.Unkown
            };
        }

        public static Scope GetScope(MemberDeclarationSyntax m)
        {
            if (m is EnumMemberDeclarationSyntax)
            {
                return GetScope(m.Parent as EnumDeclarationSyntax);
            }
            var firstToken = m.ChildNodesAndTokens().ToList().Find(s => s.IsToken);
            return firstToken.Kind() switch
            {
                SyntaxKind.PublicKeyword => Scope.Public,
                SyntaxKind.PrivateKeyword => Scope.Private,
                SyntaxKind.ProtectedKeyword => Scope.Protected,
                SyntaxKind.InternalKeyword => Scope.Internal,
                _ => Scope.Private
            };
        }

        public static Flags GetFlags(MemberDeclarationSyntax m)
        {
            Flags output = Flags.None;
            var tokens = m.ChildTokens();
            
            if (tokens.Any(t => t.IsKind(SyntaxKind.AbstractKeyword))) output |= Flags.Abstract;
            if (tokens.Any(t => t.IsKind(SyntaxKind.StaticKeyword))) output |= Flags.Static;
            if (tokens.Any(t => t.IsKind(SyntaxKind.OverrideKeyword))) output |= Flags.Override;
            if (tokens.Any(t => t.IsKind(SyntaxKind.VirtualKeyword))) output |= Flags.Virtual;
            if (m.ChildNodes().Any(n => n.IsKind(SyntaxKind.TypeParameterList))) output |= Flags.Generic;

            if (m is MethodDeclarationSyntax)
            {
                ParameterListSyntax p = m.ChildNodes().OfType<ParameterListSyntax>().First();
                if (p.DescendantTokens().Any(t => t.IsKind(SyntaxKind.ThisKeyword))) output |= Flags.Extension;
            }

            return output;
        }
    }

    public class Identifier
    {
        private readonly string[] split;
        public readonly string Qualifier;
        public readonly string Name;

        public string TraverseUp(int depth, bool asPath = false)
        {
            return string.Join(asPath ? '/' : '.', split.SkipLast(depth));
        }

        public string TraverseDown(int depth, bool asPath = false)
        {
            return string.Join(asPath ? '/' : '.', split.Take(depth));
        }

        public int Size => split.Length;

        public Identifier(string raw)
        {
            Qualifier = raw;
            split = Qualifier.Split('.');
            Name = split.Last();
        }

        public override string ToString()
        {
            return Qualifier;
        }
    }

    public class Record
    {
        public readonly Identifier Identifier;
        public readonly RecordKind Kind;
        public readonly Scope Scope;
        public readonly Flags Flags;

        public readonly string Signature;

        public readonly XElement documentation;

        public bool HasFlag(Flags flag) => Flags.HasFlag(flag);

        public Record(Identifier identifier, RecordKind kind, Scope scope, Flags flags, string signature, XElement documentation)
        {
            Identifier = identifier;
            Kind = kind;
            Scope = scope;
            Flags = flags;
            Signature = signature;
            this.documentation = documentation;
            foreach (var d in documentation.Descendants())
            {
                if (d.Value != null)
                {
                    d.Value = d.Value.Trim();
                }
            }
        }

        public string GetMarkdown(List<Record> records = null)
        {
            if ((int)Kind < 7)
            {
                List<Record> relevant = records.Where(r => (int)r.Kind > 6).ToList();

                string output = 
                    $"# {Identifier.Name}\n" +
                    $"*{Signature}*  \n" +
                    $"*{Identifier.Qualifier}*\n\n";

                output += "## Summary\n" + (Program.XMLtoMD(documentation.Element("summary")) ?? "*No summary provided*") + "\n\n";
                var remarks = Program.XMLtoMD(documentation.Element("remarks"));
                if (remarks != null) output += "## Remarks\n" + remarks + "\n\n";

                if (Kind == RecordKind.Enum)
                {
                    output += "## Members\n";
                    foreach (var m in relevant.Where(r => r.Kind == RecordKind.EnumMember))
                    {
                        output += m.GetMarkdown();
                    }
                }
                else
                {
                    List<Record> fields = relevant.Where(r => Kind == RecordKind.Field).OrderBy(r => r.Identifier.Name).ToList();
                    List<Record> properties = relevant.Where(r => Kind == RecordKind.Property).OrderBy(r => r.Identifier.Name).ToList();
                    List<Record> methods = relevant.Where(r => Kind == RecordKind.Method).OrderBy(r => r.Identifier.Name).ToList();

                    if (fields.Count > 0)
                    {
                        output += "## Fields\n";
                        foreach (var f in fields)
                        {
                            output += f.GetMarkdown();
                        }
                    }

                    if (properties.Count > 0)
                    {
                        output += "## Properties\n";
                        foreach (var p in properties)
                        {
                            output += p.GetMarkdown();
                        }
                    }

                    if (methods.Count > 0)
                    {
                        output += "## Methods\n";
                        foreach (var m in methods)
                        {
                            output += m.GetMarkdown();
                        }
                    }
                }
                return output;
            } else if (Kind == RecordKind.Field)
            {
                string output =
                    $"### {Identifier.Name}\n" +
                    $"*{Signature}*  \n" +
                    $"*{Identifier.Qualifier}*\n\n";

                output += "#### Summary\n" + (Program.XMLtoMD(documentation.Element("summary")) ?? "*No summary provided*") + "\n\n";
                var remarks = Program.XMLtoMD(documentation.Element("remarks"));
                if (remarks != null) output += "#### Remarks\n" + remarks + "\n\n";

                return output;
            } else if (Kind == RecordKind.Property)
            {
                string output =
                    $"### {Identifier.Name}\n" +
                    $"*{Signature}*  \n" +
                    $"*{Identifier.Qualifier}*\n\n";

                output += "#### Summary\n" + (Program.XMLtoMD(documentation.Element("summary")) ?? "*No summary provided*") + "\n\n";
                var remarks = Program.XMLtoMD(documentation.Element("remarks"));
                if (remarks != null) output += "#### Remarks\n" + remarks + "\n\n";

                return output;
            } else if (Kind == RecordKind.Method)
            {
                string output =
                    $"### {Identifier.Name}\n" +
                    $"*{Signature}*  \n" +
                    $"*{Identifier.Qualifier}*\n\n";

                output += "#### Summary\n" + (Program.XMLtoMD(documentation.Element("summary")) ?? "*No summary provided*") + "\n\n";
                var remarks = Program.XMLtoMD(documentation.Element("remarks"));
                if (remarks != null) output += "#### Remarks\n" + remarks + "\n\n";

                return output;
            } else if (Kind == RecordKind.EnumMember)
            {
                string output =
                    $"### {Identifier.Name}\n" +
                    $"*{Identifier.Qualifier}*\n\n";

                output += "#### Summary\n" + (Program.XMLtoMD(documentation.Element("summary")) ?? "*No summary provided*") + "\n\n";
                var remarks = Program.XMLtoMD(documentation.Element("remarks"));
                if (remarks != null) output += "#### Remarks\n" + remarks + "\n\n";

                return output;
            } else
            {
                return "";
            }
        } 
    }

    public enum RecordKind // Non-seperation : > 6
    {
        Unkown,
        Namespace,
        Class,
        Enum,
        Interface,
        Delegate,
        Event,
        Method,
        Property,
        Field,
        EnumMember
    }

    public enum Scope
    {
        Public,
        Protected,
        Private,
        Internal
    }

    [Flags]
    public enum Flags
    {
        None = 0,
        Static = 1,
        Abstract = 2,
        Override = 4,
        Virtual = 8,
        Generic = 16,
        Extension = 32
    }
}