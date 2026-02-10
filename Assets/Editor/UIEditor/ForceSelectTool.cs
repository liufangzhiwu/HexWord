using UnityEngine;
using UnityEditor;

public class ForceSelectTool : EditorWindow
{
    [MenuItem("Tools/强制选中 'Water2D_SpawnersManager'")]
    public static void SelectGhostObject()
    {
        // 目标物体名字
        string targetName = "Water2D_SpawnersManager";
        
        // 1. 使用 Resources.FindObjectsOfTypeAll 查找所有物体（包括隐藏的）
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        
        bool found = false;

        foreach (GameObject go in allObjects)
        {
            // 过滤掉资源文件（Prefab Asset），只找场景里的实例
            if (EditorUtility.IsPersistent(go)) continue;

            if (go.name == targetName)
            {
                // 2. 找到了！强制把它的隐身属性去掉
                go.hideFlags = HideFlags.None;
                
                // 3. 选中它
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
                
                // 4. 让它在 Hierarchy 里显示出来
                if (go.transform.parent != null)
                {
                    // 如果它是子物体，展开父节点
                    EditorGUIUtility.PingObject(go.transform.parent);
                }

                Debug.LogWarning($"🎉 抓到了！物体名: {go.name} | 之前状态: {go.hideFlags}");
                Debug.LogWarning("已强制解除它的'隐身'状态，现在你应该能在 Hierarchy 里看到它了。");
                
                found = true;
                break; // 找到一个就停（如果有很多个，去掉 break）
            }
        }

        if (!found)
        {
            Debug.LogError($"依然没找到名为 '{targetName}' 的物体。请确认场景是否已加载。");
        }
    }
}