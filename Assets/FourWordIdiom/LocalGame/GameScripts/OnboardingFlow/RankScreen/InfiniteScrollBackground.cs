using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 无限滚动背景 (UV Scrolling)
/// </summary>
[RequireComponent(typeof(RawImage))]
public class InfiniteScrollBackground : MonoBehaviour
{
    [Tooltip("关联的 ScrollRect")]
    public ScrollRect targetScrollRect;
    
    [Tooltip("滚动速度比例，1为同步滚动，0.5为视差慢速滚动(推荐)")]
    public float parallaxSpeed = 1f;
    
    private RawImage backgroundImage;
    
    void Awake()
    {
        backgroundImage = GetComponent<RawImage>();
    }
    
    void Start()
    {
        if (targetScrollRect != null)
        {
            // 监听滑动事件
            targetScrollRect.onValueChanged.AddListener(OnScroll);
        }
    }
    private void OnScroll(Vector2 normalizedPos)
    {
        // 获取 Content 实际滑动的像素距离
        float contentX = targetScrollRect.content.anchoredPosition.x;

        // 计算 UV 偏移量 (移动像素 / 图片实际宽度)
        float uvOffsetX = -(contentX * parallaxSpeed) / backgroundImage.rectTransform.rect.width;

        // 核心：修改 RawImage 的 UV 坐标实现无缝平铺滚动！
        backgroundImage.uvRect = new Rect(uvOffsetX, 0, 1, 1);
    }
    
    void OnDestroy()
    {
        if (targetScrollRect != null)
        {
            targetScrollRect.onValueChanged.RemoveListener(OnScroll);
        }
    }
}
