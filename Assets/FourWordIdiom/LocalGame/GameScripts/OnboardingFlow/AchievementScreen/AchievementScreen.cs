using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class AchievementScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
   
    [SerializeField] private Text HeaderText; //标题文本
    [SerializeField] private Text continueAchieveText; //标题文本
    [SerializeField] private Text finishAchieveText; //标题文本
    [SerializeField] private Text lockAchieveText; //标题文本
    [SerializeField] private Transform continueAchieveParent; 
    [SerializeField] private Transform finishAchieveParent; 
    [SerializeField] private Transform lockAchieveParent; 
    
    [SerializeField] private HorGoningAchieveItem horGoningAchievePerfab;
    [SerializeField] private HorGetAchieveItem horGetAchievePerfab;
    [SerializeField] private HorLockAchieveItem horLockAchievePerfab;
    
    
    private ObjectPool horGoingobjectPool; // 对象池实例
    private ObjectPool horGetobjectPool; // 对象池实例
    private ObjectPool horLockobjectPool; // 对象池实例
    
    
    private Dictionary<int,HorGoningAchieveItem> achieveGoingItems = new Dictionary<int,HorGoningAchieveItem>();
    private Dictionary<int,HorGetAchieveItem> achieveGetItems = new Dictionary<int,HorGetAchieveItem>();
    private Dictionary<int,HorLockAchieveItem> achieveLockItems = new Dictionary<int,HorLockAchieveItem>();
    
    protected void Start()
    {
        if (horGoningAchievePerfab == null)
        {
            horGoningAchievePerfab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "HorGoningAchieveItem").GetComponent<HorGoningAchieveItem>();
        }
        // 初始化对象池
        horGoingobjectPool = new ObjectPool(horGoningAchievePerfab.gameObject, ObjectPool.CreatePoolContainer(transform, "AchieveGoingItemPool"));
       
        if (horGetAchievePerfab == null)
        {
            horGetAchievePerfab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "HorGetAchieveItem").GetComponent<HorGetAchieveItem>();
        }
        // 初始化对象池
        horGetobjectPool = new ObjectPool(horGetAchievePerfab.gameObject, ObjectPool.CreatePoolContainer(transform, "AchieveGetItemPool"));
        
        if (horLockAchievePerfab == null)
        {
            horLockAchievePerfab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "HorLockAchieveItem").GetComponent<HorLockAchieveItem>();
        }
        // 初始化对象池
        horLockobjectPool = new ObjectPool(horLockAchievePerfab.gameObject, ObjectPool.CreatePoolContainer(transform, "AchieveLockItemPool"));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
       
        //HeaderText.text = MultilingualManager.Instance.GetString("CharacterInfoTitle");
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
        
  
        StartCoroutine(CrateAchieveItem(AchieveState.GoingAchieved));
 
        StartCoroutine(CrateAchieveItem(AchieveState.FinishAchieved));
       
        StartCoroutine(CrateAchieveItem(AchieveState.LockAchieved));
      
      
        StartCoroutine(ShowInitUI());
    }

    private IEnumerator ShowInitUI()
    {
        yield return new WaitForSeconds(0.4f);
          
        InitUI();
    }
    
    private void InitUI()
    {
        int total = AchievementManager.Instance.GetAllAchieveItems().Count;
        int achieveing = achieveGoingItems.Count;
        int finishachieve = GameDataManager.Instance.AchieveSaveDataList.finishAchieveList.Count;
        
        HeaderText.text=MultilingualManager.Instance.GetString("MyAchievements","hudie");
        continueAchieveText.text=MultilingualManager.Instance.GetString("Achievement","hudie")+" "+achieveing+"/"+total;
        finishAchieveText.text=MultilingualManager.Instance.GetString("AchievedSuccess","hudie")+" "+finishachieve+"/"+total;
        lockAchieveText.text=MultilingualManager.Instance.GetString("UnlockedAchievements","hudie");
    }
    
    private IEnumerator CrateAchieveItem(AchieveState achieveState)
    {        
        ClearTaskItems();
        
        yield return new WaitForSeconds(0.01f);
        AchieveSaveDatas achieveSaveDataList = GameDataManager.Instance.AchieveSaveDataList;

        switch (achieveState)
        {
            case AchieveState.GoingAchieved:
               
                // 从配置表中读取初始数据
                foreach (var achieveSave in achieveSaveDataList.achieveSaveDatalist )
                {
                    if (!achieveSave.iscomplete)
                    {
                        if (!achieveGoingItems.ContainsKey(achieveSave.achieveTypeId))
                        {
                            // 从对象池获取 AchieveItem 对象
                            HorGoningAchieveItem achieveItem = horGoingobjectPool.GetObject<HorGoningAchieveItem>(continueAchieveParent);
            
                            // 赋值 AchieveItem 的数据
                            achieveItem.SetTaskData(achieveSave);
                            achieveGoingItems.Add(achieveItem.achieveSaveData.achieveTypeId, achieveItem);
                        }
                        else
                        {
                            HorGoningAchieveItem achieveItem = achieveGoingItems[achieveSave.achieveTypeId];
                            // 赋值 AchieveItem 的数据
                            achieveItem.InitUI();
                        }
                    }
                }
                break;
            case AchieveState.FinishAchieved:
        
                // 从配置表中读取初始数据
                foreach (var achieveData in achieveSaveDataList.finishAchieveList )
                {
                    // 从对象池获取 AchieveItem 对象
                    HorGetAchieveItem achieveItem = horGetobjectPool.GetObject<HorGetAchieveItem>(finishAchieveParent);
            
                    // 赋值 AchieveItem 的数据
                    achieveItem.SetTaskData(achieveData);
                    if(!achieveGetItems.ContainsKey(achieveData.achieveTypeId))
                        achieveGetItems.Add(achieveData.achieveTypeId, achieveItem);
                }
                break;
            case AchieveState.LockAchieved:
                Dictionary<AchieveType, AchieveDataItem> lockAchieveItems = AchievementManager.Instance.GetAllLockAchieveItems();
        
                // 从配置表中读取初始数据
                foreach (var achieveData in lockAchieveItems.Values )
                {
                    // 从对象池获取 AchieveItem 对象
                    HorLockAchieveItem achieveItem = horLockobjectPool.GetObject<HorLockAchieveItem>(lockAchieveParent);
            
                    // 赋值 AchieveItem 的数据
                    achieveItem.SetTaskData(achieveData);
                    if(!achieveLockItems.ContainsKey(achieveData.id))
                        achieveLockItems.Add(achieveData.id, achieveItem);
                }
                break;
        }
    }
   

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
    }
   
    
    private void ClearTaskItems()
    {
        foreach (HorGoningAchieveItem achieveItem in achieveGoingItems.Values)
        {
            horGoingobjectPool.ReturnObjectToPool(achieveItem.GetComponent<PoolObject>());
        }
        
        foreach (HorGetAchieveItem achieveItem in achieveGetItems.Values)
        {
            horGetobjectPool.ReturnObjectToPool(achieveItem.GetComponent<PoolObject>());
        }
        
        foreach (HorLockAchieveItem achieveItem in achieveLockItems.Values)
        {
            horLockobjectPool.ReturnObjectToPool(achieveItem.GetComponent<PoolObject>());
        }

        achieveGoingItems.Clear();
        achieveGetItems.Clear();
        achieveLockItems.Clear();
        //CrateTaskItem();
    }
 
    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
    }

    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }
}
