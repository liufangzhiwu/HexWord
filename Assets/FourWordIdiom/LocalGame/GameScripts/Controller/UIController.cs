// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using UnityEngine;
// using Unity.Passport.Runtime.UI;
// using Unity.Passport.Runtime;
//
// namespace Unity.Passport.Sample.Scripts
// {
//     public class UIController : MonoBehaviour
//     {
//         public static UIController Instance;
//         
//         public bool IsCompleteLogin=false;
//         
//         public bool IsLogout=false;
//         
//         string userType="";
//         
//         Persona persona = null;
//         
//         // sdk 配置（Config 是 SDK 初始化时的配置）
//         private readonly PassportUIConfig _config = new()
//         {
//             AutoRotation = true, // 是否开启自动旋转，默认值为 false。
//             InvokeLoginManually = false, // 是否通过自行调用 Login 函数启动登录面板，默认值为 false。
//             Theme = PassportUITheme.Dark, // 风格主题配置。
//             UnityContainerId = "unity-container" // WebGL 场景下 Unity 实例容器 Id。
//         };
//
//         // sdk 回调
//         private void _callback(PassportEvent e)
//         {
//             // event: 不同情况下的回调事件，详情可以参考下面的回调类型。
//             switch (e)
//             {
//                 case PassportEvent.RejectedTos:
//                     Debug.Log("用户拒绝了协议");
//                     break;
//                 case PassportEvent.LoggedIn:
//                     Debug.Log("完成登录");
//                     break;
//                 case PassportEvent.Completed:
//                     Debug.Log("完成所有流程");
//                     SelectPersona();
//                     break;
//                 case PassportEvent.LoggedOut:
//                     Debug.Log("用户登出");
//                     break;
//             }
//
//         }
//         
//         private void Awake()
//         {
//             if (Instance == null)
//             {
//                 Instance = this;
//                 DontDestroyOnLoad(gameObject); // 保持广告管理器在场景切换时不销毁
//             }
//         }
//
//         public void InitPassportUI()
//         {
//             // 调用 SDK
//             PassportUI.Init(_config, _callback);
//         }
//         
//         public void ShowLoginScreen()
//         {
//             // 调用 SDK
//             PassportUI.Init(_config, _callback);
//
//             InitPassportFeature();
//         }
//
//         private async void InitPassportFeature()
//         {
//             try
//             {
//                 await PassportFeatureSDK.Initialize();
//             }
//             catch (PassportException e)
//             {
//                 Debug.Log($"failed to initialize sdk: {e.Message}");
//                 throw;
//             }
//         }
//
//         // 登出
//         public void Logout()
//         {
//             PassportUI.Logout();
//         }
//         
//         /// <summary>
//         /// 检查玩家充值金额是否受限
//         /// </summary>
//         /// <param name="price"></param>
//         public async Task<bool> CheckPayable(int price)
//         {
//             //成年跳过
//             if(userType=="adult") return true;
//             
//             uint amount = (uint)price;
//             var payable = await PassportFeatureSDK.AntiAddiction.CheckPayable(amount);
//             if (!payable.Approved)
//             {
//                 MessageSystem.Instance.ShowTip(payable.Reason);
//             }
//             return payable.Approved;
//         }
//         
//         /// <summary>
//         /// 上报玩家充值金额
//         /// </summary>
//         /// <param name="price"></param>
//         public async void SubmitPayment(int price)
//         {
//             uint amount = (uint)price;
//             await PassportFeatureSDK.AntiAddiction.SubmitPayment(amount);
//         }
//
//         // 选择角色
//         private async void SelectPersona()
//         {
//             // 选择域
//             var realms = await PassportSDK.Identity.GetRealms(); // 获取域列表
//             var realmID = realms[0].RealmID; // 根据需要自行选择域
//             // var realmID = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"; // 也可以填写固定的 RealmID 而不是动态获取
//
//             // 获取（或创建）与选择角色
//             var personas = await PassportSDK.Identity.GetPersonas(); // 获取角色列表
//             if (!personas.Any())
//             {
//                 // 若没有角色，则新建角色
//                 persona = await PassportSDK.Identity.CreatePersona("YourDisplayName", realmID);
//             }
//             else
//             {
//                 // 若有角色，则选择第一个角色
//                 persona = personas[0];
//             }
//             
//             if (persona!=null)
//             {
//                 GameDataManager.instance.LoadPlayerProfile(persona);
//                 //Debug.Log("Gold: " + persona.Properties["Gold"]);
//                 
//                 // 选择角色
//                 await PassportSDK.Identity.SelectPersona(persona.PersonaID);
//             }
//             else
//             {
//                 GameDataManager.instance.LoadPlayerProfile(null);
//                 Debug.Log("Load Fail Gold: ");
//             }
//             
//             CheckLogin();
//            
//         }
//         
//         private async void CheckLogin()
//         {
//             var playableData = await PassportFeatureSDK.AntiAddiction.CheckPlayable();
//             if (playableData.Playable)
//             {
//                 Debug.Log("游戏可玩");
//                 // 打印所有字段信息
//                 Debug.Log($"UserId: {playableData.UserId}\n" +
//                           $"DisplayName: {playableData.DisplayName}\n" +
//                           $"UserType: {playableData.UserType}\n" +
//                           $"Playable: {playableData.Playable}\n" +
//                           $"Reason: {playableData.Reason}\n" +
//                           $"RemainingTimeInSecond: {playableData.RemainingTimeInSecond}\n" +
//                           $"Description: {playableData.Description}");
//                 
//                 IsCompleteLogin = true;
//                 await Task.Delay(2000);
//                 userType = playableData.UserType;
//                 
//                 if (SystemManager.Instance != null)
//                 {
//                     EventDispatcher.instance.TriggerOnUpdateGameLobbyUI();
//                     EventDispatcher.instance.TriggerChangeGoldUI(0, false);
//                     SystemManager.Instance.HidePanel(PanelType.AppRating);
//                 }
//
//                 if (WaterManager.instance!=null)
//                 {
//                     WaterManager.instance.ClearWater();
//                 }
//                     
//             }
//             else
//             {
//                 Debug.Log("游戏不可玩");
//                 MessageSystem.Instance.ShowTip(playableData.Description,false,MessageShowType.Window);
//             }
//         }
//
//         /// <summary>
//         /// 更新用户信息
//         /// </summary>
//         /// <param name="userInfo"></param>
//         public async void UpdateUserInfo(Dictionary<string, string> userInfo)
//         {
//             var realms = await PassportSDK.Identity.GetRealms(); // 获取域列表
//             var realmID = realms[0].RealmID; // 根据需要自行选择域
//             
//             PassportSDK.Identity.UpdatePersona("YourDisplayName", realmID,userInfo);
//         }
//     }
// }