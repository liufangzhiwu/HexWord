// 避免频繁的UI布局变化

using UnityEngine;

public class UIStabilizer : MonoBehaviour
{
    private RectTransform rectTransform;
    private Vector2 lastSize;
    
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        lastSize = rectTransform.sizeDelta;
    }
    
    void Update()
    {
        // 只在尺寸真正变化时重建
        if (rectTransform.sizeDelta != lastSize)
        {
            lastSize = rectTransform.sizeDelta;
            // 必要的更新逻辑
        }
    }
}