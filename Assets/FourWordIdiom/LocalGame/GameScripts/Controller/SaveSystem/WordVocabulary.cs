using System.Collections;
using System.Collections.Generic;
//using Beebyte.Obfuscator;
using UnityEngine;

public class WordVocabulary<T>
{
    /// <summary>
    /// 关卡内词语（生词本）
    /// </summary>
    //[SkipRename]
    public List<T> LevelWords { get; set; } = new List<T>();

    /// <summary>
    /// 用户词库
    /// </summary>
    //[SkipRename]
    public List<T> UserNotes { get; set; } = new List<T>();
}



