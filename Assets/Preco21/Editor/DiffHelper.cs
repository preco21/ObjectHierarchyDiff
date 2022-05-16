using System;
using System.Linq;
using System.Collections.Generic;

namespace Preco21
{
    // simple O(nm) diffing algorithm helper; this has a limitation where it cannot cope with multiple same keys in same level of siblings.
    // in order to around such a case will require LCS to find edit distance between other elements for generating diffs which is in general, more complex to implement.
    // however, the number of nodes in Unity's GameObject tree is generally < 1000, so this shouldn't be that problematic for general use cases. 
    public class DiffHelper
    {
        public enum Type
        {
            NONE,
            INSERT,
            DELETE,
            CHANGE,
        }

        public struct Node<T>
        {
            public string Key;
            public T Value;
            public List<Node<T>> Children;
        }

        public struct Record<T>
        {
            public Type Type;
            public List<string> Path;
            public T Value;
        }

        public static T? GetValueOrNull<T>(List<T> a, Predicate<T> predicate) where T : struct
        {
            var index = a.FindIndex(predicate);
            return index < 0 ? (T?)null : a[index];
        }

        public static List<Record<T>> Compare<T>(Node<T>? a, Node<T>? b, List<string> _path = null)
        {
            var path = _path ?? new List<string>();
            var records = new List<Record<T>>();
            // insertion
            if (!a.HasValue && b.HasValue)
            {
                var bVal = b.GetValueOrDefault();
                var newPath = path.Append(bVal.Key).ToList();
                var recInsert = new Record<T>
                {
                    Type = Type.INSERT,
                    Path = newPath,
                    Value = bVal.Value,
                };
                records.Add(recInsert);
                foreach (var node in bVal.Children)
                {
                    records.AddRange(Compare<T>(null, node, newPath));
                }
                return records;
            }
            // deletion
            if (a.HasValue && !b.HasValue)
            {
                var aVal = a.GetValueOrDefault();
                var newPath = path.Append(aVal.Key).ToList();
                var recDelete = new Record<T>
                {
                    Type = Type.DELETE,
                    Path = newPath,
                    Value = aVal.Value,
                };
                records.Add(recDelete);
                foreach (var node in aVal.Children)
                {
                    records.AddRange(Compare<T>(node, null, newPath));
                }
                return records;
            }
            // changes
            if (a.HasValue && b.HasValue)
            {
                var aVal = a.GetValueOrDefault();
                var bVal = b.GetValueOrDefault();
                // both values are same; just using `a` for record fields
                var newPath = path.Append(aVal.Key).ToList();
                var recChange = new Record<T>
                {
                    Type = Type.CHANGE,
                    Path = newPath,
                    Value = aVal.Value,
                };
                records.Add(recChange);
                var left = aVal.Children.Select<Node<T>, (Node<T>?, Node<T>?)>((nodeA) => (nodeA, GetValueOrNull(bVal.Children, (nodeB) => nodeA.Key == nodeB.Key)));
                var right = bVal.Children.Select<Node<T>, (Node<T>?, Node<T>?)>((nodeB) => (GetValueOrNull(aVal.Children, (nodeA) => nodeB.Key == nodeA.Key), nodeB));
                var combined = left.Concat(right).GroupBy((tuple) => new { tuple.Item1, tuple.Item2 }).Select((node) => node.First());
                foreach (var tuple in combined)
                {
                    records.AddRange(Compare(tuple.Item1, tuple.Item2, newPath));
                }
                return records;
            }
            throw new Exception("invariant: at least one node must not be null");
        }
    }
}