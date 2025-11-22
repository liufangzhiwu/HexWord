// using System.Collections;
// using System.Collections.Generic;
// using Unity.VisualScripting;
// using UnityEngine;
//
// public class APIGateway: MonoBehaviour
// {
//     private static APIGateway _instance;
//     public static APIGateway Instance => _instance;
//
//     [SerializeField] private string APIUrl = "https://zen.test.mindwordplay.cn/api/";
//
//     public LoginApi LoginApi { get; private set; }
//     public ConfigApi ConfigApi { get; private set; }
//     public LeaderboardApi LeaderboardApi { get; private set; }
//     public HTTPClient HttpClient { get; private set; }
//
//     private void Awake()
//     {
//         if (_instance != null && _instance != this)
//         {
//             Destroy(gameObject);
//         }
//         else
//         {
//             _instance = this;
//             DontDestroyOnLoad(gameObject);
//         }
//     }
//     private void Start()
//     {
//         HttpClient = HTTPClient.Instance.Initialize(APIUrl);
//         //HttpClient = HTTPClient.Instance.Initialize();
//
//         LoginApi = new LoginApi(HttpClient);
//         ConfigApi = new ConfigApi(HttpClient);
//         LeaderboardApi = new LeaderboardApi(HttpClient);
//         
//         //StartCoroutine(HttpClient.Get<object>("", 
//         // onSuccess=>
//         //{
//         //    Debug.Log($"API Gateway is ready.{onSuccess}");
//         //}, error =>
//         //{
//         //    Debug.LogError($"API Gateway initialization failed: {error}");
//         //}));
//     }
//
// }
