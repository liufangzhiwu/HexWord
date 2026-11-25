using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 权益类倒计时的唯一时间来源（去广告 / 无限体力 / 限时档期等）。
///
/// ★ 以【服务器时间】为准，不用设备本地时间。
///   国内游戏，服务器时间固定按东八区（UTC+8）解释，不做任何时区换算。
///
/// 取时方式（无需后端改接口）：
///   每次 HTTP 响应都带 <c>Date</c> 头（GMT）。<see cref="HTTPClient"/> 解析后调 <see cref="SyncServerTime"/>，
///   这里把服务器时间与 <c>Time.realtimeSinceStartup</c>（单调、不受用户改表影响）绑成锚点：
///       Now = 锚点服务器时间 + (现在 realtime - 锚点 realtime)
///   于是：
///     · 玩家把设备时间调到未来 / 调回来，都影响不到权益倒计时（只跟真实流逝有关）；
///     · 进程内时间单调不减，不会出现"买完 7 天变 286 小时"这类时间轴错位。
///
/// 离线兜底（本次启动还没收到任何 HTTP 响应）：
///   Now = 设备时间 + 上次同步保存的「服务器 - 设备」差值，并对「历史见过的最晚时间」做单调钳制，
///   因此设备时间在离线期间被回拨也不会把倒计时拉长。
/// </summary>
public static class GameTime
{
    /// <summary>国内游戏：服务器时间固定东八区，不做时区换算。</summary>
    private static readonly TimeSpan ServerZone = TimeSpan.FromHours(8);

    private const string PrefOffset = "gametime_server_offset_seconds"; // 服务器时间 - 设备时间（离线兜底用）
    private const string PrefMaxSeen = "gametime_max_seen_unix";
    private const string PrefLastServer = "gametime_last_server_unix";

    /// <summary>离线兜底时，小于该值的回拨视为校时抖动，不钳制。</summary>
    private const long FallbackToleranceSeconds = 60;

    /// <summary>同步日志的最小间隔（秒），避免每个接口都刷一条。</summary>
    private const long SyncLogIntervalSeconds = 60;

    private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private static bool _inited;
    private static long _offsetSeconds;      // 服务器时间 - 设备时间
    private static long _maxSeenUnix;        // 见过的最大(修正后)时间
    private static long _persistedMaxSeen;
    private static long _lastSyncLogUnix;

    // 服务器锚点
    private static bool _hasAnchor;
    private static DateTime _anchorServerTime;
    private static float _anchorRealtime;

    /// <summary>本进程是否已经拿到过服务器时间。</summary>
    public static bool HasServerTime
    {
        get { EnsureInit(); return _hasAnchor; }
    }

    /// <summary>当前时间（服务器时间为准）。</summary>
    public static DateTime Now
    {
        get
        {
            EnsureInit();

            if (_hasAnchor)
            {
                // 单调推进：只跟"真实流逝"有关，与设备时钟无关
                double elapsed = Time.realtimeSinceStartup - _anchorRealtime;
                if (elapsed < 0) elapsed = 0;
                return _anchorServerTime.AddSeconds(elapsed);
            }

            return OfflineNow();
        }
    }

    /// <summary>Today（跨天判定请用这个，不要用 DateTime.Today）。</summary>
    public static DateTime Today => Now.Date;

    /// <summary>距离上次服务器同步经过的秒数（未同步返回 -1）。</summary>
    public static long SecondsSinceSync
    {
        get
        {
            EnsureInit();
            if (!_hasAnchor) return -1;
            return (long)Math.Max(0, Time.realtimeSinceStartup - _anchorRealtime);
        }
    }

    private static DateTime RawNow => DateTime.Now;

    /// <summary>启动时调用一次（不调也会在首次访问时自动初始化）。</summary>
    public static void Init()
    {
        EnsureInit();
    }

    /// <summary>
    /// 用服务器时间校准（HTTP 响应头 Date / 登录接口返回的服务器时间）。
    /// </summary>
    /// <param name="serverTime">服务器当前时间（东八区本地时间）</param>
    public static void SyncServerTime(DateTime serverTime)
    {
        EnsureInit();

        if (serverTime.Year < 2000)
        {
            Debug.LogWarning($"[GameTime] 服务器时间异常，忽略本次同步: {serverTime:yyyy-MM-dd HH:mm:ss}");
            return;
        }

        bool firstSync = !_hasAnchor;

        _anchorServerTime = serverTime;
        _anchorRealtime = Time.realtimeSinceStartup;
        _hasAnchor = true;

        long serverUnix = ToUnix(serverTime);
        long offset = serverUnix - ToUnix(RawNow);

        _offsetSeconds = offset;
        _maxSeenUnix = serverUnix;
        _persistedMaxSeen = serverUnix;

        PlayerPrefs.SetString(PrefOffset, offset.ToString());
        PlayerPrefs.SetString(PrefMaxSeen, serverUnix.ToString());
        PlayerPrefs.SetString(PrefLastServer, serverUnix.ToString());
        PlayerPrefs.Save();

        if (serverUnix - _lastSyncLogUnix >= SyncLogIntervalSeconds)
        {
            _lastSyncLogUnix = serverUnix;
            Debug.Log($"[GameTime] 已同步服务器时间: {serverTime:yyyy-MM-dd HH:mm:ss}" +
                      $"（设备时间 {RawNow:yyyy-MM-dd HH:mm:ss}，相差 {offset}s）{(firstSync ? " [本次进程首次]" : "")}");
        }
    }

    /// <summary>
    /// 解析 HTTP 响应头 Date（RFC1123 GMT，如 "Mon, 23 Nov 2026 09:47:05 GMT"）→ 东八区服务器时间。
    /// </summary>
    public static bool TryParseHttpDate(string raw, out DateTime serverTime)
    {
        serverTime = DateTime.MinValue;
        if (string.IsNullOrEmpty(raw)) return false;

        DateTime utc;
        if (DateTime.TryParseExact(raw.Trim(), "r", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc))
        {
            serverTime = utc.Add(ServerZone);
            return true;
        }

        // 兜底：非标准格式（多空格 / 其他时区缩写）
        DateTimeOffset dto;
        if (DateTimeOffset.TryParse(raw.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out dto))
        {
            serverTime = dto.ToOffset(ServerZone).DateTime;
            return true;
        }

        return false;
    }

    /// <summary>离线兜底：设备时间 + 上次同步的差值，并按历史最晚时间做单调钳制。</summary>
    private static DateTime OfflineNow()
    {
        long corrected = ToUnix(RawNow) + _offsetSeconds;

        // 设备时间被回拨：钳在历史见过的最晚时间上，倒计时不会因此变长
        if (_maxSeenUnix > 0 && corrected < _maxSeenUnix - FallbackToleranceSeconds)
        {
            Debug.LogWarning($"[GameTime] 离线状态下检测到设备时间回拨，已钳制到历史最晚时间 " +
                             $"{Epoch.AddSeconds(_maxSeenUnix):yyyy-MM-dd HH:mm:ss}");
            corrected = _maxSeenUnix;
        }

        if (corrected > _maxSeenUnix)
        {
            _maxSeenUnix = corrected;
            // 每推进 60s 以上才落盘，减少 PlayerPrefs IO
            if (_persistedMaxSeen <= 0 || _maxSeenUnix - _persistedMaxSeen >= 60)
            {
                _persistedMaxSeen = _maxSeenUnix;
                PlayerPrefs.SetString(PrefMaxSeen, _maxSeenUnix.ToString());
                PlayerPrefs.Save();
            }
        }

        return Epoch.AddSeconds(corrected);
    }

    private static void EnsureInit()
    {
        if (_inited) return;
        _inited = true;

        long.TryParse(PlayerPrefs.GetString(PrefOffset, "0"), out _offsetSeconds);
        long.TryParse(PlayerPrefs.GetString(PrefMaxSeen, "0"), out _maxSeenUnix);
        _persistedMaxSeen = _maxSeenUnix;
        _lastSyncLogUnix = 0;
    }

    private static long ToUnix(DateTime t)
    {
        return (long)(t - Epoch).TotalSeconds;
    }
}
