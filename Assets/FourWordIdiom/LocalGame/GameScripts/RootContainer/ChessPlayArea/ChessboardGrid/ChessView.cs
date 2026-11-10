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
    [SerializeField] private Image _emojiDisplay;
    [SerializeField] private GameObject _errorFrame; // 拖入你新建的红框节点
    [SerializeField] public Image _bg;            // 背景图
    [SerializeField] private GameObject _choose;   // 选择框
    [SerializeField] private Text _tipText;        // 提示文本
    [SerializeField] private GameObject CoinObj;   // 金币图案
    [SerializeField] private GameObject _iceObj;   // 冰块节点 
    [SerializeField] private GameObject _flowerObj;  // 花朵节点
    [SerializeField] private GameObject _leafObj;    // 树叶节点
    [SerializeField] ParticleSystem _successParticle;
    
    [SerializeField] private GameObject _focusHaloObj; // 聚焦光圈节点
   
    [Header("雷达扫描专属层")]
    [SerializeField] private Image _radarBg;      // 拖入新增的 Radar_BG
    [SerializeField] private Text _radarText;     // 拖入新增的 Radar_Text
    [SerializeField] private Image _radarEmojiDisplay;  // 雷达扫描专属Emoji (与 _radarText 同级)
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
        if (_errorFrame != null) _errorFrame.SetActive(false);
        _tipText.gameObject.SetActive(false);
        _isGoldLeaf = false;
        chesspiece = pz;
        if (chesspiece.bowl != null)
        {
            _isGoldLeaf= chesspiece.isGoldLeaf;
        }
        iceLogicBroken = false;
        flowerLogicBroken = false; // 🌟 每次初始化格子时重置
        _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
        //Debug.Log($"当前词： {Answer} {CurrState}");
        // 设置选择框尺寸
        int row = ChessStageController.Instance.CurrStageData.MaxRow;
        int col = ChessStageController.Instance.CurrStageData.MaxCol;
        int maxRC = Mathf.Max(row + 1, col);   // 7 7 →7   8 8 →8   8 9 →9
        _choose.GetComponent<Image>().sprite = maxRC switch
        {
            7 => AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_162"),
            8 => AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_142"),
            9 => AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_126"),
            _ => AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_162")   // 更大也按最小格
        };
        //_choose.GetComponent<Image>().SetNativeSize();
        
        IsOK = false;
        UpdateTile();
        StopFocusAnim();
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
    /// 统一切换文字或Emoji的渲染状态
    /// </summary>
    private void SetDisplayContent(string letter, bool isEmoji, Color textColor, bool isTip = false)
    {
        bool isEmojiActive = isEmoji && GameDataManager.Instance.UserData.IsEmojiOn;
        
        // 如果是提示模式（提示框逻辑单独处理）
        if (isTip)
        {
            if (isEmojiActive)
            {
                if (_tipText != null) _tipText.gameObject.SetActive(false);
                if (_emojiDisplay != null)
                {
                    _emojiDisplay.gameObject.SetActive(true);
                    _emojiDisplay.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(letter);
                }
            }
            else
            {
                if (_emojiDisplay != null) _emojiDisplay.gameObject.SetActive(false);
                if (_tipText != null)
                {
                    _tipText.gameObject.SetActive(true);
                    _tipText.text = letter;
                }
            }
            return;
        }

        // 正式显示逻辑
        if (isEmojiActive)
        {
            if (_textDisplay != null) _textDisplay.gameObject.SetActive(false);
            if (_emojiDisplay != null)
            {
                _emojiDisplay.gameObject.SetActive(true);
                _emojiDisplay.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(letter);
                // 错误状态时，可以让图片稍微泛红，正常时为白色
                _emojiDisplay.color = Color.white;
            }
        }
        else
        {
            if (_emojiDisplay != null) _emojiDisplay.gameObject.SetActive(false);
            if (_textDisplay != null)
            {
                _textDisplay.gameObject.SetActive(true);
                _textDisplay.text = letter;
                _textDisplay.color = textColor;
            }
        }
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
        SetDisplayContent(Answer, chesspiece.isEmoji, Color.white, true);
        chesspiece.tip = true;
        ChessStageController.Instance.ModifyChreepiece(chesspiece);
        _bg.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 更新方块当前显示
    /// </summary>
    public void UpdateTile(bool lateChose = false)
    {
        if (_errorFrame != null) _errorFrame.SetActive(false);
        switch (CurrState)
        {
            case TileState.None:
            case TileState.Check:
                _textDisplay.text = "";
                if (_emojiDisplay != null) _emojiDisplay.gameObject.SetActive(false);
                _bg.gameObject.SetActive(false);
                break;
            case TileState.Default:
                SetDisplayContent(Answer, chesspiece.isEmoji, new Color32(100, 80, 66, 255));
                if (_isGoldLeaf&&chesspiece.bowl!=null)
                {
                    if (chesspiece.bowl.totalcount > 1)
                    {
                        _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
                        _bg.GetComponent<UIShiny>().enabled = false;
                        chesspiece.isGoldLeaf=false;
                        _isGoldLeaf = false;
                    }
                    else
                    {
                        _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
                        _bg.GetComponent<UIShiny>().enabled = true;
                        _bg.GetComponent<UIShiny>().Play();
                    }
                   
                }
                else
                {
                    _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
                    _bg.GetComponent<UIShiny>().enabled = false;
                }
                _bg.gameObject.SetActive(true);
                break;
            case TileState.Fill:
                if (chesspiece.bowl != null)
                {
                    SetDisplayContent(chesspiece.bowl.letter, chesspiece.bowl.isEmoji, new Color32(100, 80, 66, 255));
                }
                if (_isGoldLeaf)
                {
                    if (chesspiece.bowl.count >= 1)
                    {
                        _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
                        _bg.GetComponent<UIShiny>().enabled = false;
                        _isGoldLeaf=false;
                        //chesspiece.isGoldLeaf = false;
                    }
                    else
                    {
                        _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
                        _bg.GetComponent<UIShiny>().enabled = true;
                        _bg.GetComponent<UIShiny>().Play();
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
                    _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
                    _bg.GetComponent<UIShiny>().enabled = false;
                }
                _bg.gameObject.SetActive(true);
                break;
            case TileState.Error:
                //Debug.LogWarning($"更新词: {Answer} " + JsonUtility.ToJson(chesspiece));
                SetDisplayContent(chesspiece.bowl.letter, chesspiece.bowl.isEmoji, Color.red);
                _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
                _bg.gameObject.SetActive(true);
                // 红框节点是 WT 新增的，HexWord 的棋子预制体暂无该节点，需做空保护
                if (_errorFrame != null)
                {
                    _errorFrame.SetActive(true);
                    Canvas errCanvas = _errorFrame.GetComponent<Canvas>();
                    if (errCanvas == null) errCanvas = _errorFrame.AddComponent<Canvas>(); // 兜底：如果预制体没挂Canvas，代码自动挂载

                    errCanvas.overrideSorting = true;
                    // 保持和光标在同一个 Layer
                    errCanvas.sortingLayerName = UIPanelLayer.BasePanel;
                    // 设为 4 (你的光标 sortingOrder 是 5，这样当光标选中错误格子时，光标依然能盖在错误框上面)
                    errCanvas.sortingOrder = 4;
                }
                break;
            case TileState.Success:
                Color successColor = (chesspiece.bowl != null && _isGoldLeaf && chesspiece.bowl.totalcount <= 1) 
                    ? new Color32(100, 80, 66, 255) : Color.white;
                SetDisplayContent(Answer, false, successColor);
                if (chesspiece.bowl!=null)
                {
                    if (_isGoldLeaf)
                    {
                        if (chesspiece.bowl.totalcount > 1)
                        {
                            _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
                            _bg.GetComponent<UIShiny>().enabled = false;
                            //chesspiece.bowl.isGoldLeaf = false;
                        }
                        else
                        {
                            _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
                            _bg.GetComponent<UIShiny>().enabled = true;
                            _bg.GetComponent<UIShiny>().Play();
                            
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
                        _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
                    }
                    
                }
                else
                {
                    _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
                }
                _bg.gameObject.SetActive(true);
                break;
        }
        if (!lateChose) 
            SetChoose(CurrState == TileState.Check);

        if(chesspiece.tip)
        {
            SetDisplayContent(Answer, chesspiece.isEmoji, Color.white, true);
            bool showTip = CurrState is TileState.Check or TileState.None;
            if (_tipText != null) _tipText.gameObject.SetActive(showTip && !chesspiece.isEmoji);
            if (_emojiDisplay != null) _emojiDisplay.gameObject.SetActive(showTip && chesspiece.isEmoji);
        }
        if (chesspiece.hasFlower)
        {
            _flowerObj.GetComponent<Image>().enabled = true;
            // 利用关卡ID判断奇偶数，实现轮换 (1 或 2)
            int stageId = ChessStageController.Instance.CurrStageData.StageId;
            int flowerSkinIndex = ((stageId / 5) % 2 == 0) ? 1 : 2;
            if (stageId == 62) flowerSkinIndex = 1;
            if (stageId == 66) flowerSkinIndex = 1;
            // 1. 替换静态 Image 贴图 (请确保图集里的名字叫 flower_skin_01 和 flower_skin_02)
            Image flowerImg = _flowerObj.GetComponent<Image>();
            if (flowerImg != null)
            {
                flowerImg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas($"flower_skin_0{flowerSkinIndex}");
            }
            // 2. 切换对应的粒子特效节点
            Transform hua1 = _flowerObj.transform.Find("hua_01");
            Transform hua2 = _flowerObj.transform.Find("hua_02");
            if (hua1 != null) hua1.gameObject.SetActive(flowerSkinIndex == 1);
            if (hua2 != null) hua2.gameObject.SetActive(flowerSkinIndex == 2);
            _textDisplay.text = Answer.ToString();
            Color c = _textDisplay.color;
            c.a = 0f;
            _textDisplay.color = c;
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
            _leafObj.SetActive(false); // 不长叶子了就彻底关掉GameObject！
        }
        if (CoinObj != null)
        {
            // 规则：有金币 && 还没变绿 → 显示；Success 后隐藏
            bool showCoin = chesspiece.hasGold;
            CoinObj.SetActive(showCoin);
            if (showCoin)
            {
                //  核心规则：如果有字（填入/报错/变绿），金币透明度设为半透明以看清底下文字
                CanvasGroup cg = CoinObj.GetComponent<CanvasGroup>() ?? CoinObj.AddComponent<CanvasGroup>();
                bool hasText = CurrState == TileState.Fill || CurrState == TileState.Error || CurrState == TileState.Success;
                cg.alpha = hasText ? 0.35f : 1.0f;
            }
        }
        if (CurrState == TileState.Success) StopFocusAnim();
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
    
        AudioManager.Instance.PlaySoundEffect("GoldLeafFly",0); // ⚠️ 请替换为金箔起飞真实音效名
        
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
        if (IsLocked || !PassDebounce() || ChessPlayArea.Instance.chessboardGrid.IsBlockInput) return;
        if (chesspiece.hasIce) return;
        ChessPlayArea.Instance?.NotifyPlayerInteraction();
        
        _isProcessingInteraction = true;
        TileTransform.DOScale(1.15f, 0.1f).SetEase(Ease.OutQuad);
        AudioManager.Instance.PlaySoundEffect("WordClick");
        AudioManager.Instance.TriggerVibration(40,40);
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
        // 兜底：格子被对象池回收时，打断动画并强制归位
        if (CoinObj != null)
        {
            CoinObj.transform.DOKill();
            CoinObj.transform.localPosition = Vector3.zero; 
        }
        UIShiny shiny = GetComponentInChildren<UIShiny>(true);
        if (shiny != null)
        {
            shiny.Stop();
            shiny.enabled = false;
        }
    }
    
    /// <summary>
    /// 同步变色、放大、播放粒子（无波浪延迟）
    /// </summary>
    /// <param name="duration">特效持续总时长，保证和外框一模一样</param>
    /// <param name="onStart">瞬间变绿的回调</param>
    public void PlaySuccessAnimation(float duration, Action onStart = null)
    {
        TileTransform.DOKill();
        
        Sequence seq = DOTween.Sequence();
        seq.SetLink(gameObject); // 🌟 添加防报错护盾
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
        // seq.Append(TileTransform.DOScale(1.15f, 0.15f).SetEase(Ease.OutQuad));
        
        // 2. 悬停在这个大小，等待粒子和发光框播完 (耗时 = 总时长 - 放大和缩回的 0.3 秒)
        // float holdTime = Mathf.Max(0f, duration - 0.3f);
        // Debug.Log("发光框播完时间是 " + holdTime);
        // seq.AppendInterval(holdTime);
        
        // 3. 完美缩回原状 (耗时 0.15秒)
        // seq.Append(TileTransform.DOScale(1f, 0.15f).SetEase(Ease.InQuad));
    }

    public void PlayRevealAnimation1(Transform index)
    {
        StartCoroutine(PlayRevealAnimation(index));
    }
    public IEnumerator PlayRevealAnimation(Transform index)
    {
        // 3. 特效实例化与定位
        var effectInstance = ChessPlayArea.Instance.chessboardGrid.TipsEffectPool.GetObject(transform.parent);
        effectInstance.transform.position = index.position;

        // 4. 添加随机旋转增加差异化
        effectInstance.transform.Rotate(0, 0, UnityEngine.Random.Range(-5, 5));

        // 5. 文字显示动画序列
        var sequence = DOTween.Sequence();
        sequence.SetLink(gameObject);
        // 第一阶段：特效展示期间
        sequence.AppendInterval(0.2f); // 比原版稍短的等待

        // 6. 文字显示带动画效果
        sequence.AppendCallback(() =>
        {
            SetTipMessage();
            // emoji 显示节点缺失时退回文字节点，避免空引用
            Transform activeTipTrans = (chesspiece.isEmoji && _emojiDisplay != null)
                ? _emojiDisplay.transform
                : _tipText.transform;
            // 添加缩放动画
            activeTipTrans.localScale = Vector3.zero;
            activeTipTrans.DOScale(Vector3.one, 0.3f)
                .SetEase(Ease.OutBack);
        });

        // 7. 特效自动销毁计时器
        sequence.AppendInterval(3.5f); // 比原版更早销毁特效
        sequence.AppendCallback(() => {
            if (effectInstance != null)
            {
                effectInstance.transform.DOScale(Vector3.zero, 0.2f)
                    .OnComplete(() =>   ChessPlayArea.Instance.chessboardGrid.TipsEffectPool.ReturnObjectToPool(effectInstance.GetComponent<PoolObject>()));
            }
        });
        if (effectInstance != null)
        {
            sequence.Append(effectInstance.transform.DOScale(Vector3.zero, 0.2f));
        }

        yield return sequence.WaitForCompletion();

        // 8. 确保特效被清理
        if (effectInstance != null && effectInstance.activeInHierarchy)
        {
            ChessPlayArea.Instance.chessboardGrid.TipsEffectPool.ReturnObjectToPool(effectInstance.GetComponent<PoolObject>());
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
        iceLogicBroken = false;   //  动画结束，清除逻辑标记
    }
    
    // 2. 花朵逻辑
    public IEnumerator PlayFlowerBloomAnim()
    {
        chesspiece.hasFlower = false;
        _flowerObj.GetComponent<Image>().enabled = false;
        // 播放花朵绽放、消失的动画
        // _flowerObj.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack);
        _flowerObj.GetComponentInChildren<ParticleSystem>(false).Play();
        yield return new WaitForSeconds(0.25f);
        if (chesspiece.isEmoji && _emojiDisplay != null)
        {
            Color imgCol = Color.white; 
            imgCol.a = 0; 
            _emojiDisplay.color = imgCol;
            _emojiDisplay.gameObject.SetActive(true); 
            _emojiDisplay.DOFade(1f, 0.4f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                UpdateTile(true); 
                _flowerObj.SetActive(false);
            });
        }
        else if (_textDisplay != null)
        {
            // 确保文字此时是激活状态且颜色 alpha 为 0
            Color textCol = new Color32(100, 80, 66, 0); 
            _textDisplay.color = textCol;
            
            // 使用 DOTween 让文字平滑淡入（耗时 0.4 秒，可自行调整）
            if (CurrState == TileState.Success)
            {
                UpdateTile(true); 
                _flowerObj.SetActive(false);
            }
            else
            {
                _textDisplay.DOFade(1f, 0.4f).SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        UpdateTile(true); 
                        _flowerObj.SetActive(false);
                    });  
            }
           
        }
        // if(fadeTween != null) yield return fadeTween.WaitForCompletion();
        yield return null;
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
                leafImg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas($"leaf_skin_0{skinIndex}");
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
    /// 树叶在整组填完失败时的“枯萎/缩小隐藏”失败动画
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
    
    /// <summary>
    /// 触发单格子的持续扫光特效
    /// </summary>
    public void PlayHintShiny(float duration = 5f)
    {
        StartCoroutine(HandleShinyRoutine(duration));
    }

    private IEnumerator HandleShinyRoutine(float duration)
    {
        UIShiny shiny = GetComponentInChildren<UIShiny>(true);
        if (shiny == null) yield break;

        shiny.enabled = true;
        shiny.Play(); 

        // 核心防御：记录当前填入的字块数据，防止格子被对象池回收复用后依然在发光
        var currentPiece = chesspiece;

        yield return new WaitForSeconds(duration);

        // 安全检查：确认格子没有被销毁且字块没有被替换
        if (this != null && shiny != null && chesspiece != null && chesspiece.Equals(currentPiece))
        {
            if (!_isGoldLeaf) 
            {
                shiny.enabled = false;
                shiny.Stop(); 
            }
        }
    }
    
    /// <summary>
    ///  播放最后词组的波浪聚焦动画
    /// </summary>
    /// <param name="delay">延迟时间（用于制造波浪差）</param>
    public void PlayFocusWaveAnim(float delay)
    {
        if (_focusHaloObj == null) return;
        
        _focusHaloObj.SetActive(true);
        _focusHaloObj.transform.DOKill(); // 清理旧动画
        
        CanvasGroup cg = _focusHaloObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = _focusHaloObj.AddComponent<CanvasGroup>();

        // 初始状态：缩小到0，完全透明
        _focusHaloObj.transform.localScale = Vector3.zero;
        cg.alpha = 0f;

        Sequence seq = DOTween.Sequence();
        seq.SetLink(gameObject); // 防报错护盾
        seq.SetDelay(delay);     // 🌟 核心：波浪延迟

        // 1. 由小到大，同时淡入 (带点 Q 弹效果)
        seq.Append(_focusHaloObj.transform.DOScale(1.1f, 0.4f).SetEase(Ease.OutBack));
        seq.Join(cg.DOFade(1f, 0.4f));

        // 2. 出现后，保持轻微的呼吸律动，持续吸引注意力
        seq.Append(_focusHaloObj.transform.DOScale(0.99f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
        seq.Join(cg.DOFade(0.6f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
    }

    /// <summary>
    /// 关闭聚焦动画 (填入字时调用)
    /// </summary>
    public void StopFocusAnim()
    {
        if (_focusHaloObj != null && _focusHaloObj.activeSelf)
        {
            _focusHaloObj.transform.DOKill();
            CanvasGroup cg = _focusHaloObj.GetComponent<CanvasGroup>();
            
            // 快速淡出后隐藏
            if (cg != null) cg.DOFade(0f, 0.2f).OnComplete(() => _focusHaloObj.SetActive(false));
            else _focusHaloObj.SetActive(false);
        }
    }
    
    private static Material _radarRevealMat;
    // 开启雷达预览状态 (被道具触发时调用)
    public void EnableRadarPreview()
    {
        if (CurrState == TileState.Success) return; // 已经填对的不用管
        // 雷达预览节点是 WT 新增的，HexWord 的棋子预制体暂无，缺节点时直接跳过
        if (_radarBg == null) return;
        
        // 1. 如果专属遮罩材质还没创建，动态创建它
        if (_radarRevealMat == null)
        {
            _radarRevealMat = new Material(Shader.Find("UI/Default"));
            // 强行写入 Stencil 参数：1 代表 ID，3 代表 Equal (只有在雷达扫过时才渲染)
            _radarRevealMat.SetInt("_Stencil", 1);
            _radarRevealMat.SetInt("_StencilComp", 3);
        }
        _radarBg.maskable = false;
        _radarBg.gameObject.SetActive(true);
        _radarBg.material = _radarRevealMat;
        
        bool isEmojiActive = chesspiece.isEmoji && GameDataManager.Instance.UserData.IsEmojiOn;
        
        if (isEmojiActive)
        {
            if (_radarText != null) _radarText.gameObject.SetActive(false);
            if (_radarEmojiDisplay != null)
            {
                _radarEmojiDisplay.gameObject.SetActive(true);
                _radarEmojiDisplay.maskable = false;
                _radarEmojiDisplay.material = _radarRevealMat;
                _radarEmojiDisplay.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(Answer);
            }
        }
        else
        {
            if (_radarEmojiDisplay != null) _radarEmojiDisplay.gameObject.SetActive(false);
            if (_radarText != null)
            {
                _radarText.gameObject.SetActive(true);
                _radarText.maskable = false;
                _radarText.material = _radarRevealMat;
                _radarText.text = Answer.ToString();
            }
        }
    }
    
    // 关闭雷达预览 (扫描结束时调用)
    public void DisableRadarPreview()
    {
        if (_radarBg == null)
        {
            UpdateTile();
            return;
        }

        // 1. 恢复默认的 UI 材质 (传 null 即可恢复默认)
        _radarBg.material = null;
        if (_radarText != null) _radarText.material = null;
        if (_radarEmojiDisplay != null) _radarEmojiDisplay.material = null;
        // 2. 恢复 Maskable 属性
        _radarBg.maskable = true;
        if (_radarText != null) _radarText.maskable = true;
        if (_radarEmojiDisplay != null) _radarEmojiDisplay.maskable = true;
        
        if (_radarEmojiDisplay != null) _radarEmojiDisplay.gameObject.SetActive(false);
        _radarBg.gameObject.SetActive(false);
        if (_radarText != null) _radarText.gameObject.SetActive(false);
        // 刷新回格子真实的逻辑状态
        UpdateTile(); 
    }
    
    /// <summary>
    /// 独立的方法供棋盘在生成时调用
    /// </summary>
    public void ShowGold(bool show)
    {
        chesspiece.hasGold = show;
        if (CoinObj != null)
        {
            CoinObj.SetActive(show);
            if (show)
            {
                CoinObj.transform.localPosition = Vector3.zero;
                CanvasGroup cg = CoinObj.GetComponent<CanvasGroup>() ?? CoinObj.AddComponent<CanvasGroup>();
                bool hasText = CurrState == TileState.Fill || CurrState == TileState.Error || CurrState == TileState.Success;
                cg.alpha = hasText ? 0.35f : 1.0f;
            }
        }
    }
    
    /// <summary>
    /// 播放金币被收集的Q弹消失动画
    /// </summary>
    public void PlayGoldCollectAnim(float offsetX, Action onComplete = null)
    {
        if (CoinObj == null || !CoinObj.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        CoinObj.transform.DOKill();
        CoinObj.transform.localScale = Vector3.one;

        Sequence seq = DOTween.Sequence();
        seq.SetLink(gameObject);
        // 恢复不透明度
        CanvasGroup cg = CoinObj.GetComponent<CanvasGroup>();
        if (cg != null) seq.Append(cg.DOFade(1f, 0.2f));
        // 1. 向上跳起到词组上方 (使用 OutBack 带一点Q弹阻尼感)
        seq.Join(CoinObj.transform.DOLocalMove(
            new Vector3(CoinObj.transform.localPosition.x + offsetX, CoinObj.transform.localPosition.y + 140f, 0), 
            0.25f)
            .SetEase(Ease.OutBack));
        // 2. 滞空停留 1 秒
        seq.AppendInterval(0.15f);
        
        seq.OnComplete(() =>
        {
            onComplete?.Invoke();
        });
    }
    
    /// <summary>
    /// 提供当前金币弹起后的真实坐标系位置
    /// </summary>
    public Transform GetCoinTransform()
    {
        return CoinObj != null ? CoinObj.transform : transform;
    }

    /// <summary>
    /// 隐藏金币本体
    /// </summary>
    public void HideCoin()
    {
        if (CoinObj != null) CoinObj.SetActive(false);
    }
}
