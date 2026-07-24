using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeaderboardApi 
{
    private HTTPClient httpClient;
    public LeaderboardApi(HTTPClient client)
    {
        httpClient = client;
    }

    public IEnumerator GetLeaderboard(string boardId, System.Action<LeaderboardResponse> action)
    {
        var url = $"leaderboards/zen/{boardId}";
        yield return httpClient.Get<LeaderboardResponse>(url,
            entries => {
                action?.Invoke(entries);
            },
            error => {
                action?.Invoke(null);
            });
    }
    
    /// <summary>
    /// 👇=== 新增：主动加入禅修榜接口 ===👇
    /// 玩家在雷达界面点击匹配时调用，通知服务端锁定新赛季底分
    /// </summary>
    public IEnumerator JoinZenRank(System.Action<JoinZenRankResponse> action)
    {
        var url = "leaderboards/zen/join";
        
        // POST 请求，无需传参，通过 Header 的 Token 识别玩家
        yield return httpClient.Post<JoinZenRankResponse>(url, null,
            response => {
                action?.Invoke(response);
            },
            error => {
                Debug.LogError($"[LeaderboardApi] 主动加入排行榜请求失败: {error}");
                action?.Invoke(null);
            });
    }
    
    /// <summary>
    /// 👇=== 确认领奖接口 ===👇
    /// 发送领奖确认请求，通知服务端结束玩家的上个赛季状态并更新真实段位
    /// </summary>
    public IEnumerator ClaimZenReward(System.Action<ClaimZenRewardResponse> action)
    {
        var url = "leaderboards/zen/claim";
        
        // 发送 POST 请求。因为服务端通过 auth token 识别用户，不需要额外 body 参数，传 null 即可
        yield return httpClient.Post<ClaimZenRewardResponse>(url, null,
            response => {
                action?.Invoke(response);
            },
            error => {
                Debug.LogError($"[LeaderboardApi] 领奖确认请求失败: {error}");
                action?.Invoke(null);
            });
    }
    
    /// <summary>
    /// 检查是否有段位榜(禅修榜)的结算奖励
    /// </summary>
    public IEnumerator CheckZenSettlement(System.Action<ZenSettlementResponse> action)
    {
        var url = "leaderboards/zen/check-settlement";
        yield return httpClient.Get<ZenSettlementResponse>(url,
            response => {
                action?.Invoke(response);
            },
            error => {
                Debug.LogError($"[LeaderboardApi] 检查结算失败: {error}");
                action?.Invoke(null);
            });
    }
}
