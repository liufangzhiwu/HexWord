using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 购买记录弹窗（UIWindow）。
/// 数据源：UserData.purchaseRecords（GiftTable 购买成功后写入）。
/// 每条记录展示：礼包名称、价格、礼包内容、购买时间（由 PurchaseRecordItem 负责）。
/// 支持空状态 emptyRoot。
/// </summary>
public class BuyListScreen : UIWindow
{
    [Header("关闭")]
    [SerializeField] private Button closeBtn;       // 关闭按钮

    [Header("列表")]
    [SerializeField] private ScrollRect scrollRect;         // 可选：用于打开时回到顶部
    [SerializeField] private Transform listParent;          // 滚动内容 Content（挂 VerticalLayoutGroup + ContentSizeFitter）
    [SerializeField] private PurchaseRecordItem itemPrefab;  // 单条记录 Prefab

    [Header("状态")]
    [SerializeField] private GameObject contentRoot;         // 有记录时显示（列表根，可选）
    [SerializeField] private GameObject emptyRoot;           // 无记录时显示（可选）

    /// <summary>本次生成的记录项（用于清理，避免面板复用时旧项残留）</summary>
    private readonly List<PurchaseRecordItem> _spawned = new List<PurchaseRecordItem>();

    // ======================= UIWindow 生命周期 =======================

    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
    }

    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();

        if (closeBtn != null)
            closeBtn.AddClickAction(Close);
        else
            LogBuyList("closeBtn 未配置，关闭按钮不可用");
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
        // 这里不清理：避免动画结束时列表先空再隐藏造成闪烁。
        // 清理统一放在 OnDisable。
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ClearItems();
    }

    // ======================= 初始化 / 刷新 =======================

    private void InitUI()
    {
        Refresh();
    }

    /// <summary>刷新列表（先清空旧项，再按 purchaseRecords 重新生成）</summary>
    public void Refresh()
    {
        ClearItems();

        var records = GameDataManager.Instance?.UserData?.purchaseRecords;
        int count = records?.Count ?? 0;
        bool has = count > 0;

        if (emptyRoot != null) emptyRoot.SetActive(!has);
        if (contentRoot != null) contentRoot.SetActive(has);

        if (!has)
        {
            LogBuyList("无购买记录，显示空状态");
            ResetScrollToTop();
            return;
        }

        if (listParent == null || itemPrefab == null)
        {
            LogBuyList("listParent 或 itemPrefab 未配置，无法生成列表");
            return;
        }

        for (int i = 0; i < records.Count; i++)
        {
            var rec = records[i];
            if (rec == null) continue;

            var item = Instantiate(itemPrefab, listParent);
            item.SetData(rec);
            item.gameObject.SetActive(true);
            _spawned.Add(item);
        }

        LogBuyList($"生成购买记录 {_spawned.Count} 条（数据共 {records.Count} 条）");
        ResetScrollToTop();
    }

    private void ClearItems()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] != null)
            {
                _spawned[i].gameObject.SetActive(false);
                Destroy(_spawned[i].gameObject);
            }
        }
        _spawned.Clear();
    }

    private void ResetScrollToTop()
    {
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    // ======================= 关闭 =======================

    private void Close()
    {
        base.Close(); // 隐藏面板
    }

    // ======================= 日志 =======================

    private void LogBuyList(string message)
    {
        Debug.Log($"[BuyListScreen] {message}");
    }
}