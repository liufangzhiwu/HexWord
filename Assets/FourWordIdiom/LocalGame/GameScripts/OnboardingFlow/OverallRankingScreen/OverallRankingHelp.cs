using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OverallRankingHelp : UIWindow
{
    [SerializeField] private Button closeBtn;
    [SerializeField] private Text titleText;
    [SerializeField] private Text help1Text;
    [SerializeField] private Text help2Text;
    [SerializeField] private Text help3Text;
    [SerializeField] private Text help4Text;
    [SerializeField] private Text help5Text;
    [SerializeField] private Text closetips;


    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        closeBtn.AddVibraClickAction(()=>base.Close());
    }
    // Start is called before the first frame update
    protected  void Start()
    {
        // 多语言
        titleText.text = MultilingualManager.Instance.GetString("MonthlyWorthy","hudie");
        
        help1Text.text = MultilingualManager.Instance.GetString("BeatLevel","hudie");
        help2Text.text = MultilingualManager.Instance.GetString("CollectLotus","hudie");
        help3Text.text = MultilingualManager.Instance.GetString("EnterTop","hudie");
        help4Text.text = MultilingualManager.Instance.GetString("AcquireBox","hudie");
        help5Text.text = MultilingualManager.Instance.GetString("JoinWorthy","hudie");
        closetips.text = MultilingualManager.Instance.GetString("ButterflyUI08","hudie");
    }
    
}
