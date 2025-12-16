using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;

public class Account_honor : IAccounts
{
    public bool IsLogin { get; set; }
    private AndroidJavaObject _currentActivity;
    
    public void Init(float delay)
    {

        UnityTimer.Delay(delay, () =>
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            _currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        });
    }

    public void Login(bool isShowLoginPanel = false)
    {
        try
        {
            AndroidJavaClass honorSdk = new AndroidJavaClass("com.hihonor.mcs.game.HonorGameSdk");
            honorSdk.CallStatic("Init", _currentActivity);
            Debug.Log("荣耀 SDK 初始化调用成功!");
        }
        catch (Exception e)
        {
            Debug.LogError("荣耀 SDK 初始化失败： " + e.Message);
        }
    }

    public void Logout()
    {
        // 退出挽留
        _currentActivity.Call("exitControl");
    }

    public void VerifyPlayer()
    {
        
    }

    /// <summary>
    /// 上报游戏角色
    /// </summary>
    public void ReportRole(string roleId, string roleName, string chapter)
    {
        _currentActivity.Call("reportUserGameInfoData", roleId, roleName, chapter);
    }
  
}
