using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class Launch : MonoBehaviour
{
    public static Launch Instance;
    [SerializeField] private Button _ageTip;

    private float timer = 0f;
    public bool isTiming = false;

    public GameFlowStatus flowStatus = GameFlowStatus.NotStarted;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    // Start is called before the first frame update
    private IEnumerator Start()
    {
        yield return null;
        UnityMainThreadDispatcher.Instance();
        MultilingualManager.Instance.LoadLocalization();
        GameDataManager.Instance.LoadPlayerProfile();
        transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(false);
        _ageTip.AddClickAction(OnAgeTipClick);
        yield return new WaitForSeconds(0.3f);

        if (!GameDataManager.Instance.UserData.IsAgreePrivacy)
        {
#if UNITY_IOS
             GameDataManager.Instance.UserData.IsAgreePrivacy = true;
             isTiming = true;
#elif UNITY_huawei||UNITY_ANDROID
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
        if (timer >= 2f)
        {
            isTiming = false;
            OpenNextPage();
        }
    }

    public void OpenNextPage()
    {
        // 移除登录逻辑，直接初始化游戏
        Game.self.InitGame();
        Debug.Log("完成初始化游戏服务流程");
        flowStatus = GameFlowStatus.LoggingIn;
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