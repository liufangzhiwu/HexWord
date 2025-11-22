// using Middleware;
// using System;
// using System.Collections;
// using UnityEngine;
//
// /**
//  * 登录相关的接口
//  */
// public class LoginApi
// {
//     private HTTPClient httpClient;
//     public LoginApi(HTTPClient client)
//     {
//         httpClient = client;
//     }
//
//     /**
//      * 登录
//      */
//     public IEnumerator Login(Action<object> action)
//     {
//         string deviceId = Game.GetUniqueId();
//         if(string.IsNullOrEmpty(deviceId))
//         {
//             deviceId = SystemInfo.deviceUniqueIdentifier;
//         }
//         var data = new LoginRequest
//         {
//             deviceId = deviceId,
//             platform = Application.platform.ToString(),
//             version = Application.version ?? "1.0.0",
//             language = Application.systemLanguage.ToString(),
//         };
//        
//         yield return httpClient.Post<LoginResponse>("auth/device-login",
//             data,
//             response =>
//             {
//                 // 保存Token
//                 HTTPClient.Instance.SetAuthToken(response.token);
//                 Debug.Log("Login success!" + response.token);
//                 action?.Invoke(response);
//             },
//             error =>
//             {
//                 Debug.Log($"Login failed: {error}");
//                 action?.Invoke(null);
//             });
//     }
//
//     /**
//      * 退出游戏， 保存数据
//      */
//     public IEnumerator Logout(object data, Action<bool> action)
//     {
//          yield return httpClient.Post<string>("auth/logout",
//             data,
//             response =>
//             {
//                 Debug.Log("Logout success! " + response);
//                 action?.Invoke(true);
//             },
//             onError =>
//             {
//                 Debug.Log($"Logout failed: {onError}");
//                 action?.Invoke(true);
//             });
//     }
//
//     /**
//      * 获取游戏数据
//      */
//     public IEnumerator GetUserData(Action<GetGameDataResponse> callback)
//     {
//         yield return httpClient.Get<GetGameDataResponse>("auth/getGameData",
//             onSuccess => {
//                 Debug.Log("GetUserData success!" + onSuccess.gameData);
//                 callback?.Invoke(onSuccess);
//             },
//             onError => {
//                 Debug.Log($"GetUserData failed: {onError}");
//                 callback?.Invoke(null);
//             });
//     }
//     /**
//      * 更新游戏数据
//      */
//     public IEnumerator UpdateUserData(object data)
//     {
//         yield return httpClient.Post<System.Collections.Generic.List<bool>>("auth/update-gameData",
//             data,
//             response =>
//             {
//                 // 保存游戏数据成功
//                 Debug.Log("保存游戏数据成功 success! " + response);
//             },
//             error =>
//             {
//                 Debug.Log($"保存游戏数据失败 failed: {error}");
//             });
//     }
//
//     // 登录后获取用户信息
//     public IEnumerator FetchUserProfile(Action<UserProfile> action)
//     {
//         yield return httpClient.Get<UserProfile>("auth/profile",
//             profile =>
//             {
//                 action?.Invoke(profile);
//                 Debug.Log($"Welcome {profile.nickname}");
//             },
//             error =>
//             {
//                 // Debug.LogError($"Fetch profile failed: {error}");
//             });
//     }
// }
//
