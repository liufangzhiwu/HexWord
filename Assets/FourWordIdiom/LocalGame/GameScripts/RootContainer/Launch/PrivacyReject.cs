using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PrivacyReject : MonoBehaviour
{
    [SerializeField] private HyperlinkText _descriptionText;
    [SerializeField] private Button _callbackButton;
    [SerializeField] private Button _rejectButton;
    // Start is called before the first frame update
    void Start()
    {
        _descriptionText.text = MultilingualManager.Instance.GetString("PrivacyAgreement07");
        _descriptionText.onHyperlinkClick = OnClickText;
        _callbackButton.AddClickAction(OnCallbackClick);
        _rejectButton.AddClickAction(OnRejectClick);
    }

    private void OnRejectClick()
    {
        Application.Quit();
    }

    private void OnCallbackClick()
    {
        gameObject.SetActive(false);
        transform.parent.GetComponentInChildren<PrivacyGuidance>(true).gameObject.SetActive(true);
    }
    
    void OnClickText(string url)
    {
        Debug.Log("点击"+url);
        Application.OpenURL(url);
    }
}
