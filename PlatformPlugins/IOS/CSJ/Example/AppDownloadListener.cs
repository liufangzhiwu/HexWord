#if UNITY_IOS
using System.Threading;
using ByteDance.Union;
using Middleware;
using UnityEngine;


public sealed class AppDownloadListener : IAppDownloadListener
{
    private Ads_ios Ads_ios;

    public AppDownloadListener(Ads_ios Ads_ios)
    {
        this.Ads_ios = Ads_ios;
    }

    public void OnIdle()
    {
        Debug.Log("CSJM_Unity "+ "Ads_ios " + $"OnIdle 下载未开始 on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
        // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
        //     this.Ads_ios.information.text = "OnIdle，下载未开始";
    }

    public void OnDownloadActive(
        long totalBytes, long currBytes, string fileName, string appName)
    {
        Debug.Log("CSJM_Unity "+ "Ads_ios " + $"OnDownloadActive 下载中，点击下载区域暂停  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
        // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
        //     this.Ads_ios.information.text = "下载中，点击下载区域暂停";
    }

    public void OnDownloadPaused(
        long totalBytes, long currBytes, string fileName, string appName)
    {
        Debug.Log("CSJM_Unity "+ "Ads_ios " + $"OnDownloadPaused 下载暂停，点击下载区域继续  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId} ");
        // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
        //     this.Ads_ios.information.text = "下载暂停，点击下载区域继续";
    }

    public void OnDownloadFailed(
        long totalBytes, long currBytes, string fileName, string appName)
    {
        Debug.LogError("CSJM_Unity "+ "Ads_ios " + $"OnDownloadFailed 下载失败，点击下载区域重新下载  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
        // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
        //     this.Ads_ios.information.text = "下载失败，点击下载区域重新下载";
    }

    public void OnDownloadFinished(
        long totalBytes, string fileName, string appName)
    {
        Debug.Log("CSJM_Unity "+ "Ads_ios " + $"OnDownloadFinished 下载完成  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
        // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
        //     this.Ads_ios.information.text = "下载完成";
    }

    public void OnInstalled(string fileName, string appName)
    {
        Debug.Log("CSJM_Unity "+ "Ads_ios " + $"OnInstalled 安装完成，点击下载区域打开  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
        // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
        //     this.Ads_ios.information.text = "安装完成，点击下载区域打开";
    }
}
#endif
