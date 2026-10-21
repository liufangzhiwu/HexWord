using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    
    private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);

    // ======================= 中文数字 =======================

    /// <summary>中文数字表（1~10 显示用，索引 0 位保留"零"）</summary>
    private static readonly string[] ChineseNumbers =
    {
        "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十"
    };

    /// <summary>
    /// 数字转中文（1~10 用中文：一二三四五六七八九十；其余用阿拉伯数字兜底）。
    /// 用法：UIUtilities.GetChineseNumber(3) → "三"
    /// </summary>
    public static string GetChineseNumber(int n)
    {
        if (n >= 1 && n <= 10)
            return ChineseNumbers[n];
        return n.ToString();
    }

    // ======================= 按钮扩展 =======================

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
                onClickAction?.Invoke();
                targetButton.transform.DOScale(new Vector3(0.9f, 0.9f, 0.9f), 0.15f).OnComplete(() =>
                {
                    targetButton.transform.DOScale(Vector3.one, 0.15f);
                    EventDispatcher.instance.TriggerChangeFreeTipsPanel();
                });
            }
            else
            {
                onClickAction?.Invoke();
            }
        });
    }
    
    public static void AddVibraClickAction(this Button targetButton, UnityAction onClickAction, string soundName = "Button", bool includeAnimation = true)
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

            AudioManager.Instance.TriggerVibration(40, 50);
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
        int days = (int)timeSpan.TotalDays;
        int hour = timeSpan.Hours;

        if (timeSpan.Minutes > 0 && hour == 0)
        {
            hour += 1;
        }           

        string formattedday = days<=0?"": days+DaySymbol;
        string formattedhour = hour < 10 ? "0" + hour+HourSymbol : hour+HourSymbol;

        return formattedday + formattedhour;
    }
    
    /// <summary>是否为iPad设备</summary>
    public static bool IsiPad()
    {
        return (Screen.width / (float)Screen.height) > 0.62f;
    }
    
    /// <summary>获取屏幕缩放比例</summary>
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
        int minutes = (int)timeSpan.TotalMinutes;
        int seconds = timeSpan.Seconds;

        if (seconds > 0 && minutes == 0)
        {
            minutes += 1;
        }
        
        string min = minutes <= 0?"": minutes+"分";
        string sec = seconds < 10 ? "0" + seconds+"秒" : seconds+"秒";

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

    /// <summary>截取UI元素并保存为图片</summary>
    public static Sprite CaptureUIElement(RectTransform targetRect, string filePath, 
        Vector2Int targetSize = default, TextureFormat textureFormat = TextureFormat.RGBA32)
    {
        Rect screenRect = GetScreenRectFromRectTransform(targetRect);
        Texture2D screenshot = CaptureScreenArea(screenRect, textureFormat);
        
        if (targetSize != default && targetSize.x > 0 && targetSize.y > 0)
        {
            Texture2D scaledTexture = ScaleTexture(screenshot, targetSize);
            UnityEngine.Object.Destroy(screenshot);
            screenshot = scaledTexture;
        }
        
        SaveTextureToFile(screenshot, filePath);
        Sprite resultSprite = CreateSpriteFromTexture(screenshot);
        
        return resultSprite;
    }
    
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
    
    private static Texture2D CaptureScreenArea(Rect screenRect, TextureFormat format)
    {
        int x = Mathf.FloorToInt(screenRect.x);
        int y = Mathf.FloorToInt(screenRect.y);
        int width = Mathf.FloorToInt(screenRect.width);
        int height = Mathf.FloorToInt(screenRect.height);
        
        x = Mathf.Clamp(x, 0, Screen.width);
        y = Mathf.Clamp(y, 0, Screen.height);
        width = Mathf.Clamp(width, 1, Screen.width - x);
        height = Mathf.Clamp(height, 1, Screen.height - y);
        
        Texture2D texture = new Texture2D(width, height, format, false);
        texture.ReadPixels(new Rect(x, y, width, height), 0, 0);
        texture.Apply();
        
        return texture;
    }
    
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
    
    private static void SaveTextureToFile(Texture2D texture, string filePath)
    {
        try
        {
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
                filePath = Path.ChangeExtension(filePath, ".png");
                bytes = texture.EncodeToPNG();
            }
            
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllBytes(filePath, bytes);
            
            Debug.Log($"截图已保存到: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存截图失败: {e.Message}");
        }
    }
    
    private static Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.Tight
        );
    }
    
    public static System.Collections.IEnumerator CaptureUIElementAsync(
        RectTransform targetRect, 
        string filePath, 
        System.Action<Sprite> callback = null,
        Vector2Int targetSize = default)
    {
        yield return new WaitForEndOfFrame();
        
        Sprite result = CaptureUIElement(targetRect, filePath, targetSize);
        callback?.Invoke(result);
    }
    
    public static void CaptureFullScreen(string filePath)
    {
        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply();
        
        SaveTextureToFile(screenshot, filePath);
        UnityEngine.Object.Destroy(screenshot);
    }
    
    public static bool isEditMode => Application.isEditor;
    
    /// <summary>处理中英文标点符号"避头"排版问题</summary>
    public static string ApplyKinsokuShori(Text textComponent, string text)
    {
        if (string.IsNullOrEmpty(text) || textComponent == null) return text;

        textComponent.text = text;
        Canvas.ForceUpdateCanvases();

        var lines = textComponent.cachedTextGenerator.lines;
        
        if (lines.Count <= 1) return text;

        string punctuations = "！？。，、；：”’）】》!?,.";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder(text);
        int offset = 0;

        for (int i = 1; i < lines.Count; i++)
        {
            int startCharIdx = lines[i].startCharIdx + offset;
            
            if (startCharIdx > 0 && startCharIdx < sb.Length && punctuations.Contains(sb[startCharIdx].ToString()))
            {
                sb.Insert(startCharIdx - 1, '\n');
                offset++;
            }
        }

        return sb.ToString();
    }
    
    public static long GetCurrentDayIndex()
    {
        return (long)(DateTime.Now - Epoch).TotalDays;
    }
    
    public static long GetSomeDayIndex(DateTime date)
    {
        long dayIndex = (long)(date - Epoch).TotalDays;
        return dayIndex;
    }

    public static DateTime DayIndexToDateTime(long dayIndex)
    {
        return Epoch.AddDays(dayIndex);
    }

    public static long GetMonthKey(int year, int month)
    {
        return year * 100L + month;
    }

    public static (int year, int month) GetYearMonthFromKey(long key)
    {
        int year = (int)(key / 100);
        int month = (int)(key % 100);
        return (year, month);
    }
    
    public static string ExtractNumber(string input)
    {
        string numberStr = Regex.Match(input, @"\d+").Value;

        if (int.TryParse(numberStr, out int result))
        {
            return result.ToString(); 
        }

        return null;
    }
}