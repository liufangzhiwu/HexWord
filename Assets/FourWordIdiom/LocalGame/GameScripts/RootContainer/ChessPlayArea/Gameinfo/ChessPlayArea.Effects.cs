using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/**
 *表现层与动效 (ChessPlayArea.Effects.cs)
 * 负责所有的 DOTween 动画、粒子与飘字
 */
public partial class ChessPlayArea
{
      /// <summary>
    /// 播放入场动画, 建议在面板打开/初始化完成时调用这个方法
    /// </summary> 
    /// <param name="onComplete">动画播放完毕后的回调函数（可选）</param>
    public void PlayEnterAnimation(Action onComplete = null)
    {
        // 创建 DOTween 序列，编排入场节奏
        Sequence enterSeq = DOTween.Sequence();
        // 获取两个新的父节点
        Transform puzzleParent = puzzleTileTable != null ? puzzleTileTable.transform.parent : null;
        Transform btnGroupParent = HitsBtn != null ? HitsBtn.transform.parent : null;
        // --- 步骤 A：顶部关卡文字淡入并下落 ---
        if (Stagetxt != null)
        {
            enterSeq.Append(Stagetxt.rectTransform.DOAnchorPosY(Stagetxt.rectTransform.anchoredPosition.y - 50f, 0.4f).SetEase(Ease.OutBack));
            enterSeq.Join(Stagetxt.DOFade(1f, 0.4f));
        }
        // 👇 🌟 新增步骤：禅意分数面板 Q弹放大 (在时间轴 0.2秒 时触发)
        if (zentable != null)
        {
            enterSeq.Insert(0.2f, zentable.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));
        }
        
        // --- 步骤 B：上方棋盘整体淡入 ---
        if (chessboardGrid != null && chessboardGrid.TryGetComponent<CanvasGroup>(out var gridCG))
        {
            enterSeq.Append(gridCG.DOFade(1f, 0.5f).SetEase(Ease.InOutSine));
        }
        // --- 步骤 C：下方待选字盘【父节点】滑入并淡入 ---
        if (puzzleParent != null)
        {
            RectTransform parentRect = puzzleParent.GetComponent<RectTransform>();
            if (parentRect != null)
            {
                enterSeq.Insert(0.2f, parentRect.DOAnchorPosY(parentRect.anchoredPosition.y + 300f, 0.5f).SetEase(Ease.OutCubic));
            }
            if (puzzleParent.TryGetComponent<CanvasGroup>(out var tableCG))
            {
                enterSeq.Insert(0.2f, tableCG.DOFade(1f, 0.5f)); 
            }
        }
        // --- 步骤 D: 按钮【父节点】整体淡入 ---
        if (btnGroupParent != null && btnGroupParent.TryGetComponent<CanvasGroup>(out var btnCG))
        {
            // 整个按钮组一起在 0.6 秒处平滑淡入
            enterSeq.Insert(0.6f, btnCG.DOFade(1f, 0.4f).SetEase(Ease.InOutSine));
        }
        
        enterSeq.OnComplete(() =>
        {
            // 如果传入了回调方法，就执行它
            onComplete?.Invoke();
        });
    }
      
    /// <summary>
    /// 重置UI状态，防止重复打开时动画错乱
    /// </summary>
    private void PrepareForAnimation()
    {
        // 获取两个新的父节点
        Transform puzzleParent = puzzleTileTable != null ? puzzleTileTable.transform.parent : null;
        Transform btnGroupParent = HitsBtn != null ? HitsBtn.transform.parent : null;
        if (_comboScreenFX != null) 
        {
            _comboScreenFX.SetActive(false);
        }
        // ==========================================
        // 1. 杀掉旧动画
        // ==========================================
        if (Stagetxt != null) { DOTween.Kill(Stagetxt.rectTransform); DOTween.Kill(Stagetxt); }
        if (chessboardGrid != null) 
        {
            DOTween.Kill(chessboardGrid.transform);
            if (chessboardGrid.TryGetComponent<CanvasGroup>(out var gridCG)) DOTween.Kill(gridCG);
        }
    
        if (puzzleParent != null)
        {
            DOTween.Kill(puzzleParent);
            if (puzzleParent.TryGetComponent<CanvasGroup>(out var tableCG)) DOTween.Kill(tableCG);
        }

        if (btnGroupParent != null) 
        {
            DOTween.Kill(btnGroupParent);
            if (btnGroupParent.TryGetComponent<CanvasGroup>(out var btnCG)) DOTween.Kill(btnCG);
        }
        if (zentable != null) DOTween.Kill(zentable.transform);
        // ==========================================
        // 2. 强制初始状态 (隐藏)
        // ==========================================
        if (Stagetxt != null) 
        {
            Color c = Stagetxt.color; c.a = 0f; Stagetxt.color = c; 
            Stagetxt.rectTransform.anchoredPosition = new Vector2(Stagetxt.rectTransform.anchoredPosition.x, Stagetxt.rectTransform.anchoredPosition.y + 50f);
        }

        if (chessboardGrid != null) 
        {
            chessboardGrid.transform.localScale = Vector3.one;
            if (chessboardGrid.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 0f;
        }

        // 按钮父级透明度降为 0
        if (btnGroupParent != null) 
        {
            btnGroupParent.localScale = Vector3.one;
            if (btnGroupParent.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 0f;
        }

        // 字库父级初始位置往下偏移 300，透明度 0
        if (puzzleParent != null)
        {
            RectTransform tableRect = puzzleParent.GetComponent<RectTransform>();
            if (tableRect != null)
                tableRect.anchoredPosition = new Vector2(tableRect.anchoredPosition.x, tableRect.anchoredPosition.y - 300f);
        
            if (puzzleParent.TryGetComponent<CanvasGroup>(out var tableCG)) tableCG.alpha = 0f;
        }
        if (zentable != null) zentable.transform.localScale = Vector3.zero;
    }
    
     /// <summary>
    /// 🌟 新增：串联“禅意分位置起飞 ➔ 屏幕中心消失 ➔ 弹出常驻横幅”的视觉工作流
    /// </summary>
    private IEnumerator PlayZenToCenterBannerFlow(Action onComplete)
    {
        // 1. 动态索取或生成过关专用的飞行拖尾粒子
        if (_bannerLiziCache == null)
        {
            GameObject liziPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", "EndOne");
            if (liziPrefab != null)
            {
                _bannerLiziCache = Instantiate(liziPrefab, this.transform);
            }
        }
        Vector3 endLocal = transform.position;
        if (_bannerLiziCache != null)
        {
            // _bannerLiziCache.SetActive(true);
            _bannerLiziCache.transform.SetAsLastSibling();
            
            // Transform effectLightTrans = _bannerLiziCache.transform.Find("EffectLight");
            Transform lightYeTrans = _bannerLiziCache.transform.Find("EffectLight/Light_ye");
            Transform effectPointTrans = _bannerLiziCache.transform.Find("EffectPoint"); // ✨ 新增：找到 EffectPoint
            // ParticleSystem lightYePs = lightYeTrans != null ? lightYeTrans.GetComponent<ParticleSystem>() : null;
            if (lightYeTrans != null) 
            {
                lightYeTrans.gameObject.SetActive(false);  // ✨ 核心修复：前 0.5 秒强制它隐藏！绝对不准发光！
            }
            
            RectTransform liziRect = _bannerLiziCache.GetComponent<RectTransform>();
            Vector3 startWorld = zentable != null ? zentable.transform.position : transform.position;
            Vector3 startLocal = transform.InverseTransformPoint(startWorld);
            startLocal.z = 0f; // 强行抹平 Z 轴，防止受相机深度影响乱飞
            liziRect.localPosition = startLocal;
            
            // 净化物理残留
            var trails = _bannerLiziCache.GetComponentsInChildren<TrailRenderer>(true);
            foreach (var t in trails) t.Clear();
            var pss = _bannerLiziCache.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in pss) { p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            
            _bannerLiziCache.SetActive(true);
            effectPointTrans.gameObject.SetActive(true);
             yield return new WaitForSeconds(1.5f);
            AudioManager.Instance.PlaySoundEffect("result_chest_open",0,1);
            lightYeTrans.gameObject.SetActive(true);
            effectPointTrans.gameObject.SetActive(false);
            
            RectTransform gridRect = chessboardGrid.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            gridRect.GetWorldCorners(corners);
            Vector3 endWorld = (corners[0] + corners[3]) / 2f;
            endLocal = transform.InverseTransformPoint(endWorld);
            endLocal.z = 0f; // 再次抹平 Z 轴防抖
            // float yOffset = 80f; 
            endLocal.y += 155f;
            bool isFlyDone = false;
            // 飞行动画：平滑向中心聚拢
            liziRect.DOLocalMove(endLocal, 0.75f).SetEase(Ease.InOutSine).OnComplete(() => {
                isFlyDone = true;
            });
            
            yield return new WaitUntil(() => isFlyDone);
            DOVirtual.DelayedCall(0.5f, () => 
            {
                if (_bannerLiziCache != null) 
                {
                    _bannerLiziCache.SetActive(false);
                }
            });
            _bannerLiziCache.SetActive(false); // 抵达中心瞬间彻底消失
        }

        // 2. 粒子消失的刹那，原地无缝唤醒无蒙版高性能过关横幅
        bool isBannerFinished = false;
        ShowNewBannerEffect(endLocal,() => isBannerFinished = true);
        yield return new WaitUntil(() => isBannerFinished);

        onComplete?.Invoke();
    }
     
    /// <summary>
    /// 动态加载并展示横幅，处理超过百分比文本
    /// </summary>
    private void ShowNewBannerEffect(Vector3 targetLocalPos, Action onComplete)
    {
        // 1. 获取控制器刚才算好的数据
        var rule = ChessStageController.Instance.CurrentMatchedRule;
        int styleNumber = ChessStageController.Instance.CurrentBannerStyle; // 这应该是 1, 2, 3, 4
        // 2. 动态加载对于的预制体 (假设预制体名字叫 UIEffect_Banner_1 等)
        GameObject activeBanner = null;
        if (_bannerCachePool.TryGetValue(styleNumber, out activeBanner) && activeBanner != null)
        {
            // 命中缓存！直接激活，无需再次消耗 CPU 去 Instantiate
            activeBanner.SetActive(true);
            activeBanner.transform.SetAsLastSibling();
            
            // 如果横幅内有依靠 OnEnable 触发的进场动画（比如 DOTweenAnimation 组件），
            // SetActive(true) 会自动重新播放它们。
        }
        else
        {
            // 🌟 2. 缓存没命中（玩家本次打开游戏后第一次抽到该样式），执行实例化并塞入缓存
            string prefabName = $"UIEffect_jiesuan0{styleNumber}"; 
            GameObject bannerPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", prefabName);

            if (bannerPrefab != null)
            {
                activeBanner = Instantiate(bannerPrefab, this.transform);
                activeBanner.transform.SetAsLastSibling();
                
                // 存入缓存字典
                _bannerCachePool[styleNumber] = activeBanner;
            }
        }
            // 3. 赋值文本数据（请根据你预制体里的实际结构获取）
            if (activeBanner != null)
            {        
                RectTransform bannerRect = activeBanner.GetComponent<RectTransform>();
                if (bannerRect != null)
                {
                    bannerRect.localPosition = targetLocalPos;
                }
                else
                {
                    activeBanner.transform.localPosition = targetLocalPos;
                }
                if (rule != null)
                {
                    // 例：获取激励主标题与副标题
                    string title = MultilingualManager.Instance.GetString(rule.TitleKey, "pingzi");
                    string desc = MultilingualManager.Instance.GetString(rule.LongTextKey, "pingzi");
                    
                    // 你需要根据预制体里的具体层级找 Text 组件，这里假设存在
                    Text titleText = activeBanner.transform.Find("Text01")?.GetComponent<Text>();
                    
                    // 写入百分比 (这里处理 0% 防护，大厂通常会做个假的最低限度)
                    float displayPercent = ChessStageController.Instance.DisplayZenPercent;
                    string percentStr = string.Format(desc, displayPercent.ToString("F2"));
                    Text percentText = activeBanner.transform.Find("Text02")?.GetComponent<Text>();
                    if (percentText != null)
                    {
                        percentText.text = percentStr;
                    }
                    Debug.Log($"🌟 [横幅准备完毕] 标题: {title} | 鼓励词: {percentStr} | 超越: {displayPercent:F2}%");
                    // if (rule.ScatterFlowers) MessageSystem.Instance.ShowTip($"{title} \n {percentStr} \n 撒花！");
                    if (titleText != null)
                    {
                        // 1. 🌟 强行通电：在计算前，先把缩放恢复成 1（哪怕只有 1 毫秒）
                        // 确保 TextGenerator 能在一个“顶天立地”的正常物理尺寸下计算字形
                        Vector3 oldScale = activeBanner.transform.localScale;
                        activeBanner.transform.localScale = Vector3.one;
                        // 2. 强制要求 UGUI 的布局系统和 Canvas 矩阵立刻在这一帧重构
                        LayoutRebuilder.ForceRebuildLayoutImmediate(titleText.GetComponent<RectTransform>());
                        Canvas.ForceUpdateCanvases();
                        // 3. 🌟 趁热打铁：此时 TextGenerator 处于绝对正确的 743 宽度矩阵中，立刻执行计算
                        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                        percentStr = UIUtilities.ApplyKinsokuShori(titleText, percentStr);
                        // 4. 算完收工：立刻把缩放调回动画起点，让 DOTween 正常的去播它的缩放动画
                        // 此时 \n 已经死死钉在文字里了，动画怎么缩放，排版都绝对不会再变了！
                        activeBanner.transform.localScale = oldScale;
                        
                        titleText.text = styleNumber == 4 ? percentStr : title;
                    }
                }
                AudioManager.Instance.PlaySoundEffect("BannerShow",0,1); 
                
                // 4. 控制横幅的生命周期 (假设停留 2.5 秒后自动销毁进入下一环节，或者你给预制体上的按钮绑点击事件)
                DOVirtual.DelayedCall(2.5f, () => 
                {
                    activeBanner.SetActive(false);
                    onComplete?.Invoke(); // 触发回调，允许流水线继续走阶段三
                });
            }
        else
        {
            Debug.LogError($"🚨 横幅预制体实例化失败！缺少样式 {styleNumber}");
            // 兜底机制：就算预制体炸了，游戏也能正常玩下去
            if (effectMask != null) effectMask.gameObject.SetActive(false);
            onComplete?.Invoke(); 
        }
    }
   
    /// <summary>
    /// 处理树叶关卡结束时，将收集到的奖励飞向对应的顶部目标
    /// </summary>
    private IEnumerator PlayLeafRewardsFlyOutFlow()
    {
        // Debug.Log("<color=#FF5500>[飞行排查] 1. 进入飞行结算流水线</color>");
        float flyDuration = 1.2f; // ✈️ 飞行时间，可根据美术节奏自由调整
        bool hasAnyFly = false;
        
        int curLeaves = ChessStageController.Instance.CurrStageData.CollectedLeaves;
        bool isButterflyTaskFinished = ButterfliesManager.Instance.IsPupaSufficientForAllRemaining();
        
        // Debug.Log($"<color=#FF5500>[飞行排查] 2. 当前树叶: {curLeaves}, 蝴蝶任务是否完成: {isButterflyTaskFinished}</color>");
        HeaderSection header = SystemManager.Instance.GetPanel(PanelType.HeaderSection) as HeaderSection;
        if (header == null) yield break;

        // 获取终点坐标
        Transform pauseBtnPos = header.pauseBtn.transform;        // 金币终点（关卡暂停时间按钮处）
        Transform pupaPos = header.pupaProgressBar.transform;     // 蝶蛹终点
        Transform zenPos = _zenScoreText.transform;               // 禅意分/莲花终点

        // 1. 金币（2片叶子解锁）
        if (curLeaves >= 2 && leafGold != null)
        {
            // Debug.Log("<color=#FF5500>[飞行排查] 3. 准备起飞：金币</color>");
            hasAnyFly = true;
            // FlyRewardNodeToTarget(leafGold, pauseBtnPos, flyDuration);
            CustomFlyInManager.Instance.FlyInGoldToTarget(leafGold.transform, pauseBtnPos);
        }

        // 2. 蝶蛹 或 禅意分（5片叶子解锁）
        if (curLeaves >= 5)
        {
            if (!isButterflyTaskFinished && leafPupa != null)
            {
                // Debug.Log("<color=#FF5500>[飞行排查] 3. 准备起飞：阶段5蝶蛹</color>");
                hasAnyFly = true;
                FlyRewardNodeToTarget(leafPupa, pupaPos, flyDuration, () =>
                {
                    header.PlayPupaCollectVisualEffect(1);
                });
            }
            else if (isButterflyTaskFinished && leafZenReplacement != null)
            {
                if (curLeaves < 10)
                {
                    hasAnyFly = true;
                    FlyRewardNodeToTarget(leafZenReplacement, zenPos, flyDuration);
                }
            }
        }

        // 3. 莲花/禅意大奖（10片叶子解锁）
        if (curLeaves >= 10 && leafLotus != null)
        {
            // Debug.Log("<color=#FF5500>[飞行排查] 3. 准备起飞：阶段10莲花</color>");
            hasAnyFly = true;
            FlyRewardNodeToTarget(leafLotus, zenPos, flyDuration);
        }

        // 如果触发了任何飞行，挂起协程，等待动画播完再让流水线往下走（弹出过关横幅）
        if (hasAnyFly)
        {
            // TODO: 如果有统一的起飞音效，可以在这里播放
            // AudioManager.Instance.PlaySoundEffect("RewardsFlyOut");

            // 等待飞行时间结束，加上 0.1 秒的缓冲容错时间
            yield return new WaitForSeconds(flyDuration + 0.1f);
            // Debug.Log("<color=#FF5500>[飞行排查] 5. 飞行结束，允许横幅弹出</color>");
        }
        else
        {
            // Debug.Log("<color=#FF5500>[飞行排查] 未满足任何起飞条件，直接跳过</color>");
        }
    }
    /// <summary>
    /// 🌟 内部复用工具：克隆奖励图标并让其沿着贝塞尔曲线飞向目标点
    /// </summary>
    private void FlyRewardNodeToTarget(GameObject sourceNode, Transform targetPos, float duration, Action onReachTarget = null)
    {
        // 1. 克隆物体，保证在最顶层飞行，这样就不会破坏原 UI 进度条的状态结构
        GameObject flyObj = Instantiate(sourceNode, this.transform);
        flyObj.transform.position = sourceNode.transform.position;
        flyObj.transform.localScale = sourceNode.transform.localScale;
        flyObj.transform.SetAsLastSibling();

        // 确保克隆体是可见的
        flyObj.SetActive(true);

        // 如果原节点正在执行果冻弹跳动画，强制清除克隆体上的残余动画防抖
        flyObj.transform.DOKill();
        
        // 2. 使用现有的贝塞尔曲线生成器（带有 0.3f 的自然弧度）
        Vector3[] pathPoints = CreateBezierPath(flyObj.transform.position, targetPos.position, 0.3f, 15);

        // 3. 编排飞行与缩小消失的交响乐
        Sequence seq = DOTween.Sequence();
        seq.Append(flyObj.transform.DOPath(pathPoints, duration, PathType.Linear).SetEase(Ease.InOutSine));
        // 飞行的同时慢慢缩小到0，产生被对应UI“吸入”的感觉
        seq.Join(flyObj.transform.DOScale(Vector3.zero, duration).SetEase(Ease.InQuad));
        seq.OnComplete(() =>
        {
            onReachTarget?.Invoke();
            // 到达目标后彻底销毁克隆体
            Destroy(flyObj);
        });
    }
    
    /// <summary>
    /// 监听到分数变化时的处理逻辑
    /// </summary>
    private void OnChessScoreChanged(int newScore,int scoreDiff)
    {
        // int scoreDiff = newScore - _lastZenScore;
        
        // 分数没变就不播动画
        if (scoreDiff == 0)
        {
            // 如果是因为发呆断连击进来的，顺手把连击特效关掉
            if (_comboScreenFX != null && ChessStageController.Instance.PuzzleComboCount <= 0)
                _comboScreenFX.SetActive(false);
            return;
        }
        
        // 状态判定
        bool isDeduction = scoreDiff < 0;
        // 记录一下分数，防止连续触发时算错差值
        _lastZenScore = newScore;
        // 起飞点：屏幕中下方，或者用填词盘的位置
        Vector3 startPos;
        if (ScoreFlyPos.HasValue)
        {
            startPos = ScoreFlyPos.Value;
            ScoreFlyPos = null; // 用完立刻清空，防止影响下一次
        }
        else
        {
            startPos = chessboardGrid.selecteTile != null ? 
                chessboardGrid.selecteTile.transform.position : 
                chessboardGrid.transform.position; 
        }
        // 1. 发射禅意分粒子 (对/错)
        FlyToZenScore(startPos, newScore, scoreDiff, isDeduction);
        // 2. 如果是加分，发射蝶蛹粒子
        if (!isDeduction)
        {
            FlyToPupa(startPos, newScore);
        }
    }
    
    
   /// <summary>
    /// 粒子命中后，执行UI的老虎机滚动替换和爆点特效
    /// </summary>
    private void UpdateZenScoreUI(int newScore, int scoreDiff, bool isDeduction)
    {
        bool isCombo = !isDeduction && ChessStageController.Instance.PuzzleComboCount >= 2;

        // 🌟 1. 强制归位并停止主角(分数文本)之前的动画，防止快速连击导致位置偏移
        _zenScoreText.DOKill(true);
        _zenScoreText.rectTransform.DOKill(true);
        _zenScoreText.transform.localScale = Vector3.one;

        Vector2 centerPos = _zenScoreText.rectTransform.anchoredPosition;
        
        // 设定滚动方向：加分向上顶(正50)，减分向下砸(负50)
        float offset = isDeduction ? -50f : 50f; 

        // ==========================================
        // 🌟 核心：老虎机式滚动替换效果
        // ==========================================
        // ① 克隆旧分数作为“替身”，让它滚出屏幕
        GameObject oldScoreObj = _rollingScorePool.GetObject();
        oldScoreObj.transform.SetParent(_zenScoreText.transform.parent, false);
        oldScoreObj.transform.SetAsFirstSibling(); // 放到最底层，不遮挡新分数
        Text oldScoreText = oldScoreObj.GetComponent<Text>();
        RectTransform oldScoreRT = oldScoreObj.GetComponent<RectTransform>();
        oldScoreText.text = _zenScoreText.text;
        oldScoreRT.anchoredPosition = centerPos;
        oldScoreText.color = _zenScoreText.color;
        // 替身滚出并渐隐 (Ease.InBack 带有往回蓄力一下再冲出去的物理感)
        oldScoreRT.DOAnchorPosY(centerPos.y + offset, 0.4f).SetEase(Ease.InBack);
        oldScoreText.DOFade(0f, 0.4f).OnComplete(() =>
        {
            oldScoreRT.DOKill();
            oldScoreText.DOKill();
            _rollingScorePool.ReturnObjectToPool(oldScoreObj.GetComponent<PoolObject>());
        }); // 滚完立刻销毁

        // ② 将主角(真文本)直接设为新分数，并把它拉到屏幕外准备“进场”
        _zenScoreText.text = newScore.ToString();
        _zenScoreText.rectTransform.anchoredPosition = new Vector2(centerPos.x, centerPos.y - offset);
        
        // 初始透明度设为 0
        Color startColor = _zenScoreText.color; 
        startColor.a = 0f; 
        _zenScoreText.color = startColor;

        // 主角滚入中心并渐显 (Ease.OutBack 带有越过中心点再弹回来的Q弹感)
        _zenScoreText.rectTransform.DOAnchorPosY(centerPos.y, 0.5f).SetEase(Ease.OutBack);
        _zenScoreText.DOFade(1f, 0.4f);
        
        // ==========================================
        // 以下保持原状：边框、莲花特效、飘字等
        // ==========================================
        // 边框闪烁
        _scoreBorder.DOKill(); 
        // 只有扣分 (isDeduction) 或 连击 (isCombo) 时，才需要边框闪烁
        if (isDeduction || isCombo)
        {
            _scoreBorder.gameObject.SetActive(true); // 🌟 1. 播放前：强行开启节点
            
            Color borderColor = isDeduction ? Color.red : Color.yellow;
            borderColor.a = 1f;
            _scoreBorder.color = borderColor;
            
            // 🌟 2. 播放后：利用 OnComplete 在动画播完的瞬间彻底关闭节点
            _scoreBorder.DOFade(0f, 0.6f).SetEase(Ease.OutQuad).OnComplete(() => 
            {
                _scoreBorder.gameObject.SetActive(false); 
            });
            
            // 命中爆开莲花粒子
            // _lotusParticle.SetActive(false);
            // _lotusParticle.SetActive(true); 
            // ParticleSystem ps = _lotusParticle.GetComponent<ParticleSystem>();
            // if (ps != null) ps.Play();
        }
        else 
        {
            // 普通加分不需要闪烁，直接确保它处于关闭状态
            _scoreBorder.gameObject.SetActive(false); 
        }
        
        // 飘字动画 去除再展示飘字
        // GameObject floatObj = _floatingScorePool.GetObject();
        // floatObj.transform.SetParent(_floatingScoreOriginalPos.transform, false);
        // floatObj.transform.SetAsLastSibling();
        // floatObj.transform.localScale = Vector3.one;
        // Text floatText = floatObj.GetComponent<Text>();
        // CanvasGroup floatCG = floatObj.GetComponent<CanvasGroup>();
        // if (floatCG == null) floatCG = floatObj.AddComponent<CanvasGroup>();
        // // 清理旧状态
        // floatCG.DOKill(false);
        // floatText.rectTransform.DOKill(false);
        // bool enableGradient = !(isDeduction || isCombo);
        // var meshEffects = floatObj.GetComponents<UnityEngine.UI.BaseMeshEffect>();
        // foreach (var effect in meshEffects)
        // {
        //     if (effect.GetType().Name.Contains("Gradient")) effect.enabled = enableGradient;
        // }
        //
        // // 🌟 5. 获取纯色目标颜色
        // Color targetColor = Color.white;
        // if (isDeduction) targetColor = Color.red; // 扣分：红色
        // else if (isCombo) targetColor = new Color(0.6f, 1f, 0f); // 连击：黄绿色
        //
        // // 设定起始 Alpha = 0，位置复位，内容更新
        // floatText.color = new Color(targetColor.r, targetColor.g, targetColor.b, 1f);
        // floatText.rectTransform.anchoredPosition = Vector2.zero;
        // floatText.text = scoreDiff > 0 ? $"+{scoreDiff}" : scoreDiff.ToString();
        // floatText.SetAllDirty();
        // floatCG.alpha = 0f;
        // Sequence floatSeq = DOTween.Sequence();
        // floatSeq.SetTarget(floatObj);
        // // 相对移动 + 淡入淡出
        // float randomX = UnityEngine.Random.Range(-5f, 5f);
        // floatSeq.Join(floatText.rectTransform.DOAnchorPos(new Vector2( randomX,  60f), 1.2f).SetRelative(true).SetEase(Ease.OutQuad));
        // floatSeq.Join(floatCG.DOFade(1f, 0.2f));
        // floatSeq.Insert(0.8f, floatCG.DOFade(0f, 0.4f));
        // floatSeq.OnComplete(() =>
        // {
        //     _floatingScorePool.ReturnObjectToPool(floatObj.GetComponent<PoolObject>());
        // });
        
        _comboScreenFX.SetActive(isCombo);
    }
   
    /// <summary>
    /// 飞向顶部 Header 的蝶蛹 UI
    /// </summary>
    private void FlyToPupa(Vector3 startPos, int targetScore)
    {
        HeaderSection header = SystemManager.Instance.GetPanel(PanelType.HeaderSection) as HeaderSection;
        if (header == null || !header.pupaObj.activeSelf) return; // 蝶蛹没开启就不飞

        GameObject particle = _pupaTrailPool.GetObject();
        if (particle == null)
        {
            header.UpdatePupaProgress(targetScore, false); // 兜底
            return;
        }
        particle.SetActive(false);
        particle.transform.position = startPos;
        TrailRenderer[] trails = particle.GetComponentsInChildren<TrailRenderer>(true);
        foreach (var t in trails) t.Clear();
        ParticleSystem[] pss = particle.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in pss) p.Clear();
        particle.transform.SetAsLastSibling();
        particle.SetActive(true);

        Vector3 endPos = header.pupaProgressBar.transform.position;
        // ==========================================
        // 🌟 核心动态时间计算：距离 ÷ 速度 = 时间
        // ==========================================
        float distance = Vector3.Distance(startPos, endPos);
        float duration = Mathf.Clamp(distance*0.5f, 1.4f, 2.2f);
        
        Vector3 midPos = (startPos + endPos) / 2f;
        midPos.x -= distance * 0.3f; // 向右侧弯曲弧线，和禅意分岔开

        Vector3[] path = new Vector3[] { startPos, midPos, endPos };

        particle.transform.DOPath(path, duration, PathType.CatmullRom).SetEase(Ease.InQuad).OnComplete(() => 
        {
            _pupaTrailPool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
            // 🌟 命中！通知头部 UI 更新蝶蛹进度！
            header.UpdatePupaProgress(targetScore, false);
        });
    }
    
        /// <summary>
    /// 🌟 规范 API：供外部调用的棋盘飘字方法
    /// </summary>
    public void ShowBoardFloatingScore(Transform targetTile, int dir, int scoreDiff, bool isCombo)
    {
        // 1. 生成在 this.transform (PlayArea) 下，保证层级在最顶端，不会被棋盘格子遮挡
        GameObject floatObj = _floatingScorePool.GetObject(this.transform); 
        floatObj.transform.SetAsLastSibling();
        
        Text floatText = floatObj.GetComponent<Text>();
        CanvasGroup floatCG = floatObj.GetComponent<CanvasGroup>();
        if (floatCG == null) floatCG = floatObj.AddComponent<CanvasGroup>();
        
        // 2. 杀掉旧动画，重置 Scale (极其重要，防止变小)
        floatCG.DOKill(false);
        floatText.rectTransform.DOKill(false);
        floatObj.transform.DOKill(false);
        floatObj.transform.localScale = Vector3.one;
        
        // 3. 设置初始位置为目标格子的世界坐标
        floatObj.transform.position = targetTile.position;
        
        // 4. 根据方向，给初始位置加上偏移量，让字从格子的上方/右方出现，而不是中心
        RectTransform floatRT = floatObj.GetComponent<RectTransform>();
        Vector2 flyDir;
        if (dir == 1) // 横向词 -> 飘字在上方
        {
            floatRT.anchoredPosition += new Vector2(0, 60f); // 初始位置向上偏移
            flyDir = new Vector2(UnityEngine.Random.Range(-15f, 15f), 100f); // 继续向上飘
        }
        else // 纵向词 -> 飘字在右方
        {
            floatRT.anchoredPosition += new Vector2(60f, 0); // 初始位置向右偏移
            flyDir = new Vector2(100f, UnityEngine.Random.Range(-15f, 15f)); // 继续向右飘
        }

        // 5. 设置颜色和文字
        bool enableGradient = !isCombo;
        var meshEffects = floatObj.GetComponents<UnityEngine.UI.BaseMeshEffect>();
        foreach (var effect in meshEffects)
        {
            if (effect.GetType().Name.Contains("Gradient")) effect.enabled = enableGradient;
        }
        
        Color targetColor = isCombo ? new Color(0.6f, 1f, 0f) : Color.white; 
        floatText.color = new Color(targetColor.r, targetColor.g, targetColor.b, 1f);
        floatText.text = $"+{scoreDiff}";
        floatText.SetAllDirty();
        floatCG.alpha = 0f;
        
        // 6. 执行动画
        Sequence floatSeq = DOTween.Sequence();
        floatSeq.SetTarget(floatObj);
        
        // 先稍微缩小一点作为起点，实现Q弹放大的效果
        floatObj.transform.localScale = Vector3.one * 0.5f; 
        
        // 弹出：瞬间放大并显现
        floatSeq.Append(floatObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack));
        floatSeq.Join(floatCG.DOFade(1f, 0.2f));
        
        // 飞行：沿着计算好的方向飘动
        floatSeq.Join(floatText.rectTransform.DOAnchorPos(flyDir, 1.2f).SetRelative(true).SetEase(Ease.OutQuad));
        
        // 消失：淡出，并且恢复到标准大小 (不设为0.5，防止影响下一次)
        floatSeq.Insert(0.8f, floatCG.DOFade(0f, 0.4f));
        floatSeq.Insert(0.8f, floatObj.transform.DOScale(1f, 0.4f));
        
        floatSeq.OnComplete(() =>
        {
            _floatingScorePool.ReturnObjectToPool(floatObj.GetComponent<PoolObject>());
        });
    }
    /// <summary>
    /// 🌟 新增：在棋盘错误格子上显示减分飘字（复用原禅意分位置的缓动特效样式）
    /// </summary>
    public void ShowBoardDeductionFloatingScore(Transform targetTile, int scoreDiff)
    {
        // 1. 生成在 this.transform (PlayArea) 下，保证层级在最顶端
        GameObject floatObj = _floatingScorePool.GetObject(this.transform); 
        floatObj.transform.SetAsLastSibling();
        
        Text floatText = floatObj.GetComponent<Text>();
        CanvasGroup floatCG = floatObj.GetComponent<CanvasGroup>();
        if (floatCG == null) floatCG = floatObj.AddComponent<CanvasGroup>();
        
        // 2. 杀掉旧动画，重置状态
        floatCG.DOKill(false);
        floatText.rectTransform.DOKill(false);
        floatObj.transform.DOKill(false);
        floatObj.transform.localScale = Vector3.one;
        
        // 3. 设置初始位置为目标错误格子的世界坐标
        floatObj.transform.position = targetTile.position;

        // 4. 关闭材质的渐变效果，使用纯色
        ChessView chessView = targetTile.GetComponent<ChessView>();
        int curRow = chessView.chesspiece.row;
        int curCol = chessView.chesspiece.col;
        int targetDir = 1;
        // 1. 先将 col+1 查找上方是否存在格子
        if (IsTileOccupied(curRow, curCol + 1))
        {
            // 2. 上方存在格子，再将 row+1 判断右边是否存在格子
            if (!IsTileOccupied(curRow + 1, curCol))
            {
                // 右方不存在格子，改为向右飘
                targetDir = 2;
            }
            // 3. 若右方也存在格子，targetDir 依然保持为 1 (还是向上飘)
        }
        RectTransform floatRT = floatObj.GetComponent<RectTransform>();
        Vector2 flyDir;
        if (targetDir == 1) // 决定在上方飘
        {
            floatRT.anchoredPosition += new Vector2(0, 60f); 
            flyDir = new Vector2(UnityEngine.Random.Range(-15f, 15f), 100f); 
        }
        else // 决定在右方飘
        {
            floatRT.anchoredPosition += new Vector2(60f, 0); 
            flyDir = new Vector2(100f, UnityEngine.Random.Range(-15f, 15f)); 
        }
        floatRT.anchoredPosition += new Vector2(0f, 60f); // 初始Y轴向上偏移 60 像素
        var meshEffects = floatObj.GetComponents<UnityEngine.UI.BaseMeshEffect>();
        foreach (var effect in meshEffects)
        {
            if (effect.GetType().Name.Contains("Gradient")) effect.enabled = false;
        }

        // 5. 设置纯红颜色和具体的负数文本 (例如 "-5")
        floatText.color = Color.red; 
        floatText.text = scoreDiff.ToString();
        floatText.SetAllDirty();
        
        // 初始透明度设为0
        floatCG.alpha = 0f;
        
        // 6. 执行动画（完全复原废弃的禅意分飘字参数）
        Sequence floatSeq = DOTween.Sequence();
        floatSeq.SetTarget(floatObj);
        
        // 轻微的左右随机偏移，让连续扣分时文字不会完全重叠死板
        float randomX = UnityEngine.Random.Range(-25f, 25f); 
        
        // 相对移动 + 淡入淡出 (向上飘 60 像素)
        floatSeq.Join(floatText.rectTransform.DOAnchorPos(new Vector2(randomX, 60f), 1.2f).SetRelative(true).SetEase(Ease.OutQuad));
        floatSeq.Join(floatCG.DOFade(1f, 0.2f));             // 0.2秒快速淡入显现
        floatSeq.Join(floatText.rectTransform.DOAnchorPos(flyDir, 1.2f).SetRelative(true).SetEase(Ease.OutQuad));
        floatSeq.Insert(0.8f, floatCG.DOFade(0f, 0.4f));     // 停留后，在0.8秒处开始用0.4秒淡出
        floatSeq.Insert(0.8f, floatObj.transform.DOScale(1f, 0.4f));
        floatSeq.OnComplete(() =>
        {
            _floatingScorePool.ReturnObjectToPool(floatObj.GetComponent<PoolObject>());
        });
    }
    /// <summary>
    /// 辅助方法：检测指定行列是否被其他未消除的格子挡住
    /// </summary>
    private bool IsTileOccupied(int row, int col)
    {
        if (chessboardGrid == null || chessboardGrid.GridList == null) return false;

        // 遍历当前棋盘上存活的格子
        foreach (var tile in chessboardGrid.GridList.Values)
        {
            // 找到对应坐标的格子
            if (tile.chesspiece.row == row && tile.chesspiece.col == col)
            {
                // 如果这个格子存在，且不是已经被消除的空白状态（根据你的状态枚举调整）
                if (tile.CurrState != TileState.None) 
                {
                    return true; // 报告：前方有障碍物！
                }
            }
        }
        return false; // 前方畅通无阻
    }
    
    /// <summary>
    /// 飞向禅意分 UI
    /// </summary>
    private void FlyToZenScore(Vector3 startPos, int targetScore, int scoreDiff, bool isDeduction)
    {
        ObjectPool pool = isDeduction ? _zenWrongTrailPool : _zenCorrectTrailPool;
        GameObject particle = pool.GetObject();
        if (particle == null) 
        {
            // 如果没配粒子，直接执行 UI 更新兜底
            UpdateZenScoreUI(targetScore, scoreDiff, isDeduction);
            return;
        }
        particle.SetActive(false);
        particle.transform.position = startPos;
        // 强制清除拖尾和粒子的历史轨迹
        TrailRenderer[] trails = particle.GetComponentsInChildren<TrailRenderer>(true);
        foreach (var t in trails) t.Clear();
        ParticleSystem[] pss = particle.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in pss) p.Clear();
        particle.transform.SetAsLastSibling();
        particle.SetActive(true);
        
        Vector3 endPos = _zenScoreText.transform.position;
        
        // ==========================================
        // 🌟 核心动态时间计算：距离 ÷ 速度 = 时间
        // ==========================================
        float distance = Vector3.Distance(startPos, endPos);
        // 限制最快不低于 0.4 秒，最慢不超过 1.2 秒
        float duration = Mathf.Clamp(distance * 0.5f, 1.4f, 2.2f); 

        Vector3 midPos = (startPos + endPos) / 2f;
        midPos.x += distance * 0.3f; // 向左侧弯曲弧线

        Vector3[] path = new Vector3[] { startPos, midPos, endPos };

        // 飞行 0.6 秒
        particle.transform.DOPath(path, duration, PathType.CatmullRom).SetEase(Ease.InQuad).OnComplete(() => 
        {
            pool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
            // 🌟 命中！执行禅意分的UI跳动和飘字特效
            UpdateZenScoreUI(targetScore, scoreDiff, isDeduction);
        });
    }

    
     /// <summary>
    /// 🌟 新增：处理成功消除时，树叶划过贝塞尔弧线飞向收集点的华丽动效
    /// </summary>
    /// <param name="startTransform">起飞的格子 Transform</param>
    public void PlayLeafFlyToCollectionPoint(Transform startTransform)
    {
        if (leafFlyPoint == null || startTransform == null) return;
        // 获取当前应该用的皮肤索引
        int skinIndex = (ChessStageController.Instance.LeafGenCounter % 4) + 1;
        // 从对应的池子里拿对应的预制体
        GameObject flyLeaf = _leafPoolDict[skinIndex].GetObject(transform);
        flyLeaf.SetActive(true);
        flyLeaf.transform.position = startTransform.position;
        flyLeaf.transform.localScale = Vector3.one;
        
        flyLeaf.SetActive(true);
        // 强制移除克隆体身上的呼吸动画组件，防止飞行时乱抖
        flyLeaf.transform.DOKill();
        AudioManager.Instance.PlaySoundEffect("LeafFlyStart",0,1); // ⚠️ 请替换为真实的音效名
        
        Vector3 startPos = flyLeaf.transform.position;
        Vector3 endPos = leafFlyPoint.transform.position;
        // 1. 恒定速度控制
        float speed = 800f; // 像素/秒，可根据美术效果调整
        float distance = Vector3.Distance(startPos, endPos);
        float duration = Mathf.Clamp(distance / speed, 1.4f, 2.0f); // 最小/最大时间限制
        
        // 0.3f 代表向右上方鼓起一个优雅的弧度
        Vector3[] pathPoints = CreateBezierPath(startPos, endPos, 0.35f, 12); 

        // 3. 编排飞行交响乐
        Sequence flySeq = DOTween.Sequence();
        // 沿着贝塞尔曲线飞过去
        flySeq.Append(flyLeaf.transform.DOPath(pathPoints, duration, PathType.Linear).SetEase(Ease.InQuad));
        // 飞行时带有一点树叶飘落的自然自转
        flySeq.Join(flyLeaf.transform.DORotate(new Vector3(0, 0, 360f), 0.75f, RotateMode.FastBeyond360).SetEase(Ease.Linear));
        // 快接近终点时慢慢缩小钻进去
        // flySeq.Insert(0.55f, flyLeaf.transform.DOScale(Vector3.zero, 0.2f));
        flySeq.AppendCallback(() => {
            // 到达终点后，播放一个小动画再回收
            Sequence landSeq = DOTween.Sequence();
            landSeq.Append(flyLeaf.transform.DOScale(1.3f, 0.1f).SetEase(Ease.OutQuad)); // 轻微弹起
            landSeq.Append(flyLeaf.transform.DOScale(0f, 0.25f).SetEase(Ease.InBack));   // 缩小消失
            landSeq.OnComplete(() => {
                AudioManager.Instance.PlaySoundEffect("LeafReachTarget",0,1); // ⚠️ 请替换为真实的音效名
                
                _leafPoolDict[skinIndex].ReturnObjectToPool(flyLeaf.GetComponent<PoolObject>());
                // 进度条更新逻辑保持不变
                leafSlider.transform.DOKill();
                leafSlider.transform.DOScale(new Vector3(1.05f, 1.15f, 1f), 0.1f).SetLoops(2, LoopType.Yoyo);
                int curCollected = ChessStageController.Instance.CurrStageData.CollectedLeaves;
                float targetSliderValue = Mathf.Min(curCollected, leafSlider.maxValue);
                leafSlider.DOValue(targetSliderValue, 0.2f).SetEase(Ease.OutQuad).OnComplete(() => {
                    TriggerRewardNodeFeedback(curCollected);
                });
            });
        });
    }
    
    /// <summary>
    /// 阶段点果冻爆点核心驱动器
    /// </summary>
    private void TriggerRewardNodeFeedback(int currentCount)
         {
        GameObject targetNode = null;
        int zenBonus = 0;

        bool isButterflyTaskFinished = ButterfliesManager.Instance.IsPupaSufficientForAllRemaining();
        
        if (currentCount == 2) targetNode = leafGold;
        else if (currentCount == 5)
        {
            if (isButterflyTaskFinished)
            {
                zenBonus = 20;
                targetNode = leafZenReplacement;
            }
            else targetNode = leafPupa; 
        }
        else if (currentCount >= 10)
        {
            targetNode = leafLotus;
            zenBonus = 50; // 莲花大满贯给予50禅意分
        }

        if (targetNode == null) return;

        // ① 大厂标配 Q 弹缓动：通过 Punch 产生极强的肉感和果冻敲击感
        targetNode.transform.DOKill(true);
        targetNode.transform.SetAsLastSibling(); // 提层防遮挡
        // targetNode.transform.DOPunchScale(new Vector3(0.45f, 0.45f, 0f), 0.55f, 12, 0.5f);

        // ② 子物体粒子爆发
        // var pss = targetNode.GetComponentInChildren<ParticleSystem>(true);
        var pss = targetNode.transform.GetChild(0);
        pss.gameObject.SetActive(true);
        if (currentCount >= 10 && leafSlider != null)
        {
            leafSlider.value = leafSlider.maxValue;
        }
        AudioManager.Instance.PlaySoundEffect("LeafRewardUnlock",0,1);
    }
    
    /// <summary>
    /// 🌟 规范重构：全面加强出入场清理（完美解决走光、非法状态与内存留存）
    /// </summary>
    private void ClearAndResetLeafSliderComponents()
    {
        bool isLeafLevel = ChessStageController.Instance.CheckLeafMechanic(ChessStageController.Instance.CurrentStage, out _);
        
        int maxLeavesInStage = 10; // 安全兜底默认值
        
        if (leafSlider != null)
        {
            leafSlider.transform.parent.gameObject.SetActive(isLeafLevel);
            if (!isLeafLevel) return;
            
            leafSlider.transform.DOKill();
            leafSlider.transform.localScale = Vector3.one;
            if (ChessStageController.Instance.CurrStageInfo != null)
            {
                maxLeavesInStage = ChessStageController.Instance.CurrStageInfo.PhraseGroups.Count;
            }
            // 最大值动态同步关卡成语总数
            leafSlider.maxValue = Mathf.Min(maxLeavesInStage, 10); // 安全兜底
            // leafSlider.value = ChessStageController.Instance.CurrStageData.CollectedLeaves;
            leafSlider.value = Mathf.Min(ChessStageController.Instance.CurrStageData.CollectedLeaves, leafSlider.maxValue);
        }
        // 如果达不到，则从根节点彻底隐藏莲花奖励（图标及所有粒子都会随之隐藏）
        if (leafLotus != null)
        {
            leafLotus.SetActive(maxLeavesInStage >= 10);
        }
        Image leafImg = leafFlyPoint.transform.GetChild(0).GetComponent<Image>();
        if (leafImg != null)
        {
            int skinIndex = (ChessStageController.Instance.LeafGenCounter % 4) + 1; // 1, 2, 3 循环
            // 从你的图集Atlas或AdvancedBundleLoader中加载对应的叶子切图
            leafImg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas($"leaf_skin_0{skinIndex}");
        }

        bool isButterflyTaskFinished = ButterfliesManager.Instance.IsPupaSufficientForAllRemaining();
        if (leafPupa != null) leafPupa.SetActive(!isButterflyTaskFinished);
        if (leafZenReplacement != null) leafZenReplacement.SetActive(isButterflyTaskFinished);
        
        // 强行规制三个节点至标准状态
        GameObject[] rewardNodes = { leafGold, leafPupa, leafLotus,leafZenReplacement };
        foreach (var node in rewardNodes)
        {
            if (node != null)
            {
                node.transform.DOKill();
                node.transform.localScale = Vector3.one;
                // 默认强制停火子物体挂载的所有粒子，打扫干净战场
                var pss = node.GetComponentInChildren<ParticleSystem>(true);
                pss.Stop(); pss.gameObject.SetActive(false);
                // foreach (var ps in pss) { ps.Stop(); ps.gameObject.SetActive(false); }
            }
        }
        int curLeaves = ChessStageController.Instance.CurrStageData.CollectedLeaves;
        if (curLeaves >= 2) SetNodeActiveIdleState(leafGold);
        if (curLeaves >= 5) SetNodeActiveIdleState(isButterflyTaskFinished ? leafZenReplacement : leafPupa);
        if (curLeaves >= 10) SetNodeActiveIdleState(leafLotus);
    }
    
    /// <summary>
    /// 辅助方法：点亮已经解锁的节点（仅展示常亮特效，不触发果冻弹跳动画）
    /// </summary>
    private void SetNodeActiveIdleState(GameObject node)
    {
        if (node == null) return;
        var pss = node.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in pss)
        {
            ps.gameObject.SetActive(true);
            ps.Play();
        }
    }

    #region 填词成功局内鼓励

    [Header("局内鼓励反馈 (Praise) 状态管理")]
    // 独立的预制体缓存池：Key 为预制体名称，Value 为实例化的 GameObject
    private readonly Dictionary<string, GameObject> _praiseCachePool = new Dictionary<string, GameObject>();
    
    private int _currentActivePriority = int.MaxValue; // 当前活跃横幅的优先级
    private bool _isPraiseShowing = false;            // 是否正在展示鼓励分/横幅
    private Coroutine _praiseDisplayRoutine = null;   // 当前的生命周期协程参考
    // 轮询计数器
    private int _applauseBgCounter = 1; // 鼓掌底板轮询 (1~5)
    private int _longBannerCounter = 4; // 10号横幅轮询 (4~7)
    private Dictionary<int, int> _praiseTextIndexMap = new Dictionary<int, int>();
    /// <summary>
    /// 对外接口：触发局内正反馈 UI
    /// </summary>
    public void ShowPraiseUI(PraiseConfig config)
    {
        if (config == null) return;
        // ==========================================
        //  优先级抢占与打断判定
        // ==========================================
        if (_isPraiseShowing)
        {
            // 数字越小优先级越高（比如 1 高于 5）
            if (config.Priority < _currentActivePriority)
            {
                Debug.Log($"<color=#FFFF00>[正反馈抢占] 发现更高优先级横幅 (ID:{config.FeedbackID}, 优先:{config.Priority})，强行打断当前低优先级 (优先:{_currentActivePriority})！</color>");
                InterruptCurrentPraiseUI(); // 立即杀掉旧动画和延迟回调
            }
            else
            {
                Debug.Log($"<color=#808080>[正反馈忽略] 当前已有同级或更高优先级横幅在展示中，忽略低优先级 ID {config.FeedbackID}</color>");
                return;
            }
        }

        // 标记当前状态
        _isPraiseShowing = true;
        _currentActivePriority = config.Priority;

        // 执行具体的 UI 组装与展示
        _praiseDisplayRoutine = StartCoroutine(PlayPraiseUISequence(config));
    }
    
    /// <summary>
    /// 强行中断当前正在播放的鼓励 UI
    /// </summary>
    private void InterruptCurrentPraiseUI()
    {
        if (_praiseDisplayRoutine != null)
        {
            StopCoroutine(_praiseDisplayRoutine);
            _praiseDisplayRoutine = null;
        }

        // 强行清理所有可能残留在屏幕上的对象池实例的动画和状态
        foreach (var kvp in _praiseCachePool)
        {
            if (kvp.Value != null)
            {
                kvp.Value.transform.DOKill();
                kvp.Value.SetActive(false);
            }
        }
    
        _isPraiseShowing = false;
        _currentActivePriority = int.MaxValue;
    }
    private IEnumerator PlayPraiseUISequence(PraiseConfig config)
    {
        yield return new WaitForSeconds(0.2f);
        // 1. 统一计算过关横幅所在的绝对坐标 (屏幕中心偏上)
        RectTransform gridRect = chessboardGrid.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        gridRect.GetWorldCorners(corners);
        Vector3 centerWorld = (corners[0] + corners[3]) / 2f;
        Vector3 targetLocalPos = transform.InverseTransformPoint(centerWorld);
        targetLocalPos.z = 0f;
        targetLocalPos.y += 55f; // 与过关横幅保持一致的 Y 轴偏移
        
        GameObject mainEffectObj = null;
        GameObject hintBoardObj = null;

        // 2. 核心分流：根据样式决定是“双层组合”还是“单体预制件”
        if (config.BannerStyle >= 1 && config.BannerStyle <= 3)
        {
            // ==========================================
            // 样式 1~3：双层组合 (底板在下，鼓掌在上)
            // ==========================================
            
            // A. 加载并设置底板 (文字在底板上)
            string boardPrefabName = $"UIEffect_shoushi02_{_applauseBgCounter}";
            hintBoardObj = GetFromPraisePool(boardPrefabName); // 替换为真实的底板预制体名
            if (hintBoardObj != null)
            {
                hintBoardObj.transform.DOKill(); // 杀掉残留的缩放动画
                hintBoardObj.transform.localPosition = targetLocalPos;
                hintBoardObj.transform.SetAsLastSibling(); // 先把底板推到最前面
                
                SetupHintBoardText(hintBoardObj, config); // 填入轮询图片和文字
            }
            _applauseBgCounter++;
            if (_applauseBgCounter > 5) _applauseBgCounter = 1;
            
            // B. 加载并设置鼓掌特效 (盖在底板上)
            string applauseName = DeterminePraisePrefabName(config);
            mainEffectObj = GetFromPraisePool(applauseName);
            if (mainEffectObj != null)
            {
                mainEffectObj.transform.DOKill();
                Vector3 applausePos = targetLocalPos + new Vector3(0, 165f, 0); 
                mainEffectObj.transform.localPosition = applausePos;
                mainEffectObj.transform.SetAsLastSibling(); // 再次 SetAsLastSibling，确保鼓掌特效盖住底板
            }
            
        }
        else
        {
            // ==========================================
            // 样式 4~5：单体预制件 (文字在特效自身内部)
            // ==========================================
            string effectName = DeterminePraisePrefabName(config);
            mainEffectObj = GetFromPraisePool(effectName);
            
            if (mainEffectObj != null)
            {
                mainEffectObj.transform.DOKill(); 
                mainEffectObj.transform.localPosition = targetLocalPos;
                mainEffectObj.transform.SetAsLastSibling();
                
                SetupHintBoardText(mainEffectObj, config); // 填入自身文字
            }
        }
        // if (!string.IsNullOrEmpty(config.AudioName))
        // {
        //     AudioManager.Instance.PlaySoundEffect(config.AudioName, 0, 1);
        // }
        try
        {
            if (config.BannerStyle == 5)
            {
                // 样式 5：长条横幅执行专属的滚动出场动画
                yield return StartCoroutine(PlayLongBannerAnimation(mainEffectObj, targetLocalPos));
            }
            else
            {
                // 样式 1~4：执行普通的中心弹出与统一定时回收
                if (config.BannerStyle < 4)
                {
                    AudioManager.Instance.PlaySoundEffect("HandGestureBanner");
                }else if (config.BannerStyle == 4)
                {
                    AudioManager.Instance.PlaySoundEffect("Lotusbanner");
                }
                // 可选：给鼓掌/金光加个简单的 Q 弹出现动画
                mainEffectObj.transform.localScale = Vector3.one * 0.5f;
                mainEffectObj.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
                if (hintBoardObj != null)
                {
                    hintBoardObj.transform.localScale = Vector3.one * 0.5f;
                    hintBoardObj.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
                }
                yield return new WaitForSeconds(2.0f);
                // 统一在 2 秒后强制回收
                // DOVirtual.DelayedCall(2.0f, () =>
                // {
                //     if (mainEffectObj != null) mainEffectObj.SetActive(false);
                //     if (hintBoardObj != null) hintBoardObj.SetActive(false);
                // });
            }
        }
        finally
        {
            if (mainEffectObj != null) mainEffectObj.SetActive(false);
            if (hintBoardObj != null) hintBoardObj.SetActive(false);
            _isPraiseShowing = false;
            _currentActivePriority = int.MaxValue;
            _praiseDisplayRoutine = null;
        }
       
    }
    
    /// <summary>
    /// 从池子中获取或加载预制体 (通用封装)
    /// </summary>
    private GameObject GetFromPraisePool(string prefabName)
    {
        if (_praiseCachePool.TryGetValue(prefabName, out GameObject obj) && obj != null)
        {
            obj.SetActive(true);
            return obj;
        }

        GameObject prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", prefabName);
        if (prefab != null)
        {
            obj = Instantiate(prefab, this.transform);
            _praiseCachePool[prefabName] = obj;
            return obj;
        }

        Debug.LogError($"🚨 鼓励反馈预制体加载失败: {prefabName}");
        return null;
    }
    
    /// <summary>
    /// 处理填入词组后的鼓励词
    /// </summary>
    private void SetupHintBoardText(GameObject effectObj, PraiseConfig config)
    {
        Text textComp = effectObj.GetComponentInChildren<Text>();
        if (textComp != null && config.TextLoop != null && config.TextLoop.Length > 0)
        {
            // 1. 获取当前该播第几个词
            if (!_praiseTextIndexMap.TryGetValue(config.FeedbackID, out int currentIndex))
            {
                currentIndex = 0;
            }
            // 2. 赋值当前文本
            string key = config.TextLoop[currentIndex];
            string text = MultilingualManager.Instance.GetString(key, "pingzi");
            textComp.text = text;
            // 3. 索引 +1，如果超过数组长度则回到 0，实现完美的循环播放
            _praiseTextIndexMap[config.FeedbackID] = (currentIndex + 1) % config.TextLoop.Length;
        }
    }
    /// <summary>
    /// 核心路由：根据规则计算应该加载哪个预制体
    /// </summary>
    private string DeterminePraisePrefabName(PraiseConfig config)
    {
        // 样式 1, 2, 3：对应三个不同的大拇指/鼓掌预制体
        if (config.BannerStyle == 1 || config.BannerStyle == 2 || config.BannerStyle == 3) 
        {
            return $"UIEffect_shoushi01_{config.BannerStyle}"; 
        }
            
        // 样式 4：金光特效
        if (config.BannerStyle == 4) 
        {
            return "UIEffect_lianhuazuo01";
        }
            
        // 样式 5：长条横幅 (需要根据 FeedbackID 拆分子样式)
        if (config.BannerStyle == 5)
        {
            if (config.FeedbackID == 7) return "UIEffect_lianji01";
            if (config.FeedbackID == 8) return "UIEffect_lianji02";
            if (config.FeedbackID == 9) return "UIEffect_lianji03";
            
            if (config.FeedbackID == 10) 
            {
                // 11连击以上，轮询使用 4~7 号横幅
                string hname = $"UIEffect_lianji0{_longBannerCounter}";
                _longBannerCounter++;
                if (_longBannerCounter > 7) _longBannerCounter = 4; // 重置轮询
                return hname;
            }
        }
        
        // 兜底返回
        return "UIEffect_shoushi01_1"; 
    }
    
    /// <summary>
    /// 播放长条横幅专属动画 (左侧滑入 -> 停留 -> 渐隐消失)
    /// </summary>
    private IEnumerator PlayLongBannerAnimation(GameObject bannerObj, Vector3 targetLocalPos)
    {
        AudioManager.Instance.PlaySoundEffect("ConsecutiveWord");
        RectTransform rt = bannerObj.GetComponent<RectTransform>();
        
        // 确保预制体上有 CanvasGroup 组件用于控制透明度渐隐
        CanvasGroup cg = bannerObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = bannerObj.AddComponent<CanvasGroup>();

        // 1. 初始化对象池复用状态
        cg.DOKill();
        rt.DOKill();
        cg.alpha = 1f;
        
        // 2. 设定动画起点：Y轴不变，X轴放到屏幕最左侧外 (例如 -1200)
        // 这里的 -1200f 根据你的实际屏幕分辨率可适当调大，确保初始完全不可见
        Vector3 startPos = new Vector3(-1200f, targetLocalPos.y, targetLocalPos.z);
        rt.localPosition = startPos;
        
        // 3. 编排时间轴序列
        Sequence seq = DOTween.Sequence();

        // 阶段 A：从左侧极速滑入到目标中心点 (0.2s)，带一点回弹效果更自然
        seq.Append(rt.DOLocalMoveX(targetLocalPos.x, 0.6f).SetEase(Ease.OutCubic));

        // 阶段 B：停留展示 (1.0s)
        // 此时预制体自带的粒子光效会自动播放，我们只需要在时间轴上等待
        seq.AppendInterval(2.5f);

        // 阶段 C：渐隐消失 (0.2s)
        seq.Append(cg.DOFade(0f, 0.6f));

        // 阶段 D：彻底结束后的清理回收
        seq.OnComplete(() =>
        {
            bannerObj.SetActive(false);
            cg.alpha = 1f; // 恢复透明度，以备下一次从对象池中拿出来用
        });
        yield return seq.WaitForCompletion();
    }
    #endregion
}
