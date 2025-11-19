using UnityEngine;

public class AndroidVibration : MonoBehaviour
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="milliseconds"> 震动时长</param>
    /// <param name="intensity">震动强度</param>
    // Android 震动功能封装
    public static void Vibrate(long milliseconds, int intensity)
    {

#if UNITY_ANDROID && !UNITY_EDITOR    

        //获取系统服务
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
        {
            if (vibrator == null)
            {
                Debug.LogWarning("Vibrator service not available.");
                return;
            }

            //检查API版本
            int apiStage = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");

            //处理强度参数范围(1 - 255)
            int clampedIntensity = Mathf.Clamp(intensity, 1, 255);

            if (apiStage >= 26)
            {
                //修复后的VibrationEffect调用
                using (var vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                   //检查设备是否支持振幅控制
                    bool hasAmplitudeControl = false;
                    try
                    {
                        hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"hasAmplitudeControl check failed: {e.Message}");
                    }

                    AndroidJavaObject effect;

                    if (hasAmplitudeControl)
                    {
                        //支持振幅控制的设备
                       effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                           "createOneShot",
                           milliseconds,
                           clampedIntensity
                       );
                    }
                    else
                    {
                        //不支持振幅控制的设备使用默认强度
                       effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                           "createOneShot",
                           milliseconds,
                           -1  // 使用默认振幅
                       );
                    }
                    
                    //添加额外的震动参数检查
                    if (effect != null)
                    {
                        //确保震动时长有效
                        if (milliseconds <= 0)
                        {
                            Debug.LogWarning("Invalid vibration duration: " + milliseconds);
                            return;
                        }

                        //Handheld.Vibrate();
                        vibrator.Call("vibrate", effect);                       
                        Debug.Log($"Vibration triggered: {milliseconds}ms, intensity: {clampedIntensity}");
                    }
                    else
                    {
                        Debug.LogError("Failed to create VibrationEffect");

                        //回退到旧API
                        vibrator.Call("vibrate", milliseconds);
                    }
                }
            }
            else
            {
                //旧版本忽略强度参数
                vibrator.Call("vibrate", milliseconds);
            }
        }
#endif
    }
}