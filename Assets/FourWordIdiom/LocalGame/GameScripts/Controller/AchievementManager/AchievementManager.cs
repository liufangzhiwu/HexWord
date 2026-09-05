using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;

public class AchieveDataItem
{
    public int id;
    public AchieveType achieveType;
    public string achieveName;   // 对应事件名称，如 "DoubleHit"
    public string achieveTip;   // 对应事件名称，如 "DoubleHit"
    public int needValue;
    public string achieveIcon;
}

public class AvatarFrameItem
{
    public int id;
    public AvatarUnlockType unlockType;
    public int condition;  
    public string avatarIcon;
    public string unlockTipText;
}

/// <summary>
/// 成就标签
/// </summary>
public class ThreelevelTagItem
{
    public string labelKey;
    public Dictionary<int,float> ratePair;  
    public string longText;
}


public enum AvatarUnlockType
{
    Null,
    Achieved,
    FishMatch,
    FlowerRank,
   
}

public enum AchieveState
{
    Null,
    GoingAchieved,
    FinishAchieved,
    LockAchieved
}

public enum AchieveType
{
    Null,
    DoubleHit1,
    DoubleHit2,
    DoubleHit3,
    BreakIce1,
    BreakIce2,
    BreakIce3,
    CollectLeaves1,
    CollectLeaves2,
    CollectLeaves3,
    PickFlowers1,
    PickFlowers2,
    PickFlowers3,
    Perfect1,
    Perfect2,
    Perfect3,
}

public class AchievementManager : Singleton<AchievementManager>
{
 
    // 按 AchieveType 存储，便于类型查找
    private Dictionary<AchieveType, AchieveDataItem> achieveItemsByType = new Dictionary<AchieveType, AchieveDataItem>();
    private List<AvatarFrameItem> avatarFrameItems = new List<AvatarFrameItem>();
    private List<ThreelevelTagItem> threelevelTagItems = new List<ThreelevelTagItem>();
    
    private FinishAchieveTable finishAchieveTable;

    public event System.Action OnDailyAchieveBtnUI;
    public event System.Action<string> OnDailyButterflyAchieveUI;
    
    private ObjectPool finishAchievePool; // 对象池实例
    private List<FinishAchieveTable> finishAchieveTables=new List<FinishAchieveTable>(); // 对象池实例

    public override void Init()
    {
        // 加载成就配置文件
        TextAsset achievementData = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "Achievement");
        // 加载三级配置（可能包含额外成就或等级信息）
        TextAsset threeLevelData = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "ThreelevelConfiguration");
        // 加载头像框数据（未直接使用，但保留加载）
        TextAsset avatarFrameData = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "AvatarFrame");

        if (achievementData != null)
        {
            ParseAchievementData(achievementData.text);
        }
        else
        {
            Debug.LogError("Failed to load Achievement CSV data.");
        }

        if (threeLevelData != null)
        {
            ParseThreeLevelData(threeLevelData.text); // 复用相同结构解析
        }
        else
        {
            Debug.LogError("Failed to load ThreelevelConfiguration CSV data.");
        }

        if (avatarFrameData != null)
        {
            ParseAvatarFrameData(avatarFrameData.text); // 复用相同结构解析
        }
    }

    /// <summary>
    /// 解析 CSV 数据并填充成就字典
    /// </summary>
    private void ParseAchievementData(string csvData)
    {
        string[] lines = csvData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            Debug.LogWarning("CSV data has no content or header.");
            return;
        }

        // 从第 2 行开始（索引 2），跳过可能的标题行或空行
        for (int i = 2; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            string[] fields = line.Split(',');
            if (fields.Length < 5) // 需要至少 5 个字段：id, type, name, value, icon
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields (found {fields.Length}).");
                continue;
            }
           
            int id = int.Parse(fields[0].Trim());
            string name = fields[1].Trim(); // 修正：原代码使用了 fields[0] 两次，现改为 fields[1]
            string tip = fields[2].Trim();
            int needValue = int.Parse(fields[3].Trim());
            string icon = fields[4].Trim();
          

            AchieveType achieveType = (AchieveType)id;
            AchieveDataItem item = new AchieveDataItem
            {
                id = id,
                achieveType = achieveType,
                achieveName = name,
                achieveTip = tip,
                needValue = needValue,
                achieveIcon = icon
            };
            // 添加到类型字典（若类型重复则覆盖并警告）

            if (!achieveItemsByType.ContainsKey(achieveType))
            {
                achieveItemsByType.Add(achieveType,item);
            }
            
            
        }

        Debug.Log($"Loaded {achieveItemsByType.Count} achievement items.");
    }
    
      /// <summary>
    /// 解析 CSV 数据并填充成就字典
    /// </summary>
    private void ParseAvatarFrameData(string csvData)
    {
        string[] lines = csvData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            Debug.LogWarning("CSV data has no content or header.");
            return;
        }

        // 从第 2 行开始（索引 2），跳过可能的标题行或空行
        for (int i = 2; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            string[] fields = line.Split(',');
            if (fields.Length < 5) // 需要至少 5 个字段：id, type, name, value, icon
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields (found {fields.Length}).");
                continue;
            }
          
            int id = int.Parse(fields[0].Trim());
            int unlockType = int.Parse(fields[1].Trim()); // 修正：原代码使用了 fields[0] 两次，现改为 fields[1]
            int unlockValue = int.Parse(fields[2].Trim());
            string avatarIcon = fields[3].Trim();
            string unlockTipText = fields[4].Trim();
           
            AvatarFrameItem item = new AvatarFrameItem
            {
                id = id,
                unlockType = (AvatarUnlockType)unlockType,
                condition = unlockValue,
                avatarIcon = avatarIcon,
                unlockTipText = unlockTipText
            };
           
            avatarFrameItems.Add(item);
           
        }

        Debug.Log($"Loaded {avatarFrameItems.Count} avatarFrameItems items.");
    }
      
      
    /// <summary>
    /// 解析 CSV 数据并填充成就标签数据
    /// </summary>
    private void ParseThreeLevelData(string csvData)
    {
        string[] lines = csvData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            Debug.LogWarning("CSV data has no content or header.");
            return;
        }

        // 从第 2 行开始（索引 1），跳过标题行
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            string[] fields = line.Split(',');
            if (fields.Length < 3)
            {
                Debug.LogWarning($"Skipping invalid row: {line}");
                continue;
            }

            string key = fields[0].Trim();                          // LabelKey (Ordinary-达人 Rare-大师 Infrequent-大神)
            string incentiveValueStr = fields[1].Trim();            // IncentiveValue，如 "31_29_32_30_28"
            string longText = fields[2].Trim();                     // LongText

            // 分割激励值字符串，得到 X1~X5
            string[] values = incentiveValueStr.Split('_');
            if (values.Length != 5)
            {
                Debug.LogWarning($"IncentiveValue format invalid for key {key}: {incentiveValueStr}");
                continue;
            }

            // 解析五个数值（可能为浮点数）
            float x1 = float.Parse(values[0].Trim());
            float x2 = float.Parse(values[1].Trim());
            float x3 = float.Parse(values[2].Trim());
            float x4 = float.Parse(values[3].Trim());
            float x5 = float.Parse(values[4].Trim());

            // 构建日期个位数到激励值的映射字典
            Dictionary<int, float> ratePair = new Dictionary<int, float>();
            for (int dayDigit = 0; dayDigit <= 9; dayDigit++)
            {
                float value;
                // 按规则映射
                if (dayDigit == 1 || dayDigit == 5)
                    value = x1;
                else if (dayDigit == 2 || dayDigit == 6)
                    value = x2;
                else if (dayDigit == 3 || dayDigit == 7)
                    value = x3;
                else if (dayDigit == 4 || dayDigit == 8)
                    value = x4;
                else // dayDigit == 0 || dayDigit == 9
                    value = x5;

                ratePair.Add(dayDigit, value);
            }

            // 创建条目并加入列表
            ThreelevelTagItem item = new ThreelevelTagItem
            {
                labelKey = key,
                ratePair = ratePair,
                longText = longText
            };

            threelevelTagItems.Add(item);
        }

        Debug.Log($"Loaded {threelevelTagItems.Count} threelevelTagItems items.");
    }
    
    
    /// <summary>
    /// 根据档位标签和日期个位数（0~9）获取对应的激励数值
    /// </summary>
    /// <param name="labelKey">档位标签，如 "Ordinary"</param>
    /// <param name="dayDigit">日期个位数，取值范围 0~9</param>
    /// <returns>激励数值，若未找到则返回 -1（或自定义默认值）</returns>
    public float GetIncentiveValue(string labelKey, int dayDigit)
    {
        if (dayDigit < 0 || dayDigit > 9)
        {
            Debug.LogError($"Invalid dayDigit: {dayDigit}, must be between 0 and 9.");
            return -1f;
        }

        ThreelevelTagItem item=threelevelTagItems.Find(three => three.labelKey==labelKey);
        if (item!=null)
        {
            if (item.ratePair.TryGetValue(dayDigit, out float value))
                return value;
            else
                Debug.LogError($"ratePair does not contain dayDigit {dayDigit} for labelKey {labelKey}");
        }
        else
        {
            Debug.LogError($"labelKey '{labelKey}' not found.");
        }
        return -1f; // 未找到返回默认值
    }

    /// <summary>
    /// 根据当前系统日期的个位数和档位获取激励数值
    /// </summary>
    public float GetIncentiveValueByCurrentDate(string labelKey)
    {
        int dayDigit = DateTime.Now.Day % 10; // 获取日期的个位数
        return GetIncentiveValue(labelKey, dayDigit);
    }
    
    /// <summary>
    /// 根据当前档位获取对应激励条目
    /// </summary>
    public ThreelevelTagItem GetIncentiveThreeLevelData(string labelKey)
    {
        ThreelevelTagItem item=threelevelTagItems.Find(three => three.labelKey==labelKey);
        if (item!=null)
        {
            return item;
        }
        return null;
    }

    public List<AvatarFrameItem> GetAllAvatarFrameItems()
    {
        return avatarFrameItems;
    }

    /// <summary>
    /// 增加对应任务解锁头像框
    /// </summary>
    /// <param name="unlockType"></param>
    /// <param name="condition"></param>
    public void AddAvatarFrameItems(AvatarUnlockType unlockType,int condition)
    {
        AvatarFrameItem avatarFrameItem= GetAvatarTypeFrameById(unlockType,(int)condition);
        if (avatarFrameItem != null)
        {
            if (!string.IsNullOrEmpty(avatarFrameItem.avatarIcon))
            {
                GameDataManager.Instance.UserData.AddHeadBorderIcon(avatarFrameItem.id);
            }
        }
    }
      
    /// <summary>
    /// 按成就id获取头像数据
    /// </summary>
    public AvatarFrameItem GetAvatarTypeFrameById(AvatarUnlockType avatarUnlockType,int condition)
    {
        AvatarFrameItem item= avatarFrameItems.Find(item => item.unlockType == avatarUnlockType && item.condition == condition);
        return item;
    }

    /// <summary>
    /// 按成就id获取头像数据
    /// </summary>
    public AvatarFrameItem GetAvatarSomeFrameById(int achieveid)
    {
        AvatarFrameItem item= avatarFrameItems.Find(item => item.id == achieveid);
        return item;
    }
      
    /// <summary>
    /// 按 ID 获取成就数据
    /// </summary>
    public AchieveDataItem GetAchieveItemById(AchieveType type)
    {
        achieveItemsByType.TryGetValue(type, out AchieveDataItem item);
        return item;
    }

    /// <summary>
    /// 按 AchieveType 获取成就数据
    /// </summary>
    public AchieveDataItem GetAchieveItemByType(AchieveType type)
    {
        achieveItemsByType.TryGetValue(type, out AchieveDataItem item);
        return item;
    }

    /// <summary>
    /// 获取所有成就数据（以 ID 为键）
    /// </summary>
    public Dictionary<AchieveType, AchieveDataItem> GetAllAchieveItems()
    {
        return achieveItemsByType;
    }
    
    
    /// <summary>
    /// 获取所有未解锁得成就数据
    /// </summary>
    public Dictionary<AchieveType, AchieveDataItem> GetAllLockAchieveItems()
    {
        Dictionary<AchieveType, AchieveDataItem> items = new Dictionary<AchieveType, AchieveDataItem>();
        foreach (var titem in achieveItemsByType)
        {
            bool Exists = GameDataManager.Instance.AchieveSaveDataList.achieveSaveDatalist.Exists(achieveitem => achieveitem.achieveTypeId==(int)titem.Key);
            bool finiExists = GameDataManager.Instance.AchieveSaveDataList.finishAchieveList.Exists(achieveitem => achieveitem.achieveTypeId==(int)titem.Key);
            if (!Exists&!finiExists)
            {
                items.Add(titem.Key,titem.Value);
            }
        }
        return items;
    }


    public void InitFinishAchieveTable(Transform parent)
    {
        if (finishAchieveTable == null)
        {
            finishAchieveTable = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "FinishAchieveTable").GetComponent<FinishAchieveTable>();
            
            // 初始化对象池
            finishAchievePool = new ObjectPool(finishAchieveTable.gameObject, ObjectPool.CreatePoolContainer(SystemManager.Instance._uiRoot, "finishAchieveTablePool"));
        }

        
        if (finishAchieveTable != null)
        {
            AchieveSaveDatas achieveSaveDataList = GameDataManager.Instance.AchieveSaveDataList;
        
            // 从配置表中读取初始数据
            foreach (var achieveSave in achieveSaveDataList.achieveSaveDatalist)
            {
                if (achieveSave.iscomplete&&!achieveSave.iscliam) 
                {
                    FinishAchieveTable achieveItem = finishAchievePool.GetObject<FinishAchieveTable>(parent);
                    
                    AchieveSaveData achieveSaveData = achieveSave.Clone();
                    // 赋值 AchieveItem 的数据
                    achieveItem.SetTaskData(achieveSaveData);
                    finishAchieveTables.Add(achieveItem);
                    GameDataManager.Instance.AchieveSaveDataList.ClaimAchieveItemData((AchieveType)achieveSaveData.achieveTypeId);
                    break;
                }
            }
        }
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
    }
    
    
    public void DisableFinishAchieveTable()
    {
        foreach (FinishAchieveTable achieveItem in finishAchieveTables)
        {
            finishAchievePool.ReturnObjectToPool(achieveItem.GetComponent<PoolObject>());
        }
    }

 
}