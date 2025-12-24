using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.VisualScripting.Antlr3.Runtime.Collections;
using UnityEngine;
/**
 * 蝴蝶养成数据
 */
[Serializable]
public class ButterflyData
{
    public int pupa = 1;   // 蛹的总数量

    public int currPupa;  // 当前蝶园收集的蛹
    public int currGarden; // 当前选择的蝶园
    public int intervalLv;   // 经过的关卡
    
    public HashSet<int> butterflies;  // 收集的蝴蝶
    public HashSet<int> gardens;      // 开启的蝶园

    public string Getfilepath => Path.Combine(Application.persistentDataPath, "ButterflyData.json");

    #region 数据加载

    public void InitData()
    {
        this.pupa = 0;
        this.currPupa = 0;
        this.currGarden = 1;
        this.intervalLv = 0;
        this.butterflies = new HashSet<int>();
        this.gardens = new HashSet<int>();
        gardens.Add(1);
    }

    public void InitData(ButterflyData data)
    {
        pupa = data.pupa;
        currPupa = data.currPupa;
        currGarden = data.currGarden;
        intervalLv = data.intervalLv;
        butterflies = data.butterflies;
        gardens = data.gardens;
    }
    
    public void LoadData()
    {
        try
        {
            if (File.Exists(Getfilepath))
            {
                string decrypted = File.ReadAllText(Getfilepath, System.Text.Encoding.UTF8);
                string json = SecurityProvider.RestoreData(decrypted);
                if (!IsValidJson(json))
                {
                    Debug.LogError("蝴蝶养成文件 JSON格式错误：" + json);
                    InitData();
                }
                else
                {
                    ButterflyData butterflyData = JsonConvert.DeserializeObject<ButterflyData>(json);
                    if (butterflyData == null)
                    {
                        Debug.Log("蝴蝶数据加载异常: " + json);
                        InitData();
                    }
                    else
                    {
                        InitData(butterflyData);
                    }
                }
            }
            else
            {
                Debug.LogWarning("蝴蝶养成没有找到数据文件, 返回默认数据.");
                InitData();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("蝴蝶养成数据加载失败: " + e);
            InitData();
        }
    }

    public void SaveData()
    {
       string oldJson = JsonConvert.SerializeObject(this, Formatting.Indented);
       string json = SecurityProvider.ProtectData(oldJson);
       File.WriteAllText(Getfilepath, json);
    }
    
    public bool IsValidJson(string json)
    {
        try
        {
            // 尝试解析 JSON 数据，若格式错误会抛出异常
            JToken.Parse(json);
            return true; // JSON 格式正确
        }
        catch (JsonException)
        {
            return false; // JSON 格式错误
        }
    }
    #endregion
    
    public void AddPupa(int num)
    {
        this.pupa += num;
        this.currPupa += num;
        SaveData();
    }

    public void IncrementGarden()
    {
        this.currGarden++;
    }

    public void AddGarden(int gardenId)
    {
        this.currGarden = gardenId;
        gardens.Add(gardenId);
        SaveData();
    }

    public void AddButterfly(int butterfly)
    {
        this.butterflies.Add(butterfly);
        SaveData();
    }

    public void DecreasePupa(int num)
    {
        this.currPupa -= num;
        if(this.currPupa < 0)
            this.currPupa = 0;
        
        SaveData();
    }

    public void SelectGarden(int num)
    {
        this.currGarden = num;
        SaveData();
    }
}
