using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;

public class Account_honor : IAccounts
{
    public bool IsLogin { get; set; }
    
    
    public void Init(float delay)
    {
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaClass honorSdk = new AndroidJavaClass("com.hihonor.mcs.game.HonorGameSdk");
            honorSdk.CallStatic("Init", currentActivity);
            Debug.Log("荣耀 SDK 初始化调用成功!");
        }
        catch (Exception e)
        {
            Debug.LogError("荣耀 SDK 初始化失败： " + e.Message);
        }
    }

    public void Login(bool isShowLoginPanel = false)
    {
        throw new System.NotImplementedException();
    }

    public void Logout()
    {
        throw new System.NotImplementedException();
    }

    public void VerifyPlayer()
    {
        throw new System.NotImplementedException();
    }
}
