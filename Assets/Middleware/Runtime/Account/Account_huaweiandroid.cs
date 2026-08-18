#if UNITY_HUAWEI
using System;
using System.Collections;
using UnityEngine;
using HuaweiService;
using HuaweiService.Account;
using Middleware;
using Newtonsoft.Json;
using UnityEngine.HuaweiAppGallery;
using UnityEngine.HuaweiAppGallery.Listener;
using UnityEngine.HuaweiAppGallery.Model;
using AccountAuthParamsHelper = HuaweiService.Account.AccountAuthParamsHelper;
using Exception = System.Exception;

namespace Middleware
{
    public enum GameFlowStatus 
    {
        NotStarted,
        
        // --- 1. 初始化阶段 ---
        Initializing,
        InitFailed,
        
        // --- 2. 更新检查阶段 ---
        CheckingUpdate,
        UpdateRequired, // 需要更新，等待用户操作
        
        // --- 3. 登录阶段 ---
        LoggingIn,
        SilentFailed,
        LoginFailed,
            
        // 获取用户信息
        GetGamePlayer,
        GetGamePlayerFailed,
        // 上传角色信息
        GamePlayerSave,
        GamePlayerSaveFailed,
        // --- 4. 完成状态 ---
        Ready // 所有流程完成，游戏可以启动
    }

    /// <summary>
    /// 华为安卓平台账号实现（IAccounts接口）
    /// 功能：游戏服务初始化、更新检查、静默/显式登录、获取玩家信息、上报角色数据
    /// 使用方法：Init() -> 等待初始化完成 -> Login() -> 轮询 IsLogin 直到 true 或超时
    /// </summary>
    public class Account_huaweiandroid : IAccounts
    {
        // ========== IAccounts 接口属性 ==========
        public string UserId { get; set; }          // 用户唯一标识（OpenId）
        public bool IsLogin { get; set; } = false;  // 登录状态标志
        public AuthAccount CurrentAuthAccount { get; private set; } // 华为账户详细信息

        // 外部事件（可选监听）
        public Action<bool> OnLoginCompleted;       // 登录完成时触发（参数：是否成功）
        public Action<bool> OnFullFlowCompleted;    // 完整流程（初始化+更新+登录+上报）完成时触发

        // 华为 SDK 相关对象
        private AccountAuthParams mAuthParam;       // 账户鉴权参数
        private AccountAuthService mAuthService;    // 账户鉴权服务
        private bool isInitialized = false;         // 是否初始化完成（游戏服务+更新检查）
        private bool isLogining = false;             // 是否正在登录中（防止重复登录）
        
        private int _retryAttempt = 0;
        private const int MAX_RETRIES = 3; // 设置最大重试次数
        private const float RETRY_DELAY = 0.5f; // 重试间隔（秒）

        // 内部流程状态（仅用于调试）
        private enum HuaweiFlowState { None, InitGameService, CheckUpdate, SilentLogin, ActiveLogin, GetPlayer, SavePlayer, Done, Error }
        
        private GameFlowStatus _flowStatus = GameFlowStatus.NotStarted;

        // ========== IAccounts 接口实现 ==========
        /// <summary>
        /// 初始化华为账号模块（延迟执行）
        /// </summary>
        /// <param name="delay">延迟秒数，通常传0立即启动</param>
        public void Init(float delay)
        {
            Debug.Log("[HuaweiAccount] Init called, will start after delay: " + delay);
            UnityTimer.Delay(delay, () =>
            {
                Debug.Log("[HuaweiAccount] Starting InitializeGameService init coroutine");
                Game.self.StartCoroutine(InitializeSDK());
            });
        }
        
        /// <summary>
        /// 初始化加载流程
        /// </summary>
        IEnumerator InitializeSDK()
        {
            if (!UIUtilities.isEditMode)
            {
                 Debug.Log($"[HuaweiAccount] 进入初始化游戏服务流程");
                //初始化游戏服务 
                yield return InitializeGameService();
                yield return new WaitUntil(() => _flowStatus == GameFlowStatus.LoggingIn);
                // 登录开始
                yield return LoadHuaweiGameLogin();
                yield return new WaitUntil(() => _flowStatus is GameFlowStatus.Ready);
                //设置登录用户ID（需要等待游戏数据获取后）
                AnalyticMgr.SetLoginUser(Game.self.Accounts.UserId);
            }
        }
        
        // ========== 内部初始化协程 ==========
        /// <summary>
        /// 初始化华为游戏服务 + 检查更新
        /// </summary>
        private IEnumerator InitializeGameService()
        {
            Action<GameFlowStatus> statusSetter = (status) => { _flowStatus = status; };
            if (_retryAttempt >= MAX_RETRIES)
            {
                _flowStatus = GameFlowStatus.InitFailed;
                Debug.LogError($"[HuaweiAccount] 初始化游戏服务失败,并退出游戏");
                yield break;
            }

            _retryAttempt++;
            _flowStatus = GameFlowStatus.Initializing;
            HuaweiGameService.Init(new AntiAddictionHandler(), new InitHandler(statusSetter, () =>
            {
                _flowStatus = GameFlowStatus.InitFailed;
                Game.self.StartCoroutine(RetryAfterDelay(RETRY_DELAY));
                Debug.LogError($"[HuaweiAccount] 初始化游戏服务失败，重试次数：{_retryAttempt}");
            }));
            yield return new WaitUntil(() => _flowStatus is GameFlowStatus.CheckingUpdate);
            Debug.LogError($"[HuaweiAccount] 进入检查更新流程");
            HuaweiGameService.CheckUpdate(new CheckUpdateListener(statusSetter));
            Debug.LogError($"[HuaweiAccount] 检查更新流程完成");
            yield return new WaitUntil(() => _flowStatus is GameFlowStatus.LoggingIn);
            Debug.LogError($"[HuaweiAccount] 进入登录流程");
        }
        
        
        // 助手协程：用于在重试前等待一段时间
        private IEnumerator RetryAfterDelay(float delay)
        {
            MessageSystem.Instance.ShowTip($"等待 {delay} 秒后重试...");
            yield return new WaitForSeconds(delay);
    
            // 🔑 关键：重新启动初始化流程
            Game.self.StartCoroutine(InitializeGameService());
        }
        
        
        private IEnumerator LoadHuaweiGameLogin()
        {
            Game.self.loginStart = Time.time;
            
            Action<GameFlowStatus> statusSetter = (status) => { _flowStatus = status; };
        
            HuaweiGameService.SilentSignIn(new SilentLoginListener(statusSetter));
            if (_flowStatus == GameFlowStatus.SilentFailed)
            {
                Game.self.loginTimeout = 30f;
                HuaweiGameService.Login(new SilentLoginListener(statusSetter));
            }
            Game.self.loginStart = Time.time;
            Debug.Log("[HuaweiAccount] 登录完成, 当前状态" + _flowStatus );
            yield return new WaitUntil(() => _flowStatus is GameFlowStatus.GetGamePlayer);
            Player _player = null;
            HuaweiGameService.GetGamePlayer(new GetGamePlayerListener(statusSetter, player=> _player = player));
            Debug.Log("[HuaweiAccount] 获取用户信息, 当前状态" + _flowStatus );
            yield return new WaitUntil(() => _flowStatus is GameFlowStatus.GamePlayerSave);
            GameDataManager.Instance.UserData.UserId = _player.OpenId;
            AppPlayerInfo appPlayerInfo = new AppPlayerInfo();
            appPlayerInfo.Rank = "test rank";
            appPlayerInfo.Area = "test area";
            appPlayerInfo.Role = GameDataManager.Instance.UserData.UserId;
            appPlayerInfo.Sociaty = "sociaty";
            appPlayerInfo.PlayerId = _player.PlayerId;
            appPlayerInfo.OpenId = _player.OpenId;
            Game.self.Accounts.UserId = _player.OpenId;
            Game.self.Accounts.IsLogin = true;
        
            Debug.LogFormat("[HuaweiAccount] 登录华为安卓用户时的数据: {0}", JsonConvert.SerializeObject(appPlayerInfo));
            HuaweiGameService.SavePlayerInfo(appPlayerInfo.ConvertToJavaObject(), new SavePlayerInfoListener(statusSetter));
            Debug.Log("[HuaweiAccount] 数据上报完成, 当前状态" + _flowStatus );
            yield return new WaitUntil(() => _flowStatus is GameFlowStatus.Ready);
    
        }
        
        

        /// <summary>
        /// 登录入口（异步非阻塞）
        /// </summary>
        /// <param name="isShowLoginPanel">静默失败时是否自动拉起登录界面</param>
        public void Login(bool isShowLoginPanel = false)
        {
            // 重置状态
            ResetFlow();
            Game.self.StartCoroutine(InitializeSDK());
        }
        
        /// <summary>
        /// 重置所有流程状态（用于重试或手动重新登录）
        /// </summary>
        private void ResetFlow()
        {
            // 停止当前正在运行的所有相关协程
            Game.self.StopCoroutine(InitializeSDK());
    
            // 重置状态变量
            _flowStatus = GameFlowStatus.NotStarted;
            _retryAttempt = 0;
            isInitialized = false;
            isLogining = false;
            IsLogin = false;
            UserId = null;
            CurrentAuthAccount = null;
    
            Debug.Log("[HuaweiAccount] 状态已重置，可以重新开始登录流程");
        }

        /// <summary>
        /// 登出华为账号
        /// </summary>
        public void Logout()
        {
            if (mAuthService != null)
            {
                mAuthService.signOut();
                Debug.Log("[HuaweiAccount] Signed out via AuthService");
            }
            IsLogin = false;
            UserId = null;
            CurrentAuthAccount = null;
            Debug.Log("[HuaweiAccount] Logout completed, IsLogin=false");
        }

        public void VerifyPlayer()
        {
            throw new NotImplementedException();
        }
    }

    // 🔑 1. 定义局部实现类 IAntiAddictionListener
    public class AntiAddictionHandler : IAntiAddictionListener
    {
        public void OnExit()
        {
            Debug.Log("防沉迷退出回调：退出应用。");
            GameDataManager.Instance.CommitGameData();
            Application.Quit();
        }
    }
    
    // 🔑 2. 定义局部实现类 IInitListener
    public class InitHandler : IInitListener
    {
        private readonly Action<GameFlowStatus> _onCompletedCallback;
        private readonly Action  _onRetryAction;

        public InitHandler(Action<GameFlowStatus> onCompletedCallback, Action onRetryAction = null)
        {
            _onCompletedCallback = onCompletedCallback;
            _onRetryAction = onRetryAction;
        }
        public void OnSuccess()
        {
            HuaweiGameService.ShowFloatWindow();
            _onCompletedCallback?.Invoke(GameFlowStatus.CheckingUpdate);
        }

        public void OnFailure(int code, string message)
        {
            string msg = $"JosAppsClient init failed, code:{code} message:{message}";
            switch (code)
            {
                case 7002:
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        MessageSystem.Instance.ShowTip("请检查网络");
                        _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    });
                    break;
                case 7401:
                    _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    Application.Quit();
                    break;
                case 907135003:
                    _onRetryAction?.Invoke();
                    break;
                default:
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                         MessageSystem.Instance.ShowTip(msg);
                        _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    });
                    break;
            }
        }
    }
    
    public class CheckUpdateListener : ICheckUpdateListener
    {
        private readonly Action<GameFlowStatus> _onCompletedCallback;

        public CheckUpdateListener(Action<GameFlowStatus> onCompletedCallback)
        {
            _onCompletedCallback = onCompletedCallback;
        }
        public  void OnUpdateInfo(AndroidJavaObject intent)
        {
            if (intent !=null)
            {
                int status = intent.Call<int>("getIntExtra", "status", 0);
                if (status==0)
                {
                    // 无需更新，直接进入下一阶段：登录
                    _onCompletedCallback?.Invoke(GameFlowStatus.LoggingIn);
                }
                else if (status == 7)
                {
                    // 发现更新，等待用户操作或退出
                    _onCompletedCallback.Invoke(GameFlowStatus.UpdateRequired);
                    AndroidJavaObject apkUpgradeInfo = intent.Call<AndroidJavaObject>("getSerializableExtra", "updatesdk_update_info");
                    HuaweiGameService.ShowUpdateDialog(apkUpgradeInfo, true);
                    // bool isExit = intent.Call<bool>("getBooleanExtra", ",", false);
                    // TODO
                }
                else
                {
                    _onCompletedCallback?.Invoke(GameFlowStatus.LoggingIn);
                }
            }
            else
            {
                _onCompletedCallback?.Invoke(GameFlowStatus.LoggingIn);
            }
        }

        public void OnMarketInstallInfo(AndroidJavaObject intent)
        {
           
        }

        public void OnMarketStoreError(int responseCode)
        {
           
        }

        public void OnUpdateStoreError(int responseCode)
        {
           
        }
    }
    
    public class SilentLoginListener : ILoginListener
    {
        private readonly Action<GameFlowStatus> _onLoginCompleted;

        public SilentLoginListener(Action<GameFlowStatus> onLoginCompleted)
        {
            _onLoginCompleted = onLoginCompleted;
        }
        
        public void OnSuccess(SignInAccountProxy signInAccountProxy)
        {
            if (signInAccountProxy == null)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _onLoginCompleted?.Invoke(GameFlowStatus.SilentFailed);
                });
                return;
            }
            
            string msg = "get login success with signInAccountProxy info: \n";
            msg += String.Format("displayName:{0}, email:{1}, uid:{2}, openId:{3}, unionId:{4}, accessToken:{5}, serverAuthCode:{6}, idToken:{7}",
                signInAccountProxy.DisplayName, signInAccountProxy.Email, signInAccountProxy.Uid, signInAccountProxy.OpenId, signInAccountProxy.UnionId,
                signInAccountProxy.AccessToken, signInAccountProxy.ServerAuthCode, signInAccountProxy.IdToken);
            // MessageSystem.Instance.ShowTip(msg);
            _onLoginCompleted.Invoke(GameFlowStatus.GetGamePlayer);
        }

        public void OnSignOut()
        {
            string msg = "sign out success.";
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                MessageSystem.Instance.ShowTip(msg);
            });
        }

        public void OnFailure(int code, string message)
        {
            string msg = "account method failed, code:" + code + " message:" + message;
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _onLoginCompleted?.Invoke(GameFlowStatus.SilentFailed);
                //Game.self.ShowLoginErrorPanel(); //等错误处理
            });
        }
    }
    public class GetGamePlayerListener : IGetPlayerListener
    {
        private readonly Action<GameFlowStatus> _onGetPlayerCompleted;
        private readonly Action<Player> _owner;

        public GetGamePlayerListener(Action<GameFlowStatus> onGetPlayerCompleted, Action<Player> owner)
        {
            _onGetPlayerCompleted = onGetPlayerCompleted;
            _owner = owner;
        }
        public void OnSuccess(Player player)
        {
            if (player == null)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _onGetPlayerCompleted?.Invoke(GameFlowStatus.GetGamePlayerFailed);
                    MessageSystem.Instance.ShowTip("用户信息为空,请检查！");
                });
                return;
            }
            var msg = "getGamePlayer succeed. \n";
            msg += string.Format(
                "displayName:{0}, playerId:{1}, playerSign:{2}, openId:{3}, unionId:{4}, openIdSign:{5}, accessToken:{6}",
                player.DisplayName, player.PlayerId, player.PlayerSign, player.OpenId, player.UnionId, player.OpenIdSign, player.AccessToken
            );
            _owner?.Invoke(player);
            _onGetPlayerCompleted?.Invoke(GameFlowStatus.GamePlayerSave);
        }

        public void OnFailure(int code, string message)
        {
            var msg = "getCurrentPlayer failed, code:" + code + " message:" + message;
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _onGetPlayerCompleted?.Invoke(GameFlowStatus.GetGamePlayerFailed);
                MessageSystem.Instance.ShowTip(msg);
            });
        }
    }
    
    public class SavePlayerInfoListener : ISavePlayerInfoListener
    {
        private readonly Action<GameFlowStatus> _onSavePlayerInfoCompleted;

        public SavePlayerInfoListener(Action<GameFlowStatus> onSavePlayerInfoCompleted)
        {
            _onSavePlayerInfoCompleted = onSavePlayerInfoCompleted;
        }
        public void OnSuccess()
        {
            _onSavePlayerInfoCompleted?.Invoke(GameFlowStatus.Ready);
        }
        
        public void OnFailure(int code, string message)
        {
            _onSavePlayerInfoCompleted?.Invoke(GameFlowStatus.Ready);
        }
    }
}
#endif