
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class HorizontalScrollSnap : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{

    public event Action<int> OnEventTriggered;

    [Header("滚动设置")]
    [Tooltip("列间距")] [SerializeField] private float spacing = 0f;
    [Tooltip("惯性阈值")][SerializeField] private float dragThreshold = 200f;

    private ScrollRect scrollRect;
    private RectTransform content;

    private float moveOneItemLength;
    private int currentIndex = 0;
    private int maxIndex = 0;
     // 上一次的位置
    private Vector3 currentContentLocalPos;

    private float beginMousePositionX;
    private float endMousePositionX;
 
    // 记录开始拖拽时 Content 的真实位置
    private float beginDragPositionX;
    
    // 总数
    private List<RectTransform> columnPositions = new List<RectTransform>();

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        content = scrollRect.content;
        
        scrollRect.inertia = false;
    }
    
    /// <summary>
    /// 🌟 外部调用：当列表数据生成完毕后，手动通知组件刷新布局数据
    /// </summary>
    public void RefreshLayout()
    {
        // 强制刷新一下 UI，确保元素的真实宽度已经生效
        Canvas.ForceUpdateCanvases();

        if (content.childCount > 0)
        {
            maxIndex = content.childCount - 1;
            RectTransform firstItem = content.GetChild(0) as RectTransform;
            // 计算完美步长：元素宽度 + 布局间距
            moveOneItemLength = firstItem.rect.width + spacing;
        }
        else
        {
            maxIndex = 0;
            moveOneItemLength = 0;
        }
    }
    /// <summary>
    /// 🌟 新增：初始化/瞬间跳转到指定索引（不播动画，直接设置坐标，并同步内部数据）
    /// </summary>
    public void InitIndexImmediately(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex > maxIndex) return;
        
        // 1. 同步内部真实索引
        currentIndex = targetIndex; 
        
        // 2. 直接设置物理坐标，不使用 DOTween
        float targetX = -currentIndex * moveOneItemLength;
        content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);
    }
    
    
    public void SetCurrentIndex(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex > maxIndex) return;
        currentIndex = targetIndex;
        MoveToCurrentIndex();
    }
    
    public int GetCurrentIndex() => currentIndex;

    public void OnBeginDrag(PointerEventData eventData)
    {
        DOTween.Kill(content);
       // beginMousePositionX = Input.mousePosition.x;
       // 记录拖拽开始时 Content 的坐标
       beginDragPositionX = content.anchoredPosition.x;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float endDragPositionX = content.anchoredPosition.x;
        float dragDelta = beginDragPositionX - endDragPositionX;
        
        // 判断是否超过翻页阈值
        if (Mathf.Abs(dragDelta) > dragThreshold)
        {
            if (dragDelta > 0 && currentIndex < maxIndex)
            {
                currentIndex++; // 往右翻页
            }
            else if (dragDelta < 0 && currentIndex > 0)
            {
                currentIndex--; // 往左翻页
            }
        }
        // 吸附到目标位置
        MoveToCurrentIndex();
        OnEventTriggered?.Invoke(currentIndex);
    }
    
    /// <summary>
    /// 执行吸附动画
    /// </summary>
    private void MoveToCurrentIndex()
    {
        // 绝对坐标计算，彻底杜绝累加误差！
        float targetX = -currentIndex * moveOneItemLength;
        
        content.DOAnchorPosX(targetX, 0.3f)
            .SetEase(Ease.OutCubic)
            .SetId(content); // 设置 DOTween ID 方便管理
    }
    
    
}
