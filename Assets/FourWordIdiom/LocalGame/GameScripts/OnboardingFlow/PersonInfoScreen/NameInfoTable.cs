using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class NameInfoTable : MonoBehaviour
{
    [SerializeField] private Button changeHeadIconBtn;
    [SerializeField] private Button CopyAccountIdBtn;
    [SerializeField] private Image HeadIcon;
    [SerializeField] private Image HeadBorder;
    [SerializeField] private Text firstLoginTimeText;
    [SerializeField] private Text NameText;
    [SerializeField] private Text zanCountText;
    [SerializeField] private Text MulCopyText;
    [SerializeField] private Text MulZenLevelText;
    [SerializeField] private Text MulZenScoreText;
    [SerializeField] private Text MulWinStreakText;
    
    [SerializeField] private Text ZenLevelText;
    [SerializeField] private Text ZenScoreText;
    [SerializeField] private Text WinStreakText;
  
    
    // Start is called before the first frame update
    private void Start()
    {
        changeHeadIconBtn.AddClickAction(OnClickChangeHeadIconBtn);
        CopyAccountIdBtn.AddClickAction(OnCopyPackageAndOpenId);
        
        //MulCopyText.text=MultilingualManager.Instance.GetString("MulZenLevelText");
        MulZenLevelText.text=MultilingualManager.Instance.GetString("ZenLevel","hudie");
        MulZenScoreText.text=MultilingualManager.Instance.GetString("ZenScore","hudie");
        MulWinStreakText.text=MultilingualManager.Instance.GetString("LongestWinDay","hudie");
        
      
    }

    private void OnEnable()
    {
        InitUI();
    }

    private void InitUI()
    {
        DateTime firstLoginDate= DateTime.Parse(GameDataManager.Instance.UserData.firstLoginTime);
        firstLoginTimeText.text=String.Format(MultilingualManager.Instance.GetString("StartDate"), firstLoginDate.Year, firstLoginDate.Month);
        NameText.text=GameDataManager.Instance.UserData.UserName;
        zanCountText.text=GameDataManager.Instance.UserData.likeCount.ToString();
        ZenLevelText.text=GameDataManager.Instance.UserData.Zenlevel;
        ZenScoreText.text=GameDataManager.Instance.UserData.zenCount.ToString();
        WinStreakText.text=GameDataManager.Instance.UserData._signSaveData.historyWinDayTimes.ToString();
    }

    private void OnClickChangeHeadIconBtn()
    {
        SystemManager.Instance.ShowPanel(PanelType.HeadScreen);
    }
    
    /// <summary>
    /// 复制包名和 OpenId 到系统剪贴板
    /// </summary>
    public void OnCopyPackageAndOpenId()
    {
        // 1. 获取包名 (Bundle Identifier)
        string packageName = Application.identifier;

        // 2. 获取 OpenId (结合你现有的 GameDataManager 数据结构)
        string openId = "暂无数据";
        
        // 加入判空保护，防止游戏刚启动还没加载完数据时报错
        if (GameDataManager.Instance != null && GameDataManager.Instance.UserData != null)
        {
            openId = GameDataManager.Instance.UserData.UserId;
            
            // 如果本地 UserId 为空，也可以顺便获取一下设备的标识
            if (string.IsNullOrEmpty(openId))
            {
                openId = "本地为空，当前设备号: " + Game.self.GetUniqueId();
            }
        }

        // 3. 拼接文本
        string copyText = $"包名: {packageName}\n 用户ID: {openId}";

        // 4. 写入剪贴板 (Unity 原生核心 API)
        GUIUtility.systemCopyBuffer = copyText;

        // 5. 日志输出 (在真机上你可以换成调用你游戏内的飘字/Toast 提示)
        Debug.Log("复制成功：\n" + copyText);
        
        // 示例：如果你有飘字组件，可以加上
        // ToastManager.Show("信息已复制");
        MessageSystem.Instance.ShowTip("信息已复制");
    }
    
}
