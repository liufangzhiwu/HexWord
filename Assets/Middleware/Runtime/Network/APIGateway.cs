using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using Unity.VisualScripting;
using UnityEngine;

public class APIGateway: MonoBehaviour
{
    private static APIGateway _instance;
    public static APIGateway Instance => _instance;

    private string APIUrl = "https://zen.prod.mindwordplay.cn/api/";
    //测试服："https://zen.test.mindwordplay.cn/api/";
    //正式服："https://hex.prod.mindwordplay.cn/api/";

    public LoginApi LoginApi { get; private set; }
    public GameConfigApi GameConfigApi { get; private set; }
    public LeaderboardApi LeaderboardApi { get; private set; }
    public HTTPClient HttpClient { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    
    private void Start()
    {
        
#if Unity_ShowLog
            APIUrl = "https://zen.test.mindwordplay.cn/api/";
#else
            APIUrl = "https://hex.prod.mindwordplay.cn/api/";
#endif
        
        HttpClient = HTTPClient.Instance.Initialize(APIUrl);
        //HttpClient = HTTPClient.Instance.Initialize();

        LoginApi = new LoginApi(HttpClient);
        GameConfigApi = new GameConfigApi(HttpClient);
        LeaderboardApi = new LeaderboardApi(HttpClient);
        
        //StartCoroutine(HttpClient.Get<object>("", 
        // onSuccess=>
        //{
        //    Debug.Log($"API Gateway is ready.{onSuccess}");
        //}, error =>
        //{
        //    Debug.LogError($"API Gateway initialization failed: {error}");
        //}));
    }

}
