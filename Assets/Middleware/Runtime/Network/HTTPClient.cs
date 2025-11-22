// using BestHTTP;
// using Newtonsoft.Json;
// using Newtonsoft.Json.Linq;
// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class HTTPClient 
// {
//     private static readonly Lazy<HTTPClient> _instance =
//        new Lazy<HTTPClient>(() => new HTTPClient());
//
//     public static HTTPClient Instance => _instance.Value;
//
//     private string _baseUrl = "http://127.0.0.1:8000/api/";
//     private string authToken = "";
//     private Dictionary<string, string> defaultHeaders = new Dictionary<string, string>();
//
//     private HTTPClient() { }
//  
//     public HTTPClient Initialize(string baseUrl = null)
//     {
//         if(!string.IsNullOrEmpty(baseUrl))
//         {
//             _baseUrl = baseUrl;
//         }
//         string appVersion = Application.version ?? "1.0.0";
//         SetDefaultHeaders("Content-Type", "application/json");
//         SetDefaultHeaders("Accept", "application/json");
//         // 添加标准版本头
//         SetDefaultHeaders("X-Client-Version", appVersion);
//         // 可选：添加平台标识
//         SetDefaultHeaders("X-Client-Platform",Application.platform.ToString());
//         SetDefaultHeaders("X-Client-Env", "development");
//
//         if (PlayerPrefs.HasKey("auth_token"))
//         {
//             authToken = PlayerPrefs.GetString("auth_token");
//             UpdateAuthHeader();
//         }
//         return this;
//     }
//
//     public void SetAuthToken(string token)
//     {
//         authToken = token;
//
//         PlayerPrefs.SetString("auth_token", token);
//         PlayerPrefs.Save();
//         UpdateAuthHeader();
//     }
//
//     private void UpdateAuthHeader()
//     {
//         if (!string.IsNullOrEmpty(authToken))
//         {
//             SetDefaultHeaders("Authorization", $"Bearer {authToken}");
//         }
//     }
//
//     private void SetDefaultHeaders(string key, string value)
//     {
//        if(defaultHeaders.ContainsKey(key))
//        {
//            defaultHeaders[key] = value;
//        }
//        else
//        {
//            defaultHeaders.Add(key, value);
//        }
//     }
//
//     [Serializable]
//     public class ApiResponse<T>
//     {
//         public int code;
//         public T data;
//         public string message;
//     }
//
//     public IEnumerator Get<T>(string endpoint, Action<T> onSuccess, Action<string> onError, Dictionary<string, string> customHeaders = null)
//     {
//         // yield return SendRequest<T>(HTTPMethods.Get, endpoint, null, onSuccess, onError, customHeaders);
//         onError?.Invoke("HTTP Client 处于禁用状态");
//         yield return null;
//     }
//     public IEnumerator Post<T>(string endpoint, object body, Action<T> onSuccess, Action<string> onError, Dictionary<string, string> customHeaders = null)
//     {
//         // yield return SendRequest<T>(HTTPMethods.Post, endpoint, body, onSuccess, onError, customHeaders);
//         onError?.Invoke("HTTP Client 处于禁用状态");
//         yield return null;
//     }
//     private IEnumerator SendRequest<T>(HTTPMethods method, string endpoint, object body, Action<T> onSuccess, Action<string> onError, Dictionary<string, string> customHeaders)
//     {
//         float startTime = Time.realtimeSinceStartup;
//         var request = new HTTPRequest(new Uri(_baseUrl + endpoint), method, (req, resp) => {
//             float duration = Time.realtimeSinceStartup - startTime;
//             if (req == null || resp == null)
//             {
//                 onError?.Invoke("Network Error!!!");
//                 return;
//             }
//             // 控制台输出格式化日志
//             Debug.Log($"[API] {method} {req.Uri} \n" +
//                       $"Request Header: {req.DumpHeaders()}\n" +
//                       $"Request Body: {(body != null ? JsonConvert.SerializeObject(body) : "N/A")}\n" +
//                       $"Response Header: {JsonConvert.SerializeObject(resp.Headers)}\n" +
//                       $"Response Body: {(resp != null ? resp.DataAsText : "N/A")}\n" +
//                       $"Code: {resp.StatusCode} Time: {duration:F2}s");
//             HandleResponse(resp, onSuccess, onError);
//         });
//         foreach (var header in defaultHeaders)
//         {
//             request.SetHeader(header.Key, header.Value);
//         }
//         if (customHeaders != null)
//         {
//             foreach (var header in customHeaders)
//             {
//                 request.SetHeader(header.Key, header.Value);
//             }
//         }
//         if (body != null)
//         {
//             string jsonBody = JsonConvert.SerializeObject(body);
//             request.RawData = System.Text.Encoding.UTF8.GetBytes(jsonBody);
//         }
//         request.ConnectTimeout = TimeSpan.FromSeconds(8);
//         request.Timeout = TimeSpan.FromSeconds(30);
//         request.Send();
//         yield return null;
//
//     }
//
//     private void HandleResponse<T>(HTTPResponse response, Action<T> onSuccess, Action<string> onError)
//     {
//         if (response != null && response.IsSuccess)
//         {
//             if (IsValidJson(response.DataAsText) == false)
//             {
//                 // 修复：如果T是string，则允许直接传递字符串，否则报错
//                 if (typeof(T) == typeof(object))
//                 {
//                     onSuccess?.Invoke((T)(object)response.DataAsText);
//                 }
//                 else
//                 {
//                     onError?.Invoke("API 返回非 JSON 数据，且目标类型不是 string");
//                 }
//                 return;
//             }
//             try
//             {
//                 var apiResponse = JsonConvert.DeserializeObject<ApiResponse<T>>(response.DataAsText);
//                 if (apiResponse.code == 200)
//                 {
//                     onSuccess?.Invoke(apiResponse.data);
//                 }
//                 else
//                 {
//                     onError?.Invoke($"API Error {apiResponse.code}: {apiResponse.message}");
//                 }
//             }
//             catch (Exception ex)
//             {
//                 onError?.Invoke($"API JSON Parsing Error: {ex}");
//             }
//         }
//         else if(response != null)
//         {
//             onError?.Invoke($"Network Error: {response.StatusCode} - {response.Message}");
//         }
//         else
//         {
//             onError?.Invoke("Network Error!");
//             Debug.LogError("响应为空，可能因连接超时或服务器无响应");
//         }
//     }
//     private bool IsValidJson(string json)
//     {
//         try
//         {
//             // 尝试解析 JSON 数据，若格式错误会抛出异常
//             JToken.Parse(json);
//             return true; // JSON 格式正确
//         }
//         catch (JsonException)
//         {
//             return false; // JSON 格式错误
//         }
//     }
//
// }
