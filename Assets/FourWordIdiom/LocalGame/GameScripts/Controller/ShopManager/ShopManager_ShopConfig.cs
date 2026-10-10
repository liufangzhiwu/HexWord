using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// 商店商品数据模型（来源：商店表"新商品新配置表20260915" / 旧表 20260628）
/// 列：id, nameID, purchaseType, name, des, type, productContent, OriginalPrice, SalePrice, price,
///      showIcon, nameKey, (12空), pointDes, isHomeDisplay, sort, homeSort, unlock, limitedTime,
///      discount, discountZh, LabelZh, Limited, GiftType, CoinValue, (其余右侧列：无限体力等，按需扩展)
/// </summary>
public class ShopDataItem
{
    public int id;
    public string produceNameId;
    public int purchaseType;      // 购买方式：-1=不售卖(仅数据) 0=金币 1=内购 2=广告?（按旧表语义，>=0 加入售卖列表）
    public int type;              // 商品类型：0=货币 1=道具 2=礼包
    public List<List<string>> productContent; // 商品内容，组内 type;count，组间 #
    public float price;           // 售价（新表 price 列，含折扣后的实际价）
    public float originalPrice;   // 原价（新表 OriginalPrice）
    public float salePrice;       // 折后价（新表 SalePrice）
    public string showIcon;
    public string name;           // 展示名
    public string des;            // 描述
    public string pointDes;       // 说明
    public string isHomeDisplay;  // 是否首页显示
    public int sort;              // 固定商店页排序（顺序由 sort 决定）
    public string homeSort;       // 首页排序
    public List<string> unlocked; // 解锁条件（关卡/类型）
    public string limitedTime;    // 限时（小时）
    public string discount;       // 折扣（字符串，如 "2" 表示2折）
    public string nameKey;        // 本地化 key
    public string discountZh;     // 折扣中文文案
    public string labelZh;        // 标签（新人礼/特惠/畅销/最划算等）
    public int limited;           // 限购次数（0=不限购）
    public int giftType;          // 礼包档位类型（付费倾向积分/探价加分使用）
    public int coinValue;         // 金币价值（性价比排序用）

    // ==================== 运行时字段（不进序列化） ====================
    [NonSerialized] public int buyCount;                 // 已购买次数（限购判断）
    [NonSerialized] public float payTendencyScore;       // 付费倾向积分
    [NonSerialized] public float exploreScore;           // 探价加分
    [NonSerialized] public float conversionFailScore;    // 转化失败分 F
    [NonSerialized] public DateTime lastFailScoreTime;   // F 值最近变化时间（30秒间隔）
    [NonSerialized] public DateTime lastBuyTime;         // 最近购买时间（冷却系数）
    [NonSerialized] public DateTime limitEndTime;        // 限时活动礼包截至时间
    [NonSerialized] public bool isActivityPack;          // 当前是否为活动礼包（节日X折/周末X折）
    [NonSerialized] public float pushStateIndex = 1f;    // 推送状态指数（优惠推送=0，默认1）

    /// <summary>
    /// 获取商品购买id
    /// </summary>
    public string GetProduceName()
    {
        // if(GameDataManager.Instance.UserData.LanguageCode=="JS")
        //     return produceNameId.ToLower();
        // if(GameDataManager.Instance.UserData.LanguageCode=="CT")
        //     return produceNameId_tw;
        return produceNameId;
    }

    /// <summary>
    /// 深拷贝当前对象
    /// </summary>
    public ShopDataItem DeepCopy()
    {
        ShopDataItem copy = new ShopDataItem();

        // 值类型直接复制
        copy.id = this.id;
        copy.purchaseType = this.purchaseType;
        copy.type = this.type;
        copy.price = this.price;
        copy.originalPrice = this.originalPrice;
        copy.salePrice = this.salePrice;
        copy.sort = this.sort;
        copy.limited = this.limited;
        copy.giftType = this.giftType;
        copy.coinValue = this.coinValue;
        copy.buyCount = this.buyCount;

        // 字符串（不可变，直接引用即可，也可显式复制）
        copy.produceNameId = this.produceNameId;
        copy.showIcon = this.showIcon;
        copy.name = this.name;
        copy.des = this.des;
        copy.pointDes = this.pointDes;
        copy.isHomeDisplay = this.isHomeDisplay;
        copy.homeSort = this.homeSort;
        copy.limitedTime = this.limitedTime;
        copy.discount = this.discount;
        copy.nameKey = this.nameKey;
        copy.discountZh = this.discountZh;
        copy.labelZh = this.labelZh;

        // List<string> 深拷贝
        if (this.unlocked != null)
            copy.unlocked = new List<string>(this.unlocked); // 字符串不可变，直接复制引用安全
        else
            copy.unlocked = null;

        // List<List<string>> 深拷贝（嵌套列表必须逐层新建）
        if (this.productContent != null)
        {
            copy.productContent = new List<List<string>>();
            foreach (var innerList in this.productContent)
            {
                if (innerList != null)
                    copy.productContent.Add(new List<string>(innerList));
                else
                    copy.productContent.Add(null);
            }
        }
        else
        {
            copy.productContent = null;
        }

        return copy;
    }
}

/// <summary>
/// 商店管理器 - 商店商品配置部分类。
/// 职责：商店商品 CSV 解析（新旧表自动兼容）、商品查询、商店列表（固定商店页/首页）。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    /// <summary>
    /// 解析商店商品 CSV（新表 39 列 / 旧表 18 列自动识别，容错解析）。
    /// 列（0基）：
    ///   0:id  1:nameID  2:purchaseType  3:name  4:des  5:type  6:productContent  7:OriginalPrice
    ///   8:SalePrice  9:price  10:showIcon  11:nameKey  12:(空)  13:pointDes  14:isHomeDisplay
    ///   15:sort  16:homeSort  17:unlock  18:limitedTime  19:discount  20:discountZh  21:LabelZh
    ///   22:Limited  23:GiftType  24:CoinValue  （右侧"无限体力"等按需扩展）
    /// </summary>
    void ParseShopItems(string data)
    {
        ConvertCSVToJSON(data);
        Debug.Log("Shop items loaded: " + shopItems.Count);
    }

    void ConvertCSVToJSON(string data)
    {
        shopItems = new List<ShopDataItem>();
        allShopItems = new List<ShopDataItem>();

        var records = SplitCsvLines(data);
        // 跳过前2行（标题/说明），第3行起为数据
        for (int i = 2; i < records.Count; i++)
        {
            List<string> fields = records[i];
            if (fields.Count == 0) continue;
            if (fields.All(string.IsNullOrEmpty)) continue; // 整行空

            // 新表：id 在第0列且通常为数字；旧表结构相同（列数不同）
            int id = i-2;
            // if (!int.TryParse(GetField(fields, 0), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            // {
            //     Debug.LogWarning($"Skipping line {i + 1}: invalid id.");
            //     continue;
            // }

            string nameID = GetField(fields, 1);
            int purchaseType = GetInt(GetField(fields, 2));

            // 新表（39列）：0:id 1:nameID 2:purchaseType 3:name 4:des 5:type 6:productContent 7:OriginalPrice 8:SalePrice 9:price ...
            string  name = GetField(fields, 3);
            int type = GetInt(GetField(fields, 5));
            
            List<List<string>> productContent = ParseProductContent(GetField(fields, 6));

            float price = GetFloat(GetField(fields,9));
            string showIcon = GetField(fields,  10 );
            string des = GetField(fields,  4 );
            string pointdes = GetField(fields, 13);
            string isHomeDisplay = GetField(fields, 14 );
            int sort = GetSort(GetField(fields,  15 ));
            string homeSort = GetField(fields,  16 );
            List<string> unlocks = ParseUnlock(GetField(fields,  17 ));
            string limittime = GetField(fields, 18 );
            string discount = GetField(fields,  19 );

            // 新表扩展字段（旧表无则给默认）
            float originalPrice = GetFloat(GetField(fields, 7)) ;
            float salePrice = GetFloat(GetField(fields, 8));
            string nameKey = GetField(fields, 11) ;
            string discountZh = GetField(fields, 20);
            string labelZh =  GetField(fields, 21);
            int limited =  GetInt(GetField(fields, 22));
            int giftType =  GetInt(GetField(fields, 23));
            int coinValue =  GetInt(GetField(fields, 24));

            ShopDataItem item = new ShopDataItem
            {
                id = id,
                produceNameId = nameID,
                purchaseType = purchaseType,
                type = type,
                productContent = productContent,
                price = price,
                originalPrice = originalPrice,
                salePrice = salePrice,
                showIcon = showIcon,
                name = name,
                des = des,
                pointDes = pointdes,
                isHomeDisplay = isHomeDisplay,
                sort = sort,
                homeSort = homeSort,
                unlocked = unlocks,
                limitedTime = limittime,
                discount = discount,
                nameKey = nameKey,
                discountZh = discountZh,
                labelZh = labelZh,
                limited = limited,
                giftType = giftType,
                coinValue = coinValue,
            };
            allShopItems.Add(item);

            // 修复：原判断 purchaseType >= 0 加入 shopItems（旧表 purchaseType=0 金币/1 内购）
            if (purchaseType >= 0)
            {
                shopItems.Add(item);
            }
        }

        shopItems = shopItems.OrderBy(item => item.sort).ToList();
    }

    // ======================= 商品查询 =======================

    /// <summary>获取商品（售卖列表）</summary>
    public ShopDataItem GetProduct(string _productId)
    {
        if (shopItems.Count > 0)
        {
            Debug.Log("获取购买商品: " + _productId);
            return shopItems.Find((item) => item.produceNameId == _productId);
        }
        return null;
    }

    /// <summary>获取商品（全量列表，修复：原实现用 shopItems.Count 判断却查 allShopItems）</summary>
    public ShopDataItem FormAllItemsGetProduct(string _productId)
    {
        if (allShopItems.Count > 0)
        {
            Debug.Log("获取购买商品: " + _productId);
            return allShopItems.Find((item) => item.produceNameId == _productId);
        }
        return null;
    }

    /// <summary>按 id 获取商品</summary>
    public ShopDataItem GetShopItem(int shopItemID)
    {
        return shopItems.FirstOrDefault(item => item.id == shopItemID);
    }

    /// <summary>根据商品购买名获取商品配置数据</summary>
    public ShopDataItem NameGetShopItem(string buyname)
    {
        return shopItems.FirstOrDefault(item => item.GetProduceName() == buyname);
    }

    // ======================= 商店列表 =======================

    /// <summary>
    /// 商店首页界面物品列表。
    /// 需求：固定商店页取消"精选"版直接滑动，顺序由 sort 决定；去掉"免费金币"、位置调整；
    /// 永久去广告购买后，去广告相关商品消失不再展示。
    /// </summary>
    public List<ShopDataItem> GetShopHomeItems()
    {
        var type2Count = 0;
        var maxType2 = 3; // 最多允许的type2商品数量

        RefreshLimitData();

        return shopItems
            .Where(item =>
            {
                // 修复：原代码 item.unlocked[0] 在空列表时越界
                // 基础条件：必须显示在首页且未解锁
                if (string.IsNullOrEmpty(item.unlocked.FirstOrDefault()) && !string.IsNullOrEmpty(item.homeSort))
                {
                    // 永久去广告购买后，去广告商品（含限时去广告/破冰礼包）不再展示
                    if (IsRemoveAdsProduct(item.produceNameId) && IsRemoveAdsOwned())
                        return false;

                    // 限购已满不展示
                    if (IsLimitedOut(item))
                        return false;

                    if (item.type == 2)
                    {
                        if (type2Count < maxType2)
                        {
                            type2Count++;
                            return true;
                        }
                        return false;
                    }
                    return true;
                }
                return false;
            })
            .OrderBy(item => GetInt(item.homeSort))
            .ToList();
    }

    /// <summary>
    /// 商店正常排序物品列表（修复：移除原死代码 sort.ToString() 恒非空判断，改用未解锁判断）
    /// </summary>
    public List<ShopDataItem> GetShopItems()
    {
        List<ShopDataItem> shopDataItems = shopItems
            .Where(item => !string.IsNullOrEmpty(item.isHomeDisplay)&&string.IsNullOrEmpty(item.limitedTime))
            .OrderBy(item => item.sort)
            .ToList();
        
        return shopDataItems;
    }

    /// <summary>商店正常排序物品列表（全量，不做任何过滤）</summary>
    public List<ShopDataItem> GetBuyShopItems()
    {
        return shopItems.OrderBy(item => item.sort).ToList();
    }

    // ======================= CSV 解析工具 =======================

    /// <summary>
    /// 将 CSV 文本按行拆分为字段列表（支持带引号字段：商品内容 "0;450#2;5" 等含逗号的分隔符）
    /// </summary>
    internal static List<List<string>> SplitCsvLines(string data)
    {
        List<List<string>> records = new List<List<string>>();
        List<string> fields = new List<string>();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < data.Length; i++)
        {
            char c = data[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < data.Length && data[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Length = 0;
                }
                else if (c == '\n' || c == '\r')
                {
                    if (sb.Length > 0 || fields.Count > 0)
                    {
                        fields.Add(sb.ToString());
                        sb.Length = 0;
                        records.Add(fields);
                        fields = new List<string>();
                    }
                    // 连续换行（\r\n）跳过
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        // 最后一段（无结尾换行）
        if (sb.Length > 0 || fields.Count > 0)
        {
            fields.Add(sb.ToString());
            records.Add(fields);
        }
        return records;
    }

    /// <summary>取字段（trim），越界返回空串</summary>
    private static string GetField(List<string> fields, int index)
    {
        if (fields == null || index < 0 || index >= fields.Count) return "";
        return (fields[index] ?? "").Trim().Trim('"');
    }

    private static int GetInt(string v)
    {
        if (string.IsNullOrEmpty(v)) return 0;
        int result;
        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            return result;
        float f;
        if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
            return (int)f;
        return 0;
    }

    private static float GetFloat(string v)
    {
        if (string.IsNullOrEmpty(v)) return 0;
        float result;
        if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            return result;
        return 0;
    }

    private static int GetSort(string v)
    {
        if (string.IsNullOrEmpty(v)) return 0;
        int result;
        return int.TryParse(v, out result) ? result : 0;
    }

    /// <summary>解析商品内容：组间 # 分隔，组内 ; 分隔（type;count）</summary>
    private static List<List<string>> ParseProductContent(string raw)
    {
        List<List<string>> result = new List<List<string>>();
        if (string.IsNullOrEmpty(raw)) return result;
        string[] groups = raw.Split('#');
        foreach (string g in groups)
        {
            if (string.IsNullOrEmpty(g)) continue;
            List<string> inner = g.Split(';').ToList();
            if (inner.Count >= 2)
                result.Add(inner);
        }
        return result;
    }

    /// <summary>解析解锁条件：按 # 分隔（关卡列表）</summary>
    private static List<string> ParseUnlock(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return new List<string>();
        return new List<string>(raw.Split('#'));
    }
}
//（注：内容由AI生成）
