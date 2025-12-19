using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Middleware;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;
using Sequence = DG.Tweening.Sequence;

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
    [SerializeField] private float  flyInDuration = 10f;
    [SerializeField] private float circleRadiusMin = 1.5f, circleRadiusMax = 3f;
    [SerializeField] private float circleDurationMin = 0.8f, circleDurationMax = 1.2f;
    [SerializeField] private float nextFlyMin = 4f, nextFlyMax = 8f; // 落地后多久飞下一次
    
    private List<ButterflyLandPoint> allPoints = new List<ButterflyLandPoint>();
    private List<ButterflyLandPoint> vacantPoints => allPoints.FindAll(p=>!p.Occupied);

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

    private void Start()
    {
        if (butterflyPrefab == null)
        {
            butterflyPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("scenehudie","Scenes_hudie01");
        }
        butterflyPool = new ObjectPool(butterflyPrefab.gameObject,ObjectPool.CreatePoolContainer(transform, "hudie_pool"), 3, PoolBehaviour.GameObject);
        
        // 标题
        // title.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromBundle("chinesesimplified","ui_garden_title");
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EventDispatcher.instance.OnButterflyGardenChange += ChangeGardenNotify;
        // 先展示ui
        UpdateUI();
        StopAllCoroutines();
        StartCoroutine(BackgroundInit());
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
            collectBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("合成");
        else
            collectBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("收集");
        
        // 进度条
        ButterfliesManager.Instance.ShowButterflyProcess(title.transform.GetChild(0).transform , firstInter);
        firstInter = false;
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
        allPoints = bg.GetComponentsInChildren<ButterflyLandPoint>(includeInactive: true).ToList();
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
        allPoints.Clear();
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
    }

    private IEnumerator CollectAndFly(ButterflyInfo butterflyInfo, Transform startPoint)
    {
        // 播放提示信息 (通常是同步的，但我们可以假设特效播放需要时间)
        // MessageSystem.Instance.ShowTip("获得 " + MultilingualManager.Instance.GetString(butterflyInfo.Name,"hudie"));
        GameObject gradePab = AssetBundleLoader.SharedInstance.LoadGameObject("scenehudie","UI_hudie0" + butterflyInfo.Rarity);
        GameObject gradeGo = Instantiate(gradePab, transform.parent);
        gradeGo.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString(butterflyInfo.Name,"hudie");
        gradeGo.GetComponentInChildren<Image>(true).sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas(butterflyInfo.ButterflyIcon);
        gradeGo.GetComponentInChildren<Canvas>(true).sortingLayerName = "BaseEffect";
        // 🔑 模拟特效播放时间，等待特效结束 (假设 1.5 秒)
        yield return new WaitForSeconds(5f);
        Destroy(gradeGo);
        // 🔑 蝴蝶数量限制检查 (需要你在 ButterfliesManager 中实现 GetCurrentSceneButterflyCount 方法)
        const int MAX_BUTTERFLIES = 5;
        if (startPoint.childCount > MAX_BUTTERFLIES)
        {
            ObjectPool.ReturnObjectToPool(startPoint.GetChild(0).gameObject);
        }
        GameObject butterfly = butterflyPool.GetObject(startPoint);
        butterfly.transform.position = startPoint.position;
        StartCoroutine(ButterflyLife(butterfly.transform));
    }
    #endregion

    #region 蝴蝶飞行
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
            if (i < allPoints.Count - 1)
            {
                if(i > 5) break;
                
                GameObject butterfly = butterflyPool.GetObject(startPoint);
                butterfly.transform.position = startPoint.position;
                StartCoroutine(ButterflyLife(butterfly.transform));
                yield return wait;
            }
        }
    }
    
    /// <summary>
    /// 让蝴蝶不间断飞
    /// </summary>
    /// <param name="butterfly"></param>
    private IEnumerator ButterflyLife(Transform butterfly)
    {
        // 进入场景只飞一次
        ButterflyLandPoint currentPt = PickVacantNotSameObject(butterfly);
        // 占用
        if (currentPt != null)
        {
            currentPt.Occupied = true;
            currentPt.OccupiedBy = butterfly;
            Vector3 land = currentPt.transform.TransformPoint(currentPt.GetComponent<RectTransform>().rect.center);
            yield return FlyEntry(butterfly, land);
            yield return new WaitForSeconds(UnityEngine.Random.Range(nextFlyMin, nextFlyMax));
        }
        SkeletonGraphic skeletonGraphic = butterfly.GetComponent<SkeletonGraphic>();
        string[] anims = new[] { "idle01", "idle02", "run01" };
        // 场景内无限飞
        while (true)
        {
            // 选空落点
            ButterflyLandPoint nextPt = PickVacantNotSameObject(butterfly);
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
            skeletonGraphic.AnimationState.SetAnimation(0, anims[2], false);
            // 飞过去
            Vector3 land = nextPt.transform.TransformPoint(nextPt.GetComponent<RectTransform>().rect.center);
            yield return FlyRandomArc(butterfly, land);
            string anim = anims[UnityEngine.Random.Range(0,2)];
            skeletonGraphic.AnimationState.SetAnimation(0, anim, false);
            currentPt = nextPt;
            // 随机间隔再飞
            yield return new WaitForSeconds(UnityEngine.Random.Range(nextFlyMin, nextFlyMax));
        }
        yield return null;
    }
    /// <summary>
    /// 选空落点，且不与当前占用物体相同
    /// </summary>
    private ButterflyLandPoint PickVacantNotSameObject(Transform butterfly)
    {
        var candidates = vacantPoints.FindAll(p => !p.Occupied && p.OwnerObject != butterfly.parent);
        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// 水平进入场景: 弧形飞到落点，z轴设在135
    /// </summary>
    private IEnumerator FlyEntry(Transform bf, Vector3 landPoint)
    {
        Vector3 p0 = bf.position;
        Vector3 p2 = landPoint;
        float currentDuration = GetFlexibleDuration(p0, p2);
        Vector3 totalVec = p2 - p0;
        Vector3 arcDir = new Vector3(-totalVec.y, totalVec.x, 0);

        float controlHeight = -.5f;
        Vector3 p1 = p0 + totalVec * 0.5f;
        p1 += arcDir * controlHeight;
        
        float timer = 0;
        while (timer < currentDuration) // 🔑 5 秒飞行时间
        {
            timer += Time.deltaTime;
            float t = timer / currentDuration; // 0→1
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
            Vector3 lookDir = p2 - bf.position;
            // 0度=水平朝向，所以无需修正
            float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
        
            Quaternion targetRot = Quaternion.Euler(0, 0, angle);
            // 使用 Slerp 平滑地跟随目标角度，消除抖动和生硬感
            bf.rotation = Quaternion.Slerp(bf.rotation, targetRot, Time.deltaTime * 10f);
            yield return null;
        }
        
        // 强制位置归位
        bf.position = p2;
    }
    /// <summary>
    /// 蝴蝶左右摇摆的飞：场景循环
    /// </summary>
    private IEnumerator FlyRandomArc(Transform bf, Vector3 landPoint)
    {
        // --- 1. 初始化参数 ---
        float swayFrequency = 2.0f; // 摇摆频率（数值越大摆动越快）
        float swayAmplitude = .5f; // 摇摆幅度（数值越大摆动越宽）
        
        // 🔑 左右摇摆：先慢→后快→先慢
        Vector3 startPos = bf.position;
        // 🔥 计算动态时间
        float currentDuration = GetFlexibleDuration(startPos, landPoint);
        // 计算从起点到终点的总向量
        Vector3 totalVector = landPoint - startPos;
        // 计算垂直于飞行方向的“右”向量，用于施加摇摆偏移 (假设在XY平面飞行)
        // 如果是3D空间随意飞，可以用 Vector3.Cross(totalVector, Vector3.up).normalized
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
            yield return null;
        }

        // z轴在45到-135之间 精准落地
        timer = 0;
        float landDuration = 0.5f;
        Quaternion startRot = bf.rotation;
        float finalAngleZ = UnityEngine.Random.Range(45f,135f);
        Quaternion targetRot = Quaternion.Euler(0,0,finalAngleZ);
        while (timer < landDuration)
        {
            timer += Time.deltaTime;
            float t = timer / landDuration;
            bf.rotation = Quaternion.Lerp(startRot, targetRot, t);
            bf.position = Vector3.Lerp(bf.position, landPoint, t);
            yield return null;
        }
        bf.position = landPoint;
        bf.rotation = targetRot;
    }
    public float shortDistThreshold = 300f;
    /// <summary>
    /// 计算本次飞行的动态时长
    /// 逻辑：远距离用原时间，近距离时间缩短至90%
    /// </summary>
    private float GetFlexibleDuration(Vector3 start, Vector3 end)
    {
        float dist = Vector3.Distance(start, end);
    
        // 计算比例因子：
        // 当距离 >= shortDistThreshold 时，factor = 1
        // 当距离 = 0 时，factor = 0
        float factor = Mathf.Clamp01(dist / shortDistThreshold);
    
        // 在 0.9倍时间 和 1.0倍时间 之间插值
        // 距离越近，时间越趋向于 90%；距离越远，时间趋向于 100%
        return Mathf.Lerp(flyInDuration * 0.9f, flyInDuration, factor);
    }
    #endregion
}
