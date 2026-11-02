using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Random = UnityEngine.Random;


public class CustomFlyInManager : MonoBehaviour
{
    public static CustomFlyInManager  Instance;
    public Transform AwardRoot;
    [HideInInspector] public GameObject GoldObj;
    [HideInInspector] public GameObject ShopGoldObj;
    [HideInInspector] public GameObject ShopTipObj;
    [HideInInspector] public GameObject ShopAutoObj;
    [HideInInspector] public GameObject ShopButterflyObj;
    [HideInInspector] public GameObject ShopRestartObj;
    [HideInInspector] public GameObject GoldPrefab;
    [HideInInspector] public GameObject finishlevelBtnObj;
    private float BizerValue = 3.0f;

    [Header("奖励飞入曲线（贝塞尔）")]
    [Tooltip("弯曲程度：0=直线，越大越弯。按飞行距离等比计算，不会因屏幕尺寸失真")]
    [SerializeField] private float BezierBendRatio = 0.10f;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        GoldPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "GameGole");
    }
    
    /// <summary>
    /// 自定义起终点的金币飞行方法 (用于关卡内结算等特殊需求)
    /// </summary>
    /// <param name="start">起飞点</param>
    /// <param name="target">终点</param>
    public void FlyInGoldToTarget(Transform start, Transform target, Action call = null, int count = 5)
    {
        Vector3 scale = start.localScale;
        bool isaudio = true;
        if (count >= 5) scale = new Vector3(0.85f, 0.85f, 0.85f);
        if (count == 1)
        {
            scale = new Vector3(0.65f, 0.65f, 0.65f);
            isaudio = false;
        }
        
        // 开启一个传入了自定义 target 的协程
        StartCoroutine(FlyInValueGoldWithTarget(start, target, count, scale, call, isaudio));
    }

    private IEnumerator FlyInValueGoldWithTarget(Transform start, Transform target, int count, Vector3 scale, Action call, bool isaudio)
    {
        for (int i = 0; i < count; i++)
        {
            float s = 0.55f - i * 0.01f;
            yield return new WaitForSeconds(0.085f);
            if (i < 4 && isaudio)
                AudioManager.Instance.PlaySoundEffect("filyGold");
            
            // 🌟 重点：这里把 target 传给了底层的飞行逻辑，而不是写死的 GoldObj
            StartCoroutine(FlyInGoldCoroutine(start, target, GoldPrefab, true, null, scale, s));
        }
        yield return new WaitForSeconds(0.35f);
        call?.Invoke();
    }
    
    public void FlyInGold(Transform start,Action call=null,int count=5)
    {
        Vector3 scale = start.localScale;
        bool isaudio=true;
        if(count>=5) scale=new Vector3(0.85f,0.85f,0.85f);
        if (count == 1)
        {
            scale=new Vector3(0.65f,0.65f,0.65f);
            isaudio = false;
        }
        //BizerValue = Random.Range(1.3f, 4.5f);
        StartCoroutine(FlyInValueGold(start,count,scale,call,isaudio));
    }

    IEnumerator FlyInValueGold(Transform start,int count,Vector3 scale,Action call,bool isaudio)
    {
        for (int i = 0; i < count; i++)
        {
            float s = 0.55f - i * 0.01f;
            yield return new WaitForSeconds(0.085f);
            if (i<4&&isaudio)
                AudioManager.Instance.PlaySoundEffect("filyGold");
            StartCoroutine(FlyInGoldCoroutine(start,GoldObj.transform,GoldPrefab,true,null,scale,s));
        }
        yield return new WaitForSeconds(0.35f);
        call?.Invoke();
    }

    public void FlyIn(Transform start,Transform target,GameObject effect,Action call,float duration=0f)
    {
        StartCoroutine(FlyInCoroutine(start,target,effect,call,duration));
    }
    
    public void FlyAwardInRight(Vector3 start,GameObject effect,Action call)
    {
        GameObject effecttemp=Instantiate(effect,start,Quaternion.identity,SystemManager.Instance._uiRoot);
        effecttemp.GetComponentInChildren<Text>().transform.gameObject.SetActive(false);
        effecttemp.transform.localScale = new Vector3(0.6f,0.6f,0.6f);
        //Vector3 endPos = new Vector3(finishlevelBtnObj.transform.position.x+1,finishlevelBtnObj.transform.position.y,finishlevelBtnObj.transform.position.z);
        StartCoroutine(FlyAwardVectorToEndLevelBtn(start, finishlevelBtnObj, effecttemp, Vector3.right, () =>
        {
            Destroy(effecttemp.gameObject);
            call?.Invoke();
        }));
    }
    
    public void FlyAwardInLeft(Vector3 start,GameObject effect,Action call)
    {
        GameObject effecttemp=Instantiate(effect,start,Quaternion.identity,SystemManager.Instance._uiRoot);
        effecttemp.GetComponentInChildren<Text>().transform.gameObject.SetActive(false);
        effecttemp.transform.localScale = new Vector3(0.6f,0.6f,0.6f);
        //Vector3 endPos = new Vector3(finishlevelBtnObj.transform.position.x-1,finishlevelBtnObj.transform.position.y,finishlevelBtnObj.transform.position.z);
        StartCoroutine(FlyAwardVectorToEndLevelBtn(start, finishlevelBtnObj, effecttemp, Vector3.left, () =>
        {
            Destroy(effecttemp.gameObject);
            call?.Invoke();
        }));
    }
    
    public void FlyAwardIn(Vector3 start,GameObject endGameObject,GameObject effect,Action call)
    {
        GameObject effecttemp = Instantiate(effect, AwardRoot);

        RectTransform rect = effecttemp.GetComponent<RectTransform>();

        // 1. 固定锚点和轴心，让 anchoredPosition = (0,0) 表示居中
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        // 2. 坐标转换：将世界坐标 start 转为相对于 parent 的 UI 坐标
        Vector2 screenPoint = Camera.main.WorldToScreenPoint(start);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            SystemManager.Instance._uiRoot as RectTransform, 
            screenPoint, 
            Camera.main, 
            out Vector2 localPos
        );

        // 3. ✅ 用 anchoredPosition 设置正确位置（不再使用 transform.position）
        rect.anchoredPosition = localPos;

        // 4. 缩放
        effecttemp.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
       
        StartCoroutine(FlyAwardVectorToEnd(start, endGameObject, effecttemp, Vector3.left, () =>
        {
             Destroy(effecttemp.gameObject);
             call?.Invoke();
        },0.55f));
    }
    
    private IEnumerator FlyAwardVectorToEnd(Vector3 start, GameObject endGameObject, GameObject gold, Vector3 left, Action call, float duration = 0.8f)
    {
        Vector3 endPos = endGameObject.transform.position;

        // ---- 1. 准备透明度控制组件（适用于 UI） ----
        CanvasGroup cg = gold.GetComponent<CanvasGroup>();
        if (cg == null) cg = gold.AddComponent<CanvasGroup>();
        cg.alpha = 0f; // 初始透明

        // ---- 2. 主序列（三次贝塞尔曲线飞入 + 金色星光拖尾，无起手上浮） ----
        Sequence flySeq = DOTween.Sequence();

        // 2.1 主体：三次贝塞尔曲线飞入
        //     ★ 不走 DOTween 的 PathType.CubicBezier（实验性，waypoint 需按 终点/IN/OUT 每 3 个一组、起点自动补），
        //       这里直接按贝塞尔公式逐帧求值，曲线精确、无格式限制。
        //     控制点 = 起终连线 1/3、2/3 处 + 垂直偏移；偏移按飞行距离等比，避免分辨率不同导致过弯或看不出弧度。
        Vector3 p0 = start;
        Vector3 p3 = endPos;
        Vector3 chord = p3 - p0;
        float chordLen = chord.magnitude;

        Vector3 bendOffset = Vector3.zero;
        if (chordLen > 0.001f)
        {
            Vector3 perp = new Vector3(-chord.y, chord.x, 0f) / chordLen; // 屏幕平面内与飞行方向垂直
            if (perp.y < 0f) perp = -perp;                                 // 统一取朝上的一侧
            bendOffset = perp * (chordLen * BezierBendRatio);
        }

        Vector3 p1 = p0 + chord * (1f / 3f) + bendOffset;
        Vector3 p2 = p0 + chord * (2f / 3f) + bendOffset;

        Transform goldTf = gold.transform;
        float bezierT = 0f;
        flySeq.Append(DOTween.To(
                () => bezierT,
                v =>
                {
                    bezierT = v;
                    goldTf.position = EvaluateCubicBezier(p0, p1, p2, p3, v);
                },
                1f, duration)
            .SetEase(Ease.InOutSine));

        // 2.2 起始快速淡入（与飞行并行）
        flySeq.Join(cg.DOFade(1f, 0.12f).SetEase(Ease.OutQuad));

        // 2.3 飞行中轻微缩小（与飞行并行）
        flySeq.Join(gold.transform.DOScale(new Vector3(0.45f, 0.45f, 0.45f), duration));

        // 2.4 到达后淡出（顺序）
        flySeq.Append(cg.DOFade(0f, 0.15f).SetEase(Ease.InQuad));

        // 2.5 到达时目标计数器弹跳反馈（与淡出并行）
        flySeq.Join(endGameObject.transform.DOScale(new Vector3(1.12f, 1.12f, 1.12f), 0.1f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => endGameObject.transform.DOScale(Vector3.one, 0.1f)));

        // ---- 3. 飞行期间并行生成金色星光拖尾 ----
        StartCoroutine(SparkleTrailRoutine(gold.transform, duration + 0.15f));

        yield return flySeq.WaitForCompletion();

        // 执行回调（此处会销毁 gold 等）
        call?.Invoke();
    }

    /// <summary>
    /// 三次贝塞尔曲线求值：B(t) = (1-t)³P0 + 3(1-t)²t·P1 + 3(1-t)t²·P2 + t³P3，t ∈ [0,1]。
    /// </summary>
    private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        float uu = u * u;
        float tt = t * t;
        return uu * u * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + tt * t * p3;
    }

    // ======================= ★ 金色星光拖尾（对齐参考视频飞入效果） =======================

    private static Sprite _sparkleSprite;

    /// <summary>生成一枚柔和白色圆点精灵（金色星光着色用），仅创建一次并缓存。</summary>
    private static Sprite GetSparkleSprite()
    {
        if (_sparkleSprite != null) return _sparkleSprite;
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half, dy = y - half;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / half;
                float a = Mathf.Clamp01(1f - d);
                a *= a; // 柔和边缘
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        _sparkleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _sparkleSprite;
    }

    /// <summary>沿飞行物体轨迹周期性撒出金色星光，形成拖尾。</summary>
    private IEnumerator SparkleTrailRoutine(Transform coin, float duration)
    {
        const float interval = 0.032f;
        float elapsed = 0f;
        Transform parent = AwardRoot != null ? AwardRoot : coin.parent;

        // 参考尺寸：飞行金币当前渲染高度，用于星光大小成比例
        float referenceSize = 90f;
        RectTransform coinRt = coin as RectTransform;
        if (coinRt == null) coinRt = coin.GetComponent<RectTransform>();

        while (elapsed < duration)
        {
            if (coin != null)
            {
                if (coinRt != null)
                    referenceSize = coinRt.rect.height * Mathf.Abs(coin.localScale.y);
                SpawnSparkle(parent, coin.position, referenceSize);
            }
            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>在指定世界坐标生成一枚金色星光：上飘、缩小、淡出后销毁。</summary>
    private void SpawnSparkle(Transform parent, Vector3 worldPos, float referenceSize)
    {
        GameObject go = new GameObject("goldSparkle", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        float size = Mathf.Clamp(referenceSize * Random.Range(0.16f, 0.30f), 6f, 48f);
        rt.sizeDelta = new Vector2(size, size);

        Image img = go.GetComponent<Image>();
        img.sprite = GetSparkleSprite();
        img.raycastTarget = false;
        img.color = new Color(1f, 0.86f, 0.4f, 0.95f); // 金色星光

        go.transform.position = worldPos;

        // 轻微上飘 + 缩小 + 淡出
        Vector3 drift = new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(0.01f, 0.06f), 0f);
        float life = Random.Range(0.35f, 0.55f);
        go.transform.DOMove(worldPos + drift, life).SetEase(Ease.OutQuad);
        img.DOFade(0f, life).SetEase(Ease.InQuad);
        go.transform.DOScale(0.2f, life).SetEase(Ease.InQuad);
        Destroy(go, life + 0.05f);
    }
    
    
    private IEnumerator FlyAwardVectorToEndLevelBtn(Vector3 start, GameObject endGameObject, GameObject gold, Vector3 left, Action call, float duration = 0.8f)
    {
        Vector3 endPos = new Vector3(endGameObject.transform.position.x-1,endGameObject.transform.position.y,endGameObject.transform.position.z);
        Vector3 starttemp = new Vector3(start.x,start.y+0.5f,endPos.z);

        // ---- 1. 准备透明度控制组件（适用于 UI） ----
        CanvasGroup cg = gold.GetComponent<CanvasGroup>();
        if (cg == null) cg = gold.AddComponent<CanvasGroup>();
        cg.alpha = 0f; // 初始透明

        // ---- 2. 构建主序列 ----
        Sequence flySeq = DOTween.Sequence();

        // 2.1 淡入（0.2s）
        flySeq.Join(cg.DOFade(1f, 0.2f).SetEase(Ease.OutQuad));
        
        // 2.2 悬浮效果（前 0.5 秒）：上下脉冲，自动回到原位
        flySeq.Append(gold.transform.DOMove(starttemp, 0.2f).OnComplete(() =>
            {
                flySeq.Append(gold.transform.DOMove(start, 0.2f));
            })
            .SetEase(Ease.OutQuad));

        // 2.3 曲线飞入（使用 CatmullRom）
       
        Vector3 mid = (start + endPos) / 2 + left * 1.5f;
        Vector3[] path = new Vector3[] { start, mid, endPos };
        flySeq.Append(gold.transform.DOPath(path, duration, PathType.CatmullRom)
            .SetEase(Ease.InOutSine));

        // 2.4 淡出（0.2s）— 飞入完成后逐渐消失
        flySeq.Append(cg.DOFade(1f, 0.2f).SetEase(Ease.InQuad));

        // ---- 3. 等待整个序列完成 ----
        //yield return flySeq.WaitForCompletion();

        // ---- 4. 飞入完成后，按钮缩放反馈 + 回调 ----
        flySeq.Join(endGameObject.transform.DOScale(new Vector3(1.1f, 1.1f, 1.1f), 0.1f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => endGameObject.transform.DOScale(Vector3.one, 0.1f)));
        
        yield return flySeq.WaitForCompletion();
        
        // 执行回调（此处会销毁 gold 等）
        call?.Invoke();
    }
    
    private IEnumerator FlyInGoldCoroutine(Transform start,Transform target,GameObject gold,bool isCurve,Action call,Vector3 scale,float duration=0.45f)
    {
        GameObject Gold = Instantiate(gold,SystemManager.Instance._uiRoot);
        Gold.transform.position = start.position; // 设置起始位置
        Gold.transform.localScale = scale;
       
        Vector3 endPosition = target.position; // 设置起始位置

        // 计算距离
        float distance = Vector3.Distance(start.position, endPosition);
        float speed = 20.0f; // 例如：每秒移动2个单位
        duration = distance / speed;
        if(duration<0.45f) duration = 0.45f;
        
        // 根据距离计算移动时长
        Color color = Gold.GetComponent<Image>().color; // 获取当前颜色
        color.a = 0; // 设置透明度为 0
        Gold.GetComponent<Image>().color = color;
        Gold.GetComponent<Image>().DOFade(1, 0.2f);
        
        if (isCurve)
        {
            var midPos = (endPosition + start.position) / 2;
            var BezierMidPos = (midPos + start.position) / 2 + Vector3.up * 2;
            //var MidEndPos = (midPos + endPosition) / 2 + Vector3.right *0.78f;
            Vector3[] MovePoints = CreatTwoBezierCurve(start.position,endPosition,BezierMidPos).ToArray();
            Gold.transform.DOPath(MovePoints, duration).SetEase(Ease.Linear).OnComplete(() =>
            {
                call?.Invoke();
                // 确保元素最终位置在目标位置
            });
        }
        else
        {
            Gold.transform.DOMove(endPosition,duration).SetEase(Ease.Linear).OnComplete(() =>
            {
                call?.Invoke();
                // 确保元素最终位置在目标位置
            });
        }
        
        Gold.transform.DOScale(new Vector3(0.78f,0.78f,1f), duration);
        yield return new WaitForSeconds(0.2f);
        //AudioManager.Instance.TriggerVibration();
        AudioManager.Instance.PlaySoundEffect("filyGold");
        yield return new WaitForSeconds(duration);
        Gold.GetComponent<Image>().DOFade(0, 0.1f).OnComplete(() =>
        {
            Gold.gameObject.SetActive(false);
        });
        
        yield return new WaitForSeconds(5.0f);
        Destroy(Gold.gameObject);
    }
    
    /// <summary>
    ///二阶贝塞尔,nultiple光滑度
    /// </summary>
    public List<Vector3> CreatTwoBezierCurve(Vector3 startPoint, Vector3 endPoint, Vector3 middlePoint, int nultiple = 5)
    {
        List<Vector3> allPoints = new List<Vector3>();
        for (int i = 0; i < nultiple; i++)
        {
            float tempPercent = (float)i / (float)nultiple;
            float dis1 = Vector3.Distance(startPoint, middlePoint);
            Vector3 point1 = startPoint + Vector3.Normalize(middlePoint - startPoint) * dis1 * tempPercent;
            float dis2 = Vector3.Distance(middlePoint, endPoint);
            Vector3 point2 = middlePoint + Vector3.Normalize(endPoint - middlePoint) * dis2 * tempPercent;
            float dis3 = Vector3.Distance(point1, point2);
            Vector3 linePoint = point1 + Vector3.Normalize(point2 - point1) * dis3 * tempPercent;
            allPoints.Add(linePoint);
        }
        allPoints.Add(endPoint);
        return allPoints;
    }
    
    private IEnumerator FlyInCoroutine(Transform start,Transform target,GameObject effect,Action call,float duration=0f)
    {
        GameObject Effect = Instantiate(effect);
        Effect.transform.position = start.position; // 设置起始位置
        Effect.gameObject.SetActive(true);
        Vector3 endPosition = target.position; // 设置起始位置
        // 计算距离
        float distance = Vector3.Distance(start.position, endPosition);

        // 根据距离计算移动时长
        // 根据距离计算移动时长
        if(duration<0.2f)  duration = distance / 30f;

        if(duration<0.45f) duration = 0.45f;
        Debug.LogWarning("提示道具粒子效果运动 距离："+distance+"时长"+duration);
        
        var midPos = (endPosition + start.position) / 2;
        var BezierMidPos = (midPos + start.position) / 2 + Vector3.right * 2;
        //var MidEndPos = (midPos + endPosition) / 2 + Vector3.right *0.78f;
        Vector3[] MovePoints = CreatTwoBezierCurve(start.position,endPosition,BezierMidPos).ToArray();
        
        Effect.transform.DOPath(MovePoints, duration).SetEase(Ease.Linear).OnComplete(() =>
        {
            call?.Invoke();
            // 确保元素最终位置在目标位置
            Effect.gameObject.SetActive(false);
        });
        
        //Effect.transform.DOMove(endPosition,duration).SetEase(Ease.Linear).OnComplete(() =>
        //{
        //    call?.Invoke();
        //    // 确保元素最终位置在目标位置
        //   
        //});
       
       yield return new WaitForSeconds(5.0f);
       Destroy(Effect.gameObject);
    }
}


