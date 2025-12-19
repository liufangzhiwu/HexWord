using System;
using System.Collections;
using Middleware;
using UnityEngine;

namespace FourWordIdiom.LocalGame.GameScripts.RootContainer
{
    public class HonorManager: MonoBehaviour
    {
        public static HonorManager Instance;
        // --- 激励视频事件 ---
        public event Action OnRewardAdReady;              // 视频加载成功，按钮可点击
        public event Action<string> OnRewardAdLoadFail;   // 视频加载失败 (带错误信息)
        public event Action<string> OnUserEarnedReward;   // 玩家看完视频，获得奖励 (带奖励信息)
    
        // --- 插屏广告事件 ---
        public event Action OnInterstitialReady;          // 插屏加载成功
        public event Action<string> OnInterstitialLoadFail;          // 插屏展示结果
        public event Action OnInterstitialClose;           // 插屏关闭 (相当于播放完成)
        
        private void Awake()
        {
            gameObject.name = "HonorManager";
            
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 登录成功
        /// </summary>
        /// <param name="result"></param>
        public void OnLoginSuccess(string result)
        {
            Game.Accounts.IsLogin = true;
            UserData user = GameDataManager.Instance.UserData;
            Game.Accounts.ReportRole(user.UserId, user.UserName, user.CurrentHexStage.ToString());
        }
        
        /// <summary>
        /// 退游挽留
        /// </summary>
        public void OnExitControl(string code)
        {
            Debug.Log("[HonorManager] 收到退出回调，保存数据并退出 " + code);
            GameDataManager.Instance.CommitGameData();
            QuitGame();
        }

        /// <summary>
        /// 防沉迷时间通知
        /// </summary>
        /// <param name="msg"></param>
        public void OnAntiAddictionTimeOut(string msg)
        {
            Debug.LogWarning("防沉迷时间已到，正在强制下线...");
            GameDataManager.Instance.CommitGameData();
            // 暂停游戏逻辑
            Time.timeScale = 0;
            StartCoroutine(ForceExitRoutine());
        }
        private IEnumerator ForceExitRoutine()
        {
            // 方案 A：直接弹个原生 Toast (简单)
            // AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
            // 这里写 Toast 代码比较麻烦，建议直接用 UI

            // 方案 B：显示一个简单的 Unity UI (推荐)
            // 假设你有一个全局 UI 管理器
            MessageSystem.Instance.ShowTip( "防沉迷提示: 根据相关规定，您的游戏时间已耗尽，即将退出游戏。");
            
            // 等待 3 秒，让玩家看清字（或者等数据写盘完成）
            yield return new WaitForSecondsRealtime(3f); 
            
            // 4. 退出游戏
            QuitGame();
        }
        
        // 封装通用的退出逻辑
        private void QuitGame()
        {
            Application.Quit();
            
            // 确保安卓进程被杀掉
            if (Application.platform == RuntimePlatform.Android)
            {
                try 
                {
                    using (AndroidJavaClass javaClass = new AndroidJavaClass("java.lang.System"))
                    {
                        javaClass.CallStatic("exit", 0);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Java exit failed: " + e.Message);
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        /// <summary>
        /// 成功发货回调
        /// </summary>
        /// <param name="productId"></param>
        public void OnDeliverProduct(string productId)
        {
            Debug.Log($"[HonorListener] 收到安卓底层的发货通知: {productId}");
            // 1. 先发货（钱肯定是要给的）
            ShopManager.shopManager.DeliverItem(productId);
            // 2. 🔥 检查标记位，决定怎么弹窗
            if (ShopManager.shopManager.IsPurchasing)
            {
                ShopManager.shopManager.IsPurchasing = false;
                MessageSystem.Instance.ShowTip("购买成功!");
            }
            else
            {
                // 情况 B：玩家没点购买（这是启动时的补单） -> 静默处理
                // 建议给一个小提示，不然玩家发现金币突然变多了会困惑
                MessageSystem.Instance.ShowTip("检测到未到账订单，已为您补发商品!");
            }
        }
        
        /// <summary>
        /// 支付失败回调
        /// </summary>
        /// <param name="errorMsg"></param>
        public void OnPurchaseFailed(string errorMsg)
        {
            // 如果之前显示了 Loading，现在要关掉
            if (ShopManager.shopManager.IsPurchasing)
            {
                // UIManager.Instance.ShowLoading(false);
        
                // 弹窗告诉玩家失败原因
                MessageSystem.Instance.ShowTip("支付取消或失败");
        
                // ✅ 重置标记
                ShopManager.shopManager.IsPurchasing = false;
            }
        }
        
        // ============ 插屏广告 ============
        // 插屏加载成功
        public void OnInterstitialLoaded(string msg) {
            Debug.Log("插屏广告准备就绪");
            OnInterstitialReady?.Invoke();
        }
        
        // 插屏加载失败
        public void OnInterstitialLoadFailed(string msg) {
            Debug.Log("插屏加载失败: " + msg);
            OnInterstitialLoadFail?.Invoke(msg);
        }

        // 插屏关闭 (恢复游戏逻辑)
        public void OnInterstitialClosed(string msg) {
            Debug.Log("玩家关闭了插屏，继续游戏");
            OnInterstitialClose?.Invoke();
            // 如果之前暂停了游戏，在这里 Time.timeScale = 1;
        }
        
        public void OnRewardAdLoaded(string msg) {
            Debug.Log("视频准备好了，可以显示按钮了");
            // videoButton.interactable = true;
            OnRewardAdReady?.Invoke();
        }

        public void OnRewardAdLoadFailed(string msg) {
            Debug.Log("视频加载失败: " + msg);
            OnRewardAdLoadFail?.Invoke(msg);
        }
        // 🔥 玩家看完了，发钱！
        public void OnAdRewarded(string msg) {
            Debug.Log("获得奖励: " + msg);
            // GameManager.Instance.AddCoins(100);
            // Game.Ads?.Init();
            OnUserEarnedReward?.Invoke(msg);
        }
        public void OnRewardAdShowFailed(string msg) {
            Debug.LogWarning("HonorManager: 观看失败/取消 - " + msg);
        
            // 触发失败事件
            OnRewardAdLoadFail?.Invoke(msg);

            // 如果你有通用的 Toast 提示，可以在这里弹
            // ShowToast("请完整观看视频才能获得奖励");
        }
        
        void OnGUI() {
            // 在屏幕左上角画个大按钮，用来测试 C# 方法本身是不是好的
            if (GUI.Button(new Rect(0, 0, 200, 100), "测试接收消息")) {
                // 自己调自己，模拟安卓发消息
                OnRewardAdLoadFailed("Test_Message");
            }
        }
    }
}