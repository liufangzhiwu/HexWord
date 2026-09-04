using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FindMissingScripts
{
    [MenuItem("Tools/Find Missing Scripts")]
    static void Find()
    {
        // 获取所有加载的 GameObject（包括隐藏的）
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        List<GameObject> objectsWithIssues = new List<GameObject>();

        foreach (GameObject go in allObjects)
        {
            // 如果你想查找隐藏对象上的丢失脚本，可以删除下面这行 if 判断
            if (go.hideFlags == HideFlags.None)
            {
                foreach (Component component in go.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        objectsWithIssues.Add(go);
                        break;
                    }
                }
            }
        }

        if (objectsWithIssues.Count > 0)
        {
            Debug.LogWarning($"找到 {objectsWithIssues.Count} 个游戏对象存在丢失的脚本引用。");
            foreach (GameObject go in objectsWithIssues)
            {
                string info = GetDetailedInfo(go);
                // 第二个参数传入 go，这样点击日志时可以在 Hierarchy 中高亮该对象
                Debug.Log(info, go);
            }
        }
        else
        {
            Debug.Log("当前场景中没有发现丢失脚本的对象。");
        }
    }

    static string GetDetailedInfo(GameObject go)
    {
        string path = GetGameObjectPath(go);
        string info = $"对象: {go.name} (层级路径: {path})";

        // ① 判断是否为预制体实例（即场景中由预制体生成的）
        if (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            // 获取该实例对应的预制体资产路径
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                info += $" | 预制体来源: {prefabPath}";
            }
            else
            {
                info += " | 预制体来源: (未知，可能为嵌套预制体)";
            }
        }
        // ② 判断是否为预制体资源本身（即你在 Project 窗口中双击打开的那个）
        else if (PrefabUtility.IsPartOfPrefabAsset(go))
        {
            string assetPath = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(assetPath))
            {
                info += $" | 预制体资产路径: {assetPath}";
            }
        }
        // ③ 普通场景对象
        else
        {
            if (go.scene.IsValid())
            {
                info += $" | 所属场景: {go.scene.name}";
            }
            else
            {
                info += " | (该对象不在任何场景中)";
            }
        }

        return info;
    }

    static string GetGameObjectPath(GameObject obj)
    {
        string path = "/" + obj.name;
        Transform current = obj.transform;
        while (current.parent != null)
        {
            current = current.parent;
            path = "/" + current.name + path;
        }
        return path;
    }
}