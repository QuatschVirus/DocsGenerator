using Microsoft.CodeAnalysis.Host.Mef;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace DocsGenerator
{
    public class UnifiedTree<T>
    {
        public readonly char seperator = '.';
        public readonly string upwards = "#";
        public readonly string horizontal = "";
        public readonly UnifiedTreeNode<T> Root;

        public UnifiedTree(UnifiedTreeNode<T> root, char seperator = '.', string upwards = "#", string horizontal = "")
        {
            this.seperator = seperator;
            this.upwards = upwards;
            this.horizontal = horizontal;
            Root = root;
        }

        public UnifiedTree(string rootName, char seperator = '.', string upwards = "#", string horizontal = "")
        {
            this.seperator = seperator;
            this.upwards = upwards;
            this.horizontal = horizontal;
            Root = new(rootName, this, null);
        }

        public UnifiedTree<T> WithSettings(char seperator, string upwards, string horizontal)
        {
            return new(Root, seperator, upwards, horizontal);
        }

        public UnifiedTree<T> WithFilePathSettings() => WithSettings('/', "..", ".");

        public override string ToString()
        {
            return "Unified Tree:\n" + Root.ToString();
        }

        public UnifiedTreeNode<T>? GetNode(string path)
        {
            return Root[path];
        }

        public int Count => Root.Count;

        public void AddNode(string path, UnifiedTreeNode<T> node)
        {
            Root.EnsureNode(path.Split(seperator).SkipLast(1).ToArray(), node);
        }
    }

    public class UnifiedTreeNode<T>
    {
        public readonly UnifiedTree<T> Tree;

        public UnifiedTreeNode<T>? Parent { get; protected set; }
        protected readonly Dictionary<string, UnifiedTreeNode<T>> children = new();

        public string Name;

        protected T? value;
        protected bool hasValue;

        public T? Value
        {
            get => value;
            set
            {
                hasValue = true;
                this.value = value;
            }
        }

        public void ClearValue()
        {
            value = default;
            hasValue = false;
        }

        public UnifiedTreeNode(string name, UnifiedTree<T> tree, UnifiedTreeNode<T>? parent)
        {
            Tree = tree;
            Name = name;
            Parent = parent;
            Parent?.AddChild(this);
            ClearValue();
        }

        public UnifiedTreeNode(string name, T? value, UnifiedTree<T> tree, UnifiedTreeNode<T>? parent)
        {
            Tree = tree;
            Name = name;
            Parent = parent;
            Parent?.AddChild(this);
            Value = value;
        }

        public void Migrate(UnifiedTreeNode<T>? newParent)
        {
            Parent?.RemoveChild(this);
            Parent = newParent;
            Parent?.AddChild(this);
        }

        public void AddChild(UnifiedTreeNode<T> child)
        {
            children.Add(child.Name, child);
        }

        public void RemoveChild(UnifiedTreeNode<T> child)
        {
            children.Remove(child.Name);
        }

        public UnifiedTreeNode<T>? GetNode(string path) => GetNode(path.Split(Tree.seperator));

        public UnifiedTreeNode<T>? GetNode(string[] path)
        {
            if (path.Length == 0) return this;
            string primary = path.First();
            if (primary == Tree.upwards)
            {
                return Parent?.GetNode(path[1..]);
            } else if (primary == Tree.horizontal)
            {
                return GetNode(path[1..]);
            } else
            {
                if (children.TryGetValue(primary, out var child))
                {
                    return child.GetNode(path[1..]);
                } else
                {
                    return null;
                }
            }
        }

        public string GetPath(char seperator)
        {
            if (Parent == null) return Name;
            return Parent.GetPath(seperator) + "." + Name;
        }

        public string Path => GetPath(Tree.seperator);

        public IEnumerable<UnifiedTreeNode<T>> Children() => children.Values;
        public IEnumerable<UnifiedTreeNode<T>> Descendants() => Children().Concat(Children().SelectMany(x => x.Descendants()));

        public int Count => children.Values.Sum(x => x.Count) + 1;

        public UnifiedTreeNode<T>? this[string path] => GetNode(path);

        public string ToString(int depth)
        {
            return string.Concat(Enumerable.Repeat("| ", depth)) + Name + (hasValue ? " *" : "") + "\n";
        }

        public override string ToString()
        {
            return ToString(0);
        }

        public void EnsureNode(string[] path, UnifiedTreeNode<T> node)
        {
            if (path.Length == 0) node.Migrate(this);
            UnifiedTreeNode<T>? n = GetNode(path.First());
            if (n == null)
            {
                new UnifiedTreeNode<T>(path.First(), Tree, this).EnsureNode(path[1..], node);
            } else
            {
                n.EnsureNode(path[1..], node);
            }
        }
    }
}
