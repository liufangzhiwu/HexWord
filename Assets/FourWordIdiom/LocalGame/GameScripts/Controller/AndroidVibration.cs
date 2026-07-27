using System;
using UnityEngine;
using System.Runtime.InteropServices; // 必须引用

public class AndroidVibration : MonoBehaviour
{
    // ---------- 可调参数 ----------
    private const long MIN_VIBRATION_MS = 10;
    private const long MAX_VIBRATION_MS = 50;
    private const int DEFAULT_AMPLITUDE = 128;

    // ---------- 缓存 Android 对象（全局静态） ----------
    private static AndroidJavaClass _unityPlayer;
    private static AndroidJavaObject _currentActivity;
    private static AndroidJavaObject _vibrator;

    // ---------- 鸿蒙原生插件导入 ----------
    #if UNITY_OPENHARMONY&& !UNITY_EDITOR
    [DllImport("entry")] // 插件名称，与编译生成的 .so 文件名对应
    private static extern void vibrate(long milliseconds, int intensity);
    #endif

    // ---------- 初始化（懒加载） ----------
    private static void Initialize()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_OPENHARMONY
        // 注意：如果 UNITY_OPENHARMONY 定义了，则不会进入此分支，因为鸿蒙平台不会使用 AndroidJava
        if (_vibrator != null) return;

        try
        {
            if (_unityPlayer == null)
                _unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");

            if (_currentActivity == null)
                _currentActivity = _unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (_currentActivity != null && _vibrator == null)
                _vibrator = _currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (_vibrator == null)
                Debug.LogWarning("[AndroidVibration] Vibrator service not available.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidVibration] Initialization failed: {e.Message}");
        }
        #endif
    }

    /// <summary>
    /// 触发震动（兼容 Android 和 HarmonyOS）
    /// </summary>
    public static void Vibrate(long milliseconds, int intensity)
    {
        // 限制时长与强度
        milliseconds = Math.Clamp(milliseconds, MIN_VIBRATION_MS, MAX_VIBRATION_MS);
        int clampedIntensity = Math.Clamp(intensity, 1, 255);

        #if UNITY_OPENHARMONY && !UNITY_EDITOR
        // ---------- 鸿蒙平台：直接调用原生插件 ----------
        Debug.Log($"[AndroidVibration] HarmonyOS vibrate: {milliseconds}ms, intensity={clampedIntensity}");
        vibrate(milliseconds, clampedIntensity);
        return;
        #endif

        #if UNITY_ANDROID && !UNITY_EDITOR
        // ---------- Android 平台（非鸿蒙） ----------
        Initialize();

        if (_vibrator == null)
        {
            Debug.LogWarning("[AndroidVibration] Vibrator not available, fallback to Handheld.Vibrate()");
            Handheld.Vibrate();
            return;
        }

        try
        {
            try { _vibrator.Call("cancel"); } catch (Exception ex) { Debug.LogWarning($"[AndroidVibration] Cancel failed: {ex.Message}"); }

            int apiLevel = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");

            if (apiLevel >= 26)
                TriggerVibrationEffect(milliseconds, clampedIntensity);
            else
            {
                _vibrator.Call("vibrate", milliseconds);
                Debug.Log($"[AndroidVibration] Legacy vibrate: {milliseconds}ms");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidVibration] Vibrate error: {e.Message}");
            Handheld.Vibrate();
        }
        #endif
    }

    #if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_OPENHARMONY
    private static void TriggerVibrationEffect(long milliseconds, int intensity)
    {
        try
        {
            using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                bool hasAmplitudeControl = false;
                try
                {
                    hasAmplitudeControl = _vibrator.Call<bool>("hasAmplitudeControl");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AndroidVibration] hasAmplitudeControl failed: {ex.Message}");
                }

                AndroidJavaObject effect;

                if (hasAmplitudeControl)
                {
                    effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, intensity);
                    Debug.Log($"[AndroidVibration] VibrationEffect (amplitude): {milliseconds}ms, intensity={intensity}");
                }
                else
                {
                    effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, DEFAULT_AMPLITUDE);
                    Debug.Log($"[AndroidVibration] VibrationEffect (default amplitude): {milliseconds}ms");
                }

                if (effect != null)
                {
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    Debug.LogWarning("[AndroidVibration] Effect creation failed, fallback to legacy vibrate.");
                    _vibrator.Call("vibrate", milliseconds);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidVibration] VibrationEffect error: {ex.Message}, fallback to legacy.");
            _vibrator.Call("vibrate", milliseconds);
        }
    }
    #endif

    // ---------- 释放资源 ----------
    private void OnApplicationQuit()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_OPENHARMONY
        _vibrator?.Dispose();
        _currentActivity?.Dispose();
        _unityPlayer?.Dispose();
        _vibrator = null;
        _currentActivity = null;
        _unityPlayer = null;
        #endif
    }
}