using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    [SerializeField] private Image _bg;            // 背景图
    [SerializeField] private GameObject _choose;   // 选择框
    [SerializeField] private Text _tipText;  // 提示文本

    [SerializeField] private Text _score; // 提示分数
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

    public Vector2 startPosition;  // 原始位置
    private RectTransform _rectTrans;
    private bool _isProcessingInteraction; 
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
        
        chesspiece = pz;
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
        UpdateTile();
        SetScore(0);
        IsOK = false;
        ShowButterflyPupa(false);
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
            _score.gameObject.SetActive(GameCoreManager.Instance.IsTrueAuto);
        }
#else
    _score.gameObject.SetActive(false);
#endif
    }
    /// <summary>
    /// 更新方块当前显示
    /// </summary>
    public void UpdateTile(bool lateChosse = false)
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
                _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
                _bg.gameObject.SetActive(true);
                break;
            case TileState.Fill:
                if (chesspiece.bowl != null)
                {
                    _textDisplay.text = chesspiece.bowl.letter;
                    _textDisplay.color = new Color32(100,80,66,255);
                }
                _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
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
                _textDisplay.color = Color.white;
                _bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
                _bg.gameObject.SetActive(true);
                _score.gameObject.SetActive(false);
                break;
        }
        if (!lateChosse) 
            SetChoose(CurrState == TileState.Check);

        if(chesspiece.tip)
        {
            _tipText.text = Answer.ToString();
            _tipText.gameObject.SetActive(true);
        }
           // Debug.Log("当前更新的对象 "+ Answer +" -> " + CurrState);
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
    }
    
    /// <summary>
    /// 显示蝉蛹
    /// </summary>
    public void ShowButterflyPupa(bool show = false)
    {
        GameObject butterflyPupa = transform.Find("PupaObj").gameObject;
        // butterflyPupa.GetComponent<Canvas>().sortingLayerName = UIPanelLayer.BasePanel;
        // butterflyPupa.GetComponent<Canvas>().sortingOrder = 2;
        butterflyPupa.SetActive(show);
        
        //if(show) _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("pupabg");
        if(show)  _textDisplay.color = Color.white;
        else
        {
            UpdateTile();
        }
    }

    public bool GetPupaObjIsShow()
    {
        GameObject butterflyPupa = transform.Find("PupaObj").gameObject;
        return butterflyPupa.activeSelf;
    }
    
    /// <summary>
    /// 播放成功时的波浪弹跳动画
    /// </summary>
    /// <param name="delay">延迟时间（制造依次起跳的波浪感）</param>
    /// <param name="onStart">动画起跳瞬间的回调（用来瞬间变绿）</param>
    public void PlaySuccessAnimation(float delay, Action onStart = null)
    {
        // 杀掉之前的缩放动画，防止冲突
        TileTransform.DOKill();
        
        Sequence seq = DOTween.Sequence();
        seq.SetDelay(delay); // 根据字在词组里的位置，排队等待起跳
        
        seq.OnStart(() => {
            // 动画开始的瞬间，切换为绿色的成功贴图
            onStart?.Invoke();
            // 🌟极其重要：把当前放大起跳的格子提到渲染层最上面，防止被旁边的格子遮挡！
            TileTransform.SetAsLastSibling(); 
        });
        
        // 放大到 1.2倍，然后紧接着缩回正常 1倍 (时间可以自己微调)
        seq.Append(TileTransform.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad));
        seq.Append(TileTransform.DOScale(1f, 0.15f).SetEase(Ease.InQuad));
    }
    
    /// <summary>
    /// 播放边缘高亮闪烁效果 (用于提示道具)
    /// </summary>
    public void PlayHighlightEffect()
    {
        _choose.SetActive(true);
        Image chooseImg = _choose.GetComponent<Image>();
        
        // 确保颜色初始是透明的
        Color c = chooseImg.color;
        c.a = 0f;
        chooseImg.color = c;

        Sequence seq = DOTween.Sequence();
        // 瞬间提层级，防止被挡住
        TileTransform.SetAsLastSibling(); 
        
        // 1. 格子本身稍微放大弹一下
        seq.Append(TileTransform.DOScale(1.15f, 0.15f).SetEase(Ease.OutQuad));
        // 同时边缘光效淡入
        seq.Join(chooseImg.DOFade(1f, 0.15f));
        
        // 2. 缩回去，同时边缘光效淡出
        seq.Append(TileTransform.DOScale(1f, 0.2f).SetEase(Ease.InQuad));
        seq.Join(chooseImg.DOFade(0f, 0.2f));

        seq.OnComplete(() => {
            _choose.SetActive(false); // 播完隐藏
            c.a = 1f; // 恢复默认透明度供后续选中逻辑使用
            chooseImg.color = c;
        });
        Debug.LogError("进来了吗 ");
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
}
