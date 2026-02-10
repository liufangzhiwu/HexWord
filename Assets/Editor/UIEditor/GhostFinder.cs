using UnityEngine;
using UnityEditor;

public class HiddenObjectRevealer : EditorWindow
{
    [MenuItem("Tools/显形所有 Water2DParticles 物体")]
    public static void RevealSpecificHiddenObjects()
    {
        // 1. 获取内存中所有的 GameObject (包括隐藏的)
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        int count = 0;

        Undo.IncrementCurrentGroup(); // 支持撤销操作
        string undoName = "Reveal Hidden Objects";

        foreach (GameObject go in allObjects)
        {
            // --- 过滤条件 A: 排除资源文件 ---
            // 我们只找场景里的物体，不改 Project 里的 Prefab
            if (EditorUtility.IsPersistent(go.transform.root.gameObject))
                continue;

            // --- 过滤条件 B: 排除 Unity 内部不可见的物体 ---
            // 确保它是属于当前有效场景的
            if (!go.scene.IsValid())
                continue;

            // --- 过滤条件 C: 匹配名字 ---
            // 只要名字里包含这个关键词，就把它弄出来
            if (go.name.Contains("Water"))
            {
                // 记录撤销，万一你想变回去
                Undo.RegisterCompleteObjectUndo(go, undoName);

                // 🔥 核心操作: 清除所有隐藏标记
                // HideFlags.None = 正常显示、可保存、可编辑
                go.hideFlags = HideFlags.None;
                
                // 顺便把它是 Active 也打开，防止它虽然显形了但是是灰的看不清
                // go.SetActive(true); // 如果你只想在层级看到它但不想激活它，注释掉这行

                Debug.Log($"<color=cyan>已显形物体: {go.name}</color> (原本藏在: {GetPath(go.transform)})", go);
                count++;
            }
        }
        
        Undo.SetCurrentGroupName(undoName);

        // 刷新 Hierarchy 窗口
        EditorApplication.RepaintHierarchyWindow();

        if (count > 0)
        {
            EditorUtility.DisplayDialog("搜索完成", 
                $"成功找到了 {count} 个隐藏的 Water2DParticles 物体！\n\n它们现在应该出现在 Hierarchy 面板中了。\n(如果没看到，请清除 Hierarchy 搜索栏)", 
                "好的");
            
            // 尝试选中找到的第一个
            foreach (GameObject go in allObjects)
            {
                if (go != null && go.name.Contains("Water2DParticlesID") && !EditorUtility.IsPersistent(go.transform.root.gameObject))
                {
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                    break;
                }
            }
        }
        else
        {
            EditorUtility.DisplayDialog("搜索完成", "当前场景没有找到包含 'Water2DParticlesID' 的隐藏物体。", "关闭");
        }
    }

    // 获取路径的辅助函数
    static string GetPath(Transform t)
    {
        if (t.parent == null) return t.name;
        return GetPath(t.parent) + "/" + t.name;
    }
}