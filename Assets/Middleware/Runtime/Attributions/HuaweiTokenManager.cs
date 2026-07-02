using System;
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Text;

public class HuaweiTokenManager : MonoBehaviour
{
    public static HuaweiTokenManager self;
    // 请将下面的值替换为你在华为后台获取的真实值
    private const string CLIENT_ID = "1979065602840486336";
    private const string CLIENT_SECRET = "0F209BBC00F6599D5BED0CD78736F0E9124E8AAA46273EB0B4379D2B8A280197";
    // 根据你的业务选择正确的URL
    private const string TOKEN_URL = "https://connect-api.cloud.huawei.com/api/oauth2/v1/token";

    [System.Serializable]
    private class TokenRequest
    {
        public string grant_type;
        public string client_id;
        public string client_secret;
    }

    [System.Serializable]
    private class TokenResponse
    {
        public string access_token;
        public long expires_in;
        public string token_type;
    }

    private void Awake()
    {
        self = this;    
    }

    public void GetAccessToken(System.Action<string> onSuccess, System.Action<string> onError)
    {
        StartCoroutine(GetTokenCoroutine(onSuccess, onError));
    }

    private IEnumerator GetTokenCoroutine(System.Action<string> onSuccess, System.Action<string> onError)
    {
        TokenRequest requestBody = new TokenRequest
        {
            grant_type = "client_credentials",
            client_id = CLIENT_ID,
            client_secret = CLIENT_SECRET
        };
        string jsonBody = JsonUtility.ToJson(requestBody);

        using (UnityWebRequest request = new UnityWebRequest(TOKEN_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[HuaweiAttr] Token获取成功: " + request.downloadHandler.text);
                TokenResponse response = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
                onSuccess?.Invoke(response.access_token);
            }
            else
            {
                Debug.Log("[HuaweiAttr] Token获取失败: " + request.error);
                onError?.Invoke(request.error);
            }
        }
    }
}