using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/**
 * 道具与交互 (ChessPlayArea.Tools.cs)
 * 负责道具逻辑与 UI 刷新
 */
public partial class ChessPlayArea 
{
    /// <summary>
    /// 更新道具按钮
    /// </summary>
    /// <param name="value"></param>
    /// <param name="isfirst"></param>
    public void InitToolUI(int value =0, bool isfirst = false)
    {
        // Transform CompCost = CompleteBtn.transform.GetChild(0);
        Transform CompCount = CompleteBtn.transform.GetChild(1);
        Transform compText = CompCount.GetChild(0);
        Transform compAdd = CompCount.GetChild(1);
        if (GameDataManager.Instance.UserData.toolInfo[104].count > 0)
        {
            CompCount.gameObject.SetActive(true);
            compText.GetComponent<Text>().text = GameDataManager.Instance.UserData.toolInfo[104].count.ToString();
            compText.gameObject.SetActive(true);
            compAdd.gameObject.SetActive(false);
            // CompCost.gameObject.SetActive(false);
        }
        else
        {
            CompCount.gameObject.SetActive(false);
            // CompCost.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[104].cost.ToString();
            // CompCost.gameObject.SetActive(true);
            // compAdd.gameObject.SetActive(true);
            // compText.gameObject.SetActive(false);
        }

        // Transform HintCost = HitsBtn.transform.GetChild(0);
        Transform HintCount = HitsBtn.transform.GetChild(1);
        Transform hintText = HintCount.GetChild(0);
        Transform hintAdd = HintCount.GetChild(1);
        if (GameDataManager.Instance.UserData.toolInfo[102].count > 0)
        {
            HintCount.gameObject.SetActive(true);
            hintText.GetComponent<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].count.ToString();
            hintText.gameObject.SetActive(true);
            hintAdd.gameObject.SetActive(false);
            // HintCost.gameObject.SetActive(false);
        }
        else
        {
            HintCount.gameObject.SetActive(false);
            // HintCost.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].cost.ToString();
            // HintCost.gameObject.SetActive(true);
            // hintText.gameObject.SetActive(false);
            // hintAdd.gameObject.SetActive(true);
        }
    }
    public void SetToolButtonsEnabled(bool enabled)
    {
        // 按钮可交互
        if (CompleteBtn != null) CompleteBtn.interactable = enabled;
        if (HitsBtn != null) HitsBtn.interactable = enabled;

        // 禁用时的背景色：#C8C8C8 透明度 128
        Color disabledColor = new Color(200f / 255f, 200f / 255f, 200f / 255f, 128f / 255f);
        Color enabledColor = Color.white;

        // 像 InitToolUI 一样动态获取 CompCount 和 HintCount
        Transform compCount = CompleteBtn != null ? CompleteBtn.transform.GetChild(1) : null;
        Transform hintCount = HitsBtn != null ? HitsBtn.transform.GetChild(1) : null;

        // 设置自动完成数量背景
        if (compCount != null)
        {
            Image compImg = compCount.GetComponent<Image>();
            if (compImg != null) compImg.color = enabled ? enabledColor : disabledColor;
        }

        // 设置提示数量背景
        if (hintCount != null)
        {
            Image hintImg = hintCount.GetComponent<Image>();
            if (hintImg != null) hintImg.color = enabled ? enabledColor : disabledColor;
        }
    }
    public void UseComplete(bool isReset = false)
    {
        NotifyPlayerInteraction(); // 🌟 触发唤醒计时
        
        if (chessboardGrid.IsBlockInput || chessboardGrid.GameOver) return;
       
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[104];
        if(toolInfo == null) return;
        
        if(toolInfo.count <= 0)
        {
            GetItemScreen.limitRewordType = LimitRewordType.AutoComplete;
            // GetItemScreen.targetWord = GetCurrentSelectedPhrase(); // 🌟 赋值
            SystemManager.Instance.ShowPanel(PanelType.GetItemScreen);
            return;
        }

        if (CurrStageInfo.StageNumber == 5)
        {
     
            if (GameDataManager.Instance.UserData.ChessTutorialProgress[5])
            {
                usetoolCount++;
            }
            else
            {
                IsClickAuto = true;
            }
        }
        else
        {
            usetoolCount++;
        }
        chessboardGrid.IsBlockInput = true;
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, -1, "关卡内使用", GetCurrentSelectedPhrase());
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseAutoTool,1);
        InitToolUI();

        AudioManager.Instance.PlaySoundEffect("ItemUSe02");
        // 实现业务
        StartCoroutine(chessboardGrid.CompletedPhrase());

        // 触发新手引导检查
        HandleGamePlayCall(CompleteBtn.gameObject, "UseComplete");
    }
    
     /// <summary>
    /// 自动完成道具的“青蛙跳”光效
    /// </summary>
    public void PlayAutoCompleteJumpEffect(List<ChessView> targets, Action onComplete)
    {
        // 开启一个协程来完美接管时间轴
        StartCoroutine(JumpAndRevealCoroutine(targets, onComplete));
    }
    private IEnumerator JumpAndRevealCoroutine(List<ChessView> targets, Action onComplete)
    {
        if (targets == null || targets.Count == 0 || lightParticlePrefab == null) 
        {
            onComplete?.Invoke();
            yield break;
        }

        List<ChessView> emptyTargets = targets.Where(t => 
            t.CurrState == TileState.None || 
            t.CurrState == TileState.Error || 
            t.CurrState == TileState.Fill ||
            t.CurrState == TileState.Check).ToList();

        if (emptyTargets.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }
        
        GameObject particle = _lightParticlePool.GetObject(transform);
        particle.transform.position = CompleteBtn.transform.position; 
        particle.transform.SetAsLastSibling();
        
        Vector3 startPos = particle.transform.position;
        Vector3 firstTargetPos = emptyTargets[0].transform.position;
        Vector3[] firstPath = CreateBezierPath(startPos, firstTargetPos, -0.3f);
        
        Sequence seq = DOTween.Sequence();
        
        // 1. 飞到第 1 个格子
        seq.Append(particle.transform.DOPath(firstPath, 0.4f, PathType.Linear).SetEase(Ease.InOutSine));
        seq.AppendCallback(() => {
            // 删掉外面的 SetTipMessage，因为你的 PlayRevealAnimation 里面已经有了，防止重复调用！
            emptyTargets[0].PlayRevealAnimation1(emptyTargets[0].transform); 
        });
        
        // 2. 依次跳跃
        for (int i = 1; i < emptyTargets.Count; i++)
        {
            int currentIndex = i;
            // 🔥 核心修复：动态计算跳跃高度！绝对完美的青蛙跳比例！
            // 取“上一个格子”和“当前格子”的距离
            float distance = Vector3.Distance(emptyTargets[currentIndex - 1].transform.position, emptyTargets[currentIndex].transform.position);
            // 跳跃高度设定为距离的一半（比如相距 100 像素，就往上跳 50 像素）
            float jumpHeight = distance * 0.5f;
            seq.Append(particle.transform.DOJump(emptyTargets[currentIndex].transform.position, jumpHeight, 1, 0.2f).SetEase(Ease.Linear));
            
            seq.AppendCallback(() => {
                emptyTargets[currentIndex].PlayRevealAnimation1(emptyTargets[currentIndex].transform); 
            });
        }
        
        // 钻进去消失
        seq.Append(particle.transform.DOScale(Vector3.zero, 0.15f));
        
        // ==========================================
        // 🔥 核心时间轴控制：耐心等待特效播放完毕
        // ==========================================
        
        // 1. 死等 DOTween 的青蛙跳和飞行彻底结束
        yield return seq.WaitForCompletion();
        _lightParticlePool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
        // 2. 此时，最后一个格子的 PlayRevealAnimation 协程才刚刚被触发！
        // 你的协程逻辑是：等 0.2 秒 -> 弹文字缩放(0.3秒) -> 等 3.5 秒 -> 销毁。
        // 💡 为了最佳的爽快感：我们只等文字完美弹出来（0.2 + 0.3 = 0.5秒），就立刻变绿！
        // 千万不要等 3.5 秒特效全删了才变绿，那样玩家会觉得卡顿。背景残留着华丽的粒子时整句变绿，视觉冲击力最强！
        yield return new WaitForSeconds(0.35f);

        // 3. 时间刚刚好！通知游戏，播放整句变绿的成功波浪动画！
        onComplete?.Invoke();
    }
    
    
    
    /// <summary>
    /// 使用提示工具
    /// </summary>
    public void UseTips()
    {
        NotifyPlayerInteraction(); // 🌟 触发唤醒计时
        if (chessboardGrid.IsBlockInput || chessboardGrid.GameOver) return;
        
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[102];
        if (toolInfo == null) return;
        
        if(toolInfo.count <= 0)
        {
            GetItemScreen.limitRewordType = LimitRewordType.Tipstool;
            // GetItemScreen.targetWord = GetCurrentSelectedPhrase(); // 🌟 赋值
            SystemManager.Instance.ShowPanel(PanelType.GetItemScreen);
            return;
        }

        // 第二关新手引导 不计数
        if (CurrStageInfo.StageNumber == 2)
        {
            if (GameDataManager.Instance.UserData.ChessTutorialProgress[4])
            {
                usetoolCount++;
                ChessStageController.Instance.UseTipToolCount++;
            }
        }
        else
        {
            usetoolCount++;
            ChessStageController.Instance.UseTipToolCount++;
        }
        chessboardGrid.IsBlockInput = true;
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, -1, "关卡内使用",GetCurrentSelectedPhrase());
        InitToolUI();
        
        // chessboardGrid.SetSelectTip();
        
        AudioManager.Instance.PlaySoundEffect("ItemUSe01");
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseTipWordTool,1);
        StartCoroutine(FlyHintEffect(chessboardGrid.selecteTile));
        // 触发新手引导检查
        if (CurrStageInfo.StageNumber == 2)
        {
            BowlView hitBowl = puzzleTileTable.GridList
                .FirstOrDefault(bowl => bowl.letter == chessboardGrid.selecteTile.chesspiece.letter);
            HandleGamePlayCall(hitBowl!.gameObject, "UseTips");
        }
     
    }
    
        private IEnumerator FlyHintEffect(ChessView targetTile)
    {
        if (lightParticlePrefab == null) yield break;

        // 1. 在提示按钮的位置生成光效
        GameObject particle = _lightParticlePool.GetObject(transform);
        particle.transform.position = HitsBtn.transform.position;
        particle.transform.SetAsLastSibling(); // 放到最顶层
        // 🔥 解决“太小”的问题：初始设为0，瞬间放大到原来的 2.5倍 (倍数可根据你的预制体自己调)
        Vector3 targetScale = Vector3.one * 2.5f; 
        particle.transform.localScale = Vector3.zero;
        particle.transform.DOScale(targetScale, 0.2f).SetEase(Ease.OutBack);
        // 2. 计算动态距离和时长
        Vector3 startPos = particle.transform.position;
        Vector3 endPos = targetTile.transform.position;
        float duration = 0.5f;
        // 3. 生成贝塞尔弧线路径
        Vector3[] pathPoints = CreateBezierPath(startPos, endPos, 0.3f); // 150f是弧度，可以调大调小
        // 锁定屏幕防止飞行时玩家乱点
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        // 4. 沿着弧线飞行
        bool isFlying = true;
        particle.transform.DOPath(pathPoints, duration, PathType.Linear).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            isFlying = false;
        });
        particle.transform.DOScale(Vector3.zero, 0.15f).SetDelay(duration - 0.15f);
        // 等待飞到目标
        yield return new WaitUntil(() => !isFlying);
        
        // 3. 到达目标！销毁粒子
        _lightParticlePool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
        yield return StartCoroutine(chessboardGrid.ExecuteHintFillFlow(targetTile));
        
        EventDispatcher.instance.TriggerChangeTopRaycast(true);
        chessboardGrid.IsBlockInput = false;
    }
    /// <summary>
    /// 生成二阶贝塞尔曲线路径点 (完美适配任意分辨率和Canvas缩放)
    /// </summary>
    /// <param name="bendFactor">弯曲比例（0.2~0.5之间效果最好，正负代表向左/向右弯）</param>
    private Vector3[] CreateBezierPath(Vector3 start, Vector3 end, float bendFactor = 0.3f, int segments = 10)
    {
        Vector3[] path = new Vector3[segments + 1];
        Vector3 mid = (start + end) / 2f;
        
        // 1. 获取起点到终点的方向，并计算实际世界距离
        Vector3 dir = (end - start).normalized;
        float dist = Vector3.Distance(start, end);
        
        // 2. 计算出垂直于飞行方向的向量 (2D平面内的法线)
        Vector3 perpendicular = new Vector3(-dir.y, dir.x, 0); 
        
        // 3. 控制点：中点 + 垂直方向 * (总距离 * 弯曲比例)
        // 这样无论 UI 被缩放得多小，弧线永远是刚好鼓出去一截的完美状态！
        Vector3 controlPoint = mid + perpendicular * (dist * bendFactor);

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float u = 1 - t;
            path[i] = (u * u * start) + (2 * u * t * controlPoint) + (t * t * end);
        }
        return path;
    }
    private void UseButterfly()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[103];
        
        if ((toolInfo == null || toolInfo.count <= 0)&&!GameDataManager.Instance.UserData.butterflyTaskIsOpen)
        {
            Debug.LogError("蝴蝶道具数据为空！");
            // crossPuzzleGrid.SetPuzzleBoardState(true);
            butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(-300, 0.3f);
            return;
        }

        if (!GameDataManager.Instance.UserData.butterflyTaskIsOpen)
        {
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, -1,"关卡内使用");
        }
     
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseButterflyTool,1);
        useButterflyCount--;
        
        GameObject Effect_Butterfly = EffectButterFlays[useButterflyCount];
        butterflyObj.GetComponentInChildren<Text>().text = (useButterflyCount+1).ToString();
        Effect_Butterfly.gameObject.SetActive(false);
        
        if(useButterflyCount==0)
            AudioManager.Instance.PlaySoundEffect("showButterfly");
        
        ChessView selectView = chessboardGrid.GetRandomNoneNonTipChess();
        butterChess.Add(selectView);
        
        // ChessView  selectNext  蝴蝶搜索的位置
        // 播放起飞
        butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(0,0.3f).OnComplete(() =>
        {
           
            Vector3[] MovePoints = GetButterflyPath(butterflyObj.transform,selectView.transform.position + new Vector3(3f, 0,0));
       
            Effect_Butterfly.transform.DOScale(new Vector3(50, 50, 50), 0.1f).OnComplete(() =>
            {
                Effect_Butterfly.transform.DOLocalRotate(Vector3.zero,0f);
                Effect_Butterfly.gameObject.SetActive(true);
                butterflyObj.GetComponentInChildren<Text>().text = useButterflyCount.ToString();
                
                selectView.chesspiece.tip = true;
                Effect_Butterfly.transform.DOScale(new Vector3(50, 50, 50), 0.25f).OnComplete(() =>
                {
                    // if(useButterflyCount>0)
                    //     UseButterfly();
                    // else
                        butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(-300, 0.3f);
                }); 
                if(useButterflyCount>0)
                    UseButterfly();
            });
            
            Effect_Butterfly.transform.DOPath(MovePoints, 1.2f).SetEase(Ease.Linear).OnComplete(() =>
            {
                Effect_Butterfly.transform.DOLocalRotate(new Vector3(0f, 150f, 20f), 0f);
                Effect_Butterfly.transform.DOScale(new Vector3(40, 40, 40),0.1f);
                
                Vector3 endWorld = selectView.TileTransform.TransformPoint(selectView.TileTransform.rect.center);
                Vector3 endLocal = Effect_Butterfly.transform.parent.InverseTransformPoint(endWorld);

                Effect_Butterfly.transform.DOLocalMove(endLocal, 0.85f).SetEase(Ease.Linear).OnComplete(
                () => {
                    selectView.SetTipMessage();
                    Effect_Butterfly.transform.DOScale(new Vector3(40, 40, 40),0.4f).OnComplete(() =>
                    {
                        Effect_Butterfly.transform.DOLocalMoveY(1480, 0.7f);
                        Effect_Butterfly.transform.DOLocalMoveX( - 300,0.7f).SetEase(Ease.Linear).OnComplete(() =>
                        {
                            Effect_Butterfly.transform.localPosition = new Vector3(-300f,0f,0f);
                            Effect_Butterfly.gameObject.SetActive(false);
                            Effect_Butterfly.transform.DOLocalRotate(Vector3.zero,0f);

                            if (useButterflyCount < 1)
                            {
                                EventDispatcher.instance.TriggerChangeTopRaycast(true);
                            }
                        });
                        
                    });
                });
            });
            
        });
    }
    private Vector3[] GetButterflyPath(Transform starttrans, Vector3 endPos)
    {
        Vector3 butterflyEndPos = endPos;
        var midPos = (butterflyEndPos + starttrans.position) / 1.5f;
        var bezierMidPos = (midPos + starttrans.position) / 2; // + Vector3.right * 8;
        Vector3[] movePoints = CustomFlyInManager.Instance.CreatTwoBezierCurve(starttrans.position,butterflyEndPos,bezierMidPos).ToArray();
        return movePoints;
    }
}
