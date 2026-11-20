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
            // 用 SubscribeLimitTimeBtnUI 做去重订阅：本方法每次结算都会调用，
            // 而对象若一直 inactive 则 OnDisable 不触发、旧写法会让同一委托被反复 +=
            SubscribeLimitTimeBtnUI();
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
                    // ★ 进度累加必须执行：拼字/六边形结算都靠这里把本局词数记进限时活动
                    LimitTimeManager.Instance.UpdateLimitProgress(wordcount);
                    Effect.gameObject.SetActive(false);
                    AddCount.gameObject.SetActive(false);
                    AddCount.text = "+" + wordcount;
                    // ★ 灯笼飞入只是纯视觉动画，必须在对象激活时才能起协程：
                    //   ChessFinishView.prefab 里把整个 LImitTime 覆盖成 m_IsActive=0（拼字结算不展示限时入口），
                    //   而子按钮 LimitTimeBtn 的 activeSelf 仍是 true，调用方按 activeSelf 判断会照旧调进来，
                    //   直接 StartCoroutine 就会报 "Coroutine couldn't be started because the game object 'LImitTime' is inactive"
                    //   并把调用方 PlayRewardSequence 整个中断（下一步按钮/任务/竞速动画全停）。不可见时跳过动画即可。
                    if (isActiveAndEnabled)
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
        // 锁定态在按钮右上角显示锁头（预制体上的 limitlock 节点）
        if (LockObj != null) LockObj.SetActive(isLocked);
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
                // CurlimitData 可能为 null（活动未开放 / 已领完最后一段配置），判空后再拼文本
                LimitDataItem curLimitData = LimitTimeManager.Instance.CurlimitData ?? LimitTimeManager.Instance.EnsureCurLimitData();
                txtwordprogress.gameObject.SetActive(true); // 确保文本是开启的
                txtwordprogress.text = curLimitData != null ? (wordcount + "/" + curLimitData.num) : wordcount.ToString();
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

    /// <summary>是否已订阅 LimitTimeManager.OnLimitTimeBtnUI，防止重复 += 累积</summary>
    private bool _limitTimeBtnUISubscribed;

    private void OnEnable()
    {
        SubscribeLimitTimeBtnUI();
    }

    private void OnDisable()
    {
        UnsubscribeLimitTimeBtnUI();
    }

    /// <summary>
    /// 对象真正被销毁时兜底退订：
    /// 若本节点从创建起就是 inactive（如 ChessFinishView 把 LImitTime 覆盖为关闭），
    /// OnEnable/OnDisable 都不会触发，只有 OnDestroy 能保证解绑，避免事件持有已销毁对象。
    /// </summary>
    private void OnDestroy()
    {
        UnsubscribeLimitTimeBtnUI();
    }

    /// <summary>订阅限时活动按钮刷新事件（幂等，可重复调用）</summary>
    private void SubscribeLimitTimeBtnUI()
    {
        if (_limitTimeBtnUISubscribed) return;
        if (LimitTimeManager.Instance == null) return; // 管理器尚未就绪时放弃，后续调用会再试
        LimitTimeManager.Instance.OnLimitTimeBtnUI += InitLimtBtnUI;
        _limitTimeBtnUISubscribed = true;
    }

    /// <summary>退订限时活动按钮刷新事件（幂等）</summary>
    private void UnsubscribeLimitTimeBtnUI()
    {
        if (!_limitTimeBtnUISubscribed) return;
        if (LimitTimeManager.Instance != null)
            LimitTimeManager.Instance.OnLimitTimeBtnUI -= InitLimtBtnUI;
        _limitTimeBtnUISubscribed = false;
    }
}
