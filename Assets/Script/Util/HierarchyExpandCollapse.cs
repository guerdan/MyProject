#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class HierarchyExpandCollapse : EditorWindow
{
    [MenuItem("Tools/Expand All Hierarchy")]
    static void ExpandAll()
    {
        ExpandOrCollapseAll(true);
    }

    [MenuItem("Tools/Collapse All Hierarchy")]
    static void CollapseAll()
    {
        ExpandOrCollapseAll(false);
    }

    static void ExpandOrCollapseAll(bool expand)
    {
        var hierarchyWindow = GetHierarchyWindow();
        if (hierarchyWindow == null) return;

        var expandMethod = hierarchyWindow.GetType().GetMethod("SetExpandedRecursive", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (expandMethod == null) return;

        foreach (GameObject go in Object.FindObjectsOfType<GameObject>())
        {
            expandMethod.Invoke(hierarchyWindow, new object[] { go.GetInstanceID(), expand });
        }
    }

    static EditorWindow GetHierarchyWindow()
    {
        var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (var window in windows)
        {
            if (window.GetType().Name == "SceneHierarchyWindow")
            {
                return window;
            }
        }
        return null;
    }
}

#endif