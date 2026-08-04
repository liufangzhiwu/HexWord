package com.unity3d.player;

import android.app.Activity;
import android.app.Dialog;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.ActivityInfo;
import android.os.Bundle;
import android.text.Html;
import android.text.method.LinkMovementMethod;
import android.view.Gravity;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.util.DisplayMetrics;
import android.util.Log;
import android.util.TypedValue;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.Drawable;

import java.io.InputStream;
import java.io.IOException;

public class PrivacyActivity extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        getWindow().setBackgroundDrawableResource(android.R.color.black);

        SharedPreferences prefs = getSharedPreferences("PrivacyPrefs", MODE_PRIVATE);
        boolean isAgreed = prefs.getBoolean("isPrivacyAgreed", false);

        if (isAgreed) {
            startUnityActivity();
        } else {
            showPrivacyDialog();
        }
    }

    private void showPrivacyDialog() {
        DisplayMetrics dm = getResources().getDisplayMetrics();
        int screenWidth = dm.widthPixels;
        int screenHeight = dm.heightPixels;

        // 弹窗目标尺寸（基于屏幕比例）
        int targetWidth = (int) (screenWidth * 0.85);
        int targetHeight = (int) (screenHeight * 0.80);

        // ---------- 加载背景图 ----------
        Bitmap bgBitmap = null;
        try (InputStream is = getAssets().open("background.png")) {
            bgBitmap = BitmapFactory.decodeStream(is);
        } catch (IOException e) {
            e.printStackTrace();
            Log.e("PrivacyActivity", "Failed to load background.png, using fallback.");
        }

        // 计算实际弹窗尺寸（保持图片比例，适应目标区域）
        float scale;
        int dialogWidth, dialogHeight;
        if (bgBitmap != null) {
            int bw = bgBitmap.getWidth();
            int bh = bgBitmap.getHeight();
            scale = Math.min((float) targetWidth / bw, (float) targetHeight / bh);
            dialogWidth = Math.round(bw * scale);
            dialogHeight = Math.round(bh * scale);
        } else {
            // 无背景图时使用目标尺寸
            dialogWidth = targetWidth;
            dialogHeight = targetHeight;
        }

        // ---------- 构造UI元素 ----------
        // 欢迎标题
        TextView welcomeView = new TextView(this);
        welcomeView.setText("欢迎");
        welcomeView.setTextSize(TypedValue.COMPLEX_UNIT_PX, screenWidth * 0.06f);
        welcomeView.setTextColor(0xFF3A516A);
        welcomeView.setGravity(Gravity.CENTER);
        int paddingH = (int) (dialogWidth * 0.04f);
        welcomeView.setPadding(paddingH, 0, paddingH, 0);

        // 正文
        String bodyMessage = "欢迎体验我们的游戏！点击可查看我们的 " +
                "<a href=\"https://mindwordplay.cn/yhxyb\">服务条款</a> 和 " +
                "<a href=\"https://agreement-drcn.hispace.dbankcloud.cn/index.html?lang=zh&agreementId=1828334564204899008\">隐私政策</a> ，" +
                "如您同意，可点击「同意」进入游戏。<br/><br/>希望您能愉快地体验我们的产品。感谢您的选择！";

        TextView bodyView = new TextView(this);
        int bodyPadding = (int) (dialogWidth * 0.05f);
        int bodyTop = (int) (dialogHeight * 0.27f);
        bodyView.setPadding(bodyPadding, bodyTop, bodyPadding, 0);
        bodyView.setTextSize(TypedValue.COMPLEX_UNIT_PX, screenWidth * 0.048f);
        bodyView.setLineSpacing(1f, 1f);
        bodyView.setText(Html.fromHtml(bodyMessage, Html.FROM_HTML_MODE_LEGACY));
        bodyView.setMovementMethod(LinkMovementMethod.getInstance());
        bodyView.setTextColor(0xFF3A516A);
        bodyView.setLinkTextColor(0xFF3A516A);
        bodyView.setGravity(Gravity.CENTER_VERTICAL | Gravity.START);

        // 内容布局（标题 + 正文）
        LinearLayout contentLayout = new LinearLayout(this);
        contentLayout.setOrientation(LinearLayout.VERTICAL);
        contentLayout.setGravity(Gravity.TOP);
        int contentPaddingTop = (int) (dialogHeight * 0.05f);
        contentLayout.setPadding(0, contentPaddingTop, 0, 0);
        contentLayout.addView(welcomeView);

        LinearLayout.LayoutParams bodyLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1.0f);
        bodyView.setLayoutParams(bodyLp);
        contentLayout.addView(bodyView);

        // ---------- 创建两个按钮（同意 & 拒绝） ----------
        // 按钮尺寸
        int btnWidth = (int) (dialogWidth * 0.4f);
        int btnHeight = (int) (btnWidth * 0.35f);

        // ***** 同意按钮：使用 continue_button.png *****
        Button continueButton = createStyledButton("同意", btnWidth, btnHeight, screenWidth, "continue_button.png");
        continueButton.setOnClickListener(v -> {
            getSharedPreferences("PrivacyPrefs", MODE_PRIVATE)
                    .edit().putBoolean("isPrivacyAgreed", true).apply();
            startUnityActivity();
        });

        // ***** 拒绝按钮：使用 exitbtn.png *****
        Button closeButton = createStyledButton("拒绝", btnWidth, btnHeight, screenWidth, "exitbtn.png");
        closeButton.setOnClickListener(v -> finish());

        // 水平按钮容器
        LinearLayout buttonLayout = new LinearLayout(this);
        buttonLayout.setOrientation(LinearLayout.HORIZONTAL);
        buttonLayout.setGravity(Gravity.CENTER);
        int btnMargin = (int) (dialogWidth * 0.05f);
        buttonLayout.setPadding(btnMargin, 0, btnMargin, 0);

        // 为两个按钮设置等宽权重，并添加间距
        LinearLayout.LayoutParams btnParams = new LinearLayout.LayoutParams(0, btnHeight, 1.0f);
        int marginHalf = (int) (dialogWidth * 0.02f);
        btnParams.setMargins(marginHalf, 0, marginHalf, 0);
        
        // 先添加拒绝（左），再添加同意（右）
        buttonLayout.addView(closeButton, btnParams);   // 拒绝在左
        buttonLayout.addView(continueButton, btnParams); // 同意在右

        // 按钮容器的外边距
        LinearLayout.LayoutParams btnContainerLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        btnContainerLp.topMargin = (int) (dialogHeight * 0.01f);
        btnContainerLp.bottomMargin = (int) (dialogHeight * 0.06f);
        buttonLayout.setLayoutParams(btnContainerLp);

        // ---------- 根布局 ----------
        FrameLayout rootLayout = new FrameLayout(this);
        rootLayout.setBackgroundColor(Color.TRANSPARENT);

        // 背景图（若有）
        ImageView bgView = new ImageView(this);
        bgView.setScaleType(ImageView.ScaleType.FIT_CENTER);
        if (bgBitmap != null) {
            bgView.setImageBitmap(bgBitmap);
        } else {
            // 无背景图时使用半透明灰色背景
            bgView.setBackgroundColor(0x88000000);
        }
        FrameLayout.LayoutParams bgLp = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT);
        rootLayout.addView(bgView, bgLp);

        // 内容容器（包含 contentLayout 和按钮容器）
        LinearLayout contentContainer = new LinearLayout(this);
        contentContainer.setOrientation(LinearLayout.VERTICAL);
        contentContainer.addView(contentLayout, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1.0f));
        contentContainer.addView(buttonLayout);

        FrameLayout.LayoutParams contentLp = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT);
        rootLayout.addView(contentContainer, contentLp);

        // ---------- 创建并显示 Dialog ----------
        Dialog dialog = new Dialog(this, android.R.style.Theme_Translucent_NoTitleBar);
        dialog.setContentView(rootLayout);
        dialog.setCancelable(false);
        dialog.show();

        // 调整窗口属性
        Window window = dialog.getWindow();
        if (window != null) {
            window.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
            window.clearFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
            window.setDimAmount(0f);
            window.setFormat(PixelFormat.RGBA_8888);
            View decor = window.getDecorView();
            if (decor != null) {
                decor.setBackgroundColor(Color.TRANSPARENT);
                decor.setPadding(0, 0, 0, 0);
            }
            WindowManager.LayoutParams lp = window.getAttributes();
            lp.width = dialogWidth;
            lp.height = dialogHeight;
            window.setAttributes(lp);
        }
    }

    /**
     * 辅助方法：创建一个样式统一的按钮，支持自定义背景图片
     * @param text 按钮文字
     * @param width 按钮宽度
     * @param height 按钮高度
     * @param screenWidth 屏幕宽度（用于文字大小）
     * @param bgImageName assets 中的背景图片文件名（如 "continue_button.png"）
     */
    private Button createStyledButton(String text, int width, int height, int screenWidth, String bgImageName) {
        Button button = new Button(this);
        button.setText(text);
        button.setTextSize(TypedValue.COMPLEX_UNIT_PX, screenWidth * 0.05f);
        button.setGravity(Gravity.CENTER);
        button.setTextColor(0xFFFFFFFF);

        // 尝试加载指定的按钮背景图
        try (InputStream is = getAssets().open(bgImageName)) {
            Bitmap original = BitmapFactory.decodeStream(is);
            Bitmap scaled = Bitmap.createScaledBitmap(original, width, height, true);
            Drawable drawable = new BitmapDrawable(getResources(), scaled);
            button.setBackground(drawable);
        } catch (IOException e) {
            e.printStackTrace();
            // 加载失败使用纯色背景
            button.setBackgroundColor(0xFF3A516A);
        }

        int btnPad = (int) (width * 0.09f);
        button.setPadding(btnPad, btnPad / 2, btnPad, btnPad / 2);
        button.setElevation(100f);
        return button;
    }

    private void startUnityActivity() {
        Intent intent = new Intent(this, UnityPlayerActivity.class);
        startActivity(intent);
        finish();
    }
}