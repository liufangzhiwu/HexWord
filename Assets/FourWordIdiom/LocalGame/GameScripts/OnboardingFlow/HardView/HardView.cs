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

    public override void Close(CloseMethod method = CloseMethod.Default)
    {
        hardStageTable.SetActive(false);
        extrahardStageTable.SetActive(false);
        base.Close(method);
    }

    private void OnClosePanel()
    {
    
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
