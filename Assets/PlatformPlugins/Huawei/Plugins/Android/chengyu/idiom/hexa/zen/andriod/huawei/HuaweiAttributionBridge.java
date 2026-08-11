package chengyu.idiom.hexa.zen.andriod.huawei;

import android.content.ContentResolver;
import android.content.Context;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;

import com.huawei.hms.analytics.HiAnalytics;
import com.huawei.hms.analytics.HiAnalyticsInstance;
import com.huawei.hms.analytics.HiAnalyticsTools;

import org.json.JSONObject;

/**
 * 华为归因与转化事件桥接类
 * 用于 Unity 调用，请在调用前确保已获得用户隐私同意。
 */
public class HuaweiAttributionBridge {

    private static final String TAG = "HuaweiAttrBridge";

    // ---------- 归因信息查询（ContentProvider） ----------
    private static final String PROVIDER_URI = "content://com.huawei.appmarket.commondata/item/5";

    // Cursor 列索引
    private static final int INDEX_ENTER_AG_TIME = 1;         // 进入应用市场时间
    private static final int INDEX_INSTALLED_FINISH_TIME = 2; // 安装完成时间
    private static final int INDEX_START_DOWNLOAD_TIME = 3;   // 开始下载时间
    private static final int INDEX_TRACKID = 4;               // 付费推广 trackId（JSON）
    private static final int INDEX_REFERRER_EX = 5;           // 下载链接中的 referrer 参数

    /**
     * 获取华为应用市场归因信息。
     *
     * @param context     Android 上下文（通常为当前 Activity）
     * @param packageName 要查询的包名（通常为自己的应用包名）
     * @return JSON 字符串，包含归因字段；若无归因数据则返回 null
     */
    public static String getAttributionInfo(Context context, String packageName) {
        Cursor cursor = null;
        try {
            Uri uri = Uri.parse(PROVIDER_URI);
            ContentResolver resolver = context.getContentResolver();
            String[] selectionArgs = new String[]{packageName};
            cursor = resolver.query(uri, null, null, selectionArgs, null);

            if (cursor != null && cursor.moveToFirst()) {
                JSONObject json = new JSONObject();
                json.put("enterAgTime", cursor.getString(INDEX_ENTER_AG_TIME));
                json.put("installedFinishTime", cursor.getString(INDEX_INSTALLED_FINISH_TIME));
                json.put("startDownloadTime", cursor.getString(INDEX_START_DOWNLOAD_TIME));

                // trackId 本身是一个 JSON 字符串，直接解析后放入
                String trackId = cursor.getString(INDEX_TRACKID);
                if (trackId != null && !trackId.isEmpty()) {
                    json.put("trackId", new JSONObject(trackId));
                }

                String referrerEx = cursor.getString(INDEX_REFERRER_EX);
                if (referrerEx != null && !referrerEx.isEmpty()) {
                    json.put("referrerEx", referrerEx);
                }

                Log.i(TAG, "Attribution info: " + json.toString());
                return json.toString();
            } else {
                Log.w(TAG, "No attribution data found for " + packageName);
                return null;
            }
        } catch (Exception e) {
            Log.e(TAG, "getAttributionInfo error: " + e.getMessage());
            return null;
        } finally {
            if (cursor != null) {
                cursor.close();
            }
        }
    }

    // ---------- 转化事件上报（HiAnalytics） ----------

    /**
     * 初始化华为分析服务。建议在 Application 或首个 Activity 的早期调用一次。
     *
     * @param context 上下文
     */
    public static void initAnalytics(Context context) {
        try {
            // 开启调试日志，上线前可注释掉
            HiAnalyticsTools.enableLog();
            // 可以在此设置其他配置，如用户 ID、推送 Token 等
            Log.i(TAG, "HiAnalytics initialized successfully.");
        } catch (Exception e) {
            Log.e(TAG, "initAnalytics error: " + e.getMessage());
        }
    }

    /**
     * 上报转化事件（通用方法，使用字符串事件 ID 和可选的参数 JSON）。
     * 事件 ID 需要提前在 AppGallery Connect 的“转化事件”中进行定义。
     *
     * @param context        上下文
     * @param eventId        事件 ID（例如 "Purchase", "LevelUp" 等）
     * @param eventParamsJson 事件参数字符串，格式 {"key1":"value1","key2":"value2"}，可传 null
     */
    public static void reportConversionEvent(Context context, String eventId, String eventParamsJson) {
        try {
            HiAnalyticsInstance instance = HiAnalytics.getInstance(context);
            Bundle bundle = new Bundle();

            if (eventParamsJson != null && !eventParamsJson.isEmpty()) {
                JSONObject json = new JSONObject(eventParamsJson);
                java.util.Iterator<String> keys = json.keys();
                while (keys.hasNext()) {
                    String key = keys.next();
                    String value = json.getString(key);
                    bundle.putString(key, value);
                }
            }

            instance.onEvent(eventId, bundle);
            Log.i(TAG, "Reported event: " + eventId + ", params: " + eventParamsJson);
        } catch (Exception e) {
            Log.e(TAG, "reportConversionEvent error: " + e.getMessage());
        }
    }

    /**
     * 上报转化事件（简化版，无额外参数）。
     *
     * @param context 上下文
     * @param eventId 事件 ID
     */
    public static void reportConversionEvent(Context context, String eventId) {
        reportConversionEvent(context, eventId, null);
    }
}