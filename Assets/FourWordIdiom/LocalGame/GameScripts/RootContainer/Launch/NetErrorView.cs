using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class NetErrorView : MonoBehaviour
{
    [SerializeField] private HyperlinkText _descriptionText;
    [SerializeField] private Button quitBtn;
    [SerializeField] private Button CancleButton;
    
    [SerializeField] private Button tryAgainBtn;
    // Start is called before the first frame update
    void Start()
    {
        tryAgainBtn.AddClickAction(OnTryAgainClick);
        quitBtn.AddClickAction(OnQuitGameClick);
        CancleButton.AddClickAction(OnCancleClick);
    }

    private void OnEnable()
    {
        switch (Game.self.CurrentErrorType)
        {
            case CommonErrorType.LoginFail:
                ShowLoginErrorPanel();
                break;
            case CommonErrorType.ExitPopup:
                ShowQuitGamePanel();
                break;
        }
    }

    public void ShowQuitGamePanel()
    {
        _descriptionText.text = MultilingualManager.Instance.GetString("ExitPopup");
        quitBtn.gameObject.SetActive(true);
        CancleButton.gameObject.SetActive(true);
        tryAgainBtn.gameObject.SetActive(false);
    }
    
    public void ShowLoginErrorPanel()
    {
        _descriptionText.text = MultilingualManager.Instance.GetString("LoginFail");
        
        quitBtn.gameObject.SetActive(false);
        CancleButton.gameObject.SetActive(false);
        tryAgainBtn.gameObject.SetActive(true);
    }

    private void OnTryAgainClick()
    {
        LoadingController.self.StartLoading();
        transform.gameObject.SetActive(false);
    }
    
    private void OnQuitGameClick()
    {
        Application.Quit();
    }

    private void OnCancleClick()
    {
        transform.gameObject.SetActive(false);
    }

}