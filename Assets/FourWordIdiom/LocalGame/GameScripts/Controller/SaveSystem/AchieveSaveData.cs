using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 成就数据容器（支持加密存储与深拷贝）
/// </summary>
public class AchieveSaveDatas
{
    // ---------- 数据字段 ----------
    public List<AchieveSaveData> achieveSaveDatalist = new List<AchieveSaveData>();
    public List<AchieveSaveData> finishAchieveList = new List<AchieveSaveData>();

    // ---------- 文件路径 ----------
    private static string FilePath => Path.Combine(Application.persistentDataPath, "AchieveSaveDatas.json");

    // ---------- 深拷贝 ----------
    public AchieveSaveDatas Clone()
    {
        var clone = new AchieveSaveDatas();
        clone.finishAchieveList = new List<AchieveSaveData>();
        clone.achieveSaveDatalist = new List<AchieveSaveData>();
        foreach (var data in this.achieveSaveDatalist)
        {
            clone.achieveSaveDatalist.Add(data.Clone());
        }
        
        foreach (var data in this.finishAchieveList)
        {
            clone.finishAchieveList.Add(data.Clone());
        }
        
        return clone;
    }

    // ---------- 初始化 ----------
    public void InitData()
    {
        finishAchieveList = new List<AchieveSaveData>();
        achieveSaveDatalist = new List<AchieveSaveData>()
        {
            new AchieveSaveData()
            {
                achieveTypeId = 1, finishTime = null, iscliam = false, iscomplete = false, progressvalue = 0,
                startAchieveTypeId = 1
            },
            new AchieveSaveData()
            {
                achieveTypeId = 4, finishTime = null, iscliam = false, iscomplete = false, progressvalue = 0,
                startAchieveTypeId = 4
            },
            new AchieveSaveData()
            {
                achieveTypeId = 7, finishTime = null, iscliam = false, iscomplete = false, progressvalue = 0,
                startAchieveTypeId = 7
            },
            new AchieveSaveData()
            {
                achieveTypeId = 10, finishTime = null, iscliam = false, iscomplete = false, progressvalue = 0,
                startAchieveTypeId = 10
            },
            new AchieveSaveData()
            {
                achieveTypeId = 13, finishTime = null, iscliam = false, iscomplete = false, progressvalue = 0,
                startAchieveTypeId = 13
            },
        };
    }

    /// <summary>
    /// 使用另一个对象初始化（深拷贝）
    /// </summary>
    public void InitData(AchieveSaveDatas other)
    {
        if (other == null)
        {
            InitData();
            return;
        }
        // 使用深拷贝，避免引用传递
        var cloned = other.Clone();
        finishAchieveList = cloned.finishAchieveList;
        achieveSaveDatalist = cloned.achieveSaveDatalist.Count <=0 ? new List<AchieveSaveData>()
        {
            new AchieveSaveData(){achieveTypeId = 1,finishTime = null,iscliam = false,iscomplete =false,progressvalue =0,startAchieveTypeId = 1},
            new AchieveSaveData(){achieveTypeId = 4,finishTime = null,iscliam = false,iscomplete =false,progressvalue =0,startAchieveTypeId = 4},
            new AchieveSaveData(){achieveTypeId = 7,finishTime = null,iscliam = false,iscomplete =false,progressvalue =0,startAchieveTypeId = 7},
            new AchieveSaveData(){achieveTypeId = 10,finishTime = null,iscliam = false,iscomplete =false,progressvalue =0,startAchieveTypeId = 10},
            new AchieveSaveData(){achieveTypeId = 13,finishTime = null,iscliam = false,iscomplete =false,progressvalue =0,startAchieveTypeId = 13},
        } : cloned.achieveSaveDatalist;
    }

    // ---------- 保存数据 ----------
    public void SaveData()
    {
        try
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            // 如果启用了加密，请替换为您的加密方法
            string encryptedJson = SecurityProvider.ProtectData(json); 
            File.WriteAllText(FilePath, encryptedJson, System.Text.Encoding.UTF8);
            Debug.Log($"成就数据保存成功: {FilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"保存成就数据失败: {e.Message}");
        }
    }

    // ---------- 加载数据 ----------
    public void LoadData()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Debug.LogWarning("未找到成就数据文件，使用默认数据");
                InitData();
                return;
            }

            string encryptedJson = File.ReadAllText(FilePath, System.Text.Encoding.UTF8);
            string json = SecurityProvider.RestoreData(encryptedJson);

            if (!IsValidJson(json))
            {
                Debug.LogError("JSON格式无效，重置为默认数据");
                InitData();
                return;
            }

            var loadedData = JsonConvert.DeserializeObject<AchieveSaveDatas>(json);
            if (loadedData == null)
            {
                Debug.LogError("反序列化返回空对象，重置数据");
                InitData();
                return;
            }

            // 使用深拷贝赋值，确保独立
            InitData(loadedData);
            Debug.Log("成就数据加载成功");
        }
        catch (Exception e)
        {
            Debug.LogError($"加载成就数据异常: {e.Message}\n{e.StackTrace}");
            InitData();
        }
    }

    // ---------- JSON验证 ----------
    private bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            JToken.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }


    public void UpdateAchieveItemData(AchieveType chieveType,int progress)
    {
        AchieveType newAchieveType = chieveType;
        
        if(progress <=0) return;
        
        bool isexit = finishAchieveList.Exists(e => e.achieveTypeId == (int)chieveType);
        
        if (achieveSaveDatalist.Count <= 0&&!isexit)
        {
            achieveSaveDatalist.Add(new AchieveSaveData()
            {
                startAchieveTypeId = (int)newAchieveType,
                achieveTypeId = (int)newAchieveType,
                progressvalue = progress,
                iscliam = false,
                iscomplete = false
            });
        }
        else
        {

            for (int i = (int)chieveType; i <= (int)chieveType + 2; i++)
            {
                int index = i;
                bool findexit = finishAchieveList.Exists(e => e.achieveTypeId == (int)index);
                bool findnofinish = achieveSaveDatalist.Exists(e => e.achieveTypeId == (int)index&&e.iscomplete);
                if (!findexit&&!findnofinish)
                {
                    newAchieveType = (AchieveType)index;
                    break;
                }
            }
            
            AchieveSaveData curAchieveSaveData = achieveSaveDatalist.Find(achieve=>achieve.achieveTypeId == (int)newAchieveType);
            if (curAchieveSaveData != null)
            {
                AchieveDataItem achieveDataItem = AchievementManager.Instance.GetAchieveItemById(newAchieveType);
           
                curAchieveSaveData.progressvalue += progress;
        
                int newProgress = curAchieveSaveData.progressvalue;

                if (curAchieveSaveData.progressvalue >= achieveDataItem.needValue)
                {
                    curAchieveSaveData.iscomplete = true;
                    curAchieveSaveData.finishTime = DateTime.Now.ToString();
                    curAchieveSaveData.progressvalue=achieveDataItem.needValue;
                }

                if (newProgress-achieveDataItem.needValue >= 0)
                {
                    newAchieveType=newAchieveType+1;

                    if ((int)newAchieveType <= (int)chieveType + 2)
                    {
                        achieveSaveDatalist.Add(new AchieveSaveData()
                        {
                            startAchieveTypeId = (int)newAchieveType,
                            achieveTypeId = (int)newAchieveType,
                            progressvalue = newProgress,
                            iscliam = false,
                            iscomplete = false
                        });
                    }
                }
            }
            else
            {
                bool isnexit = finishAchieveList.Exists(e => e.achieveTypeId == (int)chieveType);
                if (isnexit) return;
                
                if ((int)newAchieveType <= (int)chieveType + 2)
                {
                    achieveSaveDatalist.Add(new AchieveSaveData()
                    {
                        startAchieveTypeId = (int)newAchieveType,
                        achieveTypeId = (int)newAchieveType,
                        progressvalue = progress,
                        iscliam = false,
                        iscomplete = false
                    });
                }
            }
        }
    }

    public void ClaimAchieveItemData(AchieveType achieveType)
    {
        AchieveSaveData curAchieveSaveData = achieveSaveDatalist.Find(achieve=>achieve.achieveTypeId == (int)achieveType);
      
        if ( curAchieveSaveData.iscomplete&&!curAchieveSaveData.iscliam)
        {
            curAchieveSaveData.iscliam = true;
            AchievementManager.Instance.AddAvatarFrameItems(AvatarUnlockType.Achieved,(int)achieveType);
        }

        bool isexit = finishAchieveList.Exists(e => e.achieveTypeId == (int)achieveType);
        
        if (!isexit)
        {
            finishAchieveList.Add(curAchieveSaveData.Clone());
        }
       
        achieveSaveDatalist.Remove(curAchieveSaveData);
    }
    
}

/// <summary>
/// 单个成就任务数据
/// </summary>
public class AchieveSaveData
{
    public int startAchieveTypeId;
    public int achieveTypeId;
    public int progressvalue;
    public bool iscliam;
    public bool iscomplete;
    public string finishTime;


    public AchieveSaveData Clone()
    {
        return new AchieveSaveData
        {
            startAchieveTypeId = this.startAchieveTypeId,
            achieveTypeId = this.achieveTypeId,
            progressvalue = this.progressvalue,
            iscliam = this.iscliam,
            iscomplete = this.iscomplete,
            finishTime=this.finishTime
        };
    }
}
