using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class FishAISaveData
{
    public int aiid;
    public string ainame;
    public int ailevel;
    public int Puzzleprogress;
    /// <summary>
    /// AI进度所用时长
    /// </summary>
    public int updatePuzzleusetime;
    /// <summary>
    /// 是否已经领取奖励
    /// </summary>
    public bool iscliam;
    /// <summary>
    /// 是否已经领取奖励
    /// </summary>
    public int rank;

    /// <summary>
    /// 更新竞赛活动进度
    /// </summary>
    public void UpdateFishProgress(int progress)
    {
        Puzzleprogress += progress;
        ailevel++;
        if(Puzzleprogress>=100)
            Puzzleprogress = 100;
    }

    public void UpdatePassLvTime(int second)
    {
        updatePuzzleusetime=second;
    }
}

public class FishUserSaveData
{
    public int Puzzleprogress;
    /// <summary>
    /// 竞速活动开启时间
    /// </summary>
    public string opentime;
    /// <summary>
    /// 竞速活动关闭时间
    /// </summary>
    public string cloestime;
    /// <summary>
    /// 回合开始时间
    /// </summary>
    public string roundstarttime;
    /// <summary>
    /// 是否已经领取奖励
    /// </summary>
    public bool iscliam;
    /// <summary>
    /// 当前回合是否结束
    /// </summary>
    public bool isRoundOver;
    /// <summary>
    /// 是否已经领取奖励
    /// </summary>
    public int rank;
    /// <summary>
    /// 当前轮次
    /// </summary>
    public int curround;
    /// <summary>
    /// 回合比赛用时
    /// </summary>
    public int updatePuzzleusetime;
    /// <summary>
    /// 比赛次数
    /// </summary>
    public int matchCount;
    /// <summary>
    /// ai竞速数据
    /// </summary>
    public List<FishAISaveData> aiSaveDatas=new List<FishAISaveData>();
    
    public string Getfilepath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, "FishUserSaveData.json");
        }
    }
    
       /// <summary>
    /// 首次启动游戏数据初始化
    /// </summary>
    public void InitData()
    {
        Puzzleprogress = 0;
        opentime = "";
        cloestime = "";
        roundstarttime = "";
        iscliam = false;
        isRoundOver = false;
        rank = 0;
        curround = 1;
        matchCount = 0;
        aiSaveDatas = new List<FishAISaveData>();
    }
       
    /// <summary>
    /// 初始化保存的数据
    /// </summary>
    /// <param name="user"></param>
    public void InitData(FishUserSaveData fishUserSaveData)
    {
        Puzzleprogress = fishUserSaveData.Puzzleprogress;
        opentime = fishUserSaveData.opentime;
        cloestime = fishUserSaveData.cloestime;
        roundstarttime = fishUserSaveData.roundstarttime;
        iscliam = fishUserSaveData.iscliam;
        isRoundOver = fishUserSaveData.isRoundOver;
        rank = fishUserSaveData.rank;
        curround = fishUserSaveData.curround;
        matchCount = fishUserSaveData.matchCount;
        if(aiSaveDatas!=null)
            aiSaveDatas = new List<FishAISaveData>(fishUserSaveData.aiSaveDatas);
    }

    public void OpenRoundTime()
    {
        roundstarttime=DateTime.Now.ToString();
        
        Puzzleprogress=0;
        rank = 0;
        matchCount++;
        if (aiSaveDatas != null)
        {
            aiSaveDatas.Clear();
        }
    }
    
    /// <summary>
    /// 重置回合结束竞速数据
    /// </summary>
    public void ResetFishData()
    {
        roundstarttime="";
        isRoundOver = false;
        iscliam = false;
        aiSaveDatas.Clear();
        rank = 0;
        updatePuzzleusetime = 0;
    }        
   
    
    /// <summary>
    /// 加载数据 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    public void LoadData()
    {           
        string filePath = Getfilepath;

        try
        {
            if (File.Exists(filePath))
            {
                string Dejson = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
           
                string json = SecurityProvider.RestoreData(Dejson); //解密   
                Debug.Log("数据加载路径: " + filePath + "读取json数据" + Dejson + "解密后数据" + json);
                // 验证 JSON 数据格式
                if (!IsValidJson(json))
                { 
                    Debug.LogError("JSON 格式错误: " + json);
                    InitData();
                }
                else
                {
                    FishUserSaveData fishUser = JsonConvert.DeserializeObject<FishUserSaveData>(json);               
                    Debug.Log("竞速数据已加载: " + json+" 竞速回合数据 "+fishUser.curround);
                    InitData(fishUser);
                    Debug.Log("竞速回合数据: " + fishUser.curround);
                    if (fishUser.curround<=0)
                    { 
                        Debug.Log("数据加载异常: " + json);
                        InitData();
                    }
                }
            }
            else
            {
                Debug.LogWarning("没有找到数据文件, 返回默认数据.");
                InitData();
            }      
        }
        catch (Exception e)
        {
            Console.WriteLine("竞速数据加载失败"+e);
            InitData();
        }
        
              
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

    /// <summary>
    /// 更新竞赛活动进度
    /// </summary>
    public void UpdateFishProgress(int progress)
    {
        if(string.IsNullOrEmpty(roundstarttime)) return;
        
        Puzzleprogress += progress;
        
        if (Puzzleprogress >= 100)
        {
            Puzzleprogress = 100;
            updatePuzzleusetime = (int)DateTime.Now.Subtract(DateTime.Parse(roundstarttime)).TotalSeconds;
        }

        if (Puzzleprogress <=0)
        {
            Puzzleprogress = 0;
        }
    }
    
    /// <summary>
    /// 更新竞赛回合
    /// </summary>
    public void UpdateRound(int value)
    {
        curround += value;
        if (curround >= 5)
        {
            curround = 5;
        }

        if (curround <= 1)
        {
            curround = 1;
        }
    }
    
    // 保存数据
    public void SaveData()
    {     
        string filePath = Getfilepath;
        string oldjson = JsonConvert.SerializeObject(this, Formatting.Indented); // 转换为 JSON 格式          
        string json = SecurityProvider.ProtectData(oldjson); //加密
        File.WriteAllText(filePath, json); // 写入文件
        Debug.Log("用户竞速数据已保存: " + json);
        //PlayerPrefs.SetString(path, JsonMapper.ToJson(data));
    }
   
}



