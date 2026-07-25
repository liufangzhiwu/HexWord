using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ZenRankHelpScreen : UIWindow
{
    [SerializeField] private Button closeBtn;
    [SerializeField] private Text wordtips;
    [SerializeField] private Text titleImage;
    [SerializeField] private Text slidertips;
    [SerializeField] private Text rewardtips;
    [SerializeField] private Text zenuptips;
    [SerializeField] private Text closetips;


    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        closeBtn.AddVibraClickAction(()=>base.Close());
       
    }
    // Start is called before the first frame update
    protected  void Start()
    {
        // titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas()
        
        // 多语言
        titleImage.text = MultilingualManager.Instance.GetString("MeditationList");
        wordtips.text = MultilingualManager.Instance.GetString("CollectLotus");
        slidertips.text = MultilingualManager.Instance.GetString("limitedRewardsDes03");
        rewardtips.text = MultilingualManager.Instance.GetString("EnterTop");
        zenuptips.text = MultilingualManager.Instance.GetString("ImproveRanking");
        closetips.text = MultilingualManager.Instance.GetString("limitedRewardsDes05");
    }
    
}
