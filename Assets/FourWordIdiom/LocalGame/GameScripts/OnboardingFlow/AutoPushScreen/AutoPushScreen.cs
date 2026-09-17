using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class AuPushScreen : UIWindow
{
    [Header("UI Elements")]
    [SerializeField] private Button agreeButton;
    [SerializeField] private Button refuseButton;
    [SerializeField] private Button closeButton;
   
    // Start is called before the first frame update
    void Start()
    {
        agreeButton.AddClickAction(OnAgreeButtonClicked);
        refuseButton.AddVibraClickAction(OnRefuseButtonClicked);
        closeButton.AddVibraClickAction(OnCloseClicked);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }
    
    private void OnAgreeButtonClicked()
    {
        GameDataManager.Instance.UserData.IsAutoPush = true;
        
        Game.self.Pushs.RequestEnableNotification();
    }
    
    private void OnRefuseButtonClicked()
    {
        GameDataManager.Instance.UserData.IsAutoPush = false;
    }
    
    private void OnCloseClicked()
    {
        Close();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }
}