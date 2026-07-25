using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
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
    [SerializeField] private Image _bg;     // 图片
    // 👇 新增：角标的UI组件
    [SerializeField] private GameObject _badgeGroup; // 角标的背景图(红色圆圈等)
    [SerializeField] private Text _badgeText;        // 角标里的数字Text
    [JsonIgnore]
    [HideInInspector] public string letter => bowl?.letter ?? "";  // 生成的字
    [JsonIgnore]
    [HideInInspector] public bool locked => bowl.status == 1;   // 是否锁定

    public Bowl bowl { get; private set; }        // 设置的词
    private ChessBowlGrid _bowlGrid;               // 父类状态
    
    public void ClearGoldLeaf() => SetGoldLeaf(false);

    private void Awake()
    {
        _mesk.SetActive(false);
    }

    private bool _isGoldLeaf = false;
    
    public void SetGoldLeaf(bool active)
    {
        _isGoldLeaf = active;
        bowl.isGoldLeaf = _isGoldLeaf;
       
        UpdateBadge(); // 刷新角标显示
    }
    
    public void Setup(Bowl bowl, ChessBowlGrid bowlGrid)
    {
        this.bowl = bowl;
        this._bowlGrid = bowlGrid;
        _textDisplay.text = bowl.letter.ToString();
        _mesk.SetActive(locked);
        _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
        if(bowl.status == 2 )
        {
            gameObject.SetActive(false);
        }
        // 👇 新增：初始化时更新角标
        UpdateBadge();
        
        if (bowl.isGoldLeaf)
        {
            SetGoldLeaf(true);
        }
        else
        {
            _isGoldLeaf = false;
        }

    }
    // 👇 新增：更新角标的方法
    public void UpdateBadge()
    {
        if (_badgeGroup != null && _badgeText != null)
        {
            if (bowl.isGoldLeaf)
            {
                if (bowl.count > 1)
                {
                    _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
                    _badgeText.color = new Color32(168, 122, 81, 255);
                    _badgeGroup.SetActive(true);
                    _badgeText.text = bowl.count.ToString();
                    _isGoldLeaf = false;
                    //bowl.totalcount=bowl.count;
                }
                else
                {
                    _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
                    _badgeGroup.SetActive(false);
                    _isGoldLeaf = true;
                }
              
            }else if (bowl.count > 1)
            {
                _badgeGroup.SetActive(true);
                _badgeText.text = bowl.count.ToString();
                _badgeText.color = new Color32(168, 122, 81, 255);
                _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
            }
            else
            {
                // 只有 1 个的时候隐藏角标
                _badgeGroup.SetActive(false);
                _bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fill_bg");
            }
            float scale = UIUtilities.GetScreenRatio();
            if (UIUtilities.IsiPad())
                _badgeText.fontSize = 46;
            else if (scale < 0.85f)
                _badgeText.fontSize = 40;
            else
                _badgeText.fontSize = 46;
            
        }
    }
    public void FlyToCell(ChessView tile, Transform parent, Action onComplete)
    {
        
        RectTransform selfRT = GetComponent<RectTransform>();
        GameObject clone = _bowlGrid.PhantomPool.GetObject();
        clone.transform.SetParent(parent.parent, false); // 设到指定的飞行层级
        BowlView cloneView = clone.GetComponent<BowlView>();
        if (cloneView != null) 
        {
            if (cloneView._badgeGroup != null) cloneView._badgeGroup.SetActive(false); 
            if (cloneView._mesk != null) cloneView._mesk.SetActive(false);
            
            // 复制文字内容和颜色，解决“看不到字”的问题
            if (cloneView._textDisplay != null && this._textDisplay != null) 
            {
                cloneView._textDisplay.text = this._textDisplay.text;
                cloneView._textDisplay.color = this._textDisplay.color;
            }
            
            // 复制初始底板图，解决“上次飞行被染绿/红后，下次起飞直接是绿/红”的问题
            if (transform.childCount > 0 && clone.transform.childCount > 0)
            {
                Image myBg = transform.GetChild(0).GetComponent<Image>();
                Image cloneBg = clone.transform.GetChild(0).GetComponent<Image>();
                if (myBg != null && cloneBg != null)
                {
                    cloneBg.sprite = myBg.sprite;
                    cloneBg.color = myBg.color;
                }
            }
        } 
        
        RectTransform cloneRT = clone.GetComponent<RectTransform>();
        Canvas canvas = clone.GetComponent<Canvas>();
        // if(canvas == null ) 
        //     canvas = clone.AddComponent<Canvas>();
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
        
        // ======================= 核心修改：计算到达边缘的位置 =======================
        Vector3 startWorld = clone.transform.position;
        // 目标格子的中心点
        Vector3 centerWorld = tile.TileTransform.TransformPoint(tile.TileTransform.rect.center);
        // 计算目标格子在世界空间下的一半宽度（即边缘半径）
        float tileEdgeRadius = tile.TileTransform.TransformVector(new Vector3(tile.TileTransform.rect.width * 0.5f, 0, 0)).magnitude;
        Vector3 dir = (centerWorld - startWorld).normalized;
        // 新的飞行终点设为：目标格子的边缘
        Vector3 endWorld = centerWorld - dir * tileEdgeRadius;
        float fullDistance = Vector3.Distance(startWorld, centerWorld);
        float flyDistance = Vector3.Distance(startWorld, endWorld);
        if (fullDistance <= tileEdgeRadius)
        {
            endWorld = centerWorld;
            flyDistance = fullDistance;
        }
        // Vector3 endWorld = tile.TileTransform.TransformPoint(tile.TileTransform.rect.center);
        // float distance = Vector3.Distance(clone.transform.position, endWorld);
        float duration1 = Mathf.Sqrt(flyDistance) * 0.08f;
        float duration = Mathf.Clamp(duration1, 0.15f, 0.45f);
        // Debug.Log($"飞行距离: {flyDistance}, 预计时间: {duration1}, 最终限制时间: {duration}");
        float   switchDist = cloneRT.TransformVector(new Vector3(cloneRT.sizeDelta.x * 0.5f, 0, 0)).magnitude;    // 剩余 半格宽度 时换图
        // bool    hasSwitched = false;                // 只换一次
        clone.transform.DOMove(endWorld, duration).SetEase(Ease.Linear);
            // .OnUpdate(() =>
            // {
            //     if (hasSwitched || !clone) return;
            //     float remain = Vector3.Distance(clone.transform.position, endWorld);
            //     // Debug.Log($"当前距离 {remain} 检查距离 {switchDist} 当前位置{clone.transform.position} 目标位置{endWorld} ");
            //     if (remain <= switchDist) 
            //     {
            //         if (tile.CurrState == TileState.Success)
            //         {
            //             if (_isGoldLeaf)
            //             {
            //                 if (bowl.count >= 1)
            //                 {
            //                     tile._isGoldLeaf = false;
            //                     clone.transform.GetChild(0).GetComponent<Image>().sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
            //                 }
            //                 else
            //                 {
            //                     clone.transform.GetChild(0).GetComponent<Image>().sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
            //                     tile._isGoldLeaf = _isGoldLeaf;
            //                 }
            //             }
            //             else
            //             {
            //                 clone.transform.GetChild(0).GetComponent<Image>().sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("success_bg");
            //                 tile._isGoldLeaf = _isGoldLeaf;
            //             }
            //            
            //         }else if (tile.CurrState is TileState.Error or TileState.Fill)
            //         {
            //             if (_isGoldLeaf)
            //             {
            //                 if (bowl.count >= 1)
            //                 {
            //                     clone.transform.GetChild(0).GetComponent<Image>().sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
            //                 }
            //                 else
            //                 {
            //                     clone.transform.GetChild(0).GetComponent<Image>().sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
            //                    
            //                 }
            //             }
            //             else
            //             {
            //                 clone.transform.GetChild(0).GetComponent<Image>().sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("error_bg");
            //             }
            //             tile._isGoldLeaf = _isGoldLeaf;
            //         }
            //         hasSwitched = true;
            //     }
            // });
        clone.transform.DOScale(tile.TileTransform.localScale * 0.5f, duration).SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                // Vector3 targetWorldScale = tile.TileTransform.lossyScale;
                // clone.transform.DOScale(targetWorldScale, 0.01f).SetEase(Ease.Linear)
                //     .OnComplete(() =>
                //     {
                //         if(clone && clone.activeInHierarchy) 
                //         {
                //             _bowlGrid.PhantomPool.ReturnObjectToPool(clone.GetComponent<PoolObject>());
                //         }
                //         onComplete?.Invoke();
                //     });
                if(clone && clone.activeInHierarchy) 
                {
                    _bowlGrid.PhantomPool.ReturnObjectToPool(clone.GetComponent<PoolObject>());
                }
                onComplete?.Invoke();
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
        ChessPlayArea.Instance?.NotifyPlayerInteraction();
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
        AudioManager.Instance.TriggerVibration(40,40);
        
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
