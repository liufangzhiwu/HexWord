using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


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

    // 错误动画配置
    private readonly float initialShakeAmplitude = 8f;                      // 初始抖动幅度
    private readonly float amplitudeDecayFactor = 0.8f;                     // 抖动幅度衰减系数
    private readonly float initialShakeTime = 0.09f;                        // 初始移动时间
    private readonly float durationDecayFactor = 0.7f;                      // 移动时间衰减系数
    private readonly int shakeCount = 4;                                    // 抖动次数
    
    public Chesspiece chesspiece;   // 生成的格子属性

    // 基础属性
    [HideInInspector] public string Id => chesspiece.id;
    [HideInInspector] public int Row => chesspiece.row;
    [HideInInspector] public int Col => chesspiece.col;
    [HideInInspector] public string Answer => chesspiece?.letter ?? "";        // 正确答案
    [HideInInspector] public int Direction => chesspiece.direction;     // 排列方向
    [HideInInspector] public TileState CurrState => chesspiece.state;   // 当前状态


    private Vector2 _startPosition; // 初始位置
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
            7 => AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_162"),
            8 => AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_142"),
            9 => AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_126"),
            _ => AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Highlight_162")   // 更大也按最小格
        };
        //_choose.GetComponent<Image>().SetNativeSize();
        UpdateTile();
    }

    /// <summary>
    /// 设置格子状态
    /// </summary>
    /// <param name="state">状态</param>
    public void SetTileState(TileState state, bool update = true)
    {
        chesspiece.state = state;
        //if (state == TileState.None || state == TileState.Check)
        //    chesspiece.bowl = null;
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
                _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
                _bg.gameObject.SetActive(true);
                break;
            case TileState.Fill:
                if (chesspiece.bowl != null)
                {
                    _textDisplay.text = chesspiece.bowl.letter;
                    _textDisplay.color = new Color32(100,80,66,255);
                }
                _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
                _bg.gameObject.SetActive(true);
                break;
            case TileState.Error:
                //Debug.LogWarning($"更新词: {Answer} " + JsonUtility.ToJson(chesspiece));
                 _textDisplay.text = chesspiece.bowl.letter;
                 _textDisplay.color = Color.red;
                _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
                _bg.gameObject.SetActive(true);
                break;
            case TileState.Success:
                _textDisplay.text = Answer.ToString();
                _textDisplay.color = Color.white;
                _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
                _bg.gameObject.SetActive(true);
                break;
        }
        if (!lateChosse) 
            SetChoose(CurrState == TileState.Check);

        if(chesspiece.tip)
        {
            _tipText.text = Answer.ToString();
            _tipText.gameObject.SetActive(true);
        }
           
    }
    #region 点击事件
    /// <summary>
    /// 错误抖动动画
    /// </summary>
    public IEnumerator PlayErrorAnimation(bool isX)
    {
        for (int i = 0; i < shakeCount; i++)
        {
            // 计算当前抖动参数
            float amplitude = initialShakeAmplitude * Mathf.Pow(amplitudeDecayFactor, i);
            float duration = initialShakeTime * Mathf.Pow(durationDecayFactor, i);

            if (isX)
            {
                float targetX = _startPosition.x + (i % 2 == 0 ? -amplitude : amplitude);
                TileTransform.DOLocalMoveX(targetX, duration).SetEase(Ease.Linear);
                //TileTransform.DOAnchorPosX(_startPosition.x + offset, _scaleAnimDuration);
            }
            else
            {
                float targety = _startPosition.y + (i % 2 == 0 ? -amplitude : amplitude);
                TileTransform.DOAnchorPosY(targety, duration);
            }
            yield return new WaitForSeconds(duration);
        }
        TileTransform.anchoredPosition = _startPosition;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (IsLocked || !_isProcessingInteraction) return;

        transform.DOScale(1f, 0.1f).OnComplete(() =>
        {
            // TileTransform.anchorMax = Vector2.zero;
            // TileTransform.anchorMin = Vector2.zero;
            // TileTransform.pivot = Vector2.zero;
            // TileTransform.anchoredPosition = Vector2.zero;
        });
        //TileTransform.anchorMax = Vector2.zero;
        //TileTransform.anchorMin = Vector2.zero;
        //TileTransform.pivot = Vector2.zero;
        OnSelectHandler?.Invoke(this);
        _isProcessingInteraction = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsLocked || !PassDebounce()) return;   // 防抖
        
        _isProcessingInteraction = true;
        
        // Vector2 center = Vector2.one * 0.5f;
        // TileTransform.anchorMin = center;
        // TileTransform.anchorMax = center;
        // TileTransform.pivot     = center;
        // TileTransform.anchoredPosition = Vector2.zero;
        
        transform.DOScale(1.15f, 0.1f);
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
}
