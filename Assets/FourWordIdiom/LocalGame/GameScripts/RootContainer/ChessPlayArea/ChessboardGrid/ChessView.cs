using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;


/// <summary>
/// 格子状态
/// </summary>
public enum TileState
{
    None,
    Check,
    Default,
    Fill,
    Error,
    Success,
}


public class ChessView : MonoBehaviour, IPointerUpHandler, IPointerDownHandler
{
    public delegate void SelectHandler(ChessView data);
    [HideInInspector] public event SelectHandler OnSelectHandler;
    [Header("UI组件")]
    [SerializeField] private Text _textDisplay;    // 文字显示
    [SerializeField] public Image _bg;            // 背景图
    [SerializeField] private GameObject _choose;   // 选择框
    [SerializeField] private Text _tipText;  // 提示文本
    [SerializeField] private GameObject _iceObj;   // 冰块节点 
    [SerializeField] private GameObject _flowerObj;  // 花朵节点
    [SerializeField] private GameObject _leafObj;    // 树叶节点
    
    [SerializeField] private Text _score; // 提示分数
    [SerializeField] ParticleSystem _successParticle;
    // 错误动画配置
    private float shakeRadius   = 10f;      // 最大晃动半径（像素）
    private int   shakeSlices   = 16;      // 采样次数（越高越细腻）
    private float shakeDuration = 0.45f;   // 总时长
    
    public Chesspiece chesspiece;   // 生成的格子属性

    // 基础属性
    [HideInInspector] public string Id => chesspiece.id;
    [HideInInspector] public int Row => chesspiece.row;
    [HideInInspector] public int Col => chesspiece.col;
    [HideInInspector] public string Answer => chesspiece?.letter ?? "";        // 正确答案
    [HideInInspector] public int Direction => chesspiece.direction;     // 排列方向
    [HideInInspector] public TileState CurrState => chesspiece.state;   // 当前状态
     // 🌟 新增：数据快照标记。用于在逻辑抢跑时，告诉后方的动画层“我这格需要飞叶子！”
    [HideInInspector] public bool isPendingLeafFlight = false;
    public Vector2 startPosition;  // 原始位置
    private RectTransform _rectTrans;
    private bool _isProcessingInteraction; 
    public bool _isGoldLeaf=false; 
    public bool iceLogicBroken;        // 🌟 新增：冰块逻辑已破，但动画可能还在播
    public bool flowerLogicBroken;     // 🌟 新增：花朵逻辑已破，但动画可能还在播
    // 是否锁定
    public bool IsLocked
    {
        get
        {
            return CurrState == TileState.Default || CurrState == TileState.Success;
        }
    }

    public bool IsOK;
    public bool Correct => CurrState == TileState.Default || CurrState == TileState.Success || Answer.Equals(chesspiece.bowl?.letter);
    public RectTransform TileTransform => _rectTrans ??= transform as RectTransform;
    
    /// <summary>
    /// 初始化格子
    /// </summary>
    /// <param name="Puzzle">词</param>
    public void SetInit(Chesspiece pz)
    {
        _choose.SetActive(false);
        _tipText.gameObject.SetActive(false);
        _isGoldLeaf = false;
        chesspiece = pz;
        if (chesspiece.bowl != null)
        {
            _isGoldLeaf= chesspiece.isGoldLeaf;
        }
        iceLogicBroken = false;
        flowerLogicBroken = false; // 🌟 每次初始化格子时重置
        _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
        //Debug.Log($"当前词： {Answer} {CurrState}");
        // 设置选择框尺寸
        int row = ChessStageController.Instance.CurrStageData.MaxRow;
        int col = ChessStageController.Instance.CurrStageData.MaxCol;
        int maxRC = Mathf.Max(row + 1, col);   // 7 7 →7   8 8 →8   8 9 →9
        _choose.GetComponent<Image>().sprite = maxRC switch
        {
            7 => AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_162"),
            8 => AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_142"),
            9 => AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_126"),
            _ => AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_162")   // 更大也按最小格
        };
        //_choose.GetComponent<Image>().SetNativeSize();
     
        SetScore(0);
        IsOK = false;
        UpdateTile();
    }

    /// <summary>
    /// 设置格子状态
    /// </summary>
    /// <param name="state">状态</param>
    public void SetTileState(TileState state, bool update = true)
    {
        chesspiece.state = state;
        if(update)
            UpdateTile();
        
        ChessStageController.Instance.ModifyChreepiece(chesspiece);
    }

    /// <summary>
    /// 设置填入的字
    /// </summary>
    /// <param name="puzzle"></param>
    public void SetPuzzle(Bowl bowl)
    {
        chesspiece.bowl = bowl;
        chesspiece.state = TileState.Fill;
        _isGoldLeaf=bowl.isGoldLeaf;
        // Debug.Log("设置词完成 "+ chesspiece.state +" "+JsonUtility.ToJson(chesspiece.bowl));
        ChessStageController.Instance.ModifyChreepiece(chesspiece);
    }
    
    /// <summary>
    /// 设置提示框显示状态
    /// </summary>
    public void SetChoose(bool state, string layerName = UIPanelLayer.BasePanel)
    {
        if (CurrState == TileState.Success)
        {
            _choose.SetActive(false);
            return;
        }
        _choose.SetActive(state);
        Canvas cv = _choose.GetComponent<Canvas>();
        cv.overrideSorting = true;           // 允许覆盖
        cv.sortingOrder = 5;             // 最上层
        cv.sortingLayerName = layerName;         // 必须存在的层名
    }
    /// <summary>
    /// 设置消息提示字
    /// </summary>
    public void SetTipMessage()
    {
        _tipText.text = Answer.ToString();
        _tipText.gameObject.SetActive(true);
        chesspiece.tip = true;
        ChessStageController.Instance.ModifyChreepiece(chesspiece);
        _bg.gameObject.SetActive(false);
    }

    public void SetScore(float score)
    {
#if Unity_ShowLog || UNITY_EDITOR
        if (score < 1f)
        {
            _score.text = "";
            _score.gameObject.SetActive(false);
        }
        else
        {
            _score.text = score.ToString(CultureInfo.InvariantCulture);
            _score.gameObject.SetActive(GameCoreManager.Instance.AutoLevelTalbe.activeSelf);
        }
#else
    _score.gameObject.SetActive(false);
#endif
    }
    /// <summary>
    /// 更新方块当前显示
    /// </summary>
    public void UpdateTile(bool lateChose = false)
    {
        switch (CurrState)
        {
            case TileState.None:
                _textDisplay.text = "";
                _bg.gameObject.SetActive(false);
                break;
            case TileState.Check:
                _textDisplay.text = "";
                _bg.gameObject.SetActive(false);
                break;
            case TileState.Default:
                _textDisplay.text = Answer.ToString();
                _textDisplay.color = new Color32(100, 80, 66, 255);
                if (_isGoldLeaf&&chesspiece.bowl!=null)
                {
                    if (chesspiece.bowl.totalcount > 1)
                    {
                        _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
                        _bg.GetComponent<UIShiny>().enabled = false;
                        chesspiece.isGoldLeaf=false;
                        _isGoldLeaf = false;
                    }
                    else
                    {
                        _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
                        _bg.GetComponent<UIShiny>().enabled = true;
                    }
                   
                }
                else
                {
                    _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
                    _bg.GetComponent<UIShiny>().enabled = false;
                }
                _bg.gameObject.SetActive(true);
                break;
            case TileState.Fill:
                if (chesspiece.bowl != null)
                {
                    _textDisplay.text = chesspiece.bowl.letter;
                    _textDisplay.color = new Color32(100,80,66,255);
                }
                if (_isGoldLeaf)
                {
                    if (chesspiece.bowl.count >= 1)
                    {
                        _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
                        _bg.GetComponent<UIShiny>().enabled = false;
                        _isGoldLeaf=false;
                        //chesspiece.isGoldLeaf = false;
                    }
                    else
                    {
                        _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
                        _bg.GetComponent<UIShiny>().enabled = true;
                        chesspiece.isGoldLeaf=true;
                        ChessView tileView = ChessStageController.Instance.GoldLeafChessViews.Find(x=>x.Answer==chesspiece.bowl.letter);
                        if (tileView == null)
                        {
                            ChessStageController.Instance.GoldLeafChessViews.Add(this);
                        }
                    }
                   
                }
                else
                {
                    _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
                    _bg.GetComponent<UIShiny>().enabled = false;
                }
                _bg.gameObject.SetActive(true);
                break;
            case TileState.Error:
                //Debug.LogWarning($"更新词: {Answer} " + JsonUtility.ToJson(chesspiece));
                 _textDisplay.text = chesspiece.bowl.letter;
                 _textDisplay.color = Color.red;
                _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
                _bg.gameObject.SetActive(true);
                break;
            case TileState.Success:
                _textDisplay.text = Answer.ToString();
                if (chesspiece.bowl!=null)
                {
                    if (_isGoldLeaf)
                    {
                        if (chesspiece.bowl.totalcount > 1)
                        {
                            _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
                            _textDisplay.color = Color.white;
                            _bg.GetComponent<UIShiny>().enabled = false;
                            //chesspiece.bowl.isGoldLeaf = false;
                        }
                        else
                        {
                            _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
                            _textDisplay.color = new Color32(100,80,66,255);

                            _bg.GetComponent<UIShiny>().enabled = true;
                            chesspiece.bowl.isGoldLeaf = true;
                            chesspiece.isGoldLeaf=true;
                            ChessView tileView = ChessStageController.Instance.GoldLeafChessViews.Find(x=>x.Answer==chesspiece.bowl.letter);
                            if (tileView == null)
                            {
                                ChessStageController.Instance.GoldLeafChessViews.Add(this);
                                //ChessStageController.Instance.UpdateGoldLeafCount(1);
                            }
                        }
                    }
                    else
                    {
                        _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
                        _textDisplay.color = Color.white;
                    }
                    
                }
                else
                {
                    _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
                    _textDisplay.color = Color.white;
                }
                _bg.gameObject.SetActive(true);
                _score.gameObject.SetActive(false);
                break;
        }
        if (!lateChose) 
            SetChoose(CurrState == TileState.Check);

        if(chesspiece.tip)
        {
            _tipText.text = Answer.ToString();
            _tipText.gameObject.SetActive(CurrState is TileState.Check or TileState.None);
        }
        if (chesspiece.hasFlower)
        {
            _flowerObj.GetComponent<Image>().enabled = true;
            _textDisplay.text = "";
            _flowerObj.transform.localScale = Vector3.one;
            _flowerObj.SetActive(true);
        }else
            _flowerObj.SetActive(false);
        
        // 如果有冰块，可能需要让背景变灰或者显示冰层
        _iceObj.transform.localScale = Vector3.one;
        _iceObj.SetActive(chesspiece.hasIce);
        _iceObj.GetComponent<Image>().enabled = true;
        if (chesspiece.hasLeaf)
        {
            _leafObj.SetActive(true);
            _leafObj.GetComponent<Image>().enabled = true;
            CanvasGroup leafCG = _leafObj.GetComponent<CanvasGroup>() ?? _leafObj.AddComponent<CanvasGroup>();
            bool hasText = CurrState == TileState.Fill || CurrState == TileState.Error || CurrState == TileState.Success;
            leafCG.alpha = hasText ? 0.35f : 1.0f; // 核心规则：有字半透明，无字全亮
        }else
        {
            _leafObj.SetActive(false); // 🌟 核心防残留：不长叶子了就彻底关掉GameObject！
        }
        
    }
    
    /// <summary>
    /// 播放错误动画
    /// </summary>
    /// <param name="isX">水平抖动</param>
    public void PlayError(bool isX)
    {
        _rectTrans.anchoredPosition = startPosition;
        StopAllCoroutines();
        StartCoroutine(PlayErrorAnimation(isX));
    }
    
    public void FlyToThemeBtn(GameObject TargetBtn, Transform parent, Action onComplete)
    {
        RectTransform selfRT = GetComponent<RectTransform>();
        GameObject clone = Instantiate(_bg.gameObject, parent);
  
        RectTransform cloneRT = clone.GetComponent<RectTransform>();
        Canvas canvas = clone.GetComponent<Canvas>();
        if(canvas == null ) 
            canvas = clone.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerName = UIPanelLayer.TipsPanel;
        canvas.sortingOrder = 10;
    
        // 复制尺寸+锚点
        cloneRT.anchorMin = new Vector2(0.5f,0.5f);
        cloneRT.anchorMax = new Vector2(0.5f,0.5f);
        cloneRT.sizeDelta = selfRT.sizeDelta;
        
        cloneRT.pivot = selfRT.pivot;
        cloneRT.localScale = selfRT.localScale * 0.9f;
        clone.transform.position = selfRT.position;
    
        Vector3 endWorld = TargetBtn.GetComponent<RectTransform>().position;
        Vector3 startPos = clone.transform.position;
        // 计算向上偏移10像素的点（世界坐标系，向上即Y轴增加）
        Vector3 midUpPos = startPos + Vector3.up * 2f;
    
        float duration = 0.2f;
        // 使用 Sequence 实现先向上移动再弧线移动到终点
        Sequence seq = DOTween.Sequence();
        seq.Append(clone.transform.DOMove(midUpPos, duration).SetEase(Ease.Linear));
        // 从中间点弧线移动到终点，使用带弧线的曲线 (OutQuad 会先快后慢产生一点弧线感，或使用 Bezier)
        seq.Append(clone.transform.DOMove(endWorld, duration * 2f).SetEase(Ease.Linear));
    
        seq.OnComplete(() =>
        {
            Destroy(clone);
            onComplete?.Invoke();
        });
    }
    
    #region 点击事件
    /// <summary>
    /// 错误抖动动画
    /// </summary>
 
    private IEnumerator PlayErrorAnimation(bool isX)
    {
        // 记录真正的原点
        Vector2 origin = _rectTrans.anchoredPosition;
        int   slices = shakeSlices;
        float dt     = shakeDuration / slices;

        for (int i = 0; i < slices; i++)
        {
            float t   = (float)i / slices;
            float ang = Random.Range(0, Mathf.PI * 2);
            float len = Random.value * shakeRadius * (1 - t);
            Vector2 rnd = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
            _rectTrans.anchoredPosition = origin + rnd;
            yield return new WaitForSeconds(dt);
        }
        // float elapsed  = 0;
        // while (elapsed < shakeDuration)
        // {
        //     // 归一化时间 0~1
        //     float t     = elapsed / shakeDuration;
        //     // 衰减：1 -> 0
        //     float decay = 1f - t;
        //     // 随机角度 + 随机半径（0~1）
        //     float angle = Random.Range(0, Mathf.PI * 2);
        //     float len   = Random.value * shakeRadius * decay;
        //     Vector2 rnd = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * len;
        //
        //     // 如果只允许单轴，把另一轴压 0
        //     if (isX) rnd.y = 0;
        //     else     rnd.x = 0;
        //
        //     _rectTrans.anchoredPosition = origin + rnd;
        //
        //     yield return null;          // 等一帧
        //     elapsed += Time.deltaTime;
        // }
        _rectTrans.anchoredPosition = origin;
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        if (IsLocked || !_isProcessingInteraction) return;

        transform.DOScale(1f, 0.1f);
        OnSelectHandler?.Invoke(this);
        _isProcessingInteraction = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsLocked || !PassDebounce()) return;   // 防抖
        if (chesspiece.hasIce) return;
        ChessPlayArea.Instance?.NotifyPlayerInteraction();
        
        _isProcessingInteraction = true;
        TileTransform.DOScale(1.15f, 0.1f).SetEase(Ease.OutQuad);
        AudioManager.Instance.PlaySoundEffect("WordClick");
    }
    private float lastClickTime = -1f;
    private const float DEBOUNCE_INTERVAL = 0.35f;   // 可调
    /// <summary>
    /// 点击防抖
    /// </summary>
    /// <returns></returns>
    private bool PassDebounce()
    {
        if (Time.time - lastClickTime < DEBOUNCE_INTERVAL) return false;
        lastClickTime = Time.time;
        return true;
    }
    #endregion

    private void OnDisable()
    {
        OnSelectHandler = null;
        chesspiece = null;
        lastClickTime = -1f;
        _successParticle.Stop();
        _successParticle.gameObject.SetActive(false);
        if (_iceObj != null) _iceObj.transform.DOKill();
        if (_flowerObj != null) _flowerObj.transform.DOKill();
    }
    
    /// <summary>
    /// 🌟 重构：同步变色、放大、播放粒子（无波浪延迟）
    /// </summary>
    /// <param name="duration">特效持续总时长，保证和外框一模一样</param>
    /// <param name="onStart">瞬间变绿的回调</param>
    public void PlaySuccessAnimation(float duration, Action onStart = null)
    {
        TileTransform.DOKill();
        
        Sequence seq = DOTween.Sequence();
        
        seq.OnStart(() => {
            // 瞬间变绿！
            onStart?.Invoke(); 
            
            // 瞬间喷发粒子
            _successParticle.gameObject.SetActive(true);
            _successParticle.Stop(); 
            _successParticle.Play();
            // 提层级，防止放大时被其他非完成格子压住
            TileTransform.SetAsLastSibling(); 
        });
        
        // 1. 同步放大到 1.15倍 (耗时 0.15秒)
        seq.Append(TileTransform.DOScale(1.15f, 0.15f).SetEase(Ease.OutQuad));
        
        // 2. 悬停在这个大小，等待粒子和发光框播完 (耗时 = 总时长 - 放大和缩回的 0.3 秒)
        float holdTime = Mathf.Max(0f, duration - 0.3f);
        // Debug.Log("发光框播完时间是 " + holdTime);
        seq.AppendInterval(holdTime);
        
        // 3. 完美缩回原状 (耗时 0.15秒)
        seq.Append(TileTransform.DOScale(1f, 0.15f).SetEase(Ease.InQuad));
    }

    public void PlayRevealAnimation1(Transform index)
    {
        StartCoroutine(PlayRevealAnimation(index));
    }
    public IEnumerator PlayRevealAnimation(Transform index)
    {
        // 1. 使用差异化特效资源名称
        const string effectBundle = "useritems";
        const string effectAsset = "ToolTipsEffect"; // 修改资源名称

        // 2. 异步加载优化
        var loadOperation = AssetBundleLoader.SharedInstance.LoadGameObject(effectBundle, effectAsset);      

        // 3. 特效实例化与定位
        var effectInstance = Instantiate(loadOperation, transform.parent);
        effectInstance.transform.position = index.position;

        // 4. 添加随机旋转增加差异化
        effectInstance.transform.Rotate(0, 0, UnityEngine.Random.Range(-5, 5));

        // 5. 文字显示动画序列
        var sequence = DOTween.Sequence();

        // 第一阶段：特效展示期间
        sequence.AppendInterval(0.2f); // 比原版稍短的等待

        // 6. 文字显示带动画效果
        sequence.AppendCallback(() =>
        {
            SetTipMessage();
            // 添加缩放动画
            _tipText.transform.localScale = Vector3.zero;
            _tipText.transform.DOScale(Vector3.one, 0.3f)
                .SetEase(Ease.OutBack);
        });

        // 7. 特效自动销毁计时器
        sequence.AppendInterval(3.5f); // 比原版更早销毁特效
        sequence.AppendCallback(() => {
            if (effectInstance != null)
            {
                effectInstance.transform.DOScale(Vector3.zero, 0.2f)
                    .OnComplete(() => Destroy(effectInstance));
            }
        });

        yield return sequence.WaitForCompletion();

        // 8. 确保特效被清理
        if (effectInstance != null && effectInstance.activeInHierarchy)
        {
            Destroy(effectInstance);
        }
    }
    
    
    // 1. 冰块逻辑
    public IEnumerator PlayIceBreakAnim()
    {
        if (_iceObj == null || !_iceObj.activeInHierarchy) yield break;
        _iceObj.transform.DOKill();
        chesspiece.hasIce = false;
        _iceObj.GetComponent<Image>().enabled = false;
        // 播放碎冰粒子特效，然后隐藏冰块
        // _iceObj.transform.DOScale(1.2f, 0.1f).SetEase(Ease.OutBack);
        _iceObj.GetComponentInChildren<ParticleSystem>(true).Play();
        yield return new WaitForSeconds(1.2f);
        UpdateTile();
        _iceObj?.SetActive(false);
        iceLogicBroken = false;   // 🌟 动画结束，清除逻辑标记
    }
    
    // 2. 花朵逻辑
    public IEnumerator PlayFlowerBloomAnim()
    {
        chesspiece.hasFlower = false;
        _flowerObj.GetComponent<Image>().enabled = false;
        // 播放花朵绽放、消失的动画
        // _flowerObj.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack);
        _flowerObj.GetComponentInChildren<ParticleSystem>(true).Play();
        yield return new WaitForSeconds(1.2f);
        UpdateTile(true); 
        _flowerObj.SetActive(false);
        flowerLogicBroken = false;   // 🌟 动画结束，清除逻辑标记
    }
    
    // 3. 树叶逻辑
    public void ShowLeaf(bool show)
    {
        chesspiece.hasLeaf = show;
        _leafObj.transform.DOKill();
        
        if (show) 
        {
            _leafObj.SetActive(true);
            // 🌟 核心规则：根据当前是否填了字，动态调节树叶透明度
            CanvasGroup leafCG = _leafObj.GetComponent<CanvasGroup>() ?? _leafObj.AddComponent<CanvasGroup>();

            // 如果处于 None 或 Check 状态（还没填字），树叶完全显示(1.0)；如果填了字(Fill/Error)，变半透明(0.35)让玩家看清字！
            bool hasText = CurrState == TileState.Fill || CurrState == TileState.Error || CurrState == TileState.Success;
            leafCG.alpha = hasText ? 0.35f : 1.0f;

            // 🌟 核心换肤：根据全局树叶生成计数，循环切换 3 张不同的叶子皮肤图
            Image leafImg = _leafObj.GetComponent<Image>();
            if (leafImg != null)
            {
                int skinIndex = (ChessStageController.Instance.LeafGenCounter % 4) + 1; // 1, 2, 3 循环
                // 从你的图集Atlas或AdvancedBundleLoader中加载对应的叶子切图
                leafImg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas($"leaf_skin_0{skinIndex}");
            }
            
            // 保持原有的呼吸动效，但不改动 Alpha 轴
            _leafObj.transform.localScale = Vector3.one;
            _leafObj.transform.DOScale(1.1f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
        else
        {
            _leafObj.SetActive(false); // 🌟 显式隐藏保证清除
        }
    }
    
    /// <summary>
    /// 🌟 新增：树叶在整组填完失败时的“枯萎/缩小隐藏”失败动画
    /// </summary>
    public void PlayLeafFillFailedAnim()
    {
        if (_leafObj == null || !_leafObj.activeSelf) return;

        _leafObj.transform.DOKill();
        CanvasGroup leafCG = _leafObj.GetComponent<CanvasGroup>() ?? _leafObj.AddComponent<CanvasGroup>();

        // 缩放变小 + 快速淡出
        _leafObj.transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack);
        leafCG.DOFade(0f, 0.4f).OnComplete(() => 
        {
            chesspiece.hasLeaf = false;
            _leafObj.SetActive(false);
        });
    }
}
