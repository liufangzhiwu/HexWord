using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class RankOtherSaveData
{
    /// <summary>
    /// 用户id(具有唯一值)
    /// </summary>
    public int userid;
    /// <summary>
    /// 排名
    /// </summary>
    public int rank;
    /// <summary>
    /// 昵称
    /// </summary>
    public string username;
    /// <summary>
    /// 禅意值
    /// </summary>
    public int realmvalue;
}

public class RankSaveData
{
    public int totalrank;               // 总排名
    public int monthrank;               // 月排名
    public int totalrealmvalue;         // 总共禅意值
    public int monthrealmvalue;         // 月禅意值
    public bool ismonthcliam;             // 月排名是否已经领取奖励
  
    /// 总榜排名数据
    public List<RankOtherSaveData> totalRankSaveDatas=new List<RankOtherSaveData>();
    /// 月榜排名数据
    public List<RankOtherSaveData> monthRankSaveDatas=new List<RankOtherSaveData>();
    
    public string Getfilepath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, "RankSaveData.json");
        }
    }
    
       /// <summary>
    /// 首次启动游戏数据初始化
    /// </summary>
    public void InitData()
    {
        totalrank = 0;
        monthrank = 0;
        totalrealmvalue = 0;
        monthrealmvalue = 0;
        ismonthcliam = false;
        totalRankSaveDatas = new List<RankOtherSaveData>();
        monthRankSaveDatas = new List<RankOtherSaveData>();
    }
       
    /// <summary>
    /// 初始化保存的数据
    /// </summary>
    /// <param name="user"></param>
    public void InitData(RankSaveData rankSaveData)
    {
        totalrank = rankSaveData.totalrank;
        monthrank = rankSaveData.monthrank;
        totalrealmvalue = rankSaveData.totalrealmvalue;
        monthrealmvalue = rankSaveData.monthrealmvalue;
        ismonthcliam = rankSaveData.ismonthcliam;
        totalRankSaveDatas = new List<RankOtherSaveData>();
        monthRankSaveDatas = new List<RankOtherSaveData>();
        totalRankSaveDatas = new List<RankOtherSaveData>(rankSaveData.totalRankSaveDatas);
        monthRankSaveDatas = new List<RankOtherSaveData>(rankSaveData.monthRankSaveDatas);
    }
    
    /// <summary>
    /// 重置月榜数据
    /// </summary>
    public void ResetRankData()
    {
        ismonthcliam = false;
        monthRankSaveDatas.Clear();
        monthrank = 0;
        monthrealmvalue = 0;
    }        
    
    /// <summary>
    /// 添加总榜排名数据
    /// </summary>
    public void AddTotalRankDatas(RankOtherSaveData rankOtherSaveData)
    {
        RankOtherSaveData CurotherData= totalRankSaveDatas.Find(x => x.userid == rankOtherSaveData.userid);
        if (CurotherData==null)
        {
            totalRankSaveDatas.Add(rankOtherSaveData);
        }
        else
        {
            CurotherData.rank=rankOtherSaveData.rank;
            CurotherData.username=rankOtherSaveData.username;
            CurotherData.realmvalue=rankOtherSaveData.realmvalue;
        }
    } 
    
    /// <summary>
    /// 添加月榜排名数据
    /// </summary>
    public void AddMonthRankDatas(RankOtherSaveData rankOtherSaveData)
    {
        RankOtherSaveData CurotherData= monthRankSaveDatas.Find(x => x.userid == rankOtherSaveData.userid);
        if (CurotherData==null)
        {
            monthRankSaveDatas.Add(rankOtherSaveData);
        }
        else
        {
            CurotherData.rank=rankOtherSaveData.rank;
            CurotherData.username=rankOtherSaveData.username;
            CurotherData.realmvalue=rankOtherSaveData.realmvalue;
        }
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
                Debug.Log("排名加载路径: " + filePath + "读取json数据" + Dejson + "解密后数据" + json);
                // 验证 JSON 数据格式
                if (!IsValidJson(json))
                { 
                    Debug.LogError("JSON 格式错误: " + json);
                    InitData();
                }
                else
                {
                    RankSaveData rankSaveData = JsonConvert.DeserializeObject<RankSaveData>(json);               
                    Debug.Log("排名数据已加载: " + json+" 排名回合数据 "+rankSaveData.totalrank);
                    InitData(rankSaveData);
                    Debug.Log("排名回合数据: " + rankSaveData.totalrank);
                    if (rankSaveData.totalrank<=0)
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
   
    
    // 保存数据
    public void SaveData()
    {     
        string filePath = Getfilepath;
        string oldjson = JsonConvert.SerializeObject(this, Formatting.Indented); // 转换为 JSON 格式          
        string json = SecurityProvider.ProtectData(oldjson); //加密
        File.WriteAllText(filePath, json); // 写入文件
        // Debug.Log("用户排名数据已保存: " + json);
    }
   
}



