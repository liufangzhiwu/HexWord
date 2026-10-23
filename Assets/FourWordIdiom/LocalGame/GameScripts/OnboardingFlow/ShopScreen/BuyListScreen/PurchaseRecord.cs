using System.Collections.Generic;

/// <summary>
/// 一次礼包购买记录（供“购买记录”弹窗展示）。
/// 通过 Newtonsoft.Json 序列化到 UserData.purchaseRecords。
/// </summary>
public class PurchaseRecord
{
    /// <summary>订单号（唯一标识，用于去重/排查；一般用第三方回调的 order_id）</summary>
    public string orderId;

    /// <summary>礼包 produceNameId（对应 ShopDataItem.produceNameId）</summary>
    public string produceNameId;

    /// <summary>名称多语言 key（ShopDataItem.name），用于运行时翻译</summary>
    public string nameKey;

    /// <summary>实际显示名称快照（避免历史记录随多语言/配置变化而丢失原始名称）</summary>
    public string nameText;

    /// <summary>实际支付价格（ProductItem.LocalizedPrice；为 0 时回退 ShopDataItem.price）</summary>
    public float price;

    /// <summary>原价（ShopDataItem.originalPrice，用于展示划线价）</summary>
    public float originalPrice;

    /// <summary>币种（ISO，来自第三方回调；仅用于记录/日志）</summary>
    public string currency;

    /// <summary>礼包底图图标名（ShopDataItem.showIcon）</summary>
    public string showIcon;

    /// <summary>礼包 type（ShopDataItem.type：1=广告礼包，2=普通礼包），用于展示时还原部分样式</summary>
    public int limitRewordType;

    /// <summary>购买时间（yyyy-MM-dd HH:mm:ss）</summary>
    public string buyTime;

    /// <summary>
    /// 礼包内容快照（深拷贝 ShopDataItem.productContent）。
    /// 每一项是 [type, count, ...]，展示时直接喂给 GiftItem.SetShopData。
    /// </summary>
    public List<List<string>> productContent = new List<List<string>>();
}