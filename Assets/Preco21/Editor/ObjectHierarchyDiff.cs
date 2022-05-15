using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Preco21;

public class ObjectHierarchyDiff : EditorWindow
{
    private static string LOG_TAG = "[ObjectHierarchyDiff]:";
    private enum ExtractDiffType
    {
        BASE,
        TARGET
    };

    private GameObject baseObject = null;
    private GameObject targetObject = null;
    private ExtractDiffType extractDiffType = ExtractDiffType.BASE;
    private string additionPrefix = "__added";
    private string deletionPrefix = "__deleted";

    [MenuItem("ObjectHierarchyDiff/ObjectHierarchyDiff")]
    public static void ShowWindow()
    {
        GetWindow<ObjectHierarchyDiff>().Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Setup", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Base Object");
        baseObject = EditorGUILayout.ObjectField(baseObject, typeof(GameObject), true) as GameObject;
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Target Object");
        targetObject = EditorGUILayout.ObjectField(targetObject, typeof(GameObject), true) as GameObject;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label("Options", EditorStyles.boldLabel);
        extractDiffType = (ExtractDiffType)EditorGUILayout.Popup("Extract Diffs On", (int)extractDiffType, new string[] { "Base (deletion: -)", "Target (addition: +)" });

        switch (extractDiffType)
        {
            case ExtractDiffType.BASE:
                deletionPrefix = EditorGUILayout.TextField("Deletion Prefix", deletionPrefix);
                break;
            case ExtractDiffType.TARGET:
                additionPrefix = EditorGUILayout.TextField("Addition Prefix", additionPrefix);
                break;
            default:
                throw new Exception("invariant: unexpected `ExtractDiffType`");
        }

        GUILayout.Space(15);

        if (GUILayout.Button("Run"))
        {
            var (valid, message) = Validate();
            if (valid)
            {
                Run();
            }
            else
            {
                Debug.LogError($"{LOG_TAG} invariant: {message}");
            }
        }
    }

    private (bool, string) Validate()
    {
        if (baseObject == null)
        {
            return (false, "`Base Object` must be specified.");
        }
        if (targetObject == null)
        {
            return (false, "`Target Object` must be specified.");
        }
        if (extractDiffType == ExtractDiffType.BASE && deletionPrefix.Trim().Equals(""))
        {
            return (false, "`Deletion Prefix` must be specified.");
        }
        if (extractDiffType == ExtractDiffType.TARGET && additionPrefix.Trim().Equals(""))
        {
            return (false, "`Addition Prefix` must be specified.");
        }
        return (true, "");
    }

    private void Run()
    {
        var leftNode = ConvertGameObjectToNode(baseObject);
        var rightNode = ConvertGameObjectToNode(targetObject);
        var diff = DiffHelper.Compare<GameObject>(leftNode, rightNode);
        if (diff.Count < 1)
        {
            Debug.LogWarning($"{LOG_TAG} No diffs found.");
            return;
        }
        DiffHelper.Node<GameObject> extractTo;
        List<DiffHelper.Record<GameObject>> subsetDiff;
        string prefix;
        switch (extractDiffType)
        {
            case ExtractDiffType.BASE:
                Debug.Log($"{LOG_TAG} Extracting Diffs on Base Object...");
                extractTo = leftNode;
                subsetDiff = diff.Where((e) => e.Type == DiffHelper.Type.DELETE).ToList();
                prefix = deletionPrefix;
                break;
            case ExtractDiffType.TARGET:
                Debug.Log($"{LOG_TAG} Extracting Diffs on Target Object...");
                extractTo = rightNode;
                subsetDiff = diff.Where((e) => e.Type == DiffHelper.Type.INSERT).ToList();
                prefix = additionPrefix;
                break;
            default:
                throw new Exception("invariant: unexpected `ExtractDiffType`");
        }
        Undo.RegisterFullObjectHierarchyUndo(extractTo.Value, $"ObjectHierarchyDiff: Run on `{extractTo.Value.name}`");
        void traverse(DiffHelper.Node<GameObject> node, List<string> path)
        {
            // FIXME: except root
            var currentPath = path.Count < 1 ? path.Append(node.Key).ToList() : path;
            var pathWithoutRoot = currentPath.Skip(1).ToList();
            foreach (var d in subsetDiff)
            {
                var diffPath = d.Path.Skip(1);
                if (Enumerable.SequenceEqual(pathWithoutRoot, diffPath))
                {
                    node.Value.name = $"{node.Value.name}{prefix}";
                }
            }
            foreach (var obj in node.Children)
            {
                traverse(obj, currentPath.Append(obj.Key).ToList());
            }
        }
        traverse(extractTo, new List<string>());
    }

    private DiffHelper.Node<GameObject> ConvertGameObjectToNode(GameObject gameObject, DiffHelper.Node<GameObject>? anchor = null)
    {
        var node = anchor ?? new DiffHelper.Node<GameObject>
        {
            Key = gameObject.name,
            Value = gameObject,
            Children = new List<DiffHelper.Node<GameObject>>(),
        };
        Transform[] children = gameObject.transform.GetComponentsInChildren<Transform>();
        foreach (var obj in children)
        {
            // for whatever reasons, Unity yields children nodes in awkward way, in which a node contains all the descendants
            // to around this, we need to check for some conditions here
            if (obj.gameObject == gameObject || obj.parent.gameObject != gameObject)
            {
                continue;
            }
            var newNode = new DiffHelper.Node<GameObject>
            {
                Key = obj.name,
                Value = obj.gameObject,
                Children = new List<DiffHelper.Node<GameObject>>(),
            };
            node.Children.Add(ConvertGameObjectToNode(obj.gameObject, newNode));
        }
        return node;
    }
}
