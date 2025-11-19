// using System;
// using System.Collections;
// using DG.Tweening;
// using UnityEngine;
// using UnityEngine.UI;
//
// public class LimitedDiscountScreen : UIBase
// {
//     [SerializeField] private Button closeBtn; // 关闭按钮
//     [SerializeField] private Text title; // 音效文本显示
//     [SerializeField] private Text time; // 语言选择文本显示
//     [SerializeField] private GameObject rewardItemParent;
//     [SerializeField] private Button ClaimBtn;
//  
//     protected override void OnEnable()
//     {
//         base.OnEnable();
//         InitUI();
//         AudioManager.Instance.PlaySoundEffect("ShowUI");
//         EventManager.OnUpdateLayerCoin?.Invoke(true,false);
//     }
//
//     private void InitUI()
//     {
//         title.text = LanguageManager.Instance.GetString("ADPopTitle");
//         ClaimBtn.GetComponentInChildren<Text>().text= LanguageManager.Instance.GetString("ADPopReceive");
//     }
//
//     protected override void InitButton()
//     {
//         closeBtn.AddClick(OnCloseBtn); // 绑定关闭按钮事件
//         ClaimBtn.AddClick(OnCloseBtn);
//     }
//     
//     private void OnCloseBtn()
//     {
//         base.HidePanel(); // 隐藏面板
//     }
//     
//     public override void OnHideAniEnd()
//     {
//         base.OnHideAniEnd();
//     }
//
//     protected override void OnDisable()
//     {
//         base.OnDisable();
//         ClaimBtn.interactable = true;
//         closeBtn.interactable = true;
//         if(UIManager.Instance.AnyPopPanelIsShowing())
//             EventManager.OnUpdateLayerCoin?.Invoke(true,true);
//         else
//         {
//             EventManager.OnUpdateLayerCoin?.Invoke(false,true);
//         }
//     }
// }
