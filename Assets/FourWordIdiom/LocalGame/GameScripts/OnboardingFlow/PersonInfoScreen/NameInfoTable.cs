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
    [SerializeField] private Image headIcon;
    [SerializeField] private Image headBorder;
    [SerializeField] private Image redPoint;
    
    [SerializeField] private Text firstLoginTimeText;
    [SerializeField] private Text nameText;
    [SerializeField] private Text zanCountText;
    [SerializeField] private Text mulCopyText;
    [SerializeField] private Text mulZenLevelText;
    [SerializeField] private Text mulZenScoreText;
    [SerializeField] private Text mulWinStreakText;
    
    [SerializeField] private Text zenLevelText;
    [SerializeField] private Text zenScoreText;
    [SerializeField] private Text winStreakText;
  
    
    // Start is called before the first frame update
    private void Start()
    {
        changeHeadIconBtn.AddClickAction(OnClickChangeHeadIconBtn);
        CopyAccountIdBtn?.AddClickAction(OnCopyPackageAndOpenId);
        
        //MulCopyText.text=MultilingualManager.Instance.GetString("MulZenLevelText");
        mulZenLevelText.text=MultilingualManager.Instance.GetString("ZenLevel","hudie");
        mulZenScoreText.text=MultilingualManager.Instance.GetString("ZenScore","hudie");
        mulWinStreakText.text=MultilingualManager.Instance.GetString("LongestWinDay","hudie");
        
      
    }

    private void OnEnable()
    {
        
        EventDispatcher.instance.OnChangeHeadIconUpdateUI += UpdateHeadIcon;
      
    }

    public void InitMyUI()
    {
        changeHeadIconBtn.gameObject.SetActive(true);
        DateTime firstLoginDate= DateTime.Parse(GameDataManager.Instance.UserData.firstLoginTime);
        
        if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.UserName))
        {
            GameDataManager.Instance.UserData.UserName = FishInfoController.Instance.GeneratePlayerName();
        }
        
        firstLoginTimeText.text=String.Format(MultilingualManager.Instance.GetString("StartDate"), firstLoginDate.Year, firstLoginDate.Month);
        zanCountText.text = GameCoreManager.Instance.userProfile.likes_count.ToString();
        zenLevelText.text = OverallRankingManager.Instance.GetZenLevelByScore(GameDataManager.Instance.UserData.overallZenScore).ToString();
        zenScoreText.text=GameDataManager.Instance.UserData.overallZenScore.ToString();
        winStreakText.text=GameDataManager.Instance.UserData._signSaveData.historyWinDayTimes.ToString();
        
        if (GameDataManager.Instance.UserData.UserName.Length > 8)
        {
            nameText.text=GameDataManager.Instance.UserData.UserName.Substring(0,8)+"...";
        }
        else
        {
            nameText.text=GameDataManager.Instance.UserData.UserName;
        }
        redPoint.gameObject.SetActive(GameDataManager.Instance.UserData.isGetNewHeadIcon||GameDataManager.Instance.UserData.isGetNewHeadBorderIcon);
        UpdateHeadIcon();
    }

    private void UpdateHeadIcon()
    {
        headIcon.sprite = LoadheadIcon("head"+GameDataManager.Instance.UserData.UserHeadId);

        int userHeadBorderId = GameDataManager.Instance.UserData.UserHeadBorderId;
        headBorder.sprite = LoadheadIcon("AvatarFrameIcon"+userHeadBorderId);
        if (GameDataManager.Instance.UserData.UserName.Length > 8)
        {
            nameText.text=GameDataManager.Instance.UserData.UserName.Substring(0,8)+"...";
        }
        else
        {
            nameText.text=GameDataManager.Instance.UserData.UserName;
        }
    }
    
    public void InitOtherUI(int likeCount)
    {
        changeHeadIconBtn.gameObject.SetActive(false);
        //DateTime firstLoginDate= DateTime.Parse(GameCoreManager.Instance.otherPersonProfile.join_date_text);

        firstLoginTimeText.text = GameCoreManager.Instance.otherPersonProfile.join_date_text;
        zanCountText.text = likeCount.ToString();
        zenLevelText.text = OverallRankingManager.Instance.GetZenLevelByScore(GameCoreManager.Instance.otherPersonProfile.overallZenScore).ToString();
        zenScoreText.text=GameCoreManager.Instance.otherPersonProfile.overallZenScore.ToString();
        winStreakText.text=GameCoreManager.Instance.otherPersonProfile.max_win_streak.ToString();
        
        if (GameCoreManager.Instance.otherPersonProfile.nickname?.Length > 8)
        {
            nameText.text=GameCoreManager.Instance.otherPersonProfile.nickname.Substring(0,8)+"...";
        }
        else
        {
            nameText.text=GameCoreManager.Instance.otherPersonProfile.nickname;
        }

        UpdateOtherHeadIcon();
    }
    
    private void UpdateOtherHeadIcon()
    {
        headIcon.sprite = LoadheadIcon("head"+GameCoreManager.Instance.otherPersonProfile.avatar);

        int userHeadBorderId = int.Parse(GameCoreManager.Instance.otherPersonProfile.avatar_frame);
        headBorder.sprite = LoadheadIcon("AvatarFrameIcon"+userHeadBorderId);
    }

    private void OnClickChangeHeadIconBtn()
    {
        SystemManager.Instance.ShowPanel(PanelType.HeadScreen);
        redPoint.gameObject.SetActive(false);
    }
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon,"UserHeadIcons");
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

    private void OnDisable()
    {
        EventDispatcher.instance.OnChangeHeadIconUpdateUI -= UpdateHeadIcon;
    }
    
}
