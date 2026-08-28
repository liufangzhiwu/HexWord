using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum ZenPathState
{
    Completed,  // 已经过去的阶段（白底）
    Current,    // 当前阶段（黄底）
    Locked      // 尚未解锁的阶段（蓝底半透明）
}

public class ZenPathData
{
    public int Id;
    public string NameKey;
    public ZenPathState State;
}

public class OverallRankingNames : UIWindow // 假设 UIWindow 是你的基类
{
    [SerializeField] private Button closeButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Button bottomCloseButton; // 底部的“点击关闭”文本按钮

    [Header("List Settings")]
    [SerializeField] private Transform contentParent; // ScrollView 的 Content 节点
    [SerializeField] private OverallNameItemUI itemPrefab; // 拖入你做好的 Item 预制体

    private List<OverallNameItemUI> _spawnedItems = new List<OverallNameItemUI>();
    private ObjectPool _prefabPool;
    
    protected override void Awake()
    {
        base.Awake();
        closeButton.AddClickAction(Close); // 尽量用原生 onClick，或保留你的 AddClickAction
        if(bottomCloseButton != null) bottomCloseButton.AddClickAction(Close);
        if (itemPrefab != null)
            itemPrefab.gameObject.SetActive(false);

        _prefabPool = new ObjectPool(itemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "ZenPathPool"), 5,PoolBehaviour.GameObject);
    }

    private void Start()
    {
        // 1. 设置多语言静态文本
        titleText.text = MultilingualManager.Instance.GetString("ZenRoad","hudie");
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshList(GetRealData());
    }
    private void RefreshList(List<ZenPathData> datas)
    {
        // 清理旧节点（大厂规范：频繁打开的界面应该使用对象池 ObjectPool 这里为简单起见先做销毁）
        foreach (var item in _spawnedItems)
        {
            if (item == null) continue;
            var poolObj = item.GetComponent<PoolObject>();
            if (poolObj != null)
                _prefabPool.ReturnObjectToPool(poolObj);
        }
        _spawnedItems.Clear();
        
        // 循环生成新节点
        foreach (var data in datas)
        {
            OverallNameItemUI newItem = _prefabPool.GetObject<OverallNameItemUI>(contentParent);
            newItem.gameObject.SetActive(true);
            newItem.Init(data);
            _spawnedItems.Add(newItem);
        }
    }
    
    // 🌟 核心：根据配置表和玩家真实分数计算状态
    private List<ZenPathData> GetRealData()
    {
        List<ZenPathData> realData = new List<ZenPathData>();
        
        int myScore = GameDataManager.Instance.UserData.overallZenScore;
        var realmList = OverallRankingManager.Instance.RealmLevelList;
        
        if (realmList == null || realmList.Count == 0) return realData;

        // 1. 找出玩家当前的真实境界等级
        int currentLevel = OverallRankingManager.Instance.GetZenLevelByScore(myScore);
        Debug.Log("当前等级" + currentLevel);
        // 2. 遍历配置表，划分每个阶段的状态
        foreach (var realm in realmList)
        {
            ZenPathState state = ZenPathState.Locked;
            
            if (realm.Level < currentLevel) 
            {
                state = ZenPathState.Completed; // 等级比我低，已完成
            }
            else if (realm.Level == currentLevel) 
            {
                state = ZenPathState.Current;   // 等级跟我一样，当前进行中
            }
            else 
            {
                state = ZenPathState.Locked;    // 等级比我高，未解锁
            }

            realData.Add(new ZenPathData 
            { 
                Id = realm.Level, 
                NameKey = realm.NameKey, // 传入配置表里的 ZL01 等字段
                State = state 
            });
        }

        return realData;
    }
}
