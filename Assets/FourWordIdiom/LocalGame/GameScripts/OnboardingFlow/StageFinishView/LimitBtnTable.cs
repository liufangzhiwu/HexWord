using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LimitBtnTable : MonoBehaviour
{
    
    [Header("UI LimitBtn")]
    public Button _limitTimeEventButton;
    public GameObject LockObj; //挂载锁头物体
    [SerializeField] private Image limitOver;
    [SerializeField] private Image lantern;
    [SerializeField] private Text txtwordprogress;
    [SerializeField] private GameObject LimitClaim;
    [SerializeField] private GameObject Worddouble;
    [SerializeField] private GameObject TimeObj;
    [SerializeField] private Text AddCount;
    [SerializeField] private GameObject Effect;
    [SerializeField] private GameObject lightAnim;


    public void InitUI()
    {
        AddCount.transform.DOLocalMoveY(-10, 0.1f);
        LimitClaim.GetComponentInChildren<Text>().text= MultilingualManager.Instance.GetString("ADPopReceive");
    }
    
     /// <summary>
    /// 检查并显示限时活动
    /// </summary>
    public void CheckAndShowLimitedTimeEvent(Transform parent)
    {
        InitLimtBtnUI();
        
        if (!_limitTimeEventButton.interactable) return;
        
        bool isJustUnlocked = false;
        
        if (GameDataManager.Instance.UserData.levelMode == 2) 
        {
            int playedStage = ChessStageController.Instance.CurrStageData.StageId; // 刚打完的关卡
            int unlockStage = AppGameSettings.UnlockRequirements.TimeLimitMode;    // 解锁要求的关卡
            
            // 如果玩家刚好打完门槛前一关，且底层进度刚好迈入了解锁关卡
            if (playedStage == unlockStage - 1 && GameDataManager.Instance.UserData.CurrentChessStage == unlockStage)
            {
                isJustUnlocked = true;
                Debug.Log($"[限时活动防蹭分] 刚打完第{playedStage}关新鲜解锁限时活动，绝不将本局进度白送给活动！");
            }
        }
        // 如果是被抓包的新鲜解锁玩家，直接 return，只亮图标展示解锁状态，绝不加分！
        if (isJustUnlocked)
        {
            return; 
        }
        
            // 限时活动逻辑
            LimitTimeManager.Instance.OnLimitTimeBtnUI += InitLimtBtnUI;                
            _limitTimeEventButton.gameObject.SetActive(true);
            LimitTimeManager.Instance.UpdateLimitTimeBtnUI();
            
            int wordcount = 0;
            switch ((LevelType)GameDataManager.Instance.UserData.levelMode)
            {
                case LevelType.BlockWord:
                    wordcount=StageHexController.Instance.LimitPuzzlecount;
                    break;
                case LevelType.ChessWord:
                    wordcount=ChessStageController.Instance.GetDoubleRewardedPuzzleCount();
                    break;
                case LevelType.HexWord:
                    wordcount=StageHexController.Instance.LimitPuzzlecount;//立刻清零
                    break;
            }
            
            if (!LimitTimeManager.Instance.IsComplete())
            {
                //txtwordprogress.text = wordcount + "/" + LimitTimeManager.Instance.CurlimitData.num;
                if (wordcount > 0)
                {
                    LimitTimeManager.Instance.UpdateLimitProgress(wordcount);
                    Effect.gameObject.SetActive(false);
                    AddCount.gameObject.SetActive(false);
                    AddCount.text = "+" + wordcount;
                    StartCoroutine(ShowLimitWordAnim(parent));
                }
                LimitTimeManager.Instance.UpdateLimitTimeBtnUI();
            }
            else
            {
                txtwordprogress.gameObject.SetActive(false);
                LimitClaim.gameObject.SetActive(false);
                Worddouble.gameObject.SetActive(false);
                limitOver.gameObject.SetActive(true);
                Effect.gameObject.SetActive(false);
            }
    }
    
    public void InitLimtBtnUI()
    {
        bool isLocked = !LimitTimeManager.Instance.IsOpen();
        if (isLocked)
        {
            // 强制显示底板和文本
            TimeObj.gameObject.SetActive(true);
            txtwordprogress.gameObject.SetActive(true);
            txtwordprogress.text = $"第{AppGameSettings.UnlockRequirements.TimeLimitMode}关";
            
            Worddouble.gameObject.SetActive(false);
            LimitClaim.gameObject.SetActive(false);
            limitOver.gameObject.SetActive(false);
            
            if (lightAnim != null) lightAnim.gameObject.SetActive(false);
            // 确保底板 Image 是开启的
            Image lockBg = TimeObj.GetComponent<Image>();
            if (lockBg != null) lockBg.enabled = true;
            
            if (txtwordprogress.transform.parent != null)
            {
                Image parentBg = txtwordprogress.transform.parent.GetComponent<Image>();
                if (parentBg != null) parentBg.enabled = true;
            }
            return;
        }
        
        TimeObj.gameObject.SetActive(!LimitTimeManager.Instance.IsClaim());
        if (!LimitTimeManager.Instance.IsComplete())
        {
            if (lightAnim != null) lightAnim.gameObject.SetActive(true);
            
            if (!LimitTimeManager.Instance.IsClaim())
            {
                Worddouble.gameObject.SetActive(LimitTimeManager.Instance.LimitTimeCanShow());
                int wordcount = LimitTimeManager.Instance.GetCurWordCount();
                txtwordprogress.gameObject.SetActive(true); // 确保文本是开启的
                txtwordprogress.text = wordcount + "/" + LimitTimeManager.Instance.CurlimitData.num;
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
        Image bg = TimeObj.GetComponent<Image>();
        if (bg != null) 
        {
            bg.enabled = (txtwordprogress.gameObject.activeSelf && !string.IsNullOrEmpty(txtwordprogress.text)) 
                         || limitOver.gameObject.activeSelf;
        }
    }
    
     /// <summary>
    /// 播放连词更新动画
    /// </summary>
    IEnumerator ShowLimitWordAnim(Transform parent)
    { 
        yield return new WaitForSeconds(1f);
        GameObject dengObj = Instantiate(lantern.gameObject,parent);
        AddCount.transform.SetParent(parent);
        AddCount.transform.localPosition=new Vector3(0f,0f,0f); 
        Effect.transform.SetParent(parent);
        Effect.transform.localPosition=new Vector3(0f,0f,0f); 
        
        dengObj.transform.localScale=new Vector3(1.2f,1.2f,1.2f);
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
        
        canvas.DOFade(1, 0.2f).OnComplete(() =>
        {
            dengObj.transform.DOLocalPath(MovePoints, 0.3f).OnComplete(() =>
            {
                Effect.gameObject.SetActive(true);
                AddCount.color=Color.clear;
                AddCount.gameObject.SetActive(true);
                InitLimtBtnUI();   
                Destroy(dengObj);
            });
            
            canvas.DOFade(1, 0.3f).OnComplete(() =>
            {
                AudioManager.Instance.PlaySoundEffect("levelOverLimitwordAward");
                lantern.transform.DOScale(new Vector3(1.2f,1.15f,1.15f), 0.3f).OnComplete(() =>
                {
                    AddCount.DOColor(Color.white,0.2f);
                    AddCount.transform.DOLocalMoveY(130, 1.3f).OnComplete(() =>
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
        if (LimitTimeManager.Instance != null)
        {
            LimitTimeManager.Instance.OnLimitTimeBtnUI -= InitLimtBtnUI;
        }
    }
}
