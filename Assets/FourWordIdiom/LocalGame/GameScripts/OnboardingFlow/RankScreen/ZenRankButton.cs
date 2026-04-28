using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Game = Middleware.Game;

public class ZenRankButton : MonoBehaviour
{
    public Button rankBtn;
    public Image rankImage;
    public Text zenLevelText;
    public GameObject wifi;
    
    // public GameObject effect;
    
    // Start is called before the first frame update

    private void Awake()
    {
        if (rankBtn == null)
            rankBtn = GetComponent<Button>();
        
        if(rankImage == null)
            rankImage = GetComponentInChildren<Image>();
        
        if(zenLevelText == null)
            zenLevelText = GetComponentInChildren<Text>();
        
    }

    private void OnEnable()
    {
        CheckRankProgress();
    }

    void Start()
    {
        rankBtn.AddClickAction(OnRankButtonClick);
    }

    private void OnRankButtonClick()
    {
        if (Game.IsNetworkActive)
        {
            if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.UserName))
            {
                SystemManager.Instance.ShowPanel(PanelType.HeadScreen);
            }
            else
            {
                
                if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
                {
                    SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
                }else if (GameCoreManager.Instance.PanelState == PanelState.FinishHexPanel)
                {
                    SystemManager.Instance.HidePanel(PanelType.StageFinishView);
                }else if (GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel)
                {
                    SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
                }else if (GameCoreManager.Instance.PanelState == PanelState.GameHexPanel)
                {
                    SystemManager.Instance.HidePanel(PanelType.HexGamePlayArea);
                }
                else if (GameCoreManager.Instance.PanelState == PanelState.GamePingPanel)
                {
                    SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);
                }
                
                SystemManager.Instance.HidePanel(PanelType.HeaderSection , true, () =>
                {
                    SystemManager.Instance.ShowPanel(PanelType.ZenRankScreen);
                });
                
            }
        }else
            MessageSystem.Instance.ShowTip(MultilingualManager.Instance.GetString("RestorePurchasesTips01"), false);
    }
   
    public void CheckRankProgress()
    {
        bool isShow =
            GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.ZenOpenLevel ||
            GameDataManager.Instance.UserData.CurrentChessStage >= AppGameSettings.UnlockRequirements.ZenOpenLevel;
        
        rankBtn.gameObject.SetActive(isShow);

        string zenName = MultilingualManager.Instance.GetString(GameDataManager.Instance.UserData.Zenlevel);
        zenLevelText.text = zenName;

        string zenLevel = ExtractNumber(GameDataManager.Instance.UserData.Zenlevel);
        Sprite zenIcon = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("zenicon_"+zenLevel);
        if(zenIcon != null)
            rankImage.sprite = zenIcon;

        
        if (Game.IsNetworkActive)
            wifi.SetActive(false);
        else
            wifi.SetActive(true);
    }
    
    private string ExtractNumber(string input)
    {
        // 1. 使用正则匹配出字符串中连续的数字部分 (如 "01", "10")
        string numberStr = Regex.Match(input, @"\d+").Value;

        // 2. 将提取出的字符串转为 int，自动消除前导 0
        if (int.TryParse(numberStr, out int result))
        {
            // 3. 把干净的数字再转回字符串返回（如果你的业务需要 int，直接返回 result 即可）
            return result.ToString(); 
        }

        return null;
    }
    
    // ==========================================
    // 🌟 新增：外部调用此方法，告诉按钮“你接收到了新的禅意”
    // ==========================================
    public void PlayAbsorbEffect(int addZenCount)
    {
        // 1. 播放按钮的震动反馈（被莲花砸中的物理感）
        // DOPunchScale(震动强度, 震动时间)
        transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 10, 1f).OnComplete(() =>
        {
            // 确保震动结束后比例恢复正常
            transform.localScale = Vector3.one;
        });

        // 2. 可以在这里顺便做个数字跳动的特效，或者直接刷新 UI
        // （这里假设你的 CheckRankProgress 能拉取到最新的本地数据来刷新界面）
        CheckRankProgress();
        
        // 可选：播放一声清脆的“叮”音效
        // AudioManager.Instance.PlaySoundEffect("ZenAbsorb");
    }
}
