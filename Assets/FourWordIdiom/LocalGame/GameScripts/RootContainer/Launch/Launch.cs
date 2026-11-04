using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_HUAWEI
using UnityEngine.HuaweiAppGallery;
#endif
using UnityEngine.UI;

public class Launch : MonoBehaviour
{
    [SerializeField] private Button _ageTip;

    /// <summary>启动页停留时长（原 3s，现按发行要求定为 2.2s）</summary>
    private const float SplashHoldSeconds = 2.2f;

    private float timer = 0f;
    public bool isTiming = false;
    private void OnEnable()
    {
        MultilingualManager.Instance.LoadLocalization();
        GameDataManager.Instance.LoadPlayerProfile();

        StartCoroutine(InitUI());
    }

    // Start is called before the first frame update
    private IEnumerator InitUI()
    {
        transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(false);
        _ageTip.AddClickAction(OnAgeTipClick);
        yield return new WaitForSeconds(0.15f);

        if (!GameDataManager.Instance.UserData.IsAgreePrivacy)
        {
#if UNITY_HUAWEI
             GameDataManager.Instance.UserData.IsAgreePrivacy = true;
             isTiming = true;
#elif UNITY_OPENHARMONY||UNITY_IOS
            yield return new WaitForSeconds(2f);
            GameObject pg = Resources.Load<GameObject>("Privacy/PrivacyGuidance");
            GameObject ps = Instantiate(pg, transform);
            ps.SetActive(true);
#endif
        }
        else
        {
            isTiming = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!isTiming) return;
        timer += Time.deltaTime;
        if (timer >= SplashHoldSeconds)
        {
            isTiming = false;
            OpenNextPage();
        }
    }

    public void OpenNextPage()
    {
        gameObject.SetActive(false);
        transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(true);
    }
    
    private void OnAgeTipClick()
    {
        GameObject go = Resources.Load<GameObject>("Privacy/AgeWindow");
        GameObject aw = Instantiate(go, transform);
        aw.SetActive(true);
    }
}