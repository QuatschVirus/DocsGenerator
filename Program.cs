﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Frozen;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.IO;

namespace DocsGenerator
{
    public static class Program
    {
        const string credits = "\n---\n*Generated by [DocsGenerator](https://github.com/QuatschVirus/DocsGenerator)*";
        const string configPath = "dg-cfg.json";

        static JsonElement config;

        public static SyntaxTree[] trees;
        public static UnifiedTree<Record> records = new("Code Dcoumentation");

        static void Main(string[] args)
        {
            string basepath = (args.Length > 0) ? args[0] : ".";
            string cp = Path.Combine(basepath, configPath);
            if (File.Exists(cp)) {
                config = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(cp));
            }
            Console.WriteLine("Welcome to DocsGenerator! Config loaded, beginning import...");

            string[] files = Directory.GetFiles(basepath, "*.cs");
            trees = files.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p))).ToArray();
            Console.WriteLine($"Import complete, imported {trees.Length} syntax tress. Beginning indexing");

            DocumentationWalker walker = new();
            foreach (var t in trees)
            {
                Console.WriteLine($"Indexing tree for {t.FilePath}");
                walker.Visit(t.GetRoot());
            }
            Console.WriteLine($"Indexing complete, found {records.Count} records");
        }
    }

    public class DocumentationWalker : CSharpSyntaxWalker
    {
        public override void VisitDocumentationCommentTrivia(DocumentationCommentTriviaSyntax node)
        {
            Console.WriteLine(node.Parent.ToString());
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
    }

    public class Record
    {
        public readonly Identifier Identifier;
        public readonly RecordKind Kind;
        public readonly Scope Scope;
        public readonly Flags Flags;

        public readonly string Signature;

        public readonly List<XmlElement> documentation = new();

        public bool HasFlag(Flags flag) => Flags.HasFlag(flag);

        public Record(Identifier identifier, RecordKind kind, Scope scope, Flags flags, string signature, List<XmlElement> documentation)
        {
            Identifier = identifier;
            Kind = kind;
            Scope = scope;
            Flags = flags;
            Signature = signature;
            this.documentation = documentation;
        }
    }

    public enum RecordKind
    {
        Namespace,
        Class,
        Enum,
        Interface,
        Delegate,
        Event,
        Method,
        Property,
        Field
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