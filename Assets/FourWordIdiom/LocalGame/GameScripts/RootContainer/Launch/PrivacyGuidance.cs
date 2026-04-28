using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PrivacyGuidance : MonoBehaviour
{
    [SerializeField] private Text _titleText;
    [SerializeField] private HyperlinkText _descriptionText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _rejectButton;
    // Start is called before the first frame update
    void Start()
    {
        _descriptionText.text = MultilingualManager.Instance.GetString("PrivacyAgreement01_General");
        _descriptionText.onHyperlinkClick = OnClickText;
        _confirmButton.AddClickAction(OnConfirmClick);
        _rejectButton.AddClickAction(OnRejectClick);
    }

    private void OnRejectClick()
    {
        gameObject.SetActive(false);
        GameObject go = Resources.Load<GameObject>("Privacy/PrivacyReject");
        GameObject pr = Instantiate(go, transform.parent);
        pr.SetActive(true);
    }

    private void OnConfirmClick()
    {
        // 向下一个场景发送确认信息, 
        GameDataManager.Instance.UserData.IsAgreePrivacy = true;
        GameDataManager.Instance.UserData.SaveData();
        StartCoroutine(LoadingSequence());
    }

    private IEnumerator LoadingSequence()
    {
        yield return new WaitForSeconds(0.5f);
        transform.parent.GetComponent<Launch>().OpenNextPage();
        gameObject.SetActive(false);
    }
    
    void OnClickText(string url)
    {
        Debug.Log("点击"+url);
        Application.OpenURL(url);
    }
}