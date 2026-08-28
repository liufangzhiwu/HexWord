using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class AchievementTable : MonoBehaviour
{
    [SerializeField] private Button achieveBtn; // 成就按钮
    
    [SerializeField] private Text mullitterTitleText;
    [SerializeField] private Transform achieveItemParent;

    [SerializeField] private AchieveItem achieveItemPrefab;
    private Dictionary<int,AchieveItem> achieveItems = new Dictionary<int,AchieveItem>();
    
    private ObjectPool objectPool; // 对象池实例
    
    // Start is called before the first frame update
    private void Start()
    {
        achieveBtn.AddClickAction(OnAchieveBtnBtn); // 绑定关闭按钮事件
        
        if (achieveItemPrefab == null)
        {
            achieveItemPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "VerachieveItem").GetComponent<AchieveItem>();
        }
        // 初始化对象池
        objectPool = new ObjectPool(achieveItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "AchieveItemPool"));

        
        //StartCoroutine(CrateAchieveItem());
    }

    private void OnEnable()
    {
        StartCoroutine(CrateAchieveItem());
        //StartCoroutine(UpdateTaskItemUI());
    }

    private void UpateAchieveItems()
    {
        int total = AchievementManager.Instance.GetAllAchieveItems().Count;
        int achieveing = achieveItems.Count;
        
        mullitterTitleText.text=MultilingualManager.Instance.GetString("Achievement","hudie")+" "+achieveing+"/"+total;
    }
    
    
    private IEnumerator CrateAchieveItem()
    {        
        ClearTaskItems();

        AchieveSaveDatas achieveSaveDataList = GameDataManager.Instance.AchieveSaveDataList;
        
        yield return new WaitForSeconds(0.02f);
        
        // 从配置表中读取初始数据
        foreach (var achieveSave in achieveSaveDataList.achieveSaveDatalist )
        {
            if (achieveSave!= null)
            {
                if (!achieveSave.iscomplete)
                {
                    if (!achieveItems.ContainsKey(achieveSave.achieveTypeId))
                    {
                        // 从对象池获取 AchieveItem 对象
                        AchieveItem achieveItem = objectPool.GetObject<AchieveItem>(achieveItemParent);
                     
                        // 赋值 AchieveItem 的数据
                        achieveItem.SetTaskData(achieveSave);
                        achieveItems.Add(achieveSave.achieveTypeId, achieveItem);
                    }
                    else
                    {
                        AchieveItem achieveItem = achieveItems[achieveSave.achieveTypeId];
                        achieveItem.InitUI();
                    }
                }
            }
        }
        
        UpateAchieveItems();
    }

    
    private void ClearTaskItems()
    {
        foreach (AchieveItem achieveItem in achieveItems.Values)
        {
            objectPool.ReturnObjectToPool(achieveItem.GetComponent<PoolObject>());
        }

        achieveItems.Clear();
        //CrateTaskItem();
    }
    
    private void OnAchieveBtnBtn()
    {
        SystemManager.Instance.ShowPanel(PanelType.AchievementScreen);
    }
   
}
