using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class OtherPeopleScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button SeeButterflyBtn; // 关闭按钮
    [SerializeField] private Button LikeButton; // LikeButton
   
    [SerializeField] private Text HeaderText; //标题文本
    [SerializeField] private Text zanCountText; //标题文本
    
    [SerializeField] private NameInfoTable nameInfoTable; //标题文本
    [SerializeField] private FillWordTable fillWordTable; //标题文本
    [SerializeField] private flowerWordTable nflowerWordTable; //标题文本
    [SerializeField] private MonthRankTable monthRankTable; //标题文本
    
    protected void Start()
    {
       //InitHeadIconList();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
       
        //HeaderText.text = MultilingualManager.Instance.GetString("CharacterInfoTitle");
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
        
        nameInfoTable.InitOtherUI(GameCoreManager.Instance.otherPersonProfile.likes_count);
        fillWordTable.InitOtherUI();
        nflowerWordTable.InitOtherUI();
        monthRankTable.InitOtherUI();
        
        zanCountText.text =String.Format("已获 <size=55>{0}</size> 次赞",GameCoreManager.Instance.otherPersonProfile.likes_count);

        UpdateLikeCount(GameCoreManager.Instance.otherPersonProfile.likes_count,GameCoreManager.Instance.otherPersonProfile.has_liked);
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
        LikeButton.AddClickAction(OnClickLikeBtn); // 绑定关闭按钮事件
        SeeButterflyBtn.AddClickAction(OnClickSeeBtn); // 绑定关闭按钮事件
    }
    
    private void OnClickSeeBtn()
    {
        SystemManager.Instance.ShowPanel(PanelType.OtherPersonButterScreen);
    }

    private void OnClickLikeBtn()
    {
        // // 禁止给自己点赞
        // if (GameCoreManager.Instance.otherPersonProfile.user_id == GameDataManager.Instance.UserData.PlayerId)
        // {
        //     MessageSystem.Instance.ShowTip("不可以给自己点赞！");
        //     return;
        // }

        // 发起点赞/取消点赞请求
        StartCoroutine(APIGateway.Instance.SocialApi.LikeUser(
            GameCoreManager.Instance.otherPersonProfile.user_id.ToString(),
            (res) =>
            {
                if (res != null)
                {
                    // 根据返回的 action 判断当前是否为“已赞”状态
                    bool isLiked = (res.action == "like");
                    // 更新 UI（赞数和按钮状态）
                    UpdateLikeCount(res.likes_count, isLiked);
                }
                else
                {
                    // 请求失败提示
                    MessageSystem.Instance.ShowTip("点赞操作失败，请稍后重试");
                }
            }
        ));
    }

    /// <summary>
    /// 更新点赞 UI
    /// </summary>
    /// <param name="count">最新点赞数</param>
    /// <param name="isLiked">当前是否已点赞（true=已赞，false=未赞）</param>
    private void UpdateLikeCount(int count, bool isLiked)
    {
        zanCountText.text =String.Format("已获 <size=55>{0}</size> 次赞",count);
        if (LikeButton != null)
        {
            LikeButton.gameObject.transform.GetChild(0).gameObject.SetActive(!isLiked);
            LikeButton.gameObject.transform.GetChild(1).gameObject.SetActive(isLiked);
        }
        
        nameInfoTable.InitOtherUI(count);
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
