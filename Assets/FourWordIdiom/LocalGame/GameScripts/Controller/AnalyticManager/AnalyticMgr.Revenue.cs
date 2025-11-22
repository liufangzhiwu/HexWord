using System.Collections.Generic;
using Middleware;
using Newtonsoft.Json;

public partial class AnalyticMgr
{
    /// <summary>
    /// 购买商品
    /// </summary>
    /// <param name="transactionId">交易ID</param>
    /// <param name="currency">货币类型</param>
    /// <param name="value">金额</param>
    /// <param name="items">商品列表</param>
    public class Item
    {
        public string item_id;//如SKU_12345
        public string item_name;//如Stan或FriendsTee
        public int quantity;//如3
    }
    
    public static void Purchase(string transactionId,string currency,float value,List<Item> items)
    {
        
#if UNITY_ANDROID
        var itemJson = JsonConvert.SerializeObject(items);
        var properties = new Dictionary<string, object>
        {
            {"transaction_id", transactionId},
            {"currency", currency},
            {"value",value},
            {"items",itemJson},
        }; 
        Game.Analytics.LogEvent("purchase", properties, Define.DataTarget.Firebase);
#endif
    }
    
    public static void PurchaseFailed(string transactionId,string reason)
    {
        var properties = new Dictionary<string, object>
        {
            {"transaction_id", transactionId},
            {"failed_reason", reason},
        }; 

        Game.Analytics.LogEvent("purchase_failed", properties, Define.DataTarget.Think);
#if UNITY_ANDROID
        Game.Analytics.LogEvent("purchase_failed", properties, Define.DataTarget.Firebase);
#endif
    }
    
    /// <summary>
    /// 插屏广告开始
    /// </summary>
    public static void InsetAdStart(string adName)
    {
        var properties = new Dictionary<string, object>
        {
            {"adName",adName},
        }; 
        

        Game.Analytics.LogEvent("insertAd_start", properties, Define.DataTarget.Think);
#if UNITY_ANDROID
        Game.Analytics.LogEvent("insertAd_start", properties, Define.DataTarget.Firebase);
#endif
    }
    
    /// <summary>
    /// 插屏广告失败
    /// </summary>
    public static void InsetAdFail(string adName)
    {
        var properties = new Dictionary<string, object>
        {
            {"adName",adName},
        }; 

        Game.Analytics.LogEvent("insertAd_fail", properties, Define.DataTarget.Think);
#if UNITY_ANDROID
        Game.Analytics.LogEvent("insertAd_fail", properties, Define.DataTarget.Firebase);
#endif
        
    }
    
    /// <summary>
    /// 插屏广告成功
    /// </summary>
    public static void InsetAdSuccess(string adName)
    {
        var properties = new Dictionary<string, object>
        {
            {"adName",adName},
        }; 

        Game.Analytics.LogEvent("insertAd_success", properties, Define.DataTarget.Think);
#if UNITY_ANDROID
        Game.Analytics.LogEvent("insertAd_success", properties, Define.DataTarget.Firebase);
#endif
    }
    
    /// <summary>
    /// 视频广告开始
    /// </summary>
    public static void VideoStart(string adName)
    {
        var properties = new Dictionary<string, object>
        {
            {"adName",adName},
        }; 
        Game.Analytics.LogEvent("videoAd_start", properties, Define.DataTarget.Think);
        
#if UNITY_ANDROID
        
        properties = new Dictionary<string, object>
        {
            {"adName",adName},
            {"level_name",GameDataManager.Instance.UserData.CurrentHexStage}
        }; 
        Game.Analytics.LogEvent("videoAd_start", properties, Define.DataTarget.Firebase);
#endif
    }
    
    /// <summary>
    /// 视频广告失败
    /// </summary>
    public static void VideoAdFail(string adName)
    {

        var properties = new Dictionary<string, object>
        {
            {"adName",adName},
        }; 
        Game.Analytics.LogEvent("videoAd_fail", properties, Define.DataTarget.Think);
        
#if UNITY_ANDROID
        
        properties = new Dictionary<string, object>
        {
            {"adName",adName},
            {"level_name",GameDataManager.Instance.UserData.CurrentHexStage}
        }; 
        Game.Analytics.LogEvent("videoAd_fail", properties, Define.DataTarget.Firebase);
#endif
    }
    
    /// <summary>
    /// 视频广告成功
    /// </summary>
    public static void VideoAdSuccess(string adName)
    {

        var properties = new Dictionary<string, object>
        {
            {"adName",adName}
        }; 
        Game.Analytics.LogEvent("videoAd_success", properties, Define.DataTarget.Think);
        
#if UNITY_ANDROID
        
        properties = new Dictionary<string, object>
        {
            {"adName",adName},
            {"level_name",GameDataManager.Instance.UserData.CurrentHexStage}
        }; 
        Game.Analytics.LogEvent("videoAd_success", properties, Define.DataTarget.Firebase);
#endif
    }
    
    /// <summary>
    /// 视频广告点击
    /// </summary>
    public static void VideoAdClick(string adName)
    {
        var properties = new Dictionary<string, object>
        {
            {"adName",adName},
        }; 
        Game.Analytics.LogEvent("videoAd_click", properties, Define.DataTarget.Think);
    }
    
    /// <summary>
    /// 视频按钮展示
    /// </summary>
    public static void VideoAdShow(string adName)
    {
        var properties = new Dictionary<string, object>
        {
            {"adName",adName},
        }; 
        Game.Analytics.LogEvent("videoAd_show", properties, Define.DataTarget.Think);
    }
}