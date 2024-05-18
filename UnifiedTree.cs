using Microsoft.CodeAnalysis.Host.Mef;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

#nullable enable
namespace DocsGenerator
{
    public delegate (Tout, bool) TraverserDelegate<in Tin, TTree, Tout>(Tin input, string path, UnifiedTreeNode<TTree> node);

    public class UnifiedTree<T>
    {
        public readonly char seperator = '.';
        public readonly string upwards = "#";
        public readonly string horizontal = "";
        public UnifiedTreeNode<T> Root { get; protected set; }

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
            UnifiedTree<T> newTree = new(Root.Name, seperator, upwards, horizontal);
            newTree.Root = Root.Clone(newTree, null);
            return newTree;
        }

        public UnifiedTree<T> WithFilePathSettings() => WithSettings('/', "..", ".");

        public override string ToString()
        {
            return $"Unified Tree ({Count}):\n" + Root.ToString();
        }

        public UnifiedTreeNode<T>? GetNode(string path)
        {
            return Root[path];
        }

        public int Count => Root.Count;

        public void AddNode(string path, T? value)
        {
            Root.EnsureNode(path.Split(seperator), value);
        }

        public List<Tout> Traverse<Tin, Tout>(TraverserDelegate<Tin, T, Tout> @delegate, Tin input, out bool canceled, bool includeNull = false)
        {
            List<Tout> result = new();
            canceled = Root.Traverse(@delegate, input, result, includeNull);
            return result;
        }
    }
    public class UnifiedTreeNode<T>
    {
        public UnifiedTree<T> Tree { get; protected set; }

        public UnifiedTreeNode<T>? Parent { get; protected set; }
        protected readonly Dictionary<string, UnifiedTreeNode<T>> children = new();

        public string Name;

        protected T? value;
        protected bool hasValue;

        public bool HasValue => hasValue;

        public T? Value
        {
            get => value;
            set
            {
                hasValue = true;
                this.value = value;
            }
        }

        public UnifiedTreeNode<T> Clone(UnifiedTree<T> forTree, UnifiedTreeNode<T>? parent)
        {
            UnifiedTreeNode<T> node = new(Name, Value, forTree, parent)
            {
                hasValue = HasValue
            };
            foreach (var c in Children())
            {
                c.Clone(forTree, node);
            }
            return node;
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
            return Parent.GetPath(seperator) + Tree.seperator + Name;
        }

        public string Path => GetPath(Tree.seperator);

        public IEnumerable<UnifiedTreeNode<T>> Children() => children.Values;
        public IEnumerable<UnifiedTreeNode<T>> Descendants() => Children().Concat(Children().SelectMany(c => c.Descendants()));

        public int Count => children.Values.Sum(x => x.Count) + 1;

        public UnifiedTreeNode<T>? this[string path] => GetNode(path);

        public string ToString(int depth)
        {
            string output = string.Concat(Enumerable.Repeat("| ", depth)) + Name + (hasValue ? " *" : "") + "\n";
            foreach (var child in Children())
            {
                output += child.ToString(depth + 1);
            }
            return output;
        }

        public override string ToString()
        {
            return ToString(0);
        }

        public void EnsureNode(string[] path, T? value)
        {
            if (path.Length == 0)
            {
                Value = value;
            } else
            {
                UnifiedTreeNode<T>? n = GetNode(path.First());
                if (n == null)
                {
                    new UnifiedTreeNode<T>(path.First(), Tree, this).EnsureNode(path[1..], value);
                }
                else
                {
                    n.EnsureNode(path[1..], value);
                }
            }
        }

        public bool Traverse<Tin, Tout>(TraverserDelegate<Tin, T, Tout> @delegate, Tin input, List<Tout> results, bool includeNull)
        {
            (Tout result, bool cancel) = @delegate(input, Path, this);
            if (result != null || includeNull)
            {
                results.Add(result);
            }

            if (cancel) return true;
            foreach (var child in Children())
            {
                if (child.Traverse(@delegate, input, results, includeNull)) return true;
            }
            return false;
        }

        public void Retree(UnifiedTree<T> tree)
        {
            Tree = tree;
        }
    }
}
