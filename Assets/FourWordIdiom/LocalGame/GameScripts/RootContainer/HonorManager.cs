using System;
using System.Collections;
using Middleware;
using UnityEngine;

namespace FourWordIdiom.LocalGame.GameScripts.RootContainer
{
    public class HonorManager: MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
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
        public void OnExitControl()
        {
            Debug.Log("[HonorManager] 收到退出回调，保存数据并退出");
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
        
        

    }
}