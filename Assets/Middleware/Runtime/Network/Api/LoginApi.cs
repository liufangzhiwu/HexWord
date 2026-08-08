using Middleware;
using System;
using System.Collections;
using UnityEngine;

/**
 * 登录相关的接口
 */
public class LoginApi
{
    private HTTPClient httpClient;
    public LoginApi(HTTPClient client)
    {
        httpClient = client;
    }
    // 辅助：获取当前渠道厂商
    private string GetCurrentFactory()
    {
        // 如果是华为渠道包
#if UNITY_HUAWEI || UNITY_OPENHARMONY
    return "huawei";
#elif UNITY_hornor
    return "hornor";
    // 如果是 Google Play 渠道包 (包括 PC 版)
#elif UNITY_ANDROID || UNITY_STANDALONE_WIN
        // 注意：Google Play Games PC 版也是 Google 厂商
        return "google"; 
#elif UNITY_IOS
    return "apple";
#else
    return "editor";
#endif
    }
    /**
     * 登录
     */
    public IEnumerator Login(Action<object> action)
    {
      
        string platform = Application.platform.ToString();
        
        string openId = GameDataManager.Instance.UserData.UserId;
        string factory = GetCurrentFactory();
        string push_token = GameDataManager.Instance.UserData.PushToken;

        if (string.IsNullOrEmpty(openId))
        {
            openId = SystemInfo.deviceUniqueIdentifier;
            
            Debug.LogError(string.Format("[{0}] - {1} OpenId为空！请检查登录逻辑。", factory, openId));
        }

        
#if UNITY_HUAWEI ||UNITY_hornor 
     platform ="Android"; 
#elif UNITY_OPENHARMONY
    platform ="OpenHarmony"; 
    // 如果是 Google Play 渠道包 (包括 PC 版)
#elif UNITY_ANDROID || UNITY_STANDALONE_WIN
        // 注意：Google Play Games PC 版也是 Google 厂商
        platform ="Android"; 
#elif UNITY_IOS
     platform= "IPhonePlayer";
#else
     platform ="Android"; 
#endif
        
        var data = new LoginRequest
        {
            factory = factory,
            openId = openId,
            deviceId = openId,
            platform = platform,
            version = Application.version ?? "1.0.0",
            language = Application.systemLanguage.ToString(),
            push_token = push_token,
        };
       
        yield return httpClient.Post<LoginResponse>("auth/device-login",
            data,
            response =>
            {
                // 保存Token
                HTTPClient.Instance.SetAuthToken(response.token,response.offline_Seconds);
                Debug.Log("Login success!" + response.token);
                action?.Invoke(response);
            },
            error =>
            {
                Debug.Log($"Login failed: {error}");
                action?.Invoke(null);
            });
    }

    /**
     * 退出游戏， 保存数据
     */
    public IEnumerator Logout(object data, Action<bool> action)
    {
         yield return httpClient.Post<string>("auth/logout",
            data,
            response =>
            {
                Debug.Log("Logout success! " + response);
                action?.Invoke(true);
            },
            onError =>
            {
                Debug.Log($"Logout failed: {onError}");
                action?.Invoke(true);
            });
    }

    /**
     * 获取游戏数据
     */
    public IEnumerator GetUserData(Action<GameDataDto> callback)
    {
        yield return httpClient.Get<GameDataDto>("auth/getGameData",
            onSuccess => {
                Debug.Log("GetUserData success!" + onSuccess.UserData);
                callback?.Invoke(onSuccess);
            },
            onError => {
                Debug.Log($"GetUserData failed: {onError}");
                callback?.Invoke(null);
            });
    }
    /**
     * 更新游戏数据
     */
    public IEnumerator UpdateUserData(GameDataDto data, Action<bool> callback)
    {
        yield return httpClient.Post<bool>("auth/update-gameData",
            data,
            response =>
            {
                // 保存游戏数据成功
                Debug.Log("保存游戏数据成功 success! " + response);
                callback?.Invoke(true);
            },
            error =>
            {
                Debug.Log($"保存游戏数据失败 failed: {error}");
                callback?.Invoke(true);
            });
    }

    // 登录后获取用户信息
    public IEnumerator FetchUserProfile(Action<UserProfile> action)
    {
        yield return httpClient.Get<UserProfile>("auth/profile",
            profile =>
            {
                action?.Invoke(profile);
                Debug.Log($"Welcome {profile.nickname}");
            },
            error =>
            {
                // Debug.LogError($"Fetch profile failed: {error}");
                action?.Invoke(null);
            });
    }
    
    // 登录后获取用户信息
    public IEnumerator ClearUserProfile(Action<bool> action)
    {
        yield return httpClient.Post<bool>("auth/debug/clear-data",
            null,
            response =>
            {
                action?.Invoke(response);
                Debug.Log("清理服务端数据成功！");
            },
            error =>
            {
                // Debug.LogError($"Fetch profile failed: {error}");
                action?.Invoke(false);
            });
    }
}

