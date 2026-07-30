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
    
     public static void PurchaseStart(string transactionId)
    {
        var properties = new Dictionary<string, object>
        {
            {"pay_reason", transactionId}
        }; 

        Game.self.Analytics.LogEvent("order_start", properties, Define.DataTarget.Think);
    }
    
    /// <summary>
    /// 购买成功
    /// </summary>
    /// <param name="product"></param>
    /// <param name="firstPay"></param>
    /// <param name="items"></param>
    public static void PurchaseFinished(ProductItem product,bool firstPay,List<Item> items=null)
    {
        // 1. 获取商品信息
        string transactionid = product.order_id;
        //string itemId = product.definition.id;
        string itemName = product.ItemName;
        float price = product.LocalizedPrice;
        string paytype = product.IsoCurrencyCode;
        string paymethod = "";
#if UNITY_OPENHARMONY
        paymethod = "huaweiOpenHarmony";
#elif UNITY_HUAWEI
        paymethod = "huaweiAndroid";
#endif
        
        var itemJson = JsonConvert.SerializeObject(items);
        var properties = new Dictionary<string, object>
        {
            {"order_id", transactionid},
            {"pay_type", paytype},
            {"pay_amount",price},
            {"pay_reason",itemName},
            {"is_first_pay", firstPay},
            {"pay_method",paymethod},
        }; 
        
        Game.self.Analytics.LogEvent("order_finish", properties, Define.DataTarget.Think);
    }
    
    public static void PurchaseFailed(string transactionId,string reason)
    {
        var properties = new Dictionary<string, object>
        {
            {"pay_reason", transactionId},
            {"failed_reason", reason},
        }; 

        Game.self.Analytics.LogEvent("order_failed", properties, Define.DataTarget.Think);
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
        Game.self.Analytics.LogEvent("insertAd_start", properties, Define.DataTarget.Think);
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

        Game.self.Analytics.LogEvent("insertAd_fail", properties, Define.DataTarget.Think);
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

        Game.self.Analytics.LogEvent("insertAd_success", properties, Define.DataTarget.Think);
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
        Game.self.Analytics.LogEvent("videoAd_start", properties, Define.DataTarget.Think);
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
        Game.self.Analytics.LogEvent("videoAd_fail", properties, Define.DataTarget.Think);
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
        Game.self.Analytics.LogEvent("videoAd_success", properties, Define.DataTarget.Think);
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
        Game.self.Analytics.LogEvent("videoAd_click", properties, Define.DataTarget.Think);
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
        Game.self.Analytics.LogEvent("videoAd_show", properties, Define.DataTarget.Think);
    }
}