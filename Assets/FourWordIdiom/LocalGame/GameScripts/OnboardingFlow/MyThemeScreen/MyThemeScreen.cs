using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Middleware;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Toggle = UnityEngine.UI.Toggle;

public class MyThemeScreen : UIWindow
{
    [SerializeField] private ScrollView themeScorllView; // 滚动面板
    [SerializeField] private GameObject themeParent; // 主题父类
   
    [SerializeField] private Button drawButton; // 抽取按钮
    [SerializeField] private Button helpButton; // 帮助按钮
    [SerializeField] private Button HideButton; // 关闭按钮
   
    [SerializeField] private Text HeaderText;
    [SerializeField] private Text tipsText; 
    [SerializeField] private Text goldLeafCount; 
    [SerializeField] private Text needgoldLeafCount;
    
    
    private ThemeItem _themeItemPrefab;
    public List<ThemeItem> _themeItems=new List<ThemeItem>();
    private ObjectPool objectPool; // 对象池实例
    private List<ThemeSaveItem> themeSaveItems=new List<ThemeSaveItem>();
    private List<ThemeDataItem> themeDataItems=new List<ThemeDataItem>();
    private ThemeRangeData rule;
  

    protected void Start()
    {
        if (_themeItemPrefab == null)
        {
            _themeItemPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "ThemeItem").GetComponent<ThemeItem>();
        }
    
        // 初始化对象池
        objectPool = new ObjectPool(_themeItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "ThemeItemPool"));

        CrateAllThemeItems();
    }

    protected override void OnEnable()
    {
        AudioManager.Instance.PlaySoundEffect("ShowUI");
      
        InitUI();

        ThemeManager.Instance.OnShowNewThemeBtnUI += ShowNewTheme;
        
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);

        AnalyticMgr.ThemeEnter();

        ThemeManager.Instance.OnSkinEntryClicked();
    }
    
    /// <summary>
    /// 创建主题
    /// </summary>
    private void CrateAllThemeItems()
    {
        themeSaveItems = GameDataManager.Instance.UserData.ThemeSaveItems;
        themeDataItems = ThemeManager.Instance.GetThemeDataItems();
        
        foreach (ThemeSaveItem themeSaveItem in themeSaveItems)
        {
            // 从对象池获取 ShopItem 对象
            ThemeItem fishItem = objectPool.GetObject<ThemeItem>(themeParent.transform);
            // 赋值 FishItem 的数据
            fishItem.SetUnlockUI(themeSaveItem,this);
            _themeItems.Add(fishItem);
        }

        //创建未解锁的主题
        for (int i = themeSaveItems.Count; i < themeDataItems.Count; i++)
        {
            // 从对象池获取 ShopItem 对象
            ThemeItem fishItem = objectPool.GetObject<ThemeItem>(themeParent.transform);
            // 赋值 FishItem 的数据
            fishItem.SetlockUI(this);
            _themeItems.Add(fishItem);
        }
    }

    private void InitUI()
    {
        string titleName= MultilingualManager.Instance.GetString("MyTheme", "hudie");
        HeaderText.text = titleName;
        
        string anniuDraw= MultilingualManager.Instance.GetString("DrawOne", "hudie");
        drawButton.GetComponentInChildren<Text>().text = anniuDraw;
            
        if (themeSaveItems.Count<=0)
        {
            themeSaveItems = GameDataManager.Instance.UserData.ThemeSaveItems;
        }
        
        rule =ThemeManager.Instance.GetThemeRangeDataByThemeCount(themeSaveItems.Count);
        
        if (rule != null)
        {
            int requiredGoldLeaf = rule.needGoldLeaf;
            Debug.Log($"当前主题数量 {themeSaveItems.Count}，每次抽奖需要 {requiredGoldLeaf} 金箔");
            // 继续执行抽奖逻辑...
        }
        else
        {
            Debug.LogError("无法获取金箔抽取规则");
        }

        
        goldLeafCount.text = GameDataManager.Instance.UserData.GoldLeaf.ToString();
        needgoldLeafCount.text = rule.needGoldLeaf.ToString();

        ShowlevelTipsText();

        bool iscangettheme = ThemeManager.Instance.IsCanGetThemes();
        
        drawButton.gameObject.SetActive(iscangettheme);
        tipsText.gameObject.SetActive(iscangettheme);
    }

    private void ShowlevelTipsText()
    {
        int curlevel = GameDataManager.Instance.UserData.CurrentChessStage;

        // 目标个位数条件
        int targetDigit1 = ThemeManager.Instance.levelGeNum.x;
        int targetDigit2 = ThemeManager.Instance.levelGeNum.y;

        if (curlevel < ThemeManager.Instance.golfFirstLevel)
        {
            int gap = ThemeManager.Instance.golfFirstLevel - curlevel; // 需要经过的关卡数量（包括目标关本身）
            string tips= MultilingualManager.Instance.GetString("GoldFoilInLevels", "hudie");
            tipsText.text = string.Format(tips,gap);
            //tipsText.text = $"再过{gap}关出现金箔";
            return;
        }

        int nextLevel = curlevel + 1;
        int targetLevel = -1;

        // 最多查找1000关，防止死循环（实际可根据最大关卡数设置）
        for (int i = nextLevel; i <= nextLevel + 1000; i++)
        {
            int digit = i % 10;
            if (digit == targetDigit1 || digit == targetDigit2)
            {
                targetLevel = i;
                break;
            }
        }

        if (targetLevel != -1)
        {
            int gap = targetLevel - curlevel; // 需要经过的关卡数量（包括目标关本身）
            string tips= MultilingualManager.Instance.GetString("GoldFoilInLevels", "hudie");
            tipsText.text = string.Format(tips,gap);
        }
        else
        {
            // 未找到（比如两个个位数都不在0-9范围内或超出游戏最大关卡），可显示默认文本
            tipsText.text = "即将出现金箔";
        }
    }
    
    protected override void InitializeUIComponents()
    {
        HideButton.AddClickAction(()=>Close()); 
        drawButton.AddClickAction(OnClickDrawButton);
        helpButton.AddClickAction(OnClickHelpButton);
    }

    private void OnClickHelpButton()
    {
        SystemManager.Instance.ShowPanel(PanelType.ThemeHelpScreen);
    }
    
    /// <summary>
    /// 消耗金箔随机抽取主题
    /// </summary>
    private void OnClickDrawButton()
    {
        // 获取当前已解锁的主题列表
        var themeSaveItems = GameDataManager.Instance.UserData.ThemeSaveItems;

        // 1. 检查是否还有未解锁的主题
        if (themeSaveItems.Count >= themeDataItems.Count)
        {
            MessageSystem.Instance.ShowTip("已经解锁所有主题！");
            return;
        }
     
        // 3. 检查金箔是否足够
        if (GameDataManager.Instance.UserData.GoldLeaf < rule.needGoldLeaf)
        {
            string tips= MultilingualManager.Instance.GetString("NoTicket", "hudie");
            MessageSystem.Instance.ShowTip(tips);
            return;
        }

        // 4. 扣除金箔
        GameDataManager.Instance.UserData.UpdateGoldLeaf(-rule.needGoldLeaf);

        // 5. 从未解锁的主题中随机抽取一个
        List<int> unlockedThemeIds = themeSaveItems.Select(t => t.id).ToList();
        List<ThemeDataItem> availableThemes = themeDataItems.Where(t => !unlockedThemeIds.Contains(t.id)).ToList();

        if (availableThemes.Count == 0)
        {
            // 理论上前面已判断数量，此处为防御
            MessageSystem.Instance.ShowTip("无可解锁的主题");
            return;
        }

        // 随机选择一个主题
        int randomIndex = UnityEngine.Random.Range(0, availableThemes.Count);
        ThemeDataItem newTheme = availableThemes[randomIndex];

        ThemeSaveItem newThemeSaveItem = new ThemeSaveItem()
        {
            id = newTheme.id,
            isGet = true
        };

        // 6. 解锁主题（添加到用户数据）
        GameDataManager.Instance.UserData.ThemeSaveItems.Add(newThemeSaveItem);

        // 9. 刷新UI（例如更新主题列表、金箔数量显示等）
        InitUI();

        SystemManager.Instance.ShowPanel(PanelType.NewThemeScreen);
        // 可选：播放音效、特效等
    }

    private void ShowNewTheme()
    {
        ThemeSaveItem newThemeSaveItem = GameDataManager.Instance.UserData.ThemeSaveItems[GameDataManager.Instance.UserData.ThemeSaveItems.Count-1]; 
        
        _themeItems[GameDataManager.Instance.UserData.ThemeSaveItems.Count-1].SetUnlockUI(newThemeSaveItem,this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ThemeManager.Instance.OnShowNewThemeBtnUI -= ShowNewTheme;

        if (SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface))
        {
            EventDispatcher.instance.TriggerHighlightHeaderUI(true);
        }
    }


    public override void Close(CloseMethod method = CloseMethod.Default)
    {
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.GameHexPanel)
        {
            SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.GamePingPanel)
        {
            SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
        } 
        if (SystemManager.Instance.PanelIsShowing(PanelType.ZenRankScreen))
        {
            Debug.Log("是否存在？" + PanelType.ZenRankScreen);
            SystemManager.Instance.HidePanel(PanelType.ZenRankScreen);
        }
        
        base.Close(method); // 隐藏面板
    }


}