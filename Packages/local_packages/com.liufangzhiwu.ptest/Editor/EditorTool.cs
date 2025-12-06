using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class EditorTool : Editor
{
    
    #region 文件夹操作

    
    [MenuItem("Tools/打开特殊文件夹/Persistent Folder")]
    private static void OpenPersistentDataPathFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
    
    [MenuItem("Tools/打开特殊文件夹/StreamingAssets Folder")]
    private static void OpenStreamingAssetsPathFolder()
    {
        Application.OpenURL(@"file://" + Application.streamingAssetsPath);
    }
    #endregion
    

}
