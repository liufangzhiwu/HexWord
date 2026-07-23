package com.unity3d.player;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.ActivityInfo;
import android.os.Bundle;
import android.text.Html;
import android.text.method.LinkMovementMethod;
import android.widget.ScrollView;
import android.widget.TextView;
import android.graphics.BitmapFactory;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.Drawable;
import android.view.WindowManager;
import android.util.TypedValue;
import android.widget.Button;
import android.view.Gravity;
import android.widget.LinearLayout;

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
        // 1. 构建文本内容
        String bodyMessage = "欢迎体验我们的游戏！点击可查看我们的 " +
                "<a href=\"https://mindwordplay.cn/yhxyb\">服务条款</a> 和 " +
                "<a href=\"https://agreement-drcn.hispace.dbankcloud.cn/index.html?lang=zh&agreementId=1828334564204899008\">隐私政策</a> ，" +
                "如您同意，可点击「继续」进入游戏。<br/><br/>希望您能愉快地体验我们的产品。感谢您的选择！";

        TextView welcomeView = new TextView(this);
        welcomeView.setText("欢迎");
        welcomeView.setTextSize(TypedValue.COMPLEX_UNIT_SP, 30);
        welcomeView.setTextColor(0xFF3A516A);
        welcomeView.setGravity(Gravity.CENTER);
        welcomeView.setPadding(48, 50, 48, 0);

        TextView bodyView = new TextView(this);
        bodyView.setPadding(30, 320, 30, 32);
        bodyView.setTextSize(20);
        bodyView.setLineSpacing(1f, 1f);
        bodyView.setText(Html.fromHtml(bodyMessage, Html.FROM_HTML_MODE_LEGACY));
        bodyView.setMovementMethod(LinkMovementMethod.getInstance());
        bodyView.setTextColor(0xFF3A516A);
        // ========== 新增：设置超链接颜色 ==========
        bodyView.setLinkTextColor(0xFF3A516A);   // 链接颜色与正文保持一致
        // ======================================

        LinearLayout contentLayout = new LinearLayout(this);
        contentLayout.setOrientation(LinearLayout.VERTICAL);
        contentLayout.addView(welcomeView);
        contentLayout.addView(bodyView);

        ScrollView scrollView = new ScrollView(this);
        scrollView.addView(contentLayout);

        // 2. 创建“继续”按钮（使用自定义图片，固定尺寸 260×90 像素，您已修改）
        Button continueButton = new Button(this);
        continueButton.setText("继续");
        continueButton.setTextSize(TypedValue.COMPLEX_UNIT_SP, 25);
        continueButton.setGravity(Gravity.CENTER);
        continueButton.setTextColor(0xFFFFFFFF);
        try (InputStream is = getAssets().open("continue_button.png")) {
            Drawable drawable = new BitmapDrawable(getResources(), BitmapFactory.decodeStream(is));
            continueButton.setBackground(drawable);
        } catch (IOException e) {
            e.printStackTrace();
            continueButton.setBackgroundColor(0xFF3A516A);
        }
        continueButton.setPadding(30, 20, 30, 20);

        LinearLayout.LayoutParams btnLp = new LinearLayout.LayoutParams(260, 90);
        btnLp.gravity = Gravity.CENTER_HORIZONTAL;
        btnLp.topMargin = 20;
        btnLp.bottomMargin = 50;
        continueButton.setLayoutParams(btnLp);
        continueButton.setElevation(100f);

        // 3. 根布局
        LinearLayout rootLayout = new LinearLayout(this);
        rootLayout.setOrientation(LinearLayout.VERTICAL);
        try (InputStream is = getAssets().open("background.png")) {
            Drawable drawable = new BitmapDrawable(getResources(), BitmapFactory.decodeStream(is));
            rootLayout.setBackground(drawable);
        } catch (IOException e) {
            e.printStackTrace();
            rootLayout.setBackgroundColor(0x88000000);
        }

        rootLayout.addView(scrollView, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1.0f));
        rootLayout.addView(continueButton);

        // 4. 创建 AlertDialog
        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setView(rootLayout);
        builder.setCancelable(false);
        AlertDialog dialog = builder.create();
        dialog.show();

        // 5. 按钮点击事件
        continueButton.setOnClickListener(v -> {
            getSharedPreferences("PrivacyPrefs", MODE_PRIVATE)
                    .edit().putBoolean("isPrivacyAgreed", true).apply();
            startUnityActivity();
        });

        // 6. 窗口尺寸
        WindowManager.LayoutParams lpWindow = dialog.getWindow().getAttributes();
        lpWindow.width = 550;
        lpWindow.height = 900;
        dialog.getWindow().setAttributes(lpWindow);
        dialog.getWindow().setBackgroundDrawableResource(android.R.color.transparent);
    }

    private void startUnityActivity() {
        Intent intent = new Intent(this, UnityPlayerActivity.class);
        startActivity(intent);
        finish();
    }
}