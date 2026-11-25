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
    /// 「本单购买前」是否已拥有永久去广告的快照（购买入口在发放奖励之前采集）。
    /// 展示口径：为 false（买之前没有）→ 订单里的永久去广告项要展示；
    ///           为 true（买之前已拥有）→ 隐藏，避免第二次买含去广告的礼包又列一条。
    /// 旧存档没有该字段 → 为 null，退回「当前是否已拥有」判定，与历史行为一致。
    /// </summary>
    public bool? ownedRemoveAdsBeforePurchase;

    /// <summary>
    /// 礼包内容快照（深拷贝 ShopDataItem.productContent）。
    /// 每一项是 [type, count, ...]，展示时直接喂给 GiftItem.SetShopData。
    /// </summary>
    public List<List<string>> productContent = new List<List<string>>();
}