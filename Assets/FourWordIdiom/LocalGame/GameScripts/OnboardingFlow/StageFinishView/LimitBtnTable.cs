using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LimitBtnTable : MonoBehaviour
{
    
    [Header("UI LimitBtn")]
    public Button _limitTimeEventButton;
    [SerializeField] private Image limitOver;
    [SerializeField] private Image lantern;
    [SerializeField] private Text txtwordprogress;
    [SerializeField] private GameObject LimitClaim;
    [SerializeField] private GameObject Worddouble;
    [SerializeField] private GameObject TimeObj;
    [SerializeField] private Text AddCount;
    [SerializeField] private GameObject Effect;


    public void InitUI()
    {
        AddCount.transform.DOLocalMoveY(-10, 0.1f);
        LimitClaim.GetComponentInChildren<Text>().text= MultilingualManager.Instance.GetString("ADPopReceive");
    }
    
     /// <summary>
    /// 检查并显示限时活动
    /// </summary>
    public void CheckAndShowLimitedTimeEvent()
    {
        if (GameDataManager.instance.UserData.CurrentStage >= AppGameSettings.UnlockRequirements.TimeLimitMode
            ||!string.IsNullOrEmpty(GameDataManager.instance.UserData.limitOpenTime))
        {
            // 限时活动逻辑
            LimitTimeManager.instance.OnLimitTimeBtnUI += InitLimtBtnUI;                
            _limitTimeEventButton.gameObject.SetActive(true);
            if (!LimitTimeManager.instance.IsComplete())
            {
                int wordcount = LimitTimeManager.instance.GetCurWordCount();
                txtwordprogress.text = wordcount + "/" + LimitTimeManager.instance.CurlimitData.num;
                LimitTimeManager.instance.UpdateLimitProgress(StageController.Instance.LimitPuzzlecount);
                Effect.gameObject.SetActive(false);
                AddCount.gameObject.SetActive(false);
                AddCount.text = "+" + StageController.Instance.LimitPuzzlecount;
                StartCoroutine(ShowLimitWordAnim());
            }
            
            if(GameDataManager.instance.UserData.CurrentStage > AppGameSettings.UnlockRequirements.TimeLimitMode
               ||!string.IsNullOrEmpty(GameDataManager.instance.UserData.limitOpenTime))
            {
                if (LimitTimeManager.instance.IsComplete())
                {
                    txtwordprogress.gameObject.SetActive(false);
                    LimitClaim.gameObject.SetActive(false);
                    Worddouble.gameObject.SetActive(false);
                    limitOver.gameObject.SetActive(true);
                    Effect.gameObject.SetActive(false);
                }
            }
            else
            {
                //_limitTimeEventButton.GetComponent<Animator>().enabled = true;
                AudioManager.Instance.PlaySoundEffect("BtnUnlock");
                AddCount.transform.DOScaleZ(1,1f).OnComplete(()=>
                {
                    SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
                    //_limitTimeEventButton.GetComponent<Animator>().enabled = false;
                });
            }
        }
        else
        {
            _limitTimeEventButton.gameObject.SetActive(false);
        }
    }
    
    public void InitLimtBtnUI()
    {
        TimeObj.gameObject.SetActive(!LimitTimeManager.instance.IsClaim());
        if (!LimitTimeManager.instance.IsComplete())
        {
            if (!LimitTimeManager.instance.IsClaim())
            {
                Worddouble.gameObject.SetActive(LimitTimeManager.instance.LimitTimeCanShow());
                int wordcount = LimitTimeManager.instance.GetCurWordCount();
                txtwordprogress.text = wordcount + "/" + LimitTimeManager.instance.CurlimitData.num;
                if (LimitClaim.activeSelf)
                {
                    LimitClaim.gameObject.SetActive(false);
                    LimitClaim.GetComponent<CanvasGroup>().alpha = 0;
                }
            }
            else
            {
                LimitClaim.gameObject.SetActive(true);
                Worddouble.gameObject.SetActive(false);
                LimitClaim.GetComponent<CanvasGroup>().DOFade(1,0.2f);
            }
            limitOver.gameObject.SetActive(false);
        }
        else
        {
            txtwordprogress.gameObject.SetActive(false);
            LimitClaim.gameObject.SetActive(false);
            Worddouble.gameObject.SetActive(false);
            limitOver.gameObject.SetActive(true);
        }
    }
    
     /// <summary>
    /// 播放连词更新动画
    /// </summary>
    IEnumerator ShowLimitWordAnim()
    { 
        yield return new WaitForSeconds(1f);
        GameObject dengObj = Instantiate(lantern.gameObject,lantern.transform);
        dengObj.transform.localScale=new Vector3(0.7f,0.7f,0.7f);
        dengObj.transform.SetAsLastSibling();
        dengObj.transform.localPosition=new Vector3(165f,165f,0f);
        CanvasGroup canvas = dengObj.GetComponent<CanvasGroup>();
        if (canvas == null)
        {
            canvas = dengObj.AddComponent<CanvasGroup>();
        }
        canvas.alpha = 0f;
        var midPos = (lantern.transform.localPosition + dengObj.transform.localPosition) / 2;
        var BezierMidPos = (midPos + dengObj.transform.localPosition) / 2 + Vector3.left * 50;
        Vector3[] MovePoints = CustomFlyInManager.Instance.CreatTwoBezierCurve(dengObj.transform.localPosition,lantern.transform.localPosition,BezierMidPos).ToArray();
        
        canvas.DOFade(1, 0.3f).OnComplete(() =>
        {
            dengObj.transform.DOLocalPath(MovePoints, 0.5f).OnComplete(() =>
            {
                Effect.gameObject.SetActive(true);
                AddCount.color=Color.clear;
                AddCount.gameObject.SetActive(true);
                InitLimtBtnUI();   
                Destroy(dengObj);
            });
            
            canvas.DOFade(1, 0.4f).OnComplete(() =>
            {
                AudioManager.Instance.PlaySoundEffect("levelOverLimitwordAward");
                lantern.transform.DOScale(new Vector3(1.2f,1.15f,1.15f), 0.3f).OnComplete(() =>
                {
                    AddCount.DOColor(Color.white,0.2f);
                    AddCount.transform.DOLocalMoveY(130, 0.3f).OnComplete(() =>
                    {
                        AddCount.DOColor(Color.white, 0.5f).OnComplete(() =>
                        {
                            AddCount.DOColor(Color.clear, 0.2f);
                        });
                    });
                    lantern.transform.DOScale(Vector3.one, 0.2f);
                    InitLimtBtnUI();
                });
            });
        });
    }

    private void OnDisable()
    {
        if(GameDataManager.instance.UserData.CurrentStage >= AppGameSettings.UnlockRequirements.TimeLimitMode)
        {
            LimitTimeManager.instance.OnLimitTimeBtnUI -= InitLimtBtnUI;
        }
    }
}
