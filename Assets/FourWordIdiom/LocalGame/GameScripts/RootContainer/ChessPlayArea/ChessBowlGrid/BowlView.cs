using DG.Tweening;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 格子状态
/// </summary>
public enum PuzzleState
{
    NORMAL,
    GHOST,
}
[Serializable]
public class BowlView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public delegate void ClickHandler(BowlView data);
    [HideInInspector] public event ClickHandler OnClickHandler;
    [Header("UI组件")]
    [SerializeField] private Text _textDisplay;    // 文字显示
    [SerializeField] private GameObject _mesk;     // 蒙版覆盖

    [JsonIgnore]
    [HideInInspector] public string letter => bowl?.letter ?? "";  // 生成的字
    [JsonIgnore]
    [HideInInspector] public bool locked => bowl.status == 1;   // 是否锁定

    public Bowl bowl { get; private set; }        // 设置的词
    //private ChessBowlGrid bowlGrid;               // 父类状态

    private void Awake()
    {
        _mesk.SetActive(false);
    }

    public void Setup(Bowl bowl, ChessBowlGrid bowlGrid)
    {
        this.bowl = bowl;
        //this.bowlGrid = bowlGrid;

        _textDisplay.text = bowl.letter.ToString();
        _mesk.SetActive(locked);
        if(bowl.status == 2 )
        {
            gameObject.SetActive(false);
        }
    }
    public void FlyToCell(ChessView tile, Transform parent, Action onComplete)
    {
        
        RectTransform selfRT = GetComponent<RectTransform>();

        GameObject clone = Instantiate(gameObject, parent.parent, true);
        RectTransform cloneRT = clone.GetComponent<RectTransform>();
        Canvas canvas = clone.GetComponent<Canvas>();
        if(canvas == null ) 
            canvas = clone.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerName = UIPanelLayer.TipsPanel;
        canvas.sortingOrder = 10;
        
        // 2. 一次性复制原尺寸+锚点
        cloneRT.anchorMin = new Vector2(0.5f,0.5f);
        cloneRT.anchorMax = new Vector2(0.5f,0.5f);
        cloneRT.sizeDelta = selfRT.sizeDelta;
        cloneRT.pivot = selfRT.pivot;
        cloneRT.localScale = selfRT.localScale * 1.1f;
        clone.transform.position = selfRT.position;
        Vector3 endWorld = tile.TileTransform.TransformPoint(tile.TileTransform.rect.center);
        float duration = 0.55f * (3f / 4f);
        clone.transform.DOMove(endWorld, duration).SetEase(Ease.Linear);
        clone.transform.DOScale(tile.TileTransform.localScale * 0.5f, duration).SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                Vector3 targetWorldScale = tile.TileTransform.lossyScale;
                clone.transform.DOScale(targetWorldScale, 0.1f).SetEase(Ease.Linear)
                    .OnComplete(() =>
                    {
                        Destroy(clone);
                        onComplete?.Invoke();
                    });
            });
    }
    #region 点击事件
    public void Lock()
    {
        _mesk.SetActive(true);
       
    }
    public void Unlock()
    {
        _mesk.SetActive(false);
    }

    private enum ClickState { Idle, ScalingUp, Ready, ScalingDown }
    private ClickState _currentState = ClickState.Idle;
    private Coroutine _clickRoutine;
    public void OnPointerDown(PointerEventData eventData)
    {
        //if (!PassDebounce()) return;
        //_bowlHanding = true;
        transform.DOScale(1.05f, 0.01f);
        AudioManager.Instance.PlaySoundEffect("WordClick");
        //if (ChessBowlGrid._isProcessing) return;
        //if(_clickRoutine != null) StopCoroutine(_clickRoutine);
        //_clickRoutine = StartCoroutine(ClickSequence());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        //if (!_bowlHanding && !PassDebounce()) return;
        transform.DOScale(1f, 0.01f);
        OnClickHandler?.Invoke(this);                    // 业务回调
        //_bowlHanding = false;
        //if(_currentState != ClickState.Ready) return;
        //if(_clickRoutine != null) StopCoroutine(_clickRoutine);
        //_clickRoutine = StartCoroutine(ReleaseSequence());
    }
    private IEnumerator ClickSequence()
    {
        _currentState = ClickState.ScalingUp;
        transform.DOScale(1.15f, 0.1f);
        AudioManager.Instance.PlaySoundEffect("WordClick");
        yield return new WaitForSeconds(0.01f);
        _currentState = ClickState.Ready;
        yield return new WaitForSeconds(0.05f);
    }
    private IEnumerator ReleaseSequence()
    {
        _currentState = ClickState.ScalingDown;
        transform.DOScale(1f, 0.1f);
        OnClickHandler?.Invoke(this);
        yield return new WaitForSeconds(0.01f);
        _currentState = ClickState.Idle;
    }
    private float lastClickTime = -1f;
    private const float DEBOUNCE_INTERVAL = 0.15f;   // 可调
    /// <summary>
    /// 点击防抖
    /// </summary>
    /// <returns></returns>
    private bool PassDebounce()
    {
        if (Time.time - lastClickTime < DEBOUNCE_INTERVAL)
        {
            return false;
        }
        lastClickTime = Time.time;
        return true;
    }
    #endregion
    private void OnDisable()
    {
        OnClickHandler = null;
        _mesk.SetActive(false);
        _textDisplay.text = "";
        bowl = null;
    }
}
