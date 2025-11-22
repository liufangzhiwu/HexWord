using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


public class CrayLevelBuilder : EditorWindow
{
    [MenuItem("Tools/成语关卡烘培器")]
    static void OpenWin() => GetWindow<CrayLevelBuilder>("成语关卡烘培器");

    /* 默认路径 ************ 按自己实际路径改 ************ */
    private const string JSON_PATH = "Assets/MindWordPlay/AssetFiles/Localization/ChineseSimplified/cypzData.json";
    private const string SO_PATH = "Assets/MindWordPlay/AssetFiles/Objects/ChessPackInfo.asset";

    private TextAsset jsonFile;
    private ChessPackInfo targetSO;

    private void OnEnable()
    {
        jsonFile = AssetDatabase.LoadAssetAtPath<TextAsset>(JSON_PATH);
        targetSO = AssetDatabase.LoadAssetAtPath<ChessPackInfo>(SO_PATH);
    }
    private void OnGUI()
    {
        GUILayout.Label("1. 把 JSON 拖进来（可选，覆盖默认路径）");
        jsonFile = (TextAsset) EditorGUILayout.ObjectField("JSON 文件", jsonFile, typeof(TextAsset), false);

        GUILayout.Label("2. 把/或创建 SO 拖进来");
        targetSO = (ChessPackInfo) EditorGUILayout.ObjectField("配置 SO", targetSO, typeof(ChessPackInfo), false);

        GUILayout.Space(5);
        if (GUILayout.Button("③ 一键烘焙"))
        {
            if (!jsonFile)
            {
                EditorUtility.DisplayDialog("错误", "请先拖入 JSON 文件! ", "确定");
                return;
            }

            if (!targetSO)
            {
                targetSO = CreateInstance<ChessPackInfo>();
                AssetDatabase.CreateAsset(targetSO, SO_PATH);
                AssetDatabase.SaveAssets();
            }

            targetSO.BuildFromJson(jsonFile.text);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("完成", "成语关卡已烘培进 SO! ", "确定");
        }

        GUILayout.Space(10);
        if(GUILayout.Button("打开 SO 资产"))
            Selection.activeObject = targetSO;
    }
}
