using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 基础字块控制器
/// 管理基础选中状态和动画效果
/// 版本：1.0
/// </summary>
public class TileView : MonoBehaviour
{
    [Header("核心组件")]
    [SerializeField] private GameObject tipPuzzle;   // 提示字块
    [SerializeField] private GameObject selectionPuzzle;   // 选中字块
    [SerializeField] private GameObject starttile;        // 默认字块
    [SerializeField] private GameObject showTipObj;        // 默认字块
    public Text _textDisplay;  // 文字显示

    [Header("动画设置")]
    [SerializeField] private float _scaleAnimDuration = 0.08f; // 缩放动画时长
    
    [SerializeField] private float shakeDuration = 0.5f;           // 抖动动画持续时间
    private float initialShakeAmplitude = 3f;                      // 初始抖动幅度
    private float amplitudeDecayFactor = 0.8f;                     // 抖动幅度衰减系数
    private float initialShakeTime = 0.09f;                        // 初始移动时间
    private float durationDecayFactor = 0.7f;                      // 移动时间衰减系数
    private int shakeCount = 4;                                    // 抖动次数
    
    private bool isselection = false;                              // 是否选中字块

    // 基础属性
    private Vector2 _baseScale;      // 原始尺寸
    [HideInInspector] public Vector2 _startPosition;  // 初始位置
    private RectTransform _rectTrans;

    public RectTransform TileTransform => _rectTrans ??= transform as RectTransform;
    
    // 存储tween引用以便管理
    private Sequence fadeTween;

    /// <summary>
    /// 初始化字块显示
    /// </summary>
    public void SetupCharacter(char character)
    {
        ShowElement();
        // 设置显示内容
        _textDisplay.text = character.ToString();
        starttile.SetActive(character != '\0');
        tipPuzzle.SetActive(false);
        // 记录初始状态
        _baseScale = transform.localScale;
        _startPosition = TileTransform.anchoredPosition;

        showTipObj.SetActive(false);
        
        // 重置特效状态
        showTipObj.GetComponentInChildren<Text>().text = character.ToString();
        selectionPuzzle.GetComponentInChildren<Text>().text = character.ToString();
        selectionPuzzle.GetComponent<CanvasGroup>().DOFade(0, 0);
        tipPuzzle.GetComponentInChildren<Text>().text = character.ToString();
        SetSelectionState(false);
        
        StopPulseAnimation();
    }
    
    /// <summary>
    /// 初始化字块显示
    /// </summary>
    public void DownCharSetCharacter(char character)
    {
        ShowElement();
        // 设置显示内容
        _textDisplay.text = character.ToString();
        starttile.SetActive(character != '\0');
        tipPuzzle.SetActive(false);
        // 记录初始状态
        _baseScale = transform.localScale;
        TileTransform.anchoredPosition= new Vector2(_startPosition.x,_startPosition.y-20);
        showTipObj.SetActive(false);
        
        // 重置特效状态
        showTipObj.GetComponentInChildren<Text>().text = character.ToString();
        selectionPuzzle.GetComponentInChildren<Text>().text = character.ToString();
        selectionPuzzle.GetComponent<CanvasGroup>().DOFade(0, 0);
        tipPuzzle.GetComponentInChildren<Text>().text = character.ToString();
        
        StopPulseAnimation();

        TileTransform.DOAnchorPos(_startPosition, 0.5f);
    }

    /// <summary>
    /// 更新选中状态
    /// </summary>
    public void SetSelectionState(bool isSelected, bool isInstant = false)
    {
        selectionPuzzle.SetActive(isSelected);       

        if (isInstant && isSelected)
        {
            selectionPuzzle.SetActive(true);
            PlaySelectAnimation();
        }       

        if (!isSelected&&!isInstant&&isselection)
        {
            // 向上移动效果（相对移动）
            TileTransform.DOAnchorPosY(_startPosition.y, 0.2f).OnComplete(()=>
                selectionPuzzle.SetActive(false));     
        }
    }

    /// <summary>
    /// 播放选中动画
    /// </summary>
    private void PlaySelectAnimation()
    {
        TileTransform.GetComponent<RectTransform>().anchoredPosition = _startPosition;
        // 缩放效果
        //transform.DOScale(_baseScale * 0.96f, 0.2f)
        //    .OnComplete(() => transform.DOScale(_baseScale * 1.1f, 0.2f).OnComplete(() =>
        //    {
        //        transform.DOScale(_baseScale, 0.2f);
        //    }));

        selectionPuzzle.GetComponent<CanvasGroup>().DOFade(1, 0.01f);
            TileTransform.DOAnchorPosY(_startPosition.y+5, 0.2f);

        // 向上移动效果（相对移动）
        isselection = true;
        // 触觉反馈
        TriggerVibration();
    }
    
    /// <summary>
    /// 触发振动效果
    /// </summary>
    private void TriggerVibration()
    {
        AudioManager.Instance.TriggerVibration(1, 10);
    }

    /// <summary>
    /// 触发错误状态
    /// </summary>
    public void TriggerErrorState(bool isX)
    {
        StartCoroutine(PlayErrorAnimation(isX));
    }

    /// <summary>
    /// 错误抖动动画
    /// </summary>
    private IEnumerator PlayErrorAnimation(bool isX)
    {
        //yield return new WaitForSeconds(0.01f);
        TileTransform.DOAnchorPosY(_startPosition.y, 0.2f);
        yield return new WaitForSeconds(0.02f);
        //TileTransform.anchoredPosition = _startPosition;
        // for (int i = 0; i < shakeCount; i++)
        // {
        //     // 计算当前抖动参数
        //     float amplitude = initialShakeAmplitude * Mathf.Pow(amplitudeDecayFactor, i);
        //     float duration = initialShakeTime * Mathf.Pow(durationDecayFactor, i);
        //     
        //     // if (isX)
        //     // {
        //         float targetX = _startPosition.x + (i % 2 == 0 ? -amplitude : amplitude);
        //         TileTransform.DOLocalMoveX(targetX, duration).SetEase(Ease.Linear);
        //         //TileTransform.DOAnchorPosX(_startPosition.x + offset, _scaleAnimDuration);
        //     // }
        //     // else
        //     // {
        //     //     float targety = _startPosition.y + (i % 2 == 0 ? -amplitude : amplitude);
        //     //     TileTransform.DOAnchorPosY(targety, duration);
        //     // }
        //     yield return new WaitForSeconds(duration);
        // }
         TileTransform.anchoredPosition = _startPosition;
    }
    
    /// <summary>
    /// 显示字块
    /// </summary>
    public void ShowElement()
    {
        _textDisplay.gameObject.SetActive(true);
        transform.gameObject.SetActive(true);
    }

    /// <summary>
    /// 显示字块
    /// </summary>
    public void ShowTipPuzzle()
    {
        _textDisplay.gameObject.SetActive(true);
        transform.gameObject.SetActive(true);
        tipPuzzle.gameObject.transform.localScale = new Vector3(0.6f, 0.6f, 1);
        tipPuzzle.gameObject.transform.localPosition = new Vector3(0, -10, 0);
        tipPuzzle.gameObject.SetActive(true);
        tipPuzzle.transform.DOScale(Vector3.one, 0.2f);
        tipPuzzle.gameObject.transform.DOLocalMoveY(0f, 0.2f);
    }
    
    /// <summary>
    /// 显示字块
    /// </summary>
    public void ShowNoDoTipPuzzle()
    {
        _textDisplay.gameObject.SetActive(true);
        transform.gameObject.SetActive(true);
        showTipObj.gameObject.SetActive(true);
        
        //showTipObj.GetComponent<Image>().sprite = starttile.GetComponent<Image>().sprite;
        
        //showTipObj.GetComponent<Image>().color = new Color(228, 255, 250, 255);
        
        // 确保CanvasGroup初始状态
        CanvasGroup canvasGroup = showTipObj.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
    
        // 创建序列动画
        Sequence pulseSequence = DOTween.Sequence();
    
        // 淡入
        pulseSequence.Append(canvasGroup.DOFade(1, 0.3f).SetEase(Ease.Linear));
    
        pulseSequence.AppendInterval(1f);
        // 淡出
        pulseSequence.Append(canvasGroup.DOFade(0, 0.3f).SetEase(Ease.Linear));
        
        pulseSequence.AppendInterval(1f);
        // 设置无限循环
        pulseSequence.SetLoops(-1, LoopType.Restart);
        
        // 存储引用以便后续管理
        fadeTween = pulseSequence;
        
    }

    /// <summary>
    /// 隐藏字块
    /// </summary>
    public void HideElement()
    {
        StopPulseAnimation();
        _textDisplay.gameObject.SetActive(false);
        transform.gameObject.SetActive(false);
    }

    //public void SetDownLayer()
    //{
    //    //starttile.GetComponent<Image>().DOColor(Color.gray, 1.2f);
    //}

    //public void SetTopLayer()
    //{
    //    //starttile.GetComponent<Image>().DOColor(Color.white,0.5f);
    //}
    
    /// <summary>
    /// 隐藏字块
    /// </summary>
    public void HideChoice()
    {
        selectionPuzzle.GetComponent<CanvasGroup>().DOFade(0, 0.01f).OnComplete(() =>
        {
            // 向上移动效果（相对移动）
            TileTransform.DOAnchorPosY(_startPosition.y, 0.2f);
            selectionPuzzle.SetActive(false);
        });
        
        TileTransform.anchoredPosition = _startPosition;
        AudioManager.Instance.TriggerVibration();
    }

    /// <summary>
    /// 移动到新位置
    /// </summary>
    public void MoveToPosition(Vector2 newPos, float duration = 0.2f,System.Action<GameObject> animFinished=null)
    {
        TileTransform.DOAnchorPos(newPos, duration);
        _startPosition = newPos;
        animFinished?.Invoke(gameObject);
        //position = toPos;
    }
    
    // 停止动画
    public void StopPulseAnimation()
    {
        showTipObj.gameObject.SetActive(false);
        if (fadeTween != null && fadeTween.IsActive())
        {
            fadeTween.Kill();
        }
        fadeTween = null;
    }

    /// 当对象禁用时停止动画
    private void OnDisable()
    {
        StopPulseAnimation();
    }
}