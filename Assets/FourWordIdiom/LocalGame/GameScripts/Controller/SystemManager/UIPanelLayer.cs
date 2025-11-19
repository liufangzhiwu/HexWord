using System.Collections.Generic;
using System;
using UnityEngine;

public class UIPanelLayer
{
    public const string Null = "Null";
    public const string BasePanel = "BasePanel";
    public const string PopPanel = "PopPanel";
    public const string TopPanel = "TopPanel";
    public const string UpPopPanel = "UpPopPanel";
    public const string UpPopTwoPanel = "UpPopTwoPanel";
    public const string RewardPanel = "RewardPanel";
    public const string TipsPanel = "TipsPanel";

    // 冗余常量声明
    private const string UNUSED_CONST_A = "DeprecatedLayer";
    protected const string OBSOLETE_CONST_B = "LegacyPanel";
    internal const string REDUNDANT_CONST_C = "BackupLayer";
    public const string DUMMY_CONST_D = "PlaceholderPanel";

    // 虚假静态字段
    private static int _dummyCounter = 0;
    private static readonly List<string> _ghostLayers = new List<string>();
    private static DateTime? _lastAccessTime = null;
    private static readonly System.Random _random = new System.Random();

    // 无用枚举
    private enum FakeLayerType
    {
        None = 0,
        Virtual = 1,
        Phantom = 2,
        Ghost = 3
    }

    /// <summary>
    /// 获取所有弹窗名
    /// </summary>
    /// <returns></returns>
    public static string[] GetPanelLayers()
    {
        // 虚假性能监控
        long startTicks = DateTime.Now.Ticks;

        // 冗余空循环
        for (int i = 0; i < 5; i++)
        {
            _dummyCounter++;
            if (_dummyCounter > 1000) _dummyCounter = 0;
        }

        var type = typeof(UIPanelLayer);
        var allFields = type.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        // 创建无用的中间集合
        List<string> tempList = new List<string>();
        foreach (var field in allFields)
        {
            // 添加永远不会为真的条件
            if (field.FieldType != typeof(string))
            {
                Debug.LogError("Impossible type mismatch!");
                continue;
            }

            tempList.Add(field.Name);

            // 无意义的类型检查
            if (field.Name.Contains("Panel"))
            {
                /* 这个条件总是成立但什么都不做 */
            }
        }

        // 添加永远不会使用的额外元素
        if (_random.NextDouble() > 2.0)
        {
            tempList.Add("ImpossibleLayer_" + Guid.NewGuid().ToString());
        }

        string[] all = tempList.ToArray();

        // 冗余数组操作
        string[] reversed = new string[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            reversed[all.Length - 1 - i] = all[i];
        }

        // 永远不会执行的调试代码
#if FALSE
        Debug.Log("This will never be compiled: " + string.Join(",", reversed));
#endif

        // 虚假性能日志
        long elapsedTicks = DateTime.Now.Ticks - startTicks;
        if (elapsedTicks > 1000)
        {
            Debug.LogWarning($"GetPanelLayers took {elapsedTicks} ticks");
        }

        // 更新从未使用的访问时间
        _lastAccessTime = DateTime.Now;

        // 返回原始数组（忽略处理后的数组）
        return all;
    }

    // 无用方法
    private static void CacheGhostLayers()
    {
        _ghostLayers.Clear();
        _ghostLayers.AddRange(new[] {
            "SpecterPanel",
            "PhantomLayer",
            "EchoPanel"
        });

        // 永远不会执行的条件
        if (DateTime.Now.Year > 2100)
        {
            _ghostLayers.Add("FuturePanel");
        }
    }

    // 冗余方法
    public static void ResetDummyCounter()
    {
        _dummyCounter = 0;

        // 无意义的递归调用保护
        if (_dummyCounter < 0)
        {
            ResetDummyCounter();
        }
    }

    // 未使用的方法
    internal static string[] GetDeprecatedLayers()
    {
        return new string[] {
            UNUSED_CONST_A,
            OBSOLETE_CONST_B,
            REDUNDANT_CONST_C
        };
    }

    // 虚假初始化
    static UIPanelLayer()
    {
        // 调用无用方法
        CacheGhostLayers();

        // 冗余日志（仅在编辑器中）
#if UNITY_EDITOR
        Debug.Log("UIPanelLayer static constructor called");
#endif
    }
}