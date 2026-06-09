#if UNITY_ANDROID
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Collections.Generic;
using Middleware;
using Newtonsoft.Json;

namespace Middleware
{

    public class Account_android : IAccounts
    {
        public string UserId { get; set; }
        public bool IsLogin { get; set; } = true;
        string teamPlayerId = string.Empty;
        string thirdOpenId = "";

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                InitGameService();
                InitGamePerformance();
            });
        }


        public void Login(bool isShowLoginPanel = false)
        {

        }

        public void Logout()
        {

        }

        public void SavePlayer()
        {

            if (!IsLogin)
            {
                Debug.LogError("请先登录再保存玩家信息");
                return;
            }
        }


        public void LoginBind()
        {
            if (!IsLogin)
            {
                Debug.LogError("please login at first before your bind playerId .");
                return;
            }

        }

        public void LoginUnBind()
        {
            if (!IsLogin)
            {
                Debug.LogError("please login at first before your unbind playerId.");
                return;
            }

        }

        public void VerifyPlayer()
        {

        }

        public void PlayerOn()
        {

        }

        public void PlayerOff()
        {

        }

        public void InitGameService()
        {

        }

        public void InitGamePerformance()
        {
            string bundleName = Application.identifier;
            string appVersion = Application.version;
            int messageType = 0;
        }

        /// <summary>
        /// 更新游戏玩家信息
        /// messageType
        /// 0:GamePackageInfo
        /// 1:GameConfigInfo
        /// 2:GameSceneInfo
        /// 3:GameNetInfo
        /// 4:GamePlayerInfo
        /// </summary>
        public void UpdateGameInfo()
        {

        }
    }

}

#endif
