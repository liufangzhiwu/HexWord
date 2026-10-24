using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// 商店商品数据模型（来源：商店表"新商品新配置表20260915" / 旧表 20260628）
/// </summary>
public class ShopDataItem
{
    public int id;
    public string produceNameId;
    public int purchaseType;
    public int type;
    public List<List<string>> productContent;
    public float price;
    public float originalPrice;
    public float salePrice;
    public string showIcon;
    public string name;
    public string des;
    public string pointDes;
    public int sort;
    public string homeSort;
    public List<string> unlocked;
    public string limitedTime;
    public string discount;
    public string nameKey;
    public string discountZh;
    public string labelZh;
    public int limited;
    public int giftType;
    public int coinValue;

    public List<string> bonusContent;

    public string GetProduceName()
    {
        return produceNameId;
    }

    public ShopDataItem DeepCopy()
    {
        ShopDataItem copy = new ShopDataItem();

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

        copy.produceNameId = this.produceNameId;
        copy.showIcon = this.showIcon;
        copy.name = this.name;
        copy.des = this.des;
        copy.pointDes = this.pointDes;
        copy.homeSort = this.homeSort;
        copy.limitedTime = this.limitedTime;
        copy.discount = this.discount;
        copy.nameKey = this.nameKey;
        copy.discountZh = this.discountZh;
        copy.labelZh = this.labelZh;

        if (this.unlocked != null)
            copy.unlocked = new List<string>(this.unlocked);
        else
            copy.unlocked = null;

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
        for (int i = 2; i < records.Count; i++)
        {
            List<string> fields = records[i];
            if (fields.Count == 0) continue;
            if (fields.All(string.IsNullOrEmpty)) continue;

            int id = i - 2;

            string nameID = GetField(fields, 1);
            int purchaseType = GetInt(GetField(fields, 2));

            string name = GetField(fields, 3);
            int type = GetInt(GetField(fields, 5));

            List<List<string>> productContent = ParseProductContent(GetField(fields, 6));

            float salePrice = GetFloat(GetField(fields, 8));
            string showIcon = GetField(fields, 10);
            string des = GetField(fields, 4);
            string pointdes = GetField(fields, 13);
            int sort = GetSort(GetField(fields, 14));
            string homeSort = GetField(fields, 15);
            List<string> unlocks = ParseUnlock(GetField(fields, 16));
            string limittime = GetField(fields, 17);
            string discount = GetField(fields, 19).Replace(" ", "").Trim();

            float originalPrice = GetFloat(GetField(fields, 7));

            string nameKey = GetField(fields, 11);
            string discountZh = GetField(fields, 19);
            string labelZh = GetField(fields, 20);
            int limited = GetInt(GetField(fields, 21));
            int giftType = GetInt(GetField(fields, 22));
            int coinValue = GetInt(GetField(fields, 23));

            ShopDataItem item = new ShopDataItem
            {
                id = id,
                produceNameId = nameID,
                purchaseType = purchaseType,
                type = type,
                productContent = productContent,
                price = salePrice,
                originalPrice = originalPrice,
                salePrice = salePrice,
                showIcon = showIcon,
                name = name,
                des = des,
                pointDes = pointdes,
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

            if (purchaseType >= 0)
            {
                shopItems.Add(item);
            }
        }

        shopItems = shopItems.OrderBy(item => item.sort).ToList();
    }

    // ======================= 商品查询 =======================

    public ShopDataItem GetProduct(string _productId)
    {
        if (shopItems.Count > 0)
        {
            Debug.Log("获取购买商品: " + _productId);
            return shopItems.Find((item) => item.produceNameId == _productId);
        }
        return null;
    }

    public ShopDataItem FormAllItemsGetProduct(string _productId)
    {
        if (allShopItems.Count > 0)
        {
            Debug.Log("获取购买商品: " + _productId);
            return allShopItems.Find((item) => item.produceNameId == _productId);
        }
        return null;
    }

    public ShopDataItem GetShopItem(int shopItemID)
    {
        return shopItems.FirstOrDefault(item => item.id == shopItemID);
    }

    public ShopDataItem NameGetShopItem(string buyname)
    {
        return shopItems.FirstOrDefault(item => item.GetProduceName() == buyname);
    }

    // ======================= 礼包加赠配置 =======================

    private static readonly Dictionary<string, LimitRewordType> _bonusGiftMap = new Dictionary<string, LimitRewordType>
    {
        { "RocketBox011", LimitRewordType.Butterfly },
        { "RocketBox01",  LimitRewordType.Butterfly },
        { "RocketBox02",  LimitRewordType.Butterfly },
        { "ItemBox02",    LimitRewordType.Butterfly },
        { "ClockBox01",   LimitRewordType.Butterfly },
        { "ClockBox02",   LimitRewordType.Butterfly },
        { "ClockBox03",   LimitRewordType.Butterfly },
        { "ClockBox04",   LimitRewordType.Butterfly },
        { "BulbBox03",    LimitRewordType.Butterfly },
        { "ItemBox01",    LimitRewordType.Butterfly },
        { "BulbBox04",    LimitRewordType.Butterfly },
        { "RocketBox010", LimitRewordType.Tipstool },
        { "RocketBox10",  LimitRewordType.Tipstool },
        { "BulbBox01",    LimitRewordType.AutoComplete },
        { "BulbBox02",    LimitRewordType.AutoComplete },
        { "BulbBox05",    LimitRewordType.AutoComplete },
        { "BulbBox06",    LimitRewordType.AutoComplete },
    };

    public int GetBonusGiftType(string produceNameId)
    {
        if (string.IsNullOrEmpty(produceNameId)) return -1;
        LimitRewordType type;
        if (_bonusGiftMap.TryGetValue(produceNameId, out type))
            return (int)type;
        return -1;
    }

    public bool IsBonusGift(string produceNameId, int contentType)
    {
        int bonus = GetBonusGiftType(produceNameId);
        return bonus > 0 && bonus == contentType;
    }

    // ======================= 商店列表（已加入审核判断） =======================

    /// <summary>
    /// 商店首页界面物品列表。
    /// 审核开：直接返回审核固定商店全部商品；
    /// 审核关：原逻辑（sort + 首页规则 + 推送过滤）。
    /// </summary>
    public List<ShopDataItem> GetShopHomeItems()
    {
        if (AuditConfig.IsAuditOpen)
        {
            List<ShopDataItem> auditItems = GetAuditFixedStoreItems();
            if (auditItems.Count > 0)
            {
                LogShop($"[Audit] GetShopHomeItems 审核开，全部展示 {auditItems.Count} 个");
                return auditItems;
            }
            LogShop("[Audit] GetShopHomeItems 审核开但无商品，回退常规");
        }

        var type2Count = 0;
        var maxType2 = 3;

        RefreshLimitData();

        return shopItems
            .Where(item =>
            {
                if (string.IsNullOrEmpty(item.unlocked.FirstOrDefault()) && !string.IsNullOrEmpty(item.homeSort))
                {
                    if (IsRemoveAdsProduct(item.produceNameId) && IsRemoveAdsOwned())
                        return false;

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
    /// 商店正常排序物品列表。
    /// 审核开：直接返回审核固定商店全部商品；
    /// 审核关：原逻辑（sort!=0 排序）。
    /// </summary>
    public List<ShopDataItem> GetShopItems()
    {
        List<ShopDataItem> shopDataItems = shopItems
            .Where(item => item.sort != 0)
            .OrderBy(item => item.sort)
            .ToList();

        if (AuditConfig.IsAuditOpen)
        {
            EnsureAuditExtraPack(shopDataItems, ProductSpecialPackageA);
            EnsureAuditExtraPack(shopDataItems, ProductSpecialPackageB);

            LogShop($"[Audit] GetShopItems 审核开，常规礼包基础上补入特惠A/B，共 {shopDataItems.Count} 个");
            return shopDataItems.OrderBy(item => item.price).ToList();
        }
   
        return shopDataItems;
    }

    public List<ShopDataItem> GetBuyShopItems()
    {
        return shopItems.OrderBy(item => item.sort).ToList();
    }

    // ======================= CSV 解析工具 =======================

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
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        if (sb.Length > 0 || fields.Count > 0)
        {
            fields.Add(sb.ToString());
            records.Add(fields);
        }
        return records;
    }

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

    private static List<string> ParseUnlock(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return new List<string>();
        return new List<string>(raw.Split('#'));
    }
}