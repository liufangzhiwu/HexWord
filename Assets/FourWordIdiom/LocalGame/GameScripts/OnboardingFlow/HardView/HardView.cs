using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


public class HardView : UIWindow
{
    [SerializeField] private GameObject hardStageTable; // 关闭按钮
    [SerializeField] private GameObject extrahardStageTable; // 关闭按钮
    

    protected override void OnEnable()
    {
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        InitUI();
    }

    private void InitUI()
    {
        switch (StageHexController.Instance.CurLevelMode)
        {
            case LevelModes.Normal:
                break;
            case LevelModes.Hard:
                hardStageTable.SetActive(true);
                extrahardStageTable.SetActive(false);
                break;
            case LevelModes.ExtraHard:
                hardStageTable.SetActive(false);
                extrahardStageTable.SetActive(true);
                break;
        }
    }
    

    private void OnClosePanel()
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
