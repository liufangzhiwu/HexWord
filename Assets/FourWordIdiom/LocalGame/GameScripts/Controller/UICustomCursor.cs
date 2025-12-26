// using UnityEngine;
// using UnityEngine.UI;
//
// public class UICustomCursor : MonoBehaviour
// {
//     [Header("UI References")]
//     public RectTransform cursorTransform;
//     public Image cursorImage;
//     public Canvas parentCanvas;
//     
//     [Header("Cursor Sprites")]
//     public Sprite clickCursor;
//     public Sprite holdCursor; // 长按时使用的光标样式
//     
//     [Header("Settings")]
//     float cursorSize = 200f;
//     bool hideSystemCursor = false;
//     public float fadeOutDuration = 0.1f; // 淡出持续时间
//     public float longPressThreshold = 0.1f; // 长按阈值（秒）
//     
//     // 状态变量
//     private float fadeOutTimer = 0f;
//     private bool isFadingOut = false;
//     private bool isLongPressing = false;
//     private float pressStartTime = 0f;
//     private Vector2 lastCursorPosition;
//     
//     void Start()
//     {
//         // 隐藏系统光标
//         if (hideSystemCursor)
//         {
//             Cursor.visible = false;
//         }
//         
//         // 设置初始大小
//         cursorTransform.sizeDelta = new Vector2(cursorSize, cursorSize);
//         
//         // 初始隐藏自定义光标
//         cursorImage.enabled = false;
//     }
//     
//     void Update()
//     {
//         // 检测鼠标按下
//         if (Input.GetMouseButtonDown(0))
//         {
//             OnMouseDown();
//         }
//         
//         // 检测鼠标按住
//         if (Input.GetMouseButton(0))
//         {
//             OnMouseHold();
//         }
//         
//         // 检测鼠标释放
//         if (Input.GetMouseButtonUp(0))
//         {
//             OnMouseUp();
//         }
//         
//         // 更新淡出效果（如果不是长按状态）
//         if (isFadingOut && !isLongPressing)
//         {
//             UpdateFadeOut();
//         }
//         
//         // 如果是长按状态，更新光标位置
//         if (isLongPressing)
//         {
//             UpdateCursorPosition();
//         }
//     }
//     
//     void OnMouseDown()
//     {
//         // 重置状态
//         isFadingOut = false;
//         isLongPressing = false;
//         pressStartTime = Time.time;
//         
//         // 显示光标在点击位置
//         ShowCursorAtCurrentPosition(clickCursor);
//         
//         // 记录当前位置
//         lastCursorPosition = GetCurrentMousePosition();
//     }
//     
//     void OnMouseHold()
//     {
//         // 检查是否达到长按阈值
//         if (!isLongPressing && (Time.time - pressStartTime) >= longPressThreshold)
//         {
//             // 切换到长按状态
//             StartLongPress();
//         }
//     }
//     
//     void OnMouseUp()
//     {
//         // 如果是长按状态，结束长按
//         if (isLongPressing)
//         {
//             EndLongPress();
//         }
//         else
//         {
//             // 如果是短按，开始淡出
//             StartFadeOut();
//         }
//     }
//     
//     void StartLongPress()
//     {
//         isLongPressing = true;
//         
//         // 停止淡出效果
//         isFadingOut = false;
//         
//         // 切换为长按光标样式（如果有的话）
//         if (holdCursor != null)
//         {
//             cursorImage.sprite = holdCursor;
//         }
//         
//         // 确保光标可见
//         cursorImage.enabled = true;
//         cursorImage.color = Color.white;
//         cursorTransform.localScale = Vector3.one;
//     }
//     
//     void EndLongPress()
//     {
//         isLongPressing = false;
//         StartFadeOut();
//     }
//     
//     void ShowCursorAtCurrentPosition(Sprite sprite)
//     {
//         // 获取当前位置
//         Vector2 screenPosition = GetCurrentMousePosition();
//         
//         // 设置光标位置
//         SetCursorPosition(screenPosition);
//         
//         // 设置光标样式
//         cursorImage.sprite = sprite;
//         
//         // 重置状态
//         cursorImage.enabled = true;
//         cursorImage.color = Color.white;
//         cursorTransform.localScale = Vector3.one;
//     }
//     
//     void UpdateCursorPosition()
//     {
//         // 获取当前鼠标位置
//         Vector2 currentPosition = GetCurrentMousePosition();
//         
//         // 检查位置是否有变化
//         if (currentPosition != lastCursorPosition)
//         {
//             // 更新光标位置
//             SetCursorPosition(currentPosition);
//             lastCursorPosition = currentPosition;
//         }
//     }
//     
//     Vector2 GetCurrentMousePosition()
//     {
//         // 获取屏幕坐标
//         return Input.mousePosition;
//     }
//     
//     void SetCursorPosition(Vector2 screenPosition)
//     {
//         // 转换为Canvas局部坐标
//         Vector2 localPosition;
//         RectTransformUtility.ScreenPointToLocalPointInRectangle(
//             parentCanvas.transform as RectTransform,
//             screenPosition,
//             SystemManager.Instance.MainCamera,
//             out localPosition
//         );
//         
//         // 设置光标位置
//         cursorTransform.position = parentCanvas.transform.TransformPoint(localPosition);
//     }
//     
//     void StartFadeOut()
//     {
//         // 开始淡出
//         fadeOutTimer = fadeOutDuration;
//         isFadingOut = true;
//     }
//     
//     void UpdateFadeOut()
//     {
//         // 更新计时器
//         fadeOutTimer -= Time.deltaTime;
//         
//         if (fadeOutTimer > 0)
//         {
//             // 计算透明度（从1到0）
//             float alpha = fadeOutTimer / fadeOutDuration;
//             
//             // 设置透明度
//             Color newColor = cursorImage.color;
//             newColor.a = alpha;
//             cursorImage.color = newColor;
//             
//             // 可选：逐渐缩小
//             float scale = 0.8f + 0.2f * alpha;
//             cursorTransform.localScale = Vector3.one * scale;
//         }
//         else
//         {
//             // 淡出完成，隐藏光标
//             cursorImage.enabled = false;
//             isFadingOut = false;
//         }
//     }
//     
//     // 动态调整光标大小
//     public void SetCursorSize(float size)
//     {
//         cursorSize = size;
//         cursorTransform.sizeDelta = new Vector2(size, size);
//     }
//     
//     // 改变光标颜色
//     public void SetCursorColor(Color color)
//     {
//         cursorImage.color = color;
//     }
//     
//     // 设置长按阈值
//     public void SetLongPressThreshold(float threshold)
//     {
//         longPressThreshold = Mathf.Max(0.1f, threshold); // 确保最小值为0.1秒
//     }
//     
//     // 强制结束长按（例如在特定游戏状态下）
//     public void ForceEndLongPress()
//     {
//         if (isLongPressing)
//         {
//             EndLongPress();
//         }
//     }
// }