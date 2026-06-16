using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;

public class GameConfigApi 
{
    private HTTPClient httpClient;

    public GameConfigApi(HTTPClient client)
    {
        httpClient = client;
    }
    
    /**
     * 获取游戏配置表 (CSV长文本套娃模式)
     */
    public IEnumerator GetGameConfig(string moduleName, Action<GameConfigResponse> onSuccess, Action<string> onError = null)
    {
        // 获取设备唯一码，用于服务器的 A/B 测试分流
        string userId = Game.self.GetUniqueId();
        // 🌟 安全获取客户端内存中的 ABName (分包属性)
        string abGroup = "";
        if (GameDataManager.Instance != null && GameDataManager.Instance.UserData != null)
        {
            abGroup = GameDataManager.Instance.UserData.ABName ?? "";
        }
        // 拼接 GET 请求的 URL 参数
        string endpoint = $"configs/module?module_name={moduleName}&user_id={userId}&ab_group={abGroup}";
        yield return httpClient.Get<GameConfigResponse>(endpoint,
            response =>
            {
                Debug.Log($"[Config] 获取配置成功! 模块:{response.ModuleName}, AB组:{response.AbGroup}");
                onSuccess?.Invoke(response);
            },
            error =>
            {
                // Debug.LogWarning($"[Config] 获取配置失败: {error}");
                onError?.Invoke(error);
            });
    }
}