using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RewardNamePanel : UIWindow
{
    [SerializeField] private Text titleText;
    [SerializeField] private InputField nameInputText;
    [SerializeField] private RectTransform goldPoint;
    [SerializeField] private Button rewardButton;
    [SerializeField] private Button closeButton;
    
    private readonly int _rewardGold = 20;
    
    protected override void InitializeUIComponents()
    {
        if (rewardButton != null) rewardButton.AddClickAction(OnClaimReward);
        if (closeButton != null) closeButton.AddVibraClickAction(OnClose);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EventDispatcher.instance.TriggerUpdateLayerCoin(true, false, false);
        // 每次打开重新随机名字（也可保留不变）
        if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.UserName))
        {
            string defaultName = FishInfoController.Instance.GeneratePlayerName();
            nameInputText.text = defaultName;
        }
        else
        {
            nameInputText.text = GameDataManager.Instance.UserData.UserName;
        }
    }

    private void OnClaimReward()
    {
        string playerName = nameInputText.text.Trim();
        
        if (string.IsNullOrEmpty(playerName))
        {
            MessageSystem.Instance.ShowTip("名称不能为空");
            return;
        }
        if (MultilingualManager.Instance.ContainsForbiddenWords(playerName))
        {
            string tips = MultilingualManager.Instance.GetString("CharacterInfoTips01");
            if (tips.Contains("\\n"))
                tips = tips.Replace("\\n", "\n");
            MessageSystem.Instance.ShowTip(tips, false);
            return;
        }
        // 2. 保存名称到用户数据
        var userData = GameDataManager.Instance.UserData;
        userData.UserName = playerName;
        userData.isChangeUserName = true;
        userData.SaveData(); 
        
        // 播放金币飞行特效（从按钮飞到顶部金币槽）
        CustomFlyInManager.Instance.FlyInGold(goldPoint, () =>
        {
            // 发放金币
            userData.UpdateGold(_rewardGold, true, true, "改名奖励");
            // EventDispatcher.instance.TriggerChangeGoldUI(_rewardGold, true);
            OnClose();
        }); 
        
        MessageSystem.Instance.ShowTip($"获得 {_rewardGold} 金币，昵称已更新");
        // 4. 领取成功后立即关闭窗口
      
    }
    
    private void OnClose()
    {
        Close();
    }

    protected override void OnDisable()
    {
        EventDispatcher.instance.TriggerUpdateLayerCoin(false, false, false);
        base.OnDisable();
    }
}