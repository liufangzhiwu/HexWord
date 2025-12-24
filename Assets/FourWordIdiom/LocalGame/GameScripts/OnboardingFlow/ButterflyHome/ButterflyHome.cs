using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spine.Unity;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;


public class ButterflyHome : UIWindow
{
    [Header("UI Elements")]
    [SerializeField] private Button backhome;
    [SerializeField] private Button helpBtn;
    [SerializeField] private Image title;
    // [SerializeField] private Image bgImage;

    [SerializeField] private Button sceneBtn;
    [SerializeField] private Button manualBtn;
    [SerializeField] private Button collectBtn;

    [Header("蝴蝶参数")] 
    // 设定标准：飞越屏幕高度需要多少秒？(数值越大，飞得越慢)
    // 比如设为 6.0，意味着从屏幕最底飞到最顶大概要6秒
    public float timeToCrossScreen = 6.0f;
    public float shortDistThreshold = 300f; // 低于此距离视为短途
    [SerializeField] private float  flyInDuration = 10f;
    [SerializeField] private float circleRadiusMin = 1.5f, circleRadiusMax = 3f;
    [SerializeField] private float circleDurationMin = 0.8f, circleDurationMax = 1.2f;
    [SerializeField] private float nextFlyMin = 4f, nextFlyMax = 8f; // 落地后多久飞下一次
    // 内部计算出的实际速度
    private float _worldFlySpeed;
    

    private List<ButterflyLandPoint> topPoints = new List<ButterflyLandPoint>();
    private List<ButterflyLandPoint> bottomPoints = new List<ButterflyLandPoint>();
    
    private SpriteAtlas butterflyParts;
    private GameObject butterflyPrefab;  // 蝴蝶预制件
    private ObjectPool butterflyPool;
    private bool firstInter = true;

    #region  生命周期 及初始化方法
    protected override void InitializeUIComponents()
    {
        backhome.AddClickAction(OnBackHomeClick);
        helpBtn.AddClickAction(OnHelpClick);
        sceneBtn.AddClickAction(OnSceneClick);
        manualBtn.AddClickAction(OnManualClick);
        collectBtn.AddClickAction(OnCollectClick);
        
    }

    protected override void Awake()
    {
        AssetBundleLoader.SharedInstance.LoadAtlas("butterfly_ui","UI_Butterflyscene");
    }

    private void Start()
    {
        if (butterflyPrefab == null)
        {
            butterflyPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("scenehudie","Scenes_hudie03");
        }
        butterflyPool = new ObjectPool(butterflyPrefab.gameObject,ObjectPool.CreatePoolContainer(transform, "hudie_pool"), 3, PoolBehaviour.GameObject);
        
        // 标题
        // title.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromBundle("chinesesimplified","ui_garden_title");
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        float worldScreenHeight = 10.0f; // 默认保底值
        if (rootCanvas != null)
        {
            RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
            worldScreenHeight = canvasRect.rect.height * rootCanvas.transform.lossyScale.y;
        }
        // 速度 = 距离 / 时间
        _worldFlySpeed = worldScreenHeight / timeToCrossScreen;
        Debug.Log($"屏幕高度: {worldScreenHeight}, 计算出飞行速度: {_worldFlySpeed} 像素/秒");
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Debug.Log("【Step 0】开始执行测试代码...");

// 1. 尝试加载
        var atlas = AssetBundleLoader.SharedInstance.LoadAtlas("butterfly_ui", "UI_Butterflyparts");

        if (atlas == null)
        {
            Debug.LogError("【Step 1 失败】LoadAtlas 返回了 null！原因：AB包没加载，或名字写错了。");
            return; // 中断执行
        }
        Debug.Log($"【Step 1 成功】获取到图集对象: {atlas.name}, InstanceID: {atlas.GetInstanceID()}");

// 2. 检查 Sprite Count
        Debug.Log($"【Step 2】图集记录的 Sprite 数量: {atlas.spriteCount}");
        if (atlas.spriteCount == 0)
        {
            Debug.LogError("【Step 2 警告】图集是空的！请检查 Sprite Packer Mode 设置。");
        }

// 3. 反射检查底层纹理
        var type = typeof(UnityEngine.U2D.SpriteAtlas);
        var method = type.GetMethod("GetTextures", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (method == null)
        {
            Debug.LogError("【Step 3 失败】无法获取 GetTextures 方法，Unity API 可能有变动。");
        }
        else
        {
            Debug.Log("【Step 3】成功反射获取到方法，准备调用 Invoke...");
            try
            {
                var result = method.Invoke(atlas, null);
                if (result == null)
                {
                    Debug.LogError("【Step 4 致命】Invoke 返回了 null。图集底层没有任何纹理数据！");
                }
                else
                {
                    Texture2D[] textures = (Texture2D[])result;
                    Debug.Log($"【Step 4 最终结果】底层纹理数量: {textures.Length}");
                    foreach(var tex in textures)
                    {
                        Debug.Log($"  --> 发现纹理: {tex.name} (尺寸: {tex.width}x{tex.height})");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"【Step 4 异常】调用 Invoke 时崩溃: {e.Message}");
            }
        }
        if (butterflyParts is null)
        {
           
            butterflyParts = AssetBundleLoader.SharedInstance.LoadAtlas("butterfly_ui", "UI_Butterflyparts");
            Debug.Log("蝴蝶身体图集加载成功" + butterflyParts);
            
            Debug.LogWarning("图集内数量: " + butterflyParts.spriteCount);
            Sprite[] sprites = new Sprite[butterflyParts.spriteCount];
            
            butterflyParts.GetSprites(sprites);   // 零 GC、零反射
            
            // ② 打印名称
            foreach (var sp in sprites)
                Debug.Log($"Atlas Sprite: {sp.name}");
        }
        EventDispatcher.instance.OnButterflyGardenChange += ChangeGardenNotify;
        // 先展示ui
        UpdateUI();
        StopAllCoroutines();
        StartCoroutine(BackgroundInit());
        SetButtonsState();
    }

    private void ChangeGardenNotify()
    {
       StopAllCoroutines();
       StartCoroutine( BackgroundInit());
    }
    
    /// <summary>
    /// 场景ui更新
    /// </summary>
    private void UpdateUI()
    {
        // 判断是否可以合成蝴蝶
        if(ButterfliesManager.Instance.CanMakeButterfly())
            collectBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ButterflyUI01", "hudie");
        else
            collectBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ButterflyUI02", "hudie");
        
        // 进度条
        ButterfliesManager.Instance.ShowButterflyProcess(title.transform.GetChild(0).transform , firstInter);
        firstInter = false;
    }

    private void SetButtonsState(bool state = true)
    {
        backhome.interactable = state;
        helpBtn.interactable = state;
        sceneBtn.interactable = state;
        manualBtn.interactable = state;
        collectBtn.interactable = state;
    }
    /// <summary>
    /// 背景及蝴蝶飞行初始化
    /// </summary>
    private IEnumerator BackgroundInit()
    {
        // 背景先删除再创建
        Destroy(transform.GetChild(0).gameObject);
      
        GameObject go = AssetBundleLoader.SharedInstance.LoadGameObject("butterflybg", "ButterflyBg"+ GameDataManager.Instance.ButterflyData.currGarden);
        if(go == null)
            yield break;
        
        GameObject bg = Instantiate(go, transform);
        bg.transform.SetSiblingIndex(0);
        topPoints = bg.transform.Find("top")?.GetComponentsInChildren<ButterflyLandPoint>().ToList();
        bottomPoints = bg.transform.Find("bottom")?.GetComponentsInChildren<ButterflyLandPoint>().ToList();
        // allPoints = bg.GetComponentsInChildren<ButterflyLandPoint>(includeInactive: true).ToList();
        yield return new WaitUntil(() => butterflyPool != null);
        butterflyPool.ReturnAllObjectsToPool();
        // bgImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("scenery"+ GameDataManager.Instance.ButterflyData.currGarden, "OnboardingFlow");
        // 再飞蝴蝶
        yield return FlaySelectButterfly();
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        butterflyPool.ReturnAllObjectsToPool();
        topPoints.Clear();
        bottomPoints.Clear();
        EventDispatcher.instance.OnButterflyGardenChange -= ChangeGardenNotify;
    }
    #endregion
    
    #region 按钮事件
    private void OnBackHomeClick()
    {
        SystemManager.Instance.HidePanel(PanelType.ButterflyHome);
        SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
    }

    private void OnHelpClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.ButterflyGardenHelp);
    }

    private void OnSceneClick()
    {
        GameObject GO = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem","ButterflyGarden");
        GameObject scene =  Instantiate(GO, transform.parent);
    }

    private void OnManualClick()
    {
        SystemManager.Instance.HidePanel(PanelType.ButterflyHome);
        SystemManager.Instance.ShowPanel(PanelType.ButterflyManual);
    }

    /// <summary>
    /// 收集蝴蝶按钮
    /// </summary>
    private void OnCollectClick()
    {
        if (ButterfliesManager.Instance.CanMakeButterfly())
        {
            SetButtonsState(false);
            // 🔑 启动主流程协程，处理后续的时序和场景切换
            StartCoroutine(ProcessCollectFlow());
        }
        else
        {
            SystemManager.Instance.HidePanel(PanelType.ButterflyHome);
            SystemManager.Instance.GetPanel(PanelType.PrimaryInterface)?.GetComponent<PrimaryInterface>()?.OnEnterStageClick();
        }
    }

    /// <summary>
    /// 蝴蝶解锁流程
    /// </summary>
    /// <returns></returns>
    private IEnumerator ProcessCollectFlow()
    {
        int nextGardenId = -1;
        ButterflyInfo unlockedButterfly = null;
        // 🔑 1. 调用 UnlockButterfly 签名
        bool allover = ButterfliesManager.Instance.UnlockButterfly(out nextGardenId,
            info => { unlockedButterfly = info; });
        if(unlockedButterfly != null)
            UpdateUI();
        
        // 🔑 2. 将播放特效和飞入场景的逻辑放入协程中处理时序
        if(unlockedButterfly != null)
            yield return CollectAndFly(unlockedButterfly, transform.Find("FlyStartPoint"));
        
        if (allover)
        {
            // 🔑 3. 切换场景：必须等待特效和所有逻辑完成后再进行
            if (nextGardenId is not -1)
            {
                MessageSystem.Instance.ShowTip("当前蝶园已收集完, 即将解锁下一个场景!");
                GameObject GO = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem","ButterflyGarden");
                GameObject scene =  Instantiate(GO, transform.parent);
                ButterflyGarden garden = scene.GetComponent<ButterflyGarden>();
                yield return new WaitForSeconds(1.5f);
                garden.UnlockGarden(nextGardenId);
            }
            else
            {
                MessageSystem.Instance.ShowTip("所有场景已解锁, 新场景正在制作中");
            }
        }

        yield return null;
        SetButtonsState(true);
    }

    private IEnumerator CollectAndFly(ButterflyInfo butterflyInfo, Transform startPoint)
    {
        // 播放提示信息 (通常是同步的，但我们可以假设特效播放需要时间)
        // MessageSystem.Instance.ShowTip("获得 " + MultilingualManager.Instance.GetString(butterflyInfo.Name,"hudie"));
        GameObject gradePab = AssetBundleLoader.SharedInstance.LoadGameObject("scenehudie","UI_hudie0" + butterflyInfo.Rarity);
        GameObject gradeGo = Instantiate(gradePab, transform.parent);
        gradeGo.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString(butterflyInfo.Name,"hudie");
        SpriteAtlas atlas = AssetBundleLoader.SharedInstance.LoadAtlas("butterfly_ui","UI_Butterflymaunal");
        gradeGo.GetComponentInChildren<Image>(true).sprite = atlas.GetSprite(butterflyInfo.ButterflyIcon);
        gradeGo.GetComponentInChildren<Canvas>(true).sortingLayerName = "BaseEffect";
        
        // 🔑 模拟特效播放时间，等待特效结束 (假设 1.5 秒)
        yield return new WaitForSeconds(5f);
        Destroy(gradeGo);
        // 🔑 蝴蝶数量限制检查 (需要你在 ButterfliesManager 中实现 GetCurrentSceneButterflyCount 方法)
        const int maxButterflies = 5;
        if (startPoint.childCount > maxButterflies)
        {
            ObjectPool.ReturnObjectToPool(startPoint.GetChild(0).gameObject);
        }
        GameObject butterfly = butterflyPool.GetObject(startPoint);
        butterfly.transform.position = startPoint.position;
        yield return ReplacementSkin(butterfly, butterflyInfo);
        
        StartCoroutine(ButterflyLife(butterfly.transform));
    }
    #endregion
    
    #region 蝴蝶飞行

    private IEnumerator ReplacementSkin(GameObject butterfly, ButterflyInfo butterflyInfo)
    {
        butterfly.SetActive(false);
        SpineSpriteReplacer replacer = butterfly.GetComponent<SpineSpriteReplacer>();
        string bName = $"{butterflyInfo.ButterflyIcon}_body";
        string wName = $"{butterflyInfo.ButterflyIcon}_wing";
        Sprite body = butterflyParts.GetSprite(bName);
        Sprite wing = butterflyParts.GetSprite(wName);
        Debug.LogWarning("加载的蝴蝶块 " + bName + ":" + body +" ==== " + wName+":" + wing);
        yield return null;
        replacer.InitializeButterfly(body, wing);
        butterfly.SetActive(true);
    }
    /// <summary>
    /// 根据当前场景飞蝴蝶
    /// </summary>
    private IEnumerator FlaySelectButterfly()
    {
        // 等待 start 方法完成
        yield return new WaitForEndOfFrame();
        // 获取要飞的蝴蝶, 先取当前园子内有多少种蝴蝶, 再查询拥有的蝴蝶
        List<ButterflyInfo> currGardenButterflyInfos = ButterfliesManager.Instance.GetCurrentGardenButterflies();
        List<ButterflyInfo> flyInfos = currGardenButterflyInfos.FindAll(p=> GameDataManager.Instance.ButterflyData.butterflies.Contains(p.Id));
        
        Transform startPoint = transform.Find("FlyStartPoint");
        WaitForSeconds wait = new WaitForSeconds(0.5f);
        // 调用飞行方法
        for (int i = 0; i < flyInfos.Count; i++)
        {
            if (i > 5) break;
            if (i > topPoints.Count + bottomPoints.Count -2) break;
            GameObject butterfly = butterflyPool.GetObject(startPoint);
            butterfly.transform.position = startPoint.position;
            yield return ReplacementSkin(butterfly, flyInfos[i]);
            StartCoroutine(ButterflyLife(butterfly.transform));
            yield return wait;
        }
    }
    

    /// <summary>
    /// 让蝴蝶不间断飞
    /// </summary>
    /// <param name="butterfly"></param>
    private IEnumerator ButterflyLife(Transform butterfly)
    {
        ButterflyLandPoint currentPt = null;
        SkeletonGraphic skeletonGraphic = butterfly.GetComponent<SkeletonGraphic>();
        // 确保 Spine 混合时间，保证动作切换不卡顿
        skeletonGraphic.AnimationState.Data.DefaultMix = 0.2f;
        
        string[] anims = new[] { "idle01", "idle02", "run01" };
        // 进入场景只飞一次
        currentPt = PickVacantNotSameObject(butterfly, null);
        // 占用
        if (currentPt != null)
        {
            currentPt.Occupied = true;
            currentPt.OccupiedBy = butterfly;
            Vector3 landPos = currentPt.transform.TransformPoint(currentPt.GetComponent<RectTransform>().rect.center);
            skeletonGraphic.AnimationState.SetAnimation(0, anims[2], true);
            yield return FlyEntry(butterfly, landPos, GetTargetScale(currentPt));
            string idleAnim = anims[UnityEngine.Random.Range(0, 2)]; // 随机选 idle01 或 idle02
            skeletonGraphic.AnimationState.SetAnimation(0, idleAnim, true);
            
            yield return new WaitForSeconds(UnityEngine.Random.Range(nextFlyMin, nextFlyMax));
        }
        
        // 场景内无限飞
        while (true)
        {
            // 选空落点
            ButterflyLandPoint nextPt = PickVacantNotSameObject(butterfly, currentPt);
            if (nextPt is null)
            {
                yield return new WaitForSeconds(1.0f);
                continue;
            }
        
            // 占用
            nextPt.Occupied = true;
            nextPt.OccupiedBy = butterfly;
            if (currentPt is not null)
            {
                currentPt.Occupied = false;
                currentPt.OccupiedBy = null;
            }
            skeletonGraphic.AnimationState.SetAnimation(0, anims[2], true);
            // 飞过去
            Vector3 landPos = nextPt.transform.TransformPoint(nextPt.GetComponent<RectTransform>().rect.center);
            yield return FlyRandomArc(butterfly, landPos, GetTargetScale(nextPt));
            string animName = anims[UnityEngine.Random.Range(0,2)];
            skeletonGraphic.AnimationState.SetAnimation(0, animName, true);

            currentPt = nextPt;
            // 随机间隔再飞
            yield return new WaitForSeconds(UnityEngine.Random.Range(nextFlyMin, nextFlyMax));
        }
        yield return null;
    }
    
    /// <summary>
    /// 水平进入场景: 弧形飞到落点，落点调整为正45至负45之间随机
    /// </summary>
    private IEnumerator FlyEntry(Transform bf, Vector3 landPoint, Vector3 targetScale)
    {
        Vector3 p0 = bf.position;
        Vector3 p2 = landPoint;
        Vector3 startScale = bf.localScale;
        
        float currentDuration = GetFlexibleDuration(p0, p2);
        
        Vector3 totalVec = p2 - p0;
        Vector3 arcDir = new Vector3(-totalVec.y, totalVec.x, 0);
        float controlHeight = -.5f;
        Vector3 p1 = p0 + totalVec * 0.5f + arcDir * controlHeight;
        // p1 += arcDir * controlHeight;

        Vector3 lastPos = p0;
        
        float flyDuration = currentDuration - 0.5f; 
        if (flyDuration < 0.5f) flyDuration = currentDuration * 1.8f; // 保护一下防止时间太短
        float timer = 0;
        while (timer < flyDuration) // 🔑 5 秒飞行时间
        {
            timer += Time.deltaTime;
            float t = timer / flyDuration; // 0→1
            float easeT = Mathf.Sin(t * Mathf.PI * 0.5f); // 0→1 先慢后快
            // --- A. 计算贝塞尔曲线位置 B(t) ---
            // B(t) = (1-t)^2 * P0 + 2*(1-t)*t * P1 + t^2 * P2
            float u = 1f - easeT;
            float u2 = u * u;
            float t2 = easeT * easeT;
        
            Vector3 pos = u2 * p0;
            pos += 2f * u * easeT * p1;
            pos += t2 * p2;
            pos.z = 0;
            
            bf.position = pos;
            
            // 目标方向向量 (落点 - 当前位置)
            Vector3 lookDir = pos - lastPos;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                // 0度=水平朝向，所以无需修正
                float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
                Quaternion targetRot = Quaternion.Euler(0, 0, angle - 90f);
                // 使用 Slerp 平滑地跟随目标角度，消除抖动和生硬感
                bf.rotation = Quaternion.Slerp(bf.rotation, targetRot, Time.deltaTime * 20f);
            }

            bf.localScale = Vector3.Lerp(startScale, targetScale, easeT * 0.9f);
            lastPos = pos;
            yield return null;
        }
        timer = 0;
        float landDuration = 0.5f;
        Vector3 preLandPos = bf.position;
        Quaternion preLandRot = bf.rotation;
        Vector3 preLandScale = bf.localScale; // 记录落地前的缩放
        // 🔥 随机倾斜角度：0度是正上方，-30到30度之间随机
        float finalAngleZ = UnityEngine.Random.Range(-30f, 30f); 
        Quaternion finalRot = Quaternion.Euler(0, 0, finalAngleZ);
        while (timer < landDuration)
        {
            timer += Time.deltaTime;
            float t = timer / landDuration;
            float smoothT = Mathf.SmoothStep(0, 1, t);

            // 位置归位
            bf.position = Vector3.Lerp(preLandPos, landPoint, smoothT);
            // 旋转归位 (慢慢转成头朝上、微倾斜)
            bf.rotation = Quaternion.Lerp(preLandRot, finalRot, smoothT);
            // 缩放归位 (补齐最后一点缩放)
            bf.localScale = Vector3.Lerp(preLandScale, targetScale, smoothT);

            yield return null;
        }
        // 强制位置归位
        // 强制对齐
        bf.position = landPoint;
        bf.rotation = finalRot;
        bf.localScale = targetScale;
    }
    
    /// <summary>
    /// 蝴蝶左右摇摆的飞：场景循环
    /// </summary>
    private IEnumerator FlyRandomArc(Transform bf, Vector3 landPoint, Vector3 targetScale)
    {
        // --- 1. 初始化参数 ---
        float swayFrequency = 2.0f; // 摇摆频率（数值越大摆动越快）
        float swayAmplitude = .5f; // 摇摆幅度（数值越大摆动越宽）
        
        // 🔑 左右摇摆：先慢→后快→先慢
        Vector3 startPos = bf.position;
        // 记录起始缩放
        Vector3 startScale = bf.localScale;
        
        // 🔥 计算动态时间
        float currentDuration = GetFlexibleDuration(startPos, landPoint);
        // 计算从起点到终点的总向量
        Vector3 totalVector = landPoint - startPos;
        // 计算垂直于飞行方向的“右”向量，用于施加摇摆偏移 (假设在XY平面飞行)
        Vector3 rightDir = new Vector3(-totalVector.y, totalVector.x, 0).normalized;
        
        float timer = 0;
        while (timer < currentDuration)
        {
            timer += Time.deltaTime;
            float t = timer / currentDuration; // 0→1
            float easeT = Mathf.SmoothStep(0f, 1f, t);
            
            Vector3 basePos = Vector3.Lerp(startPos, landPoint, easeT);
            float swayOffset = Mathf.Sin(t * Mathf.PI * swayFrequency) * swayAmplitude * (1f - easeT);
            Vector3 nextPos = basePos + (rightDir * swayOffset);
            
            Vector3 moveDir = nextPos - bf.position;
            if (moveDir.sqrMagnitude > 0.001f)
            {
                float angleZ = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
                bf.rotation = Quaternion.Euler(0, 0, angleZ - 90f);
            }
            bf.position = nextPos;
            bf.localScale = Vector3.Lerp(startScale, targetScale, easeT);
            yield return null;
        }

        // z轴在45到-135之间 精准落地
        timer = 0;
        float landDuration = 0.5f;
        Quaternion startRot = bf.rotation;
        float finalAngleZ = UnityEngine.Random.Range( -45f, 45f);
        Quaternion targetRot = Quaternion.Euler(0,0,finalAngleZ);
        while (timer < landDuration)
        {
            timer += Time.deltaTime;
            float t = timer / landDuration;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            
            bf.rotation = Quaternion.Lerp(startRot, targetRot, smoothT);
            bf.position = Vector3.Lerp(bf.position, landPoint, smoothT);
            bf.localScale = Vector3.Lerp(bf.localScale, targetScale, smoothT);
            yield return null;
        }
        bf.position = landPoint;
        bf.rotation = targetRot;
        bf.localScale = targetScale;
    }
    
    /// <summary>
    /// 计算本次飞行的动态时长
    /// 逻辑：远距离用原时间，近距离时间缩短至90%
    /// </summary>
    private float GetFlexibleDuration(Vector3 start, Vector3 end)
    {
        float dist = Vector3.Distance(start, end);
        
      
        if (dist <= 0.001f) return 0.65f;

        float duration = dist / _worldFlySpeed;
        
        if (dist < shortDistThreshold)
        {
            duration *= 0.9f;
        }
        
        float randomFactor = UnityEngine.Random.Range(1.0f, 1.2f);
        duration *= randomFactor;
        // 在 0.9倍时间 和 1.0倍时间 之间插值
        // 距离越近，时间越趋向于 90%；距离越远，时间趋向于 100%
        return Mathf.Max(duration, 0.8f);
    }
    /// <summary>
    /// 选空落点，且不与当前占用物体相同 此处先判断当前蝴蝶身上有没有落点，有落点就选对面的落点，无落点就先选top的落点
    /// </summary>
    private ButterflyLandPoint PickVacantNotSameObject(Transform butterfly, ButterflyLandPoint currentPt)
    {
        List<ButterflyLandPoint> candidates = new List<ButterflyLandPoint>();
        
        // 判断当前场景是否有 Bottom 落点
        bool hasBottom = bottomPoints?.Count > 0;
        if (currentPt is null)
        {
            candidates = GetVacantPoints(topPoints, butterfly);
        }else if (!hasBottom)
        {
            candidates = GetVacantPoints(topPoints, butterfly);
        }
        else
        {
            if (currentPt.area is LandArea.TOP)
            {
                candidates = GetVacantPoints(bottomPoints, butterfly);
                if (candidates.Count is 0)
                {
                    candidates = GetVacantPoints(topPoints, butterfly);
                }
            }
            else if (currentPt.area is LandArea.BOTTOM)
            {
                candidates = GetVacantPoints(topPoints, butterfly);
                if (candidates.Count is 0)
                {
                    candidates = GetVacantPoints(bottomPoints, butterfly);
                }
            }
        }

        if (candidates.Count > 0)
        {
            if (currentPt is not null)
            {
                candidates.Remove(currentPt);
            }
            if(candidates.Count > 0)
                return candidates[UnityEngine.Random.Range(0,candidates.Count)];
        }
       
        return null;
    }
    // 辅助方法：获取空闲点
    private List<ButterflyLandPoint> GetVacantPoints(List<ButterflyLandPoint> list, Transform butterfly)
    {
        return list.FindAll(p => !p.Occupied && p.OccupiedBy != butterfly);
    }
    // 辅助方法：判断列表是否已满 (没有空位)
    private bool IsListFull(List<ButterflyLandPoint> list)
    {
        if (list == null || list.Count == 0) return true;
        // 如果找不到任何一个 !Occupied 的点，那就是满了
        return !list.Exists(p => !p.Occupied);
    }
    private readonly Vector3 SCALE_TOP = Vector3.one * 0.75f;    // 远处/上方：小
    private readonly Vector3 SCALE_BOTTOM = Vector3.one * 1.1f; // 近处/下方：大
    private Vector3 GetTargetScale(ButterflyLandPoint pt)
    {
        bool isFlatScene = (bottomPoints == null || bottomPoints.Count == 0);
        // 1. 如果没有 Bottom 落点，全部正常缩放 (1.0)
        if (isFlatScene) return Vector3.one;

        // 2. 如果有 Bottom 落点，启用透视效果 (Top变小，Bottom变大)
        return (pt.area == LandArea.TOP) ? SCALE_TOP : SCALE_BOTTOM;
    }
    #endregion
}
