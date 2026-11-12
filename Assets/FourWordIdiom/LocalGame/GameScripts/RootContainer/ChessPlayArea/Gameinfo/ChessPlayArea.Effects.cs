using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/**
 *表现层与动效 (ChessPlayArea.Effects.cs)
 * 负责所有的 DOTween 动画、粒子与飘字
 */
public partial class ChessPlayArea
{
    // ======================= 禅意值变化音效 =======================
    /// <summary>禅意值变化音效名（与结算页保持一致）</summary>
    private const string ZenScoreSfxName = "event_rank_change";

    /// <summary>
    /// 禅意值变化时播放一次。
    /// 注意：玩法内得分频率高，这里刻意不用循环播放（结算页才是"一直响到分数加完"）。
    /// </summary>
    private void PlayZenScoreSfx()
    {
        AudioManager.Instance.PlaySoundEffect(ZenScoreSfxName);
    }

    /// <summary>
    /// 响应暂停界面的退出二次确认：将局内的蝶蛹进度条提层显示
    /// </summary>
    private void HighlightPupaUI(bool isHighlight)
    {
        Debug.Log($"🚨 [ChessPlayArea] 收到提层事件！isHighlight = {isHighlight}");
        // 如果当前关卡本来就不显示蝶蛹（activeSelf 为 false），直接跳过，不用提层
        if (pupaObj == null || !pupaObj.activeSelf) 
            return;

        // 获取或添加 Canvas 和 Raycaster 以便控制渲染层级
        Canvas pupaCanvas = pupaObj.GetComponent<Canvas>();
        if (pupaCanvas == null) pupaCanvas = pupaObj.AddComponent<Canvas>();
        if (pupaObj.GetComponent<GraphicRaycaster>() == null) pupaObj.AddComponent<GraphicRaycaster>();

        if (isHighlight)
        {
            // 提层：只提到 UpPopPanel 层，保证能盖住玩法界面本身，
            // 但要压在复活 / 失败弹窗（画布在 UpPopTwoPanel 层）之下，不能浮在弹窗上面。
            pupaCanvas.overrideSorting = true;
            pupaCanvas.sortingLayerName = UIPanelLayer.UpPopPanel;
            pupaCanvas.sortingOrder = 0;
        }
        else
        {
            // 恢复默认层级（回到父级 Canvas 的 BasePanel 层）
            pupaCanvas.overrideSorting = false;
            pupaCanvas.sortingOrder = 0;
        }
    }
    
    /// <summary>
    /// 播放入场动画, 建议在面板打开/初始化完成时调用这个方法
    /// </summary> 
    private IEnumerator PlayEnterAnimation()
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
        if (pupaObj != null && pupaObj.activeSelf)
        {
            if (pupaObj.TryGetComponent<CanvasGroup>(out var pupaCg))
            {
                // 在 0.2 秒处开始，用 0.4 秒的时间平滑淡入
                enterSeq.Insert(0.2f, pupaCg.DOFade(1f, 0.4f).SetEase(Ease.InOutSine));
            }
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
        if (MyThemeBtn != null && MyThemeBtn.TryGetComponent<CanvasGroup>(out var themeCg))
        {
            enterSeq.Insert(0.6f, themeCg.DOFade(1f, 0.4f).SetEase(Ease.InOutSine));
        }
        if (pauseBtn != null && pauseBtn.TryGetComponent<CanvasGroup>(out var pauseCg))
        {
            enterSeq.Insert(0.6f, pauseCg.DOFade(1f, 0.4f).SetEase(Ease.InOutSine));
        }
        yield return enterSeq.WaitForCompletion();
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
        if (MyThemeBtn != null) 
        {
            DOTween.Kill(MyThemeBtn.transform);
            if (MyThemeBtn.TryGetComponent<CanvasGroup>(out var cg)) DOTween.Kill(cg);
        }
        if (pauseBtn != null) 
        {
            DOTween.Kill(pauseBtn.transform);
            if (pauseBtn.TryGetComponent<CanvasGroup>(out var cg)) DOTween.Kill(cg);
        }
        if (pupaObj != null) 
        {
            DOTween.Kill(pupaObj.transform);
            if (pupaObj.TryGetComponent<CanvasGroup>(out var cg)) DOTween.Kill(cg);
        }
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
        if (radarRoot != null) radarRoot.SetActive(false);
        if (radarMaskImage != null) radarMaskImage.gameObject.SetActive(false);
        if (MyThemeBtn != null)
        {
            CanvasGroup cg = MyThemeBtn.GetComponent<CanvasGroup>();
            if (cg == null) cg = MyThemeBtn.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }
        if (pauseBtn != null)
        {
            CanvasGroup cg = pauseBtn.GetComponent<CanvasGroup>();
            if (cg == null) cg = pauseBtn.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }
        if (pupaObj != null)
        {
            CanvasGroup cg = pupaObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = pupaObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            
            // 恢复一下缩放，防止它之前播放收集动效时停留在放大的状态
            pupaObj.transform.localScale = Vector3.one; 
        }
        // ==========================================
        // 🌟 金币收集栏：记录原坐标，并瞬间移动到屏幕外
        // ==========================================
        if (ColdTable != null)
        {
            RectTransform rt = ColdTable.GetComponent<RectTransform>();
            // 获取或添加 CanvasGroup 用于控制透明度
            CanvasGroup cg = ColdTable.GetComponent<CanvasGroup>();
            if (cg == null) cg = ColdTable.AddComponent<CanvasGroup>();

            // 1. 只在第一次打开时记录编辑器里的“完美坐标”
            if (!_isColdTablePosRecorded)
            {
                _coldTableTargetPos = rt.anchoredPosition;
            
                // 2. 动态计算绝对安全的屏幕外坐标：
                // 因为锚点在左侧，所以直接往左推 (自身的宽度 + 50像素的安全距离)
                // 这样不管里面塞了多少子节点，都不可能走光
                _coldTableOffScreenX = -rt.rect.width - 50f; 
            
                _isColdTablePosRecorded = true;
            }

            // 3. 杀掉可能残留的旧动画，防止快速重开游戏时动画抽搐
            rt.DOKill();
            cg.DOKill();

            // 4. 瞬间就位到屏幕外，并设为全透明
            rt.anchoredPosition = new Vector2(_coldTableOffScreenX, _coldTableTargetPos.y);
            cg.alpha = 0f;
        }
        
    }
    /// <summary>
    /// 蝶蛹进度收集完成时的粒子发光散开特效
    /// </summary>
    private void PlayPupaCompleteEffect()
    {
        if (_pupaCompleteEffectNode != null)
        {
            // 1. 先关闭再打开，能自动重置很多 UI 动画状态
            _pupaCompleteEffectNode.SetActive(false);
            _pupaCompleteEffectNode.SetActive(true);
            
            // 2. 找到粒子并让它喷发
            ParticleSystem ps = _pupaCompleteEffectNode.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(); // 确保从头开始
                ps.Play();
            }
            
            DOVirtual.DelayedCall(2f, () => 
            {
                if (_pupaCompleteEffectNode != null)
                {
                    _pupaCompleteEffectNode.SetActive(false);
                }
            });
        }
    }
     /// <summary>
    /// 串联“禅意分位置起飞 ➔ 屏幕中心消失 ➔ 弹出常驻横幅”的视觉工作流
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
             _bannerLiziCache.SetActive(true);
            // _bannerLiziCache.transform.SetAsLastSibling();
            //
            // // Transform effectLightTrans = _bannerLiziCache.transform.Find("EffectLight");
            // Transform lightYeTrans = _bannerLiziCache.transform.Find("EffectLight/Light_ye");
            // Transform effectPointTrans = _bannerLiziCache.transform.Find("EffectPoint"); // ✨ 新增：找到 EffectPoint
            // // ParticleSystem lightYePs = lightYeTrans != null ? lightYeTrans.GetComponent<ParticleSystem>() : null;
            // if (lightYeTrans != null) 
            // {
            //     lightYeTrans.gameObject.SetActive(false);  // ✨ 核心修复：前 0.5 秒强制它隐藏！绝对不准发光！
            // }
            //
            // RectTransform liziRect = _bannerLiziCache.GetComponent<RectTransform>();
            // Vector3 startWorld = zentable != null ? zentable.transform.position : transform.position;
            // Vector3 startLocal = transform.InverseTransformPoint(startWorld);
            // startLocal.z = 0f; // 强行抹平 Z 轴，防止受相机深度影响乱飞
            // liziRect.localPosition = startLocal;
            //
            // // 净化物理残留
            // var trails = _bannerLiziCache.GetComponentsInChildren<TrailRenderer>(true);
            // foreach (var t in trails) t.Clear();
            // var pss = _bannerLiziCache.GetComponentsInChildren<ParticleSystem>(true);
            // foreach (var p in pss) { p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            //
            // _bannerLiziCache.SetActive(true);
            // effectPointTrans.gameObject.SetActive(true);
             yield return new WaitForSeconds(0.5f);
            AudioManager.Instance.PlaySoundEffect("result_chest_open",0,1);
            // lightYeTrans.gameObject.SetActive(true);
            // effectPointTrans.gameObject.SetActive(false);
            //
            // RectTransform gridRect = chessboardGrid.GetComponent<RectTransform>();
            // Vector3[] corners = new Vector3[4];
            // gridRect.GetWorldCorners(corners);
            // Vector3 endWorld = (corners[0] + corners[3]) / 2f;
            // endLocal = transform.InverseTransformPoint(endWorld);
            // endLocal.z = 0f; // 再次抹平 Z 轴防抖
            // // float yOffset = 80f; 
            // endLocal.y += 155f;
            // bool isFlyDone = false;
            // // 飞行动画：平滑向中心聚拢
            // liziRect.DOLocalMove(endLocal, 0.75f).SetEase(Ease.InOutSine).OnComplete(() => {
            //     isFlyDone = true;
            // });
            yield return new WaitForSeconds(1.2f);
            // yield return new WaitUntil(() => isFlyDone);
            // DOVirtual.DelayedCall(0.5f, () => 
            // {
            //     if (_bannerLiziCache != null) 
            //     {
            //         _bannerLiziCache.SetActive(false);
            //     }
            // });
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
                    string titleKey = ChessStageController.Instance.GetCurrentMatchedTitleKey();
                    string title = MultilingualManager.Instance.GetString(titleKey, "pingzi");
                    string percentStr = "";
                    if (!string.IsNullOrEmpty(rule.LongTextKey))
                    {
                        string desc = MultilingualManager.Instance.GetString(rule.LongTextKey, "pingzi");
                        // 写入百分比 (这里处理 0% 防护，大厂通常会做个假的最低限度)
                        float displayPercent = ChessStageController.Instance.DisplayZenPercent;
                        percentStr = string.Format(desc, displayPercent.ToString("F2"));
                    }
                    else if (!string.IsNullOrEmpty(rule.PhraseKey))
                    {
                        // 没有 LongTextKey 时，使用短文案填充
                        percentStr = MultilingualManager.Instance.GetString(rule.PhraseKey, "pingzi");
                    }
                    // 你需要根据预制体里的具体层级找 Text 组件，这里假设存在
                    Text titleText = activeBanner.transform.Find("Text01")?.GetComponent<Text>();
                    Text percentText = activeBanner.transform.Find("Text02")?.GetComponent<Text>();
                    if (percentText != null)
                    {
                        percentText.text = percentStr;
                    }
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
        
        // 获取终点坐标（ColdTable 是金币收集点，HexWord 当前预制体未挂该节点，兜底飞到分数牌）
        Transform goldTargetPos = ColdTable != null && ColdTable.transform.childCount > 0
            ? ColdTable.transform.GetChild(0)
            : _zenScoreText.transform;
        Transform pupaPos = pupaProgressBar.transform;     // 蝶蛹终点
        Transform zenPos = _zenScoreText.transform;               // 禅意分/莲花终点

        // 1. 金币（2片叶子解锁）
        if (curLeaves >= 2 && leafGold != null)
        {
            int leafGoldReward = 0;
            List<LeafReward> earnedRewards = ChessStageController.Instance.GetAllLeafRewards(curLeaves);
            foreach (var reward in earnedRewards) {
                if (reward.Type == 0) leafGoldReward += reward.Value;
            }
            
            // Debug.Log("<color=#FF5500>[飞行排查] 3. 准备起飞：金币</color>");
            hasAnyFly = true;
            // _visualEarnedGold = 0;
            if (ColdTable != null)
            {
                ColdTable.SetActive(true);
                SlideInColdTableAnim();
            }
            // 将真实奖励拆分成 3 份
            int part1 = leafGoldReward / 3;
            int part2 = leafGoldReward / 3;
            int part3 = leafGoldReward - part1 - part2; // 确保总和一分不差
            int[] parts = { part1, part2, part3 };
            float startFlightDelay = 0.45f;
            
            for (int i = 0; i < 3; i++)
            {
                int goldToAdd = parts[i];
                DOVirtual.DelayedCall(startFlightDelay + i * 0.2f, () =>
                {
                    CustomFlyInManager.Instance.FlyInGoldAdvanced(leafGold.transform, goldTargetPos, () =>
                    {
                        AddInLevelGoldWithAnim(goldToAdd);
                    }, 0.7f); // <--- 在这里传入覆盖时间
                });
            }
            // 播放完后稍作停留，再滑出屏幕
            DOVirtual.DelayedCall(2f, () => 
            {
                // _visualEarnedGold = 0; // 重置防穿帮
                SlideOutColdTableAnim();
                GameDataManager.Instance.UserData.UpdateGold(leafGoldReward, false, false, "树叶收集结算获得");
            });
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
                    PlayPupaCollectVisualEffect(1);
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
    /// 剩余时间兑换禅意分表现流水线 
    /// (1秒内清空时间 -> 原汁原味复刻 FlyToZenScore 的轨迹 -> 调用 UpdateZenScoreUI 结算)
    /// </summary>
    private IEnumerator PlayTimeBonusToZenScoreFlow(int bonusScore)
    {
        if (bonusScore <= 0) yield break;

        Vector3 startPos = _timerText != null ? _timerText.transform.position : Vector3.zero;
        Vector3 endPos = _zenScoreText.transform.position;

        Transform timeUIRoot = _timerText != null ? _timerText.transform.parent : null;
        Color originalBgColor = Color.white;
        if (timeUIRoot != null)
        {
            // 杀掉旧动画，防止冲突
            timeUIRoot.DOKill(true);
            // 整个时间组件(底板+文字+边缘框)一起 Q 弹放大，保持完美的UI比例
            timeUIRoot.DOScale(1.15f, 0.3f).SetEase(Ease.OutBack);
        }
        if (_timerText != null)
        {
            originalBgColor = _timerText.color;
            // 将倒计时的文字变成显眼的金黄色！(相比纯黄 Color.yellow，这种带点橙的金色质感更好)
            _timerText.color = new Color(1f, 0.85f, 0f, 1f); 
        }
        // if (gameTimeBg != null)
        // {
        //     originalBgColor = gameTimeBg.color; // 记录真实的初始颜色 (以备后患)
        //     gameTimeBg.gameObject.SetActive(true);
        //     
        //     // 杀掉旧动画，防止与红色的倒数警告冲突
        //     gameTimeBg.transform.DOKill();
        //     gameTimeBg.DOKill();
        //     // 将原本用来做红框警告的 gameTimeBg 变成常亮的黄色高亮框
        //     Color yellowGlow = Color.yellow;
        //     yellowGlow.a = 1f; 
        //     gameTimeBg.color = yellowGlow;
        // }
        
        // 1. 动画前摇：时间文本轻微闪烁
        if (_timerText != null)
        {
            _timerText.transform.DOScale(1.2f, 0.15f).SetLoops(4, LoopType.Yoyo);
        }
        yield return new WaitForSeconds(0.3f);

        // 动态计算粒子数量（防止分数太大导致粒子过多）
        float initialTime = Mathf.Max(0, _remainingTime);
        // 动态计算粒子数量：剩余时间越长，粒子越多。
        // ★ 上限从 15 降到 6：每个 huaEffect 实例内部有 2 个主粒子系统 + 1 个 TrailRenderer，
        //   15 个实例同时飞 = 30 个粒子系统并发，是结算瞬间卡顿的主要来源。
        //   6 个实例已经足够铺满整条飞行弧线，观感几乎无差别。
        // 例：10秒=2个， 60秒(1分钟)=4个， 240秒(4分钟)=6个
        int particleCount = Mathf.Clamp(Mathf.FloorToInt(initialTime / 15f), 2, 6);
        
        // 时间UI 滚动到 00:00 的总时长
        float totalTimeRollDuration = Mathf.Clamp(initialTime / 100f, 1.0f, 1.5f);
        float emitInterval = totalTimeRollDuration / particleCount;

        int baseAdd = bonusScore / particleCount;
        int remainder = bonusScore - baseAdd * particleCount;
        int currentVisualScore = _lastZenScore;

        Sequence mainSeq = DOTween.Sequence().SetLink(this.gameObject);
        // 2. 时间 UI 在 1 秒内平滑滚动到 0
        mainSeq.Insert(0, DOVirtual.Float(initialTime, 0f, totalTimeRollDuration, (v) => 
        {
            int m = Mathf.FloorToInt(v) / 60;
            int s = Mathf.FloorToInt(v) % 60;
            if (_timerText != null) _timerText.text = $"{m:00}:{s:00}";
        }).SetEase(Ease.Linear));

        float maxFlightDuration = 0f; // 记录整个飞行动画最长需要多久

        // 3. 发射粒子（完全照抄 FlyToZenScore 的算法）
        for (int i = 0; i < particleCount; i++)
        {
            int addThisStep = baseAdd + (i == particleCount - 1 ? remainder : 0);
            int stepEndScore = currentVisualScore + addThisStep;
            
            float emitTime = i * emitInterval;
            
            mainSeq.InsertCallback(emitTime, () => 
            {
                GameObject particle = _zenCorrectTrailPool.GetObject();
                if (particle != null)
                {
                    particle.SetActive(false);
                    particle.transform.position = startPos;
                    
                    TrailRenderer[] trails = particle.GetComponentsInChildren<TrailRenderer>(true);
                    foreach (var t in trails) t.Clear();
                    ParticleSystem[] pss = particle.GetComponentsInChildren<ParticleSystem>(true);
                    // ★ 先 Stop(StopEmittingAndClear) 再 Play：只 Clear 会留下发射器状态，
                    //   池对象复用时可能带着上一轮残余的发射计时，导致刚激活就喷一堆。
                    foreach (var p in pss)
                    {
                        p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        p.Play();
                    }
                    
                    particle.transform.SetAsLastSibling();
                    particle.SetActive(true);

                    // ★ 飞入路径：先朝目标方向斜向上冲，再落到目标点（目标在倒计时左上方）。
                    //   注意不能用"起点正上方"做控制点：终点在左上，纯垂直上冲会把粒子带出屏幕。
                    float distance = Vector3.Distance(startPos, endPos);
                    float duration = Mathf.Clamp(distance * 0.5f, 0.7f, 1.1f);

                    // 控制点 = 起点朝目标方向推进 55%，再整体抬高（抬高量按距离比例，避免出屏）
                    Vector3 toTarget = endPos - startPos;
                    Vector3 liftPos = startPos + toTarget * 0.55f + Vector3.up * (distance * 0.18f);
                    // 极小横向抖动，让多颗粒子不完全重叠（幅度受控，不会跑偏）
                    liftPos.x += UnityEngine.Random.Range(-distance * 0.03f, distance * 0.03f);

                    Vector3[] path = new Vector3[] { startPos, liftPos, endPos };

                    // ★ 让特效物体的 up 轴对准飞行方向：
                    //   光柱系统是 Stretched Billboard，沿粒子速度方向拉伸，
                    //   而速度方向取的是物体局部 up，所以必须把物体转过来，光柱才会顺着飞行方向。
                    Vector3 flyDir = (endPos - startPos).normalized;
                    if (flyDir.sqrMagnitude > 0.0001f)
                        particle.transform.rotation = Quaternion.FromToRotation(Vector3.up, flyDir);

                    particle.transform.DOPath(path, duration, PathType.CatmullRom)
                        .SetTarget(particle.transform)
                        .SetEase(Ease.OutQuad)
                        .OnComplete(() =>
                    {
                        // 1. 回收粒子
                        // ★ 回收前先彻底停掉粒子与拖尾：否则池对象虽然 SetActive(false)，
                        //   粒子系统仍会保留发射状态，下次复用时第一帧就喷出一大团，
                        //   倒计时连续发射时越积越多，是结算卡顿的另一半原因。
                        StopParticleCompletely(particle);
                        _zenCorrectTrailPool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
                        
                        // 2. 🎯 核心：直接调用游戏原生的 UI 结算方法，完美执行老虎机滚动！
                        UpdateZenScoreUI(stepEndScore, addThisStep, false);

                        // 3. 补充：强制播放目标底座的莲花爆开粒子！
                        // if (_lotusParticle != null)
                        // {
                        //     _lotusParticle.SetActive(false);
                        //     _lotusParticle.SetActive(true);
                        //     ParticleSystem ps = _lotusParticle.GetComponent<ParticleSystem>();
                        //     if (ps != null) ps.Play();
                        // }
                    });
                }
            });
            
            currentVisualScore = stepEndScore;
            
            // 预测这颗粒子落地的时间点，找出整个循环里的最大值
            float expectedDuration = Mathf.Clamp(Vector3.Distance(startPos, endPos) * 0.5f, 0.6f, 1.8f);
            if (emitTime + expectedDuration > maxFlightDuration)
            {
                maxFlightDuration = emitTime + expectedDuration;
            }
        }
        // 等待最晚到达的粒子飞完，再额外留 0.2 秒让老虎机动画滚完，然后再呼出横幅
        yield return new WaitForSeconds(maxFlightDuration + 0.1f);
        _remainingTime = 0f; 
        if (_timerText != null) 
        {
            _timerText.text = "00:00"; 
            // 🌟 动画结束，把文字颜色通过渐变恢复原样
            _timerText.DOColor(originalBgColor, 0.3f);
        }
        if (timeUIRoot != null)
        {
            // 整个时间 UI 统一缩放回原样 1.0f
            timeUIRoot.DOScale(1f, 0.3f).SetEase(Ease.InBack);
        }
        // if (gameTimeBg != null)
        // {
        //     // 渐隐消失
        //     gameTimeBg.DOFade(0f, 0.3f).OnComplete(() => {
        //         gameTimeBg.gameObject.SetActive(false);
        //         gameTimeBg.color = originalBgColor; // 恢复初态，避免干扰正常的倒计时报警颜色
        //     });
        // }
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

        // ★ 禅意值开始变化：播放一次音效（玩法内不做循环，结算页才一直响到加完）
        PlayZenScoreSfx();

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
        if (pupaObj == null || !pupaObj.activeSelf) return; // 蝶蛹没开启就不飞

        GameObject particle = _pupaTrailPool.GetObject();
        if (particle == null)
        {
            UpdatePupaProgress(targetScore, false); // 兜底
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

        Vector3 endPos = pupaProgressBar.transform.position;
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
            UpdatePupaProgress(targetScore, false);
        });
    }
    
    /// <summary>
    /// 仅视觉表现：蝶蛹 UI 文本 +1 并播放跳动特效（不修改底层真实数据）
    /// </summary>
    public void PlayPupaCollectVisualEffect(int addVisualValue = 1)
    {
        if (pupaObj == null) return;

        // 1. 获取圆环进度条中间的那个文本
        Text progressText = pupaObj.GetComponentInChildren<Text>(true);
        if (progressText == null) return;

        // 2. 只有当它已经处于显示状态（即已经满环弹出了）才去修改它
        if (progressText.gameObject.activeSelf)
        {
            // 提取当前文本中的数字（去掉可能带有的 "+" 号和空格）
            string currentStr = progressText.text.Replace("+", "").Trim();
            
            if (int.TryParse(currentStr, out int currentVisualCount))
            {
                // 在原有数字基础上加上新飞过来的数量
                int nextCount = currentVisualCount + addVisualValue;
                progressText.text = $"+{nextCount}";
            }
            else
            {
                // 兜底：如果原文本没数字或者解析失败，直接显示当前增加的值
                progressText.text = $"+{addVisualValue}";
            }

            // 3. 给这个文本加一个 Q 弹的放大缩小反馈，增强“吸收”的手感
            progressText.transform.DOKill();
            progressText.transform.localScale = Vector3.one; // 恢复初始大小
            progressText.transform.DOPunchScale(new Vector3(0.5f, 0.5f, 0.5f), 0.3f, 5, 1f);
        }
        // 3. 播放音效
        // AudioManager.Instance.PlaySoundEffect("getPupa");
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
        // ★ 先 Stop(StopEmittingAndClear) 再 Play，避免带上一轮残余状态
        foreach (var p in pss)
        {
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            p.Play();
        }
        particle.transform.SetAsLastSibling();
        particle.SetActive(true);
        
        Vector3 endPos = _zenScoreText.transform.position;
        
        // ==========================================
        // 🌟 核心动态时间计算：距离 ÷ 速度 = 时间
        // ==========================================
        float distance = Vector3.Distance(startPos, endPos);
        // 限制最快不低于 0.4 秒，最慢不超过 1.2 秒
        float duration = Mathf.Clamp(distance * 0.5f, 1.4f, 2.2f); 

        // ★ 飞入路径：朝目标方向斜向上冲，再落到目标点（不要用纯垂直，目标在斜上方时会出屏）
        Vector3 toTarget = endPos - startPos;
        Vector3 liftPos = startPos + toTarget * 0.55f + Vector3.up * (distance * 0.18f);
        liftPos.x += UnityEngine.Random.Range(-distance * 0.03f, distance * 0.03f);

        Vector3[] path = new Vector3[] { startPos, liftPos, endPos };

        // ★ 让特效物体的 up 轴对准飞行方向，光柱（Stretched Billboard）才会顺着飞行方向拉伸
        Vector3 flyDir = (endPos - startPos).normalized;
        if (flyDir.sqrMagnitude > 0.0001f)
            particle.transform.rotation = Quaternion.FromToRotation(Vector3.up, flyDir);

        // 飞行 0.6 秒
        particle.transform.DOPath(path, duration, PathType.CatmullRom).SetEase(Ease.OutQuad).OnComplete(() => 
        {
            // ★ 回收前彻底停掉粒子与拖尾，避免池对象残留发射
            StopParticleCompletely(particle);
            pool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
            // 🌟 命中！执行禅意分的UI跳动和飘字特效
            UpdateZenScoreUI(targetScore, scoreDiff, isDeduction);
        });
    }

    /// <summary>
    /// 彻底停止一个特效实例上的所有粒子与拖尾。
    /// 用于对象回收前：池对象只 SetActive(false) 的话，粒子系统会保留发射状态，
    /// 下次复用时第一帧就喷出一大团，连续发射时会不断累积。
    /// </summary>
    private static void StopParticleCompletely(GameObject effect)
    {
        if (effect == null) return;

        var pss = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            var ps = pss[i];
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        var trails = effect.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null) trails[i].Clear();
        }
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
    /// 规范重构：全面加强出入场清理（完美解决走光、非法状态与内存留存）
    /// </summary>
    private void ClearAndResetLeafSliderComponents()
    {
        // 退出关卡/关闭编辑器 Play Mode 时控制器可能已先销毁，这里直接跳过，避免 OnDisable 抛空引用
        if (ChessStageController.Instance == null) return;

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
    private int _applauseBgCounter
    {
        get { return GameDataManager.Instance.OverallRank.applauseBgCounter; }
        set { GameDataManager.Instance.OverallRank.applauseBgCounter = value; }
    }

    private int _longBannerCounter
    {
        get { return GameDataManager.Instance.OverallRank.longBannerCounter; }
        set { GameDataManager.Instance.OverallRank.longBannerCounter = value; }
    }

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
            string rawText = MultilingualManager.Instance.GetString(key, "pingzi");
            // 🌟 加入禁则处理
            Vector3 oldScale = effectObj.transform.localScale;
            effectObj.transform.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(textComp.GetComponent<RectTransform>());
            Canvas.ForceUpdateCanvases();
            
            textComp.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComp.text = UIUtilities.ApplyKinsokuShori(textComp, rawText);
            
            effectObj.transform.localScale = oldScale;
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

    #region 开局游戏
    /// <summary>
    /// 播放关卡开局的激励横幅 (关前激励)
    /// </summary>
    private IEnumerator PlayEnterStimulateFlow()
    {
        // 1. 向 Controller 索要仲裁结果（拦截器会在内部自动判断是否为首次进入）
        EnterStimulateResult? result = ChessStageController.Instance.GetValidEnterStimulate();
        
        // 如果没抽中、条件不满足、或者不是首次进入，直接结束协程，不耽误游戏流程
        if (result == null || !result.HasValue) yield break;

        var data = result.Value;

        // 2. 从原有的对象池加载对应的预制体
        // 假设美术提供的预制体命名规律为 UIEffect_EnterStimulate_1, UIEffect_EnterStimulate_2 等
        string prefabName = $"UIEffect_EnterStimulate_{data.BannerType}";
        GameObject bannerObj = GetFromStimulatePool(prefabName); 
        
        if (bannerObj == null) 
        {
            Debug.LogError($"[关前激励] 找不到预制体 {prefabName}");
            yield break; 
        }

        // 2. 绑定 UI 引用 (建议使用上文提到的视图组件绑定，这里为了通用仍使用 GetComponent)
        // 获取根节点的 CanvasGroup
        CanvasGroup rootCg = bannerObj.GetComponent<CanvasGroup>();
        if (rootCg == null) rootCg = bannerObj.AddComponent<CanvasGroup>();
        // 尝试获取内容组的 CanvasGroup (假设美术把文字和Emoji包在了一个叫 ContentGroup 的节点下)
        // 如果美术没有包一层，那就尝试获取根节点下的所有 Text 和 Image 自己做渐变
        Transform contentTrans = bannerObj.transform.GetChild(0);
        CanvasGroup contentCg = null;
        if (contentTrans != null)
        {
            contentCg = contentTrans.GetComponent<CanvasGroup>();
            if (contentCg == null) contentCg = contentTrans.gameObject.AddComponent<CanvasGroup>();
        }
        
        if (data.LevelType is 6)
        {
            var sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("SpecialLevel1","UI_MainBase");
            bannerObj.GetComponent<Image>().sprite = sprite;
        }else if (data.LevelType is 8)
        {
            var sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("SpecialLevel2","UI_MainBase");
            bannerObj.GetComponent<Image>().sprite = sprite;
        }
        // 3. 绑定 UI 数据 (文案与图标)
        Text textComp = bannerObj.GetComponentInChildren<Text>(true);
        if (textComp != null && !string.IsNullOrEmpty(data.TextKey))
        {
            string rawText = MultilingualManager.Instance.GetString(data.TextKey, "pingzi");
            
            // 🌟 移植过关横幅的禁则处理（标点换行修复）
            // 1. 强行通电：先将父节点缩放恢复为 1，确保在正确的物理尺寸下计算排版
            Vector3 oldScale = bannerObj.transform.localScale;
            bannerObj.transform.localScale = Vector3.one;
            
            // 2. 强制重建 UI 布局
            LayoutRebuilder.ForceRebuildLayoutImmediate(textComp.GetComponent<RectTransform>());
            Canvas.ForceUpdateCanvases();
            
            // 3. 应用禁则计算，把换行符死死钉进字符串里
            textComp.horizontalOverflow = HorizontalWrapMode.Wrap;
            string processedText = UIUtilities.ApplyKinsokuShori(textComp, rawText);
            
            // 4. 恢复预制体缩放状态，交还给下面的 DOTween 继续播入场动画
            bannerObj.transform.localScale = oldScale;
            
            textComp.text = processedText;
        }

        // 尝试寻找名为 "Emoji" 的 Image 节点并加载对应图标
        Image emojiImg = bannerObj.GetComponentsInChildren<Image>(true)
            .FirstOrDefault(img => img.gameObject != bannerObj);
        if (emojiImg != null)
        {
            if (!string.IsNullOrEmpty(data.Emoji))
            {
                emojiImg.gameObject.SetActive(true);
                emojiImg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(data.Emoji);
                emojiImg.SetNativeSize(); // 保证图标比例不失真
            }
            else
            {
                emojiImg.gameObject.SetActive(false); // 没有配图标就隐藏
            }
        }

        // 4. 编排丝滑的 Vita 风格 DOTween 出入场动画
        bannerObj.SetActive(true);
        bannerObj.transform.SetAsLastSibling(); // 保证显示在最上层

        RectTransform rt = bannerObj.GetComponent<RectTransform>();
        // --- 强制初始状态 ---
        rt.DOKill();
        rootCg.DOKill();
        if (contentCg != null) contentCg.DOKill();
        
        // 策划要求：位置在中心，50% 尺寸，底图 20% 透明度
        rt.anchoredPosition = new Vector2(0, 180f); // 假设展示位置在 Y=180
        rt.localScale = Vector3.one * 0.5f;
        rootCg.alpha = 0.2f;
        // 文字和Emoji初始完全不可见
        if (contentCg != null) contentCg.alpha = 0f;
        // 4. 编排 Vita 风格时间轴
        Sequence seq = DOTween.Sequence();

        if (data.BannerType == 3)
        {
            // [入场阶段]：0.3秒内，整体缩放从 50% 膨胀到 100%，透明度从 20% 变 100%
            // 使用 Ease.OutQuad 让膨胀感更平滑沉稳
            seq.Append(rt.DOScale(1f, 0.3f).SetEase(Ease.OutQuad));
            seq.Join(rootCg.DOFade(1f, 0.3f).SetEase(Ease.OutQuad));
            // [内容显现阶段]：底板膨胀到位后，用 0.1 秒将内容(文字和图标)淡入
            if (contentCg != null)
            {
                seq.Append(contentCg.DOFade(1f, 0.1f).SetEase(Ease.Linear));
            }
        
            // [停留] 等待 2.0 秒供玩家阅读 (期间预制体自身的粒子动画会正常播放)
            seq.AppendInterval(1.5f);

            // [退场前摇阶段]：策划要求的退出前 0.1 秒略微放大效果 (蓄力感)
            seq.Append(rt.DOScale(1.05f, 0.1f).SetEase(Ease.OutQuad));
            // [退场消失阶段]：0.2秒原地缩小并渐隐 (去掉所有的 DOAnchorPosY 移动)
            seq.Append(rt.DOScale(0.5f, 0.2f).SetEase(Ease.InQuad)); // 往回收到 50% 大小
            seq.Join(rootCg.DOFade(0f, 0.2f));                       // 同时整体透明度归零

        }
        else
        {
            // --- 表现 B：从左侧滑动入场 ---
            RectTransform canvasRect = GameBase.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            float canvasWidth = canvasRect.rect.width;
            float bannerWidth = rt.rect.width;
            // 2. 计算绝对安全的屏幕外坐标 (屏幕一半宽 + 横幅一半宽 + 100像素容错边距)
            float offScreenX = (canvasWidth / 2f) + (bannerWidth / 2f) + 100f;
            
            // 起始位置放置在屏幕极左侧绝对可视范围外
            rt.anchoredPosition = new Vector2(-offScreenX, 180f);
            rt.localScale = Vector3.one;
            rootCg.alpha = 1f;
            if (contentCg != null) contentCg.alpha = 1f; // 滑动入场时直接全显

            // 0.5秒从左侧滑到屏幕中心，带少许回弹 (OutBack) 增加物理感
            seq.Append(rt.DOAnchorPosX(0f, 0.5f).SetEase(Ease.OutBack, 1.1f));
            
            seq.AppendInterval(1.5f);
            
            // 0.4秒向右滑出屏幕并渐隐
            seq.Append(rt.DOAnchorPosX(offScreenX, 0.4f).SetEase(Ease.InCubic));
            seq.Join(rootCg.DOFade(0f, 0.4f));
        }
  
        // 5. 挂起协程，等待整个序列播放完毕再放行
        bool isDone = false;
        seq.OnComplete(() =>
        {
            bannerObj.SetActive(false);
            isDone = true;
        });

        AudioManager.Instance.PlaySoundEffect("BannerShow"); // 播放对应的弹出音效

        yield return new WaitUntil(() => isDone);
    }
    
    [Header("关前鼓励 (EnterStimulate) 状态管理")]
    // 独立的预制体缓存池：Key 为预制体名称，Value 为实例化的 GameObject
    private readonly Dictionary<string, GameObject> _enterStimulateCachePool = new Dictionary<string, GameObject>();
    
    /// <summary>
    /// 从池子中获取或加载预制体 (通用封装)
    /// </summary>
    private GameObject GetFromStimulatePool(string prefabName)
    {
        if (_enterStimulateCachePool.TryGetValue(prefabName, out GameObject obj) && obj != null)
        {
            obj.SetActive(true);
            return obj;
        }

        GameObject prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("chessitem", prefabName);
        if (prefab != null)
        {
            obj = Instantiate(prefab, this.transform);
            _enterStimulateCachePool[prefabName] = obj;
            return obj;
        }

        Debug.LogError($"🚨 鼓励反馈预制体加载失败: {prefabName}");
        return null;
    }
    #endregion
    
    private Tween _radarTween; // 缓存 DOTween 动画引用，方便随时停止
    private static Material _radarWriterMat;
    /// <summary>
    /// 开启雷达扫描 (点击雷达道具时调用)
    /// </summary>
    private IEnumerator StartRadarScanCoroutine()
    {
        // 0. 防御：雷达节点缺失时直接跳过，绝不能让开局协程中断（否则后面的计时/引导都不会跑）
        if (radarRoot == null || radarMaskImage == null || radarVisualImage == null || radarScanText == null)
        {
            Debug.LogError($"[记忆雷达] 雷达节点未绑定完整，跳过扫描。" +
                           $"radarRoot={radarRoot != null} mask={radarMaskImage != null} " +
                           $"visual={radarVisualImage != null} text={radarScanText != null}");
            yield break;
        }

        // 1. 初始化
        radarRoot.SetActive(true);
        radarMaskImage.gameObject.SetActive(true);
        if (puzzleTileTable != null)
        {
            foreach (var bowlView in puzzleTileTable.GridList)
            {
                if (bowlView != null) 
                {
                    bowlView.Lock(); // 激活自带的 _mesk 蒙版
                }
            }
            ChessBowlGrid.IsTutorialBlocking = true;
        }
        // 🌟 获取 CanvasGroup 并设为全透明
        CanvasGroup rootCg = radarRoot.GetComponent<CanvasGroup>();
        if (rootCg == null) rootCg = radarRoot.AddComponent<CanvasGroup>();
        rootCg.alpha = 0f;
        
        // 文字缩放到 0，重置状态
        radarScanText.transform.DOKill(true);
        radarScanText.DOKill(true);
        radarScanText.transform.localScale = Vector3.one;
        Color initColor = radarScanText.color;
        initColor.a = 1f;
        radarScanText.color = initColor;
        radarScanText.text = "";
        
        // 2. 动态生成“写遮罩”的材质，赋给扇形图
        if (_radarWriterMat == null)
        {
            _radarWriterMat = new Material(Shader.Find("UI/Default"));
            _radarWriterMat.SetInt("_Stencil", 1);
            _radarWriterMat.SetInt("_StencilComp", 8); // 8=Always (永远写入)
            _radarWriterMat.SetInt("_StencilOp", 2);   // 2=Replace (替换标记)
            _radarWriterMat.SetInt("_ColorMask", 0);   // 0=隐身 (只留裁剪区，不画白图)
            _radarWriterMat.SetFloat("_UseUIAlphaClip", 1.0f);
            _radarWriterMat.EnableKeyword("UNITY_UI_ALPHACLIP");
        }
        radarMaskImage.material = _radarWriterMat;
        radarMaskImage.maskable = false; // 防止被系统干扰
        
        // 3. 通知棋盘上所有还没答对的字块，换上 Stencil 遮罩材质准备被扫描
        // (这里假定写在 ChessboardGrid.cs 中。如果写在别的脚本，请通过单例调用 GridList)
        foreach (var cell in chessboardGrid.GridList.Values)
        {
            cell.EnableRadarPreview(); 
        }
     
        yield return null;
        radarMaskImage.rectTransform.localRotation = Quaternion.Euler(0, 0, 0);
        radarVisualImage.rectTransform.localRotation = Quaternion.Euler(0, 0, 0);
        
        // 4. 重置扫描图的初始角度 (从正上方 0 度开始)
        // 顺时针旋转 Z 轴是负数（-360度），RotateMode.FastBeyond360 允许突破 360 度限制
        _radarTween?.Kill(); // 杀掉旧动画防止叠加跑马
        Sequence mainSeq = DOTween.Sequence();
        _radarTween = mainSeq;
        
        // 🎬 第一幕：通过 CanvasGroup，让父节点下的【光波】和【光柱】一次性完美淡入！
        mainSeq.Append(rootCg.DOFade(1f, 0.3f).SetEase(Ease.InOutQuad));
        // 🎬 第二幕：数字 "3" 弹出 (耗时 0.2 秒)
        // (因为文字初始 Scale 为 0，所以在第一幕淡入时它是隐形的，此时弹出节奏刚好)
        mainSeq.Append(radarScanText.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
        // 🎬 第三幕：开始转动
        Sequence rotateSeq = DOTween.Sequence();
        // 1. 驱动遮罩旋转整整 3 秒
        rotateSeq.Insert(0f, radarMaskImage.rectTransform.DORotate(new Vector3(0, 0, -360f), 3f, RotateMode.FastBeyond360).SetEase(Ease.Linear));
        // 2. 实时让发光光柱同步跟着转
        rotateSeq.OnUpdate(() => {
            if (radarVisualImage != null && radarMaskImage != null)
                radarVisualImage.rectTransform.localRotation = radarMaskImage.rectTransform.localRotation;
        });
        rotateSeq.InsertCallback(0.005f, () =>
        {
            radarScanText.text = "3";
            radarScanText.transform.localScale = Vector3.one;
            radarScanText.transform.DOScale(1.25f, 0.15f).SetLoops(2, LoopType.Yoyo);
        });
        // 3. 转到 120° 时 (第 1 秒)，跳动切换 "2"
        rotateSeq.InsertCallback(1.0f, () => {
            radarScanText.text = "2";
            radarScanText.transform.DOKill(true);
            radarScanText.transform.localScale = Vector3.one;
            radarScanText.transform.DOScale(1.25f, 0.15f).SetLoops(2, LoopType.Yoyo);
        });
        // 4. 转到 240° 时 (第 2 秒)，跳动切换 "1"
        rotateSeq.InsertCallback(2.0f, () => {
            radarScanText.text = "1";
            radarScanText.transform.DOKill(true);
            radarScanText.transform.localScale = Vector3.one;
            radarScanText.transform.DOScale(1.25f, 0.15f).SetLoops(2, LoopType.Yoyo);
        });
        // 将第三幕(旋转)拼接到主时间轴
        mainSeq.Append(rotateSeq);
        // 🎬 第四幕：转满 360° 回到终点，文字 PPT 砸碎式闪烁
        mainSeq.AppendCallback(() => {
            radarScanText.text = MultilingualManager.Instance.GetString("Begin","pingzi");
            radarScanText.transform.DOKill(true);
            radarScanText.transform.localScale = Vector3.one * 2.5f;
            
            Color c = radarScanText.color;
            c.a = 0f;
            radarScanText.color = c;
        });
        // 0.3秒内：同时执行“淡入显影”与“回弹缩小到正常尺寸”
        // Join 保证透明度淡入和缩小砸向屏幕在 0.3 秒内同时发生，OutBack 赋予物理回弹感
        mainSeq.Append(radarScanText.DOFade(1f, 0.2f).SetEase(Ease.OutQuad));
        mainSeq.Join(radarScanText.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
        
        mainSeq.AppendInterval(0.4f);
        mainSeq.Append(rootCg.DOFade(0f, 0.2f).SetEase(Ease.OutQuad));
        //  阻塞等待这个唯一的动画播完
        yield return mainSeq.WaitForCompletion();
        StopRadarScan();
    }
    
    /// <summary>
    /// 关闭雷达扫描 (雷达时间结束时调用)
    /// </summary>
    private void StopRadarScan()
    {
        // 1. 停止旋转动画并清理缓存
        _radarTween?.Kill();
        // 2. 隐藏雷达父节点
        radarRoot.SetActive(false);
        radarMaskImage.gameObject.SetActive(false);
        // 3. 通知棋盘上所有字块，卸载遮罩材质，恢复正常的物理隐身状态
        foreach (var cell in chessboardGrid.GridList.Values)
        {
            cell.DisableRadarPreview();
        }
        _radarTween = null;
        if (puzzleTileTable != null)
        {
            foreach (var bowlView in puzzleTileTable.GridList)
            {
                // 只有当这个字本身没有被填入棋盘消耗掉 (即 locked 为 false) 时，才给它恢复解锁
                if (bowlView != null && !bowlView.locked) 
                {
                    bowlView.Unlock(); 
                }
            }
            ChessBowlGrid.IsTutorialBlocking = false;
        }
    }

    #region 金币收集
    // 用于记录下一个金币允许播放到达动画和音效的绝对时间
    private float _nextCoinArriveTime = 0f;
    // 仅用于记录【视觉上】已经飞进收集栏的金币，纯粹为了让 UI 动画不穿帮
    private int _visualEarnedGold = 0;
    // 保证每局只结算一次的开关
    private bool _isGoldSettled = false;
    /// <summary>
    /// 金币收集栏：从屏幕外华丽滑入并贴靠左边缘
    /// 建议在 SetupGrid 协程的最后，或者确认是金币关卡后调用
    /// </summary>
    public void SlideInColdTableAnim()
    {
        // 只有当节点激活，且确实需要显示时才播动画
        if (ColdTable != null && ColdTable.activeSelf)
        {
            RectTransform rt = ColdTable.GetComponent<RectTransform>();
            CanvasGroup cg = ColdTable.GetComponent<CanvasGroup>();

            // 使用 OutBack 带有物理回弹感，1.1f 是回弹强度，非常贴合 Vita 的高级手感
            rt.DOAnchorPosX(_coldTableTargetPos.x, 0.6f).SetEase(Ease.OutBack, 1.1f);
        
            // 同时做一个淡入，让视觉更柔和
            cg.DOFade(1f, 0.4f).SetEase(Ease.OutQuad);
        }
    }
    
    /// <summary>
    /// 金币收集栏：游戏结束或过关时，干脆利落地滑出屏幕外
    /// </summary>
    public void SlideOutColdTableAnim()
    {
        if (ColdTable != null && ColdTable.activeSelf)
        {
            RectTransform rt = ColdTable.GetComponent<RectTransform>();
            CanvasGroup cg = ColdTable.GetComponent<CanvasGroup>();

            // 退场使用 InCubic，有种加速收回的干脆感
            rt.DOAnchorPosX(_coldTableOffScreenX, 0.15f).SetEase(Ease.InCubic);
            cg.DOFade(0f, 0.15f);
        }
    }
    /// <summary>
    /// 触发局内金币滚动增加动画
    /// 配合飞入特效，在特效抵达终点时调用
    /// </summary>
    /// <param name="addAmount">本次飞入/收集的金币数量</param>
    public void AddInLevelGoldWithAnim(int addAmount)
    {
        float currentTime = Time.time;
        float delay = 0f;
        
        // 如果当前时间还没到“下一次允许播放”的时间，说明前方有金币在排队结算
        if (currentTime < _nextCoinArriveTime)
        {
            delay = _nextCoinArriveTime - currentTime;
            _nextCoinArriveTime += 0.05f; // 设定每个金币的连续结算间隔为 0.15 秒
        }
        else
        {
            // 如果队列是空的，立刻结算，并把下一次坑位推迟 0.15 秒
            _nextCoinArriveTime = currentTime + 0.05f;
        }

        if (delay > 0)
        {
            // 把动画和音效推迟执行，形成连续的 叮-叮-叮
            DOVirtual.DelayedCall(delay, () => ExecuteGoldAddSequence(addAmount), false);
        }
        else
        {
            ExecuteGoldAddSequence(addAmount);
        }
    }
    /// <summary>
    /// 真实的结算逻辑（单次文字跳动 + 单次收集音效）
    /// </summary>
    private void ExecuteGoldAddSequence(int addAmount)
    {
        if (coldNumberText == null) return;

        int baseGold = GameDataManager.Instance.UserData.Gold;
        // 如果处于过关结算期，底层其实已经提前把这局捡到的金币存进去了（baseGold已包含这笔钱）。
        // 此时必须把这笔钱抠出来当基数，否则 UI 就会在已存入的金额上重复再加一次！
        if (_isGoldSettled)
        {
            baseGold -= ChessStageController.Instance.CurrStageData.CollectedGold;
        }
        
        int startValue = baseGold + _visualEarnedGold;
        _visualEarnedGold += addAmount;
        int targetValue = baseGold + _visualEarnedGold;
        // userData.UpdateGold(addAmount, false, false, "金币词获得");

        // 1. 杀掉旧动画，防止高速排队时动画乱套
        coldNumberText.DOKill();
        coldNumberText.transform.DOKill();

        // 2. 视觉冲击：文字瞬间 Q 弹放大 (缩短时长 0.35 -> 0.15，以适应排队节奏)
        coldNumberText.transform.localScale = Vector3.one;
        coldNumberText.transform.DOPunchScale(new Vector3(0.4f, 0.4f, 0f), 0.08f, 5, 1f); 

        // 3. 核心爽感：数字快速滚动
        DOVirtual.Float(startValue, targetValue, 0.15f, (val) =>
            {
                coldNumberText.text = Mathf.RoundToInt(val).ToString();
            })
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                // 兜底：动画结束时赋最终精确值
                coldNumberText.text = targetValue.ToString();
            });
        
        // 4. 播放排队到达的清脆“叮~”结算音效
        AudioManager.Instance.PlaySoundEffect("ui_coin_add_eff", 0, 1);
    }
    #endregion
    
}
