using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 蝴蝶信息
/// </summary>
[Serializable]
public class ButterflyInfo
{
    public int Id;           // 蝴蝶id
    public string Name;      // 名称
    public float Weight;     // 抽取权重
    public int SceneID;      // 所属场景
    public int Rarity;       // 品级
    public string ButterflyIcon;      // 图片资源
}
/// <summary>
/// 蝴蝶成长
/// </summary>
[Serializable]
public class ButterflyGrow
{
    public int Id;           // id
    public int Count;     // 需要解锁数量
    public float Prob;       // 出现概率
    public int Interval;     // 必现关卡间隔
}

/// <summary>
/// 蝴蝶养成管理器
/// </summary>
public class ButterfliesManager : SingletonMono<ButterfliesManager>
{
    [Header("配置数据")] [Tooltip("蝴蝶集")] public List<ButterflyInfo> ButterfliesInfo = new List<ButterflyInfo>();
    [Tooltip("关卡养成")] public List<ButterflyGrow> ButterfliesGrow = new List<ButterflyGrow>();

    private GameObject processBar;
    private float fadeDuration = 0.5f;
    public bool IsOpen
    {
        get => GameDataManager.Instance.ButterflyData.pupa > 0;
    }

    protected override void Awake()
    {
        base.Awake();
        LoadConfigTables();
    }

    private void Start()
    {
        if (processBar == null)
        {
            AssetBundleLoader.SharedInstance.LoadAtlas("butterfly_ui", "UI_Butterfly_icon");
            GameObject go =  AssetBundleLoader.SharedInstance.LoadGameObject("commonitem","ButterflyProcessBar");
            processBar = Instantiate(go);
            processBar.SetActive(false);
        }
       
    }

    #region 配置文件解析

    private void LoadConfigTables()
    {
        TextAsset butterfliesTable =
            AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "ButterflyCollectionTable");
        if (butterfliesTable != null)
        {
            ParseButterflies(butterfliesTable.text);
        }
        else
        {
            Debug.LogError("Failed to load ButterflyCollectionTable csv data.");
        }

        TextAsset probabilityTable =
            AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "ButterflySceneTable");
        if (probabilityTable != null)
        {
            ParseProbabilityTable(probabilityTable.text);
        }
        else
        {
            Debug.LogError("Failed to load ButterflySceneTable csv data.");
        }
    }

    /// <summary>
    /// 解析蝴蝶集的配置
    /// </summary>
    private void ParseButterflies(string butterfliesTableText)
    {
        ButterfliesInfo.Clear();

        string[] lines = butterfliesTableText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 2; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',', System.StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length >= 5)
            {
                ButterflyInfo butterflyInfo = new ButterflyInfo();
                if (int.TryParse(fields[0], out int id))
                    butterflyInfo.Id = id;
                if (!string.IsNullOrEmpty(fields[1]))
                    butterflyInfo.Name = fields[1];
                if (float.TryParse(fields[3], out float prob))
                    butterflyInfo.Weight = prob;
                if (int.TryParse(fields[4], out int sceneID))
                    butterflyInfo.SceneID = sceneID;
                if (int.TryParse(fields[5], out int rarity))
                    butterflyInfo.Rarity = rarity;
                if(!string.IsNullOrEmpty(fields[6]))
                    butterflyInfo.ButterflyIcon = fields[6];

                ButterfliesInfo.Add(butterflyInfo);
            }
        }

        ButterfliesInfo.Sort((a, b) => a.Id.CompareTo(b.Id));
    }

    /// <summary>
    /// 解析养成配置
    /// </summary>
    private void ParseProbabilityTable(string probabilityTableText)
    {
        ButterfliesGrow.Clear();

        string[] lines = probabilityTableText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 2; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            ButterflyGrow butterflyGrow = new ButterflyGrow();
            if (int.TryParse(fields[0], out int id))
                butterflyGrow.Id = id;
            if (int.TryParse(fields[2], out int count))
                butterflyGrow.Count = count;
            if (float.TryParse(fields[3], out float prob))
                butterflyGrow.Prob = prob;
            if (int.TryParse(fields[4], out int interval))
                butterflyGrow.Interval = interval;

            ButterfliesGrow.Add(butterflyGrow);
        }

        ButterfliesGrow.Sort((a, b) => a.Id.CompareTo(b.Id));
    }

    #endregion

    #region  进度展示

    public void ShowButterflyProcess(Transform parent, bool isFade = false)
    {
        ButterflyGrow butterflyGrow = GetCurrentGrow();
        if(butterflyGrow == null)
             return;
        
        processBar.transform.SetParent(parent);
        processBar.transform.localScale = Vector3.one;
        processBar.transform.localPosition = Vector3.zero;
        Text text = processBar.GetComponentInChildren<Text>();
        text.text = $"{GameDataManager.Instance.ButterflyData.currPupa} / {butterflyGrow.Count}"; 
        if (text.font == null || text.font.material == null || text.font.material.mainTexture == null)
            Debug.LogError("字体或字体纹理丢失！");
        
        float ratio = GameDataManager.Instance.ButterflyData.currPupa / (float)butterflyGrow.Count;
        float targetValue = Mathf.Clamp01(ratio);
        if (isFade)
        {
            processBar.GetComponent<Slider>().value = 0;
            processBar.SetActive(true);
            StartCoroutine(Fade(0, 1,0));
            StartCoroutine(SetSliderProgress(0,targetValue,0.5f,0.3f));
        }
        else
        {
            processBar.GetComponent<Slider>().value = targetValue;
            processBar.SetActive(true);
        }
    }
    
    public IEnumerator ShowButterflyProcess(Transform parent, Transform sTransform,Action call = null)
    {
        ButterflyGrow butterflyGrow = GetCurrentGrow();
        if(butterflyGrow == null)
            yield break;
        
        processBar.transform.SetParent(parent);
        processBar.transform.localScale = Vector3.one;
        processBar.transform.localPosition = Vector3.zero;
        processBar.GetComponent<Slider>().value = 0;
        Text text = processBar.GetComponentInChildren<Text>();
        text.text = $"{GameDataManager.Instance.ButterflyData.currPupa} / {butterflyGrow.Count}"; 
        yield return new WaitForEndOfFrame();
        processBar.SetActive(true);
        yield return Fade(0, 1,0);
        if (text.font == null || text.font.material == null || text.font.material.mainTexture == null)
                Debug.LogError("字体或字体纹理丢失！");
            
            // float ratio = GameDataManager.Instance.ButterflyData.currPupa / (float)butterflyGrow.Count;
            // float targetValue = Mathf.Clamp01(ratio);
            // yield return SetSliderProgress(0,targetValue,0.5f,0.3f);
        
        Transform target = processBar.transform.Find("Icon");
        yield return  FlyPupaCoroutine(sTransform, target, call ,0.6f);
        
        float ratio = GameDataManager.Instance.ButterflyData.currPupa / (float)butterflyGrow.Count;
        float targetValue = Mathf.Clamp01(ratio);
        StartCoroutine( SetSliderProgress(0,targetValue,0.5f,0.3f));
        text.text = $"{GameDataManager.Instance.ButterflyData.currPupa} / {butterflyGrow.Count}"; 
        yield return Fade(1, 0, 0.4f);
    }

    /// <summary>
    /// 蝉蛹飞行协程
    /// </summary>
    private IEnumerator FlyPupaCoroutine(Transform start, Transform target, Action call, float duration=0f)
    {
           GameObject go = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "Pupa");
           GameObject effect = Instantiate(go, start.position, Quaternion.identity, target);
           effect.gameObject.SetActive(true);
           Vector3 endPosition = target.position;
           float distance = Vector3.Distance(start.position, endPosition);

           if (duration < 0.2f) duration = distance / 30f;
           if (duration < 0.45f) duration = 0.45f;
           
           var midPos = (endPosition + start.position) / 2f;
           var bezierMidPos = (midPos + start.position) / 2f + Vector3.right * 2;
           Vector3[] movePoints = CreateTwoCurve(start.position, endPosition, bezierMidPos).ToArray();
           bool flyover = false;
           effect.transform.DOPath(movePoints, duration).SetEase(Ease.Linear).OnComplete(() =>
           {
                call?.Invoke();
                effect.gameObject.SetActive(false);
                flyover = true;
       
           });
           yield return new WaitUntil((() => flyover));
           yield return null;
           Destroy(effect.gameObject);
    }

    /// <summary>
    ///二阶贝塞尔,multiple光滑度
    /// </summary>
    private List<Vector3> CreateTwoCurve(Vector3 startPosition, Vector3 endPosition, Vector3 middlePoint, int multiple = 5)
    {
       List<Vector3> allPoints = new List<Vector3>();
       for (int i = 0; i < multiple; i++)
       {
           float tempPercent = (float) i / (float)multiple;
           float dis1 = Vector3.Distance(startPosition, middlePoint);
           Vector3 point1 = startPosition + Vector3.Normalize(middlePoint - startPosition) * dis1 * tempPercent;
           float dis2 = Vector3.Distance(middlePoint, endPosition);
           Vector3 point2 = middlePoint + Vector3.Normalize(endPosition - middlePoint) * dis2 * tempPercent;
           float dis3 = Vector3.Distance(point1, point2);
           Vector3 linePoint = point1 + Vector3.Normalize(point2 - point1) * dis3 * tempPercent;
           allPoints.Add(linePoint);
       } 
       allPoints.Add(endPosition);
       return allPoints;
    }

    /// <summary>
    /// 设置进度条淡入
    /// </summary>
    private IEnumerator Fade(float startAlpha, float endAlpha, float waitTime = 3)
    {
        yield return new WaitForSeconds(waitTime);
        CanvasGroup canvasGroup = processBar.GetComponent<CanvasGroup>();
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = currentAlpha;
            }

            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = endAlpha;
        }

        if (endAlpha == 0f)
        {
            processBar.SetActive(false);
        }
    }
    // 设置Slider进度到指定比例（0-1之间）
    private IEnumerator SetSliderProgress(float startValue,float targetValue, float duration = 1,float waitTime = 3)
    {
        yield return new WaitForSeconds(waitTime);
        
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            processBar.GetComponent<Slider>().value = Mathf.Lerp(startValue, targetValue, timer / duration);
            yield return null;
        }
        processBar.GetComponent<Slider>().value = targetValue;
    }
    #endregion

    #region 业务

    /// <summary>
    /// 判断是否可以展示蚕蛹
    /// </summary>
    /// <returns></returns>
    public bool CanObtainedPupa()
    {
        return false;
        int levelId = GameDataManager.Instance.UserData.CurrentHexStage;
        switch ((LevelType)GameDataManager.Instance.UserData.levelMode)
        {
            case LevelType.BlockWord:
                levelId = GameDataManager.Instance.UserData.CurrentHexStage;
                break;
            case LevelType.ChessWord:
                levelId = GameDataManager.Instance.UserData.CurrentChessStage;
                break;
            case LevelType.HexWord:
                levelId = GameDataManager.Instance.UserData.CurrentHexStage;
                break;
        }
        
        if(levelId<3)
        {
            Debug.LogError("当前等级未到，不能展示蚕蛹");
            return false;
        }
        
        ButterflyGrow butterflyGrow = GetCurrentGrow();
        if (butterflyGrow == null || GameDataManager.Instance.ButterflyData.currPupa >= butterflyGrow.Count)
            return false;
        
        bool able = UnityEngine.Random.Range(0, 100) < butterflyGrow.Prob * 100;
        if (!able)
        {
            if (GameDataManager.Instance.ButterflyData.intervalLv >= butterflyGrow.Interval)
            {
                able = true;
                GameDataManager.Instance.ButterflyData.intervalLv = 0;
            }
            else
            {
                GameDataManager.Instance.ButterflyData.intervalLv++;
                StageHexController.Instance.CurStageData.PupaDatas = null;
                Debug.LogError("限时概率为不显示蚕蛹");
            }
        }else
        {
            //检查是否有可放置的蚕蛹数据
            if (StageHexController.Instance.CurStageData.PupaDatas == null)
            {
                GameDataManager.Instance.ButterflyData.intervalLv++;
                Debug.LogError("限时概率为显示蚕蛹但没有可以放置的蚕蛹位置");
                return false;
            } 
        }
        
        return able;
    }
    /// <summary>
    /// 添加蛹数
    /// </summary>
    public void AddObtainedPupa(Transform startPoint, int pupa = 1, Transform parent  = null)
    {
        if (parent != null)
        {
            Debug.Log("看看调用几次");
            StartCoroutine(ShowButterflyProcess(parent, startPoint,() =>
            {
                GameDataManager.Instance.ButterflyData.AddPupa(pupa);
            }));
        }
        else
        {
            GameDataManager.Instance.ButterflyData.AddPupa(pupa);
        }
        
    }

    /// <summary>
    ///  判断当前是否能够合成
    /// </summary>
    public bool CanMakeButterfly()
    {
        ButterflyGrow butterflyGrow = GetCurrentGrow();
        return butterflyGrow != null &&  GameDataManager.Instance.ButterflyData.currPupa >= butterflyGrow.Count;
    }

    /// <summary>
    /// 解锁一个蝴蝶, 是否返回当前解锁的蝴蝶 进行播放特效
    /// 解锁完成返回蝴蝶进行播放特效， 同时场景内的蝴蝶都已解锁通知下一个场景
    /// </summary>
    public bool UnlockButterfly(out int nextGardenId, Action<ButterflyInfo> callback = null)
    {
        nextGardenId = -1; // 默认没有下一个场景ID
        
        ButterflyGrow butterflyGrow = GetCurrentGrow();
        if (butterflyGrow == null || GameDataManager.Instance.ButterflyData.currPupa < butterflyGrow.Count) 
            return false;
        
        // 检查是否已经完成的蝶园, 已经满的蝶园不再处理
        List<ButterflyInfo> checkGardenButterflies = GetCurrentGardenButterflies();
        bool checkAllCollected = checkGardenButterflies.All(p=> GameDataManager.Instance.ButterflyData.butterflies.Contains(p.Id));
        if (checkAllCollected)
        {
            MessageSystem.Instance.ShowTip("当前蝶园已经收集完了！");
            return false;
        }
        // --- 核心解锁逻辑 ---
        GameDataManager.Instance.ButterflyData.DecreasePupa(butterflyGrow.Count);
        ButterflyInfo randomInfo = RandomButterflyByCurrentGarden();
        GameDataManager.Instance.ButterflyData.AddButterfly(randomInfo.Id);
        callback?.Invoke(randomInfo);
        
        // 检查一下是否收集完，进入下一个蝶园 
        // 若玩家收集齐最后一个场景的蝴蝶，则不再播放转场动画，而是提示“下个场景正在制作中”；在下个场景实装时，玩家进入该界面会自动播放转场动画
        bool isAll = checkGardenButterflies.All(p=> GameDataManager.Instance.ButterflyData.butterflies.Contains(p.Id));
        if (isAll)
        {
            int currentIndex = ButterfliesGrow.FindIndex(p => p.Id == butterflyGrow.Id);
            int maxIndex = ButterfliesGrow.Count - 1;
            if (currentIndex < maxIndex)
            {
                nextGardenId = ButterfliesGrow[currentIndex + 1].Id;
                GameDataManager.Instance.ButterflyData.AddGarden(nextGardenId);
            }
        }
        return isAll;
    }
    
    /// <summary>
    /// 获取当前成长配置
    /// </summary>
    private ButterflyGrow GetCurrentGrow()
    {
        return ButterfliesGrow.Find(x => x.Id == GameDataManager.Instance.ButterflyData.currGarden);
    }

    /// <summary>
    /// 获取当前园子的蝴蝶列表
    /// </summary>
    public List<ButterflyInfo> GetCurrentGardenButterflies()
    {
        return  ButterfliesInfo.FindAll(x => x.SceneID == GameDataManager.Instance.ButterflyData.currGarden);
    }
    
    /// <summary>
    /// 从当前圈子里的蝴蝶随机生成一个
    /// </summary>
    private ButterflyInfo RandomButterflyByCurrentGarden()
    {
        List<ButterflyInfo> butterflies = GetCurrentGardenButterflies();
        List<ButterflyInfo> available = butterflies.FindAll(x => !GameDataManager.Instance.ButterflyData.butterflies.Contains(x.Id));
        return SelectButterflyByWeight(available);
    }

    private ButterflyInfo SelectButterflyByWeight(List<ButterflyInfo> butterflies)
    {
        if (butterflies == null || butterflies.Count == 0)
            return null;

        // 计算总权重
        float totalWeight = butterflies.Sum(b => b.Weight);
        // 生成随机数
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);

        float currentSum = 0f;
        foreach (var butterfly in butterflies)
        {
            currentSum += butterfly.Weight;
            if (randomValue <= currentSum)
                return butterfly;
        }

        return butterflies.Last();
    }
    
    #endregion

}
