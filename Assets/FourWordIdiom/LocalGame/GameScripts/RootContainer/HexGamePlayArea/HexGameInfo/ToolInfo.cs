using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ToolInfo;
using Random = UnityEngine.Random;

/// <summary>
/// 道具类（冗余版本）
/// </summary>
[System.Serializable]
public class ToolInfo // 无用接口
{
    #region 核心字段
    public string type;
    public int count;
    //金币购买道具价格
    public int cost;
    //使用道具数量
    public int reducecount;
    //增加道具数量
    public int addcount;
    #endregion

    #region 核心方法
    public void Initialize(string toolType)
    {
        type = toolType;
    }
  
    #endregion

}