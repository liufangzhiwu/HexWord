using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
     * 玩家社交交互相关的接口 (查看档案、点赞等)
     */
public class SocialApi
{
    private HTTPClient httpClient;

    public SocialApi(HTTPClient client)
    {
        httpClient = client;
    }

    /// <summary>
    /// 获取其他玩家的公开个人档案 (游客也可调用)
    /// </summary>
    /// <param name="targetUserId">目标玩家的ID</param>
    public IEnumerator GetPublicProfile(string targetUserId, Action<PublicProfileDto> callback)
    {
        // 路由对应 PHP 的: GET /api/user/{userId}/profile
        string endpoint = $"user/{targetUserId}/profile";

        yield return httpClient.Get<PublicProfileDto>(endpoint,
            onSuccess =>
            {
                Debug.Log($"[UserApi] 获取玩家 {targetUserId} 档案成功！");
                callback?.Invoke(onSuccess);
            },
            onError =>
            {
                Debug.LogError($"[UserApi] 获取玩家 {targetUserId} 档案失败: {onError}");
                callback?.Invoke(null);
            });
    }

    /// <summary>
    /// 给其他玩家点赞 / 取消点赞 (必须登录后调用)
    /// </summary>
    /// <param name="targetUserId">目标玩家的ID</param>
    public IEnumerator LikeUser(string targetUserId, Action<LikeResponseDto> callback)
    {
        // 路由对应 PHP 的: POST /api/user/{userId}/like
        string endpoint = $"user/{targetUserId}/like";

        yield return httpClient.Post<LikeResponseDto>(endpoint,
            null, // 该接口不需要传 Body
            onSuccess =>
            {
                Debug.Log($"[UserApi] 点赞操作成功！动作: {onSuccess.action}, 最新赞数: {onSuccess.likes_count}");
                callback?.Invoke(onSuccess);
            },
            onError =>
            {
                Debug.LogError($"[UserApi] 点赞操作失败: {onError}");
                callback?.Invoke(null);
            });
    }
}