using System;
using System.Globalization;
using System.IO;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class UIUtilities
{
    public static string DaySymbol = "天";
    public static string HourSymbol = "时";
    public static string MinuteSymbol = "分";
    
    public static float REFERENCE_WIDTH = 1316;
    public static float REFERENCE_HEIGHT = 2832;

    public static void AddClickAction(this Button targetButton, UnityAction onClickAction, string soundName = "Button", bool includeAnimation = true)
    {
        targetButton.onClick.AddListener(() =>
        {
            if (!string.IsNullOrEmpty(soundName))
            {
                AudioManager.Instance.PlaySoundEffect(soundName);
            }
            else
            {
                AudioManager.Instance.PlaySoundEffect("Button");
            }

            if (includeAnimation)
            {
                targetButton.transform.DOScale(new Vector3(0.85f, 0.85f, 0.85f), 0.11f).OnComplete(() =>
                {
                    onClickAction?.Invoke();
                    targetButton.transform.DOScale(Vector3.one, 0.11f);
                });
            }
            else
            {
                onClickAction?.Invoke();
            }

            AudioManager.Instance.TriggerVibration(10, 200);
            EventDispatcher.instance.TriggerChangeFreeTipsPanel();
        });
    }

    public static float ConvertPercentageToDecimal(string percentageText)
    {
        if (percentageText.EndsWith("%"))
        {
            string numericPart = percentageText.TrimEnd('%');
            if (float.TryParse(numericPart, out float percentageValue))
            {
                return percentageValue / 100f;
            }
        }
        return 0f;
    }
    
    public static string GetDateDayStyle(TimeSpan timeSpan)
    {
        // 获取剩余的小时、分钟和秒
        int days = (int)timeSpan.TotalDays;
        int hour = timeSpan.Hours;

        // 向上取值
        if (timeSpan.Minutes > 0 && hour == 0)
        {
            hour += 1;
        }           

        // 格式化小时和分钟，确保小于10时前面补零
        string formattedday = days<=0?"": days+DaySymbol;
        string formattedhour = hour < 10 ? "0" + hour+HourSymbol : hour+HourSymbol;

        // 输出倒计时
        return formattedday + formattedhour;
    }
    
    /// <summary>
    /// 是否为iPad设备
    /// </summary>
    /// <returns></returns>
    public static bool IsiPad()
    {
        // 通过分辨率判断（iPad通常分辨率宽高比接近4:3）
        return (Screen.width / (float)Screen.height) > 0.62f;
    }
    
    /// <summary>
    /// 获取屏幕缩放比例
    /// </summary>
    /// <returns></returns>
    public static float GetScreenRatio()
    {
        float baseRatio = REFERENCE_WIDTH / REFERENCE_HEIGHT;
        float curscreenRatio = Screen.width/(float)Screen.height;

        float scale = curscreenRatio / baseRatio;
        Debug.Log("屏幕缩放比例："+scale);
        return scale;
    }
      
    public static string GetDateMintueStyle(TimeSpan timeSpan)
    {
        // 获取剩余的小时、分钟和秒
        int minutes = (int)timeSpan.TotalMinutes;
        int seconds = timeSpan.Seconds;

        // 向上取值
        if (seconds > 0 && minutes == 0)
        {
            minutes += 1;
        }
        
        // 格式化小时和分钟，确保小于10时前面补零
        string min = minutes <= 0?"": minutes+"分";
        string sec = seconds < 10 ? "0" + seconds+"秒" : seconds+"秒";

        // 输出倒计时
        return min + sec;
    }

    public static string FormatTimeRemaining(TimeSpan remainingTime)
    {
        int totalHours = (int)remainingTime.TotalHours;
        int minutes = remainingTime.Minutes;

        if (remainingTime.Seconds > 0 && minutes == 0)
        {
            minutes += 1;
        }
        if (totalHours == 24)
        {
            GameDataManager.Instance.UserData.CheckResetDailyTime();
        }

        string hoursText = totalHours.ToString();
        string minutesText = minutes < 10 ? "0" + minutes : minutes.ToString();

        return $"{hoursText}{HourSymbol}{minutesText}{MinuteSymbol}";
    }

    public static CultureInfo GetCultureForCurrency(string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode))
        {
            return CultureInfo.CreateSpecificCulture("ja-JP");
        }

        try
        {
            var matchingCulture = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .FirstOrDefault(culture =>
                {
                    try
                    {
                        return new RegionInfo(culture.Name).ISOCurrencySymbol == currencyCode;
                    }
                    catch
                    {
                        return false;
                    }
                });

            return matchingCulture ?? CultureInfo.CreateSpecificCulture("ja-JP");
        }
        catch
        {
            return CultureInfo.CreateSpecificCulture("ja-JP");
        }
    }

    public static string FormatCurrency(decimal value, CultureInfo culture)
    {
        return value % 1 == 0 ?
            value.ToString("C0", culture) :
            value.ToString("C2", culture);
    }

    public static string FormatCurrency(float value, CultureInfo culture)
    {
        return value % 1 == 0 ?
            value.ToString("C0", culture) :
            value.ToString("C2", culture);
    }

    /// <summary>
    /// 截取UI元素并保存为图片
    /// </summary>
    /// <param name="targetRect">目标UI元素的RectTransform</param>
    /// <param name="filePath">保存路径</param>
    /// <param name="targetSize">目标尺寸（可选）</param>
    /// <param name="format">图片格式</param>
    /// <returns>生成的Sprite</returns>
    public static Sprite CaptureUIElement(RectTransform targetRect, string filePath, 
        Vector2Int targetSize = default, TextureFormat textureFormat = TextureFormat.RGBA32)
    {
        // 1. 获取目标矩形在屏幕上的位置
        Rect screenRect = GetScreenRectFromRectTransform(targetRect);
        
        // 2. 创建临时纹理并截图
        Texture2D screenshot = CaptureScreenArea(screenRect, textureFormat);
        
        // 3. 可选：缩放纹理
        if (targetSize != default && targetSize.x > 0 && targetSize.y > 0)
        {
            Texture2D scaledTexture = ScaleTexture(screenshot, targetSize);
            UnityEngine.Object.Destroy(screenshot); // 销毁原纹理
            screenshot = scaledTexture;
        }
        
        // 4. 保存到文件
        SaveTextureToFile(screenshot, filePath);
        
        // 5. 创建Sprite
        Sprite resultSprite = CreateSpriteFromTexture(screenshot);
        
        return resultSprite;
    }
    
    /// <summary>
    /// 获取RectTransform在屏幕上的矩形区域
    /// </summary>
    private static Rect GetScreenRectFromRectTransform(RectTransform rectTransform)
    {
        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera renderCamera = null;
        
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || 
                canvas.renderMode == RenderMode.WorldSpace)
            {
                renderCamera = canvas.worldCamera ?? Camera.main;
            }
        }
        
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        
        for (int i = 0; i < 4; i++)
        {
            Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(renderCamera, corners[i]);
            
            if (screenPoint.x < min.x) min.x = screenPoint.x;
            if (screenPoint.y < min.y) min.y = screenPoint.y;
            if (screenPoint.x > max.x) max.x = screenPoint.x;
            if (screenPoint.y > max.y) max.y = screenPoint.y;
        }
        
        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }
    
    /// <summary>
    /// 截取屏幕区域
    /// </summary>
    private static Texture2D CaptureScreenArea(Rect screenRect, TextureFormat format)
    {
        int x = Mathf.FloorToInt(screenRect.x);
        int y = Mathf.FloorToInt(screenRect.y);
        int width = Mathf.FloorToInt(screenRect.width);
        int height = Mathf.FloorToInt(screenRect.height);
        
        // 确保坐标在屏幕范围内
        x = Mathf.Clamp(x, 0, Screen.width);
        y = Mathf.Clamp(y, 0, Screen.height);
        width = Mathf.Clamp(width, 1, Screen.width - x);
        height = Mathf.Clamp(height, 1, Screen.height - y);
        
        Texture2D texture = new Texture2D(width, height, format, false);
        texture.ReadPixels(new Rect(x, y, width, height), 0, 0);
        texture.Apply();
        
        return texture;
    }
    
    /// <summary>
    /// 缩放纹理
    /// </summary>
    private static Texture2D ScaleTexture(Texture2D source, Vector2Int targetSize)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetSize.x, targetSize.y);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        
        Texture2D result = new Texture2D(targetSize.x, targetSize.y, source.format, false);
        result.ReadPixels(new Rect(0, 0, targetSize.x, targetSize.y), 0, 0);
        result.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        
        return result;
    }
    
    /// <summary>
    /// 保存纹理到文件
    /// </summary>
    private static void SaveTextureToFile(Texture2D texture, string filePath)
    {
        try
        {
            // 确定文件格式和编码
            string extension = Path.GetExtension(filePath).ToLower();
            byte[] bytes;
            
            if (extension == ".png")
            {
                bytes = texture.EncodeToPNG();
            }
            else if (extension == ".jpg" || extension == ".jpeg")
            {
                bytes = texture.EncodeToJPG();
            }
            else
            {
                // 默认使用PNG
                filePath = Path.ChangeExtension(filePath, ".png");
                bytes = texture.EncodeToPNG();
            }
            
            // 创建目录
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 写入文件
            File.WriteAllBytes(filePath, bytes);
            
            Debug.Log($"截图已保存到: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存截图失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 从纹理创建Sprite
    /// </summary>
    private static Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), // 中心点
            100f, // 每单位像素数
            0, // 额外边框
            SpriteMeshType.Tight // 网格类型
        );
    }
    
    /// <summary>
    /// 异步截图（协程版本）
    /// </summary>
    public static System.Collections.IEnumerator CaptureUIElementAsync(
        RectTransform targetRect, 
        string filePath, 
        System.Action<Sprite> callback = null,
        Vector2Int targetSize = default)
    {
        // 等待一帧确保所有UI渲染完成
        yield return new WaitForEndOfFrame();
        
        Sprite result = CaptureUIElement(targetRect, filePath, targetSize);
        callback?.Invoke(result);
    }
    
    /// <summary>
    /// 截取整个屏幕
    /// </summary>
    public static void CaptureFullScreen(string filePath)
    {
        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply();
        
        SaveTextureToFile(screenshot, filePath);
        UnityEngine.Object.Destroy(screenshot);
    }
    
    public static bool isEditMode => Application.isEditor;
    
    
    /// <summary>
    /// 处理中英文标点符号“避头”排版问题，防止标点单独掉到下一行
    /// </summary>
    public static string ApplyKinsokuShori(Text textComponent, string text)
    {
        if (string.IsNullOrEmpty(text) || textComponent == null) return text;

        // 1. 先把干净的纯文字丢给 Text 组件，让它底层进行一次假渲染计算
        textComponent.text = text;
        Canvas.ForceUpdateCanvases(); // 强制 UGUI 刷新并生成渲染矩阵

        // 2. 获取底层的排版生成数据
        var lines = textComponent.cachedTextGenerator.lines;
        
        // 如果只有一行，说明根本没有发生换行，直接原样返回
        if (lines.Count <= 1) return text;

        // 避头标点符号库（绝对不能出现在行首的符号）
        string punctuations = "！？。，、；：”’）】》!?,.";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder(text);
        int offset = 0; // 记录因为插入 \n 导致字符串长度增加的偏移量

        // 3. 从第二行开始遍历，检查 UGUI 切断每一行的第一个字符是什么
        for (int i = 1; i < lines.Count; i++)
        {
            // 拿到这一行第一个字符在原始字符串中的索引位置
            int startCharIdx = lines[i].startCharIdx + offset;
            
            // 检查：如果这一行的开头刚好命中了一个标点符号！
            if (startCharIdx > 0 && startCharIdx < sb.Length && punctuations.Contains(sb[startCharIdx].ToString()))
            {
                // 核心：强行在标点符号的【前一个汉字】前面，插入一个真实的物理换行符 \n
                // 这样就能把“前一个字”和“标点”一起拽到下一行
                sb.Insert(startCharIdx - 1, '\n');
                offset++; // 字符串长度增加了1，后续的断行点索引需要偏移
            }
        }

        return sb.ToString();
    }
  
}