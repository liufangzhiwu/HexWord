using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.HuaweiAppGallery;
using UnityEngine.UI;

public class Launch : MonoBehaviour
{
    [SerializeField] private Button _ageTip;

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
        yield return new WaitForSeconds(0.3f);

        if (!GameDataManager.Instance.UserData.IsAgreePrivacy)
        {
#if UNITY_HUAWEI
             GameDataManager.Instance.UserData.IsAgreePrivacy = true;
             isTiming = true;
#elif UNITY_OPENHARMONY||IOS
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
        if (timer >= 3f)
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