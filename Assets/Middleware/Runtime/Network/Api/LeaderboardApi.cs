// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class LeaderboardApi 
// {
//     private HTTPClient httpClient;
//     public LeaderboardApi(HTTPClient client)
//     {
//         httpClient = client;
//     }
//
//     public IEnumerator GetLeaderboard(string boardId, System.Action<LeaderboardResponse> action)
//     {
//         var url = $"leaderboards/zen/{boardId}";
//         yield return httpClient.Get<LeaderboardResponse>(url,
//             entries => {
//                 action?.Invoke(entries);
//             },
//             error => {
//                 Debug.LogError($"GetLeaderboard failed: {error}");
//                 action?.Invoke(null);
//             });
//     }
//     public IEnumerator SubmitScore(string boardId, int score, System.Action onSuccess, System.Action<string> onError)
//     {
//         var data = new Dictionary<string, string>
//         {
//             { "score", score.ToString() }
//         };
//         var url = $"leaderboards/{boardId}/submit";
//         yield return httpClient.Post<object>(url,
//             data,
//             response => {
//                 onSuccess?.Invoke();
//             },
//             error => {
//                 onError?.Invoke(error);
//             });
//     }
// }
