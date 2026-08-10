package com.bytedance.android;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageView;

import com.bytedance.sdk.openadsdk.TTAdConstant;
import com.bytedance.sdk.openadsdk.TTAdDislike;
import com.bytedance.sdk.openadsdk.TTAppDownloadListener;
import com.bytedance.sdk.openadsdk.TTFeedAd;
import com.bytedance.sdk.openadsdk.TTImage;
import com.bytedance.sdk.openadsdk.TTNativeAd;
import com.bytedance.sdk.openadsdk.TTNativeExpressAd;
import com.bytedance.sdk.openadsdk.mediation.MediationConstant;
import com.bytedance.sdk.openadsdk.mediation.ad.MediationExpressRenderListener;
import com.bytedance.sdk.openadsdk.mediation.manager.MediationNativeManager;

import java.util.ArrayList;
import java.util.List;

import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.Map;

@SuppressWarnings("EmptyMethod")
public class NativeAdManager {

    public static final String TAG = "CSJM_Unity_";

    private static volatile NativeAdManager sManager;

    private BannerView mBannerView;
    private View mExpressView;
    private View mExpressBannerView;
    private View mFeedView;
    private Handler mHandler;

    private NativeAdManager() {
        if (mHandler == null) {
            mHandler = new Handler(Looper.getMainLooper());
        }
    }

    // unity端调用
    public static NativeAdManager getNativeAdManager() {
        if (sManager == null) {
            synchronized (NativeAdManager.class) {
                if (sManager == null) {
                    sManager = new NativeAdManager();
                }
            }
        }
        return sManager;
    }

    public ViewGroup getRootLayout(Activity context) {
        if (context == null) {
            return null;
        }
        ViewGroup rootGroup = null;
        rootGroup = context.findViewById(android.R.id.content);
        return rootGroup;
    }

    public void addAdView(Activity context, View adView, ViewGroup.LayoutParams layoutParams) {
        if (context == null || adView == null || layoutParams == null) {
            return;
        }
        ViewGroup group = getRootLayout(context);
        if (group == null) {
            return;
        }
        group.addView(adView, layoutParams);
    }

    public void removeAdView(Activity context, View adView) {
        if (context == null || adView == null) {
            return;
        }
        ViewGroup group = getRootLayout(context);
        if (group == null) {
            return;
        }
        group.removeView(adView);
    }

    // unity端调用
    //相关调用注意放在主线程
    public void showNativeBannerAd(final Context context, final TTNativeAd nativeAd,
                                   final TTNativeAd.AdInteractionListener listener,
                                   final TTAdDislike.DislikeInteractionCallback dislikeCallBack,
                                   final int left, final int top) {
        if (context == null || nativeAd == null) {
            return;
        }
        mHandler.post(new Runnable() {
            @Override
            public void run() {
                removeAdView((Activity) context, mBannerView);
                mBannerView = new BannerView(context);
                FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(
                        (int)dip2Px(context, 320), (int)dip2Px(context, 150));
                layoutParams.gravity = Gravity.LEFT | Gravity.TOP;
                layoutParams.leftMargin = left;
                layoutParams.topMargin = top;
                addAdView((Activity) context, mBannerView, layoutParams);
                //绑定原生广告的数据
                setBannerAdData(context, mBannerView, nativeAd, dislikeCallBack, listener);
            }
        });
    }

    // unity端调用
    //相关调用注意放在主线程
    public void showExpressFeedAd(final Context context, final TTNativeExpressAd nativeExpressAd,
                                  final TTNativeExpressAd.AdInteractionListener listener,
                                  final TTAdDislike.DislikeInteractionCallback dislikeCallback,
                                  final int left, final int top) {
        if (context == null || nativeExpressAd == null) {
            return;
        }
        nativeExpressAd.setExpressInteractionListener(new TTNativeExpressAd.AdInteractionListener() {
            @Override
            public void onAdDismiss() {
                if (listener != null) {
                    listener.onAdDismiss();
                }
            }

            @Override
            public void onAdClicked(View view, int i) {
                if (listener != null) {
                    listener.onAdClicked(view, i);
                }
            }

            @Override
            public void onAdShow(View view, int i) {
                if (listener != null) {
                    listener.onAdShow(view, i);
                }
            }

            @Override
            public void onRenderFail(View view, String s, int i) {
                if (listener != null) {
                    listener.onRenderFail(view, s, i);
                }
            }

            @Override
            public void onRenderSuccess(final View view, final float v, final float v1) {
                if (listener != null) {
                    listener.onRenderSuccess(view, v, v1);
                }
                mHandler.post(new Runnable() {
                    @Override
                    public void run() {
                        removeAdView((Activity) context, mExpressView);
                        mExpressView = view;
                        FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(
                                (int) dip2Px(context, v), (int) dip2Px(context, v1));
                        layoutParams.gravity = Gravity.LEFT | Gravity.TOP;
                        layoutParams.leftMargin = left;
                        layoutParams.topMargin = top;
                        addAdView((Activity) context, mExpressView, layoutParams);
                    }
                });
            }
        });
        mHandler.post(new Runnable() {
            @Override
            public void run() {
                nativeExpressAd.setDislikeCallback((Activity) context, new TTAdDislike.DislikeInteractionCallback() {
                    @Override
                    public void onSelected(int i, String s, boolean enforce) {
                        Log.e(TAG, "express feed dislike onSelected");
                        if (dislikeCallback != null) {
                            dislikeCallback.onSelected(i, s, enforce);
                        }
                        mHandler.post(new Runnable() {
                            @Override
                            public void run() {
                                removeExpressView(context);
                            }
                        });
                    }

                    @Override
                    public void onCancel() {
                        Log.e(TAG, "express feed dislike onCancel");
                        if (dislikeCallback != null) {
                            dislikeCallback.onCancel();
                        }
                    }

                    @Override
                    public void onShow() {
                        Log.e(TAG, "express feed dislike onShow");
                        if (dislikeCallback != null) {
                            dislikeCallback.onShow();
                        }
                    }
                });
            }
        });

        mHandler.post(new Runnable() {
            @Override
            public void run() {
                nativeExpressAd.render();
            }
        });
    }

    // unity端调用
    //相关调用注意放在主线程
    public void showExpressBannerAd(final Context context, final TTNativeExpressAd nativeExpressAd,
                                    final TTNativeExpressAd.AdInteractionListener listener,
                                    final TTAdDislike.DislikeInteractionCallback dislikeCallback,
                                    final int left, final int top) {
        if (context == null || nativeExpressAd == null) {
            return;
        }
        nativeExpressAd.setExpressInteractionListener(new TTNativeExpressAd.AdInteractionListener() {
            @Override
            public void onAdDismiss() {
                Log.e(TAG, "Express Banner onAdDismiss");
                if (listener != null) {
                    listener.onAdDismiss();
                }
            }
            @Override
            public void onAdClicked(View view, int i) {
                Log.e(TAG, "Express Banner onAdClicked");
                if (listener != null) {
                    listener.onAdClicked(view, i);
                }
            }

            @Override
            public void onAdShow(View view, int i) {
                Log.e(TAG, "Express Banner onAdShow");
                if (listener != null) {
                    listener.onAdShow(view, i);
                }
            }

            @Override
            public void onRenderFail(View view, String s, int i) {
                Log.e(TAG, "Express Banner onRenderFail");
                if (listener != null) {
                    listener.onRenderFail(view, s, i);
                }
            }

            @Override
            public void onRenderSuccess(final View view, final float v, final float v1) {
                Log.e(TAG, "Express Banner onRenderSuccess");
                if (listener != null) {
                    listener.onRenderSuccess(view, v, v1);
                }
                mHandler.post(new Runnable() {
                    @Override
                    public void run() {
                        removeAdView((Activity) context, mExpressBannerView);
                        // 融合需要通过get方法获取bannerView
                        mExpressBannerView = nativeExpressAd.getExpressAdView();
                        Log.e(TAG, "Express Banner getExpressAdView: " + mExpressBannerView);
//                        FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(
//                                (int) dip2Px(context, v), (int) dip2Px(context, v1));
                        FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(
                                FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT);
                        layoutParams.gravity = Gravity.LEFT | Gravity.TOP;
                        layoutParams.leftMargin = left;
                        layoutParams.topMargin = top;
                        addAdView((Activity) context, mExpressBannerView, layoutParams);
                    }
                });
            }
        });
        mHandler.post(new Runnable() {
            @Override
            public void run() {
                nativeExpressAd.setDislikeCallback((Activity) context, new TTAdDislike.DislikeInteractionCallback() {
                    @Override
                    public void onSelected(int i, String s, boolean enforce) {
                        Log.e(TAG, "express banner dislike onSelected");
                        if (dislikeCallback != null) {
                            dislikeCallback.onSelected(i, s, enforce);
                        }
                        mHandler.post(new Runnable() {
                            @Override
                            public void run() {
                                removeExpressBannerView(context);
                            }
                        });
                    }

                    @Override
                    public void onCancel() {
                        Log.e(TAG, "express banner dislike onCancel");
                        if (dislikeCallback != null) {
                            dislikeCallback.onCancel();
                        }
                    }
                    @Override
                    public void onShow() {
                        Log.e(TAG, "express banner dislike onShow");
                        if (dislikeCallback != null) {
                            dislikeCallback.onShow();
                        }
                    }
                });
            }
        });
        mHandler.post(new Runnable() {
            @Override
            public void run() {
                nativeExpressAd.render();
            }
        });
    }

    // unity端调用
    // 融合信息流，模板和自渲染都支持
    public void showFeedAd(final Context context, final TTFeedAd feedAd,
                           final TTNativeAd.AdInteractionListener listener,
                           final TTAdDislike.DislikeInteractionCallback dislikeCallback,
                           final int left, final int top) {
        final MyDislikeCallback innerDislikeCallback = new MyDislikeCallback(dislikeCallback, new Runnable() {
            @Override
            public void run() {
                mHandler.post(new Runnable() {
                    @Override
                    public void run() {
                        removeFeedView(context);
                    }
                });
            }
        });
        MediationNativeManager manager = feedAd.getMediationManager();
        if (manager != null) {
            if (manager.isExpress()) { // --- 模板feed流广告
                Log.d(TAG, "show feed express");
                mHandler.post(new Runnable() {
                    @Override
                    public void run() {
                        feedAd.setDislikeCallback((Activity) context, innerDislikeCallback);
                        feedAd.setExpressRenderListener(new MediationExpressRenderListener() {
                            @Override
                            public void onRenderFail(View view, String s, int i) {
                                Log.d(TAG, "feed express render fail, errCode: " + i + ", errMsg: " + s);
                            }

                            @Override
                            public void onAdClick() {
                                Log.d(TAG, "feed express click");
                                if (listener != null) {
                                    listener.onAdClicked(null, feedAd);
                                }
                            }

                            @Override
                            public void onAdShow() {
                                Log.d(TAG, "feed express show");
                                if (listener != null) {
                                    listener.onAdShow(feedAd);
                                }
                            }

                            @Override
                            public void onRenderSuccess(View view, float v, float v1, boolean b) {
                                Log.d(TAG, "feed express render success");
                                final View expressFeedView = feedAd.getAdView(); // *** 注意不要使用onRenderSuccess参数中的view ***
                                if (expressFeedView != null) {
                                    mHandler.post(new Runnable() {
                                        @Override
                                        public void run() {
                                            removeFeedView(context);
                                            mFeedView = expressFeedView;
                                            FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(
                                                    FrameLayout.LayoutParams.MATCH_PARENT, 600);
                                            layoutParams.gravity = Gravity.LEFT | Gravity.TOP;
                                            layoutParams.leftMargin = left;
                                            layoutParams.topMargin = top;
                                            addAdView((Activity) context, mFeedView, layoutParams);
                                        }
                                    });
                                }
                            }
                        });
                        feedAd.render(); // 调用render方法进行渲染，在onRenderSuccess中展示广告
                    }
                });

            } else { // --- 自渲染feed流广告

                Log.d(TAG, "show feed native");
                // 自渲染广告返回的是广告素材，开发者自己将其渲染成view
                mHandler.post(new Runnable() {
                    @Override
                    public void run() {
                        View feedView = FeedAdUtils.getFeedViewFromFeedAd(feedAd, (Activity) context,
                                listener, innerDislikeCallback);
                        Log.d(TAG, "getFeedViewFromFeedAd");
                        if (feedView != null) {
                            removeFeedView(context);
                            mFeedView = feedView;
                            // 如果存在，添加ks的摇扭view。目前ks关闭了自渲染摇扭功能，后续开放了，媒体可以自己设置
                            /*
                            Map<String, Object> extra = feedAd.getMediaExtraInfo();
                            if (extra != null) {
                                Object rotateViewObj = extra.get(MediationConstant.GM_EXTRA_KEY_KS_GET_ROTATE_VIEW);
                                if (rotateViewObj instanceof View) {
                                    FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(
                                            FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT);
                                    layoutParams.gravity = Gravity.CENTER;
                                    ((ViewGroup)mFeedView).addView((View) rotateViewObj, layoutParams);
                                }
                            }
                            */

                            FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(
                                    FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT);
                            layoutParams.gravity = Gravity.LEFT | Gravity.TOP;
                            layoutParams.leftMargin = left;
                            layoutParams.topMargin = top;
                            addAdView((Activity) context, mFeedView, layoutParams);
                        } else {
                            Log.d(TAG, "feedView is null");
                        }
                    }
                });
            }
        } else {
            Log.d(TAG, "feed mediationManager is null");
        }
    }

    // unity端调用
    public void destroyExpressBannerAd(final Activity activity, final TTNativeExpressAd nativeExpressAd) {
        destoryExpressAd(activity, nativeExpressAd, mExpressBannerView);
    }

    // unity端调用
    public void destroyExpressFeedAd(final Activity activity, final TTNativeExpressAd nativeExpressAd) {
        destoryExpressAd(activity, nativeExpressAd, mExpressView);
    }

    // unity端调用
    public void destroyBannerAd(final Activity activity, final TTNativeAd nativeAd) {
        mHandler.post(new Runnable() {
            @Override
            public void run() {
                removeBannerView(activity);
                if (nativeAd != null) {
                    nativeAd.destroy();
                }
            }
        });
    }

    // unity端调用
    public void destroyFeedAd(final Activity activity, final TTFeedAd feedAd) {
        mHandler.post(new Runnable() {
            @Override
            public void run() {
                removeFeedView(activity);
                if (feedAd != null) {
                    feedAd.destroy();
                }
            }
        });
    }

    //广告使用完毕后，比如关闭或移除后，请调用destory释放资源。
    private void destoryExpressAd(final Activity activity, final TTNativeExpressAd nativeExpressAd,
                                  final View adView) {
        mHandler.post(new Runnable() {
            @Override
            public void run() {
                removeAdView(activity, adView);
                if (nativeExpressAd != null) {
                    nativeExpressAd.destroy();
                }
            }
        });
    }

    private void removeFeedView(Context context) {
        removeAdView((Activity) context, mFeedView);
    }

    private void removeBannerView(Context context) {
        removeAdView((Activity) context, mBannerView);
    }

    private void removeExpressView(Context context) {
        removeAdView((Activity) context, mExpressView);
    }

    private void removeExpressBannerView(Context context) {
        removeAdView((Activity) context, mExpressBannerView);
    }

    private float dip2Px(Context context, float dipValue) {
        final float scale = context.getResources().getDisplayMetrics().density;
        return dipValue * scale + 0.5f;
    }

    private void setBannerAdData(final Context context, BannerView nativeView, TTNativeAd nativeAd,
                                 final TTAdDislike.DislikeInteractionCallback dislikeCallBack,
                                 final TTNativeAd.AdInteractionListener listener) {
        nativeView.setTitle(nativeAd.getTitle());
        final View dislike = nativeView.getDisLikeView();
        Button mCreativeButton = nativeView.getCreateButton();
        bindDislikeAction(context, nativeAd, dislike, new TTAdDislike.DislikeInteractionCallback() {
            @Override
            public void onSelected(int position, String value, boolean enforce) {
                if (dislikeCallBack != null) {
                    dislikeCallBack.onSelected(position, value, enforce);
                }
                removeBannerView(context);
            }

            @Override
            public void onCancel() {
                if (dislikeCallBack != null) {
                    dislikeCallBack.onCancel();
                }
            }

            @Override
            public void onShow() {
                if (dislikeCallBack != null) {
                    dislikeCallBack.onShow();
                }
            }
        });
        if (nativeAd.getImageList() != null && !nativeAd.getImageList().isEmpty()) {
            TTImage image = nativeAd.getImageList().get(0);
            if (image != null && image.isValid()) {
                ImageView im = nativeView.getImageView();
                loadImgByVolley(image.getImageUrl(), im);
            }
        }
        //可根据广告类型，为交互区域设置不同提示信息
        switch (nativeAd.getInteractionType()) {
            case TTAdConstant.INTERACTION_TYPE_DOWNLOAD:
                //如果初始化ttAdManager.createAdNative(getApplicationContext())没有传入activity
                // 则需要在此传activity，否则影响使用Dislike逻辑
                nativeAd.setActivityForDownloadApp((Activity) context);
                mCreativeButton.setVisibility(View.VISIBLE);
                nativeAd.setDownloadListener(new MyDownloadListener(mCreativeButton)); // 注册下载监听器
                break;
            case TTAdConstant.INTERACTION_TYPE_DIAL:
                mCreativeButton.setVisibility(View.VISIBLE);
                mCreativeButton.setText("立即拨打");
                break;
            case TTAdConstant.INTERACTION_TYPE_LANDING_PAGE:
            case TTAdConstant.INTERACTION_TYPE_BROWSER:
                mCreativeButton.setVisibility(View.VISIBLE);
                mCreativeButton.setText("查看详情");
                break;
            default:
                mCreativeButton.setVisibility(View.GONE);
        }

        //可以被点击的view, 也可以把nativeView放进来意味整个广告区域可被点击
        List<View> clickViewList = new ArrayList<>();
        clickViewList.add(nativeView);

        //触发创意广告的view（点击下载或拨打电话）
        List<View> creativeViewList = new ArrayList<>();
        //如果需要点击图文区域也能进行下载或者拨打电话动作，请将图文区域的view传入
        //creativeViewList.add(nativeView);
        creativeViewList.add(mCreativeButton);

        //重要! 这个涉及到广告计费，必须正确调用。convertView必须使用ViewGroup。
        nativeAd.registerViewForInteraction((ViewGroup) nativeView, clickViewList, creativeViewList,
                dislike, new TTNativeAd.AdInteractionListener() {
            @Override
            public void onAdClicked(View view, TTNativeAd ad) {
                if (listener != null) {
                    listener.onAdClicked(view, ad);
                }
            }

            @Override
            public void onAdCreativeClick(View view, TTNativeAd ad) {
                if (listener != null) {
                    listener.onAdCreativeClick(view, ad);
                }
            }

            @Override
            public void onAdShow(TTNativeAd ad) {
                if (listener != null) {
                    listener.onAdShow(ad);
                }
            }
        });

    }

    //接入网盟的dislike 逻辑，有助于提示广告精准投放度
    private void bindDislikeAction(Context context, TTNativeAd ad, View dislikeView,
                                   TTAdDislike.DislikeInteractionCallback callback) {
        final TTAdDislike ttAdDislike = ad.getDislikeDialog((Activity) context);
        if (ttAdDislike != null) {
            ttAdDislike.setDislikeInteractionCallback(callback);
        }
        dislikeView.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Log.e(TAG, "dislikeView onClick");
                if (ttAdDislike != null)
                    ttAdDislike.showDislikeDialog();
            }
        });
    }

    static class MyDownloadListener implements TTAppDownloadListener {
        Button mDownloadButton;
        Handler mHandler;

        public MyDownloadListener(Button button) {
            mDownloadButton = button;
            mHandler = new Handler(Looper.getMainLooper());
        }

        @Override
        public void onIdle() {
            mHandler.post(new Runnable() {
                @Override
                public void run() {
                    if (mDownloadButton != null) {
                        mDownloadButton.setText("开始下载");
                    }
                }
            });
        }

        @SuppressLint("SetTextI18n")
        @Override
        public void onDownloadActive(final long totalBytes, final long currBytes, String fileName, String appName) {
            mHandler.post(new Runnable() {
                @Override
                public void run() {
                    if (mDownloadButton != null) {
                        if (totalBytes <= 0L) {
                            mDownloadButton.setText("下载中 percent: 0");
                        } else {
                            if (totalBytes > 0) {
                                mDownloadButton.setText("下载中 percent: " + (currBytes * 100 / totalBytes));
                            }
                        }
                    }
                }
            });
        }

        @SuppressLint("SetTextI18n")
        @Override
        public void onDownloadPaused(final long totalBytes, final long currBytes, String fileName, String appName) {
            mHandler.post(new Runnable() {
                @Override
                public void run() {
                    if (mDownloadButton != null) {
                        if (totalBytes > 0) {
                            mDownloadButton.setText("下载暂停 percent: " + (currBytes * 100 / totalBytes));
                        }
                    }
                }
            });
        }

        @Override
        public void onDownloadFailed(long totalBytes, long currBytes, String fileName, String appName) {
            mHandler.post(new Runnable() {
                @Override
                public void run() {
                    if (mDownloadButton != null) {
                        mDownloadButton.setText("重新下载");
                    }
                }
            });
        }

        @Override
        public void onInstalled(String fileName, String appName) {
            mHandler.post(new Runnable() {
                @Override
                public void run() {
                    if (mDownloadButton != null) {
                        mDownloadButton.setText("点击打开");
                    }
                }
            });
        }

        @Override
        public void onDownloadFinished(long totalBytes, String fileName, String appName) {
            mHandler.post(new Runnable() {
                @Override
                public void run() {
                    if (mDownloadButton != null) {
                        mDownloadButton.setText("点击安装");
                    }
                }
            });
        }
    }

    static class MyDislikeCallback implements TTAdDislike.DislikeInteractionCallback {

        private TTAdDislike.DislikeInteractionCallback mCallback;
        private Runnable mRunnable;

        MyDislikeCallback(TTAdDislike.DislikeInteractionCallback callback, Runnable runnable) {
            this.mCallback = callback;
            this.mRunnable = runnable;
        }

        @Override
        public void onShow() {
            if (mCallback != null) {
                mCallback.onShow();
            }
        }

        @Override
        public void onSelected(int i, String s, boolean b) {
            if (mCallback != null) {
                mCallback.onSelected(i, s, b);
            }
            if (mRunnable != null) {
                mRunnable.run();
            }
        }

        @Override
        public void onCancel() {
            if (mCallback != null) {
                mCallback.onCancel();
            }
        }
    }

    public static void loadImgByVolley(final String imageUrl, final ImageView view) {
        if (view == null || imageUrl == null ) {
            return;
        }
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    URL url = new URL(imageUrl);
                    HttpURLConnection connection = (HttpURLConnection) url.openConnection();
                    //默认是get请求   如果想使用post必须指明
                    connection.setRequestMethod("GET");
                    connection.setReadTimeout(5000);
                    connection.setConnectTimeout(5000);

                    int code = connection.getResponseCode();
                    if (code == 200) {
                        InputStream inputStream = connection.getInputStream();
                        final Bitmap bit = BitmapFactory.decodeStream(inputStream);
                        view.post(new Runnable() {
                            @Override
                            public void run() {
                                view.setImageBitmap(bit);
                            }
                        });
                    } 
                } catch (Throwable ignored) {
                }
            }
        }).start();
    }
}