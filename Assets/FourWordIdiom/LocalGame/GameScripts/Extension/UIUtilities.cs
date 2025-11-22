using System;
using System.Globalization;
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
    
    public static float REFERENCE_WIDTH = 1242;
    public static float REFERENCE_HEIGHT = 2208;

    public static void AddClickAction(this Button targetButton, UnityAction onClickAction, string soundName = "Button", bool includeAnimation = true)
    {
        targetButton.onClick.AddListener(() =>
        {
            if (!string.IsNullOrEmpty(soundName))
            {
                AudioManager.Instance.PlaySoundEffect(soundName);
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
            GameDataManager.Instance.UserData.CheckResetLimitTime();
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
  
}