#if UNITY_OPENHARMONY
using UnityEngine;
using OpenHarmonyKits.Signal;
using UnityEngine.UI;
using OpenHarmonyKits.Param;
using UnityEditor;
using System;
using System.Collections.Generic;
using Middleware;
using Newtonsoft.Json;

namespace Middleware
{
    
public class Account_harmony : IAccounts
{
    public string UserId { get; set; }
    public bool IsLogin { get; set; } = false;
    string teamPlayerId = string.Empty;
    string thirdOpenId="";
    
    public void Init(float delay)
    {
        // var go = new GameObject("SignalHandler").AddComponent<SignalHandler>();
        // var go2 = new GameObject("SignalReceive").AddComponent<AdsStatusSignalHandle>();
        // Object.DontDestroyOnLoad(go);
        // Object.DontDestroyOnLoad(go2);
            
        UnityTimer.Delay(delay, () =>
        {
            InitGameService();
            InitGamePerformance();  
            Register();
        });
    }
    

    void Register()
    {
        SignalHandler.Instance.RegisterSignalDelegate<GamePlayerInitSignal>(OnGamePlayerInitTrigger);
        SignalHandler.Instance.RegisterSignalDelegate<LoginSignal>(OnLoginSignalTrigger);
        SignalHandler.Instance.RegisterSignalDelegate<LogoutSignal>(OnLogoutSignalTrigger);
        SignalHandler.Instance.RegisterSignalDelegate<Login_BindSignal>(OnLoginBindTrigger);
        SignalHandler.Instance.RegisterSignalDelegate<Login_UnBindSignal>(OnLoginUnBindTrigger);
        SignalHandler.Instance.RegisterSignalDelegate<Login_VerifySignal>(OnLoginVerifyTrigger);
        SignalHandler.Instance.RegisterSignalDelegate<SavePlayerRoleSignal>(OnSavePlayerTrigger);
        SignalHandler.Instance.RegisterSignalDelegate<PlayerChangedSingal>(OnPlayerChangedTrigger);
        SignalHandler.Instance.RegisterSignalDelegate<PlayerOnOffSignal>(OnPlayerOnOffTrigger);
        SignalHandler.Instance.RegisterSignalDelegate<GamePerformance_InitSignal>(OnGamePerformanceInit);
        SignalHandler.Instance.RegisterSignalDelegate<GamePerformance_UpdateSignal>(OnGamePerformanceUpdate);
    }

    private void OnDestroy()
    {
        if (SignalHandler.Instance != null)
        {
            SignalHandler.Instance.UnRegisterSignalDelegate<GamePlayerInitSignal>(OnGamePlayerInitTrigger);
            SignalHandler.Instance.UnRegisterSignalDelegate<LoginSignal>(OnLoginSignalTrigger);
            SignalHandler.Instance.UnRegisterSignalDelegate<LogoutSignal>(OnLogoutSignalTrigger);
            SignalHandler.Instance.UnRegisterSignalDelegate<Login_BindSignal>(OnLoginBindTrigger);
            SignalHandler.Instance.UnRegisterSignalDelegate<Login_UnBindSignal>(OnLoginUnBindTrigger);
            SignalHandler.Instance.UnRegisterSignalDelegate<Login_VerifySignal>(OnLoginVerifyTrigger);
            SignalHandler.Instance.UnRegisterSignalDelegate<SavePlayerRoleSignal>(OnSavePlayerTrigger);
            SignalHandler.Instance.UnRegisterSignalDelegate<PlayerChangedSingal>(OnPlayerChangedTrigger);
            SignalHandler.Instance.UnRegisterSignalDelegate<PlayerOnOffSignal>(OnPlayerOnOffTrigger);
            SignalHandler.Instance.UnRegisterSignalDelegate<GamePerformance_InitSignal>(OnGamePerformanceInit);
            SignalHandler.Instance.UnRegisterSignalDelegate<GamePerformance_UpdateSignal>(OnGamePerformanceUpdate);
        }
    }

    public void Login(bool isShowLoginPanel = false)
    {
        OHThirdAccountInfo info = new OHThirdAccountInfo();
        info.accountName = "Tuanjie";
        
        // if(GameDataManager.Instance.UserData.IsFirstLaunch||isShowLoginPanel)
        //     OHSDKKitManager.Instance.Login(info,true, LoginPanelType.ICON);
        // else
        // {
            OHSDKKitManager.Instance.Login(null,true, LoginPanelType.ICON);
        //}
    }

    public void Logout()
    {
        OHSDKKitManager.Instance.Logout();
    }

    public void SavePlayer()
    {
        
        if (!IsLogin)
        {
            Debug.LogError("请先登录再保存玩家信息");
            return;
        }
        
        var gSKPlayerRole = new GSKPlayerRole();
        //gSKPlayerRole.roleId = roleId;
        // gSKPlayerRole.roleName = roleName;
        OHSDKKitManager.Instance.SavePlayerInfo(gSKPlayerRole);
    }


    public void LoginBind()
    {
        if (!IsLogin)
        {
            Debug.LogError("please login at first before your bind playerId .");
            return;
        }
        OHSDKKitManager.Instance.BindPlayer(thirdOpenId,teamPlayerId);
    }

    public void LoginUnBind()
    {
        if (!IsLogin)
        {
            Debug.LogError("please login at first before your unbind playerId.");
            return;
        }
        OHSDKKitManager.Instance.UnBindPlayer(thirdOpenId,teamPlayerId);
    }

    public void VerifyPlayer()
    {
        var thirdUserInfo = new ThirdUserInfo();
        thirdUserInfo.thirdOpenId = thirdOpenId;
        thirdUserInfo.isRealName = true;
        OHSDKKitManager.Instance.VerifyCheck(thirdUserInfo);
    }

    public void PlayerOn()
    {
        if (OHSDKKitManager.ReceivePlayerChangedEvent)
        {
            Debug.LogError("Player changed event is receiving now, no need to on again.");
            return;
        }
        OHSDKKitManager.Instance.EnablePlayerChangedEvent();
    }

    public void PlayerOff()
    {
        if (!OHSDKKitManager.ReceivePlayerChangedEvent)
        {
            Debug.LogError("Player changed event is not receiving now, no need to off again.");
            return;
        }
        OHSDKKitManager.Instance.DisablePlayerChangedEvent();
    }

    public void InitGameService()
    {
        OHSDKKitManager.Instance.InitGameService();
    }

    public void InitGamePerformance() 
    {
        string bundleName = Application.identifier;
        string appVersion = Application.version;
        int messageType = 0;
        OHSDKKitManager.Instance.InitGamePerformance(bundleName, appVersion, messageType);
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
        OHGameConfigInfoParam gameConfigInfo = new OHGameConfigInfoParam();
        gameConfigInfo.messageType = 4;
        
        // // 其他自定义游戏数据（可扩展）
        // // 可以通过extra字段保存JSON格式的自定义数据
        // Dictionary<string, object> extraData = new Dictionary<string, object>
        // {
        //     { "achievements", GetAchievementCount() },
        //     { "rank", GetPlayerRank() },
        //     { "playTime", GetTotalPlayTime() },
        //     { "equipmentLevel", GetEquipmentLevel() },
        //     // ... 其他自定义数据
        // };
        //
        // // 如果有额外数据，序列化为JSON
        // if (extraData.Count > 0)
        // {
        //     string extraJson = JsonConvert.SerializeObject(extraData);
        //     // 如果GSKPlayerRole有extra字段
        //     gameConfigInfo.extra = extraJson;
        // }
        //
        // Debug.Log($"上传角色信息: ID={gSKPlayerRole.roleId}, 名称={gSKPlayerRole.roleName}, 等级={gSKPlayerRole.roleLevel}");
        // OHSDKKitManager.Instance.SavePlayerInfo(gSKPlayerRole);
        
       
       
       
        OHSDKKitManager.Instance.UpdateGameInfo(gameConfigInfo);
    }

    public void OnGamePlayerInitTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            GamePlayerInitSignal targetSignal = (GamePlayerInitSignal)signal;
            Debug.Log("[GamePlayerInit Success] " + "\n " + targetSignal.successMessage + "\n");
            Login();
        }
        else
        {
            Debug.Log(" [GamePlayerInit Error ]  Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }
    }

    public void OnLoginSignalTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            LoginSignal targetSignal = (LoginSignal)signal;
            teamPlayerId = targetSignal.localPlayer.teamPlayerId;
            UserId=targetSignal.localPlayer.gamePlayerId;
            Debug.Log("Login Success" + "\n "
                + "authorizationCode :" + targetSignal.authorizationCode + "\n "
                + "idToken : " + targetSignal.idToken + "\n"
                + "teamPlayerId : " + targetSignal.localPlayer.teamPlayerId + "\n"
                + "gamePlayerId : " + targetSignal.localPlayer.gamePlayerId + "\n");
            
            //设置登录用户ID（需要等待游戏数据获取后）
            AnalyticMgr.SetLoginUser(UserId);
            IsLogin = true;
            VerifyPlayer();
            
        }
        else
        {
            Debug.Log("Login Error" + "\n "
                +"Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }

    }

    public void OnLogoutSignalTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            LogoutSignal targetSignal = (LogoutSignal)signal;
            Debug.Log("Logout Success" + "\n "
               + "message" + targetSignal.state + "\n");
            teamPlayerId = string.Empty;
            IsLogin = false;
        }
        else
        {
            Debug.Log("Logout Error" + "\n "
               + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }

    }

    public void OnLoginBindTrigger(SignalBase signal)
    {

        if (!signal.hasError())
        {
            Login_BindSignal targetSignal = (Login_BindSignal)signal;
            Debug.Log("LoginBind Success" + "\n "
                + "thirdOpenId :" + targetSignal.thirdOpenId + "\n "
                + "teamPlayerId :" + targetSignal.teamPlayerId + "\n ");
            
            
        }
        else
        {
            Debug.Log("LoginBind Error" + "\n "
               + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }
    }

    public void OnLoginUnBindTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            Login_UnBindSignal targetSignal = (Login_UnBindSignal)signal;
            Debug.Log("LoginUnBind Success" + "\n "
                + "thirdOpenId :" + targetSignal.thirdOpenId + "\n "
                + "teamPlayerId : " + targetSignal.teamPlayerId + "\n ");
        }
        else
        {
            Debug.Log("LoginUnBind Error" + "\n "
               + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }

    }

    public void OnLoginVerifyTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            Login_VerifySignal targetSignal = (Login_VerifySignal)signal;
            Debug.Log("LoginVerify Success" + "\n "
                + "thirdOpenId: " + targetSignal.thirdOpenId + "\n "
                + "isRealName : " + targetSignal.isRealName + "\n ");
            SavePlayer();
        }
        else
        {
            Debug.Log("LoginVerify Error" + "\n "
               + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }

    }

    public void OnSavePlayerTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            SavePlayerRoleSignal targetSignal = (SavePlayerRoleSignal)signal;
            targetSignal.roleId= teamPlayerId;
            Debug.Log("SavePlayer Success" + "\n "
                + "roleId : " + targetSignal.roleId + "\n "
                + "roleName : " + targetSignal.roleName + "\n ");
            
            PlayerOn();
        }
        else
        {
            Debug.Log("SavePlayer Error" + "\n "
               + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }

    }
    /// <summary>
    /// 触发玩家状态变化
    /// </summary>
    /// <param name="signal"></param>
    public void OnPlayerChangedTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            PlayerChangedSingal targetSignal = (PlayerChangedSingal)signal;
            Debug.Log("Player Changed" + "\n "
                + "changedEvent : " + Enum.GetName(typeof(PlayerChangedEvent), targetSignal.changedEvent) + "\n ");
        }
    }
    /// <summary>
    /// 开启或关闭玩家状态变化的监听
    /// </summary>
    /// <param name="signal"></param>
    public void OnPlayerOnOffTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            PlayerOnOffSignal targetSignal = (PlayerOnOffSignal)signal;
            if (targetSignal.ReceivedPlayerChangeEvent == 1)
            {
                Debug.Log("Enable Player ChangeEvent" + "\n ");
            }
            else
            {
                Debug.Log("Disable Player ChangeEvent" + "\n ");
            }
        }
        else
        {
            Debug.Log("Change Player ChangeEvent Status Error" + "\n "
               + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }
    }

    public void OnGamePerformanceInit(SignalBase signal)
    {
        if (!signal.hasError())
        {
            GamePerformance_InitSignal targetSignal = (GamePerformance_InitSignal)signal;
            Debug.Log("GamePerformanceInit Success" + "\n "
                + "bundleName : " + targetSignal.bundleName + "\n "
             + " appVersion :" + targetSignal.appVersion + "\n "
             + $" messageType : {targetSignal.messageType}" + "\n ");
        }
        else
        {
            Debug.Log("GamePerformanceInit Error" + "\n "
               + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }

    }

    public void OnGamePerformanceUpdate(SignalBase signal)
    {
        if (!signal.hasError())
        {
            GamePerformance_UpdateSignal targetSignal = (GamePerformance_UpdateSignal)signal;
            Debug.Log("PerformanceUpdate Success" + "\n "
                + "extra message : " + targetSignal.extra + "\n "
                + $"\n messageType is{targetSignal.messageType}" + "\n ");
        }
        else
        {
            Debug.Log("PerformanceUpdate Error" + "\n "
               + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }
    }

    private void OnEnable()
    {
        OnDestroy();
    }
}

}

#endif
