package com.unity3d.player;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.text.Html;
import android.text.method.LinkMovementMethod;
import android.widget.ScrollView;
import android.widget.TextView;

public class PrivacyActivity extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // 检查用户是否已经同意过隐私政策
        SharedPreferences prefs = getSharedPreferences("PrivacyPrefs", MODE_PRIVATE);
        boolean isAgreed = prefs.getBoolean("isPrivacyAgreed", false);

        if (isAgreed) {
            // 已经同意，直接启动游戏主Activity
            startUnityActivity();
        } else {
            // 未同意，显示隐私政策弹窗
            showPrivacyDialog();
        }
    }

    /**
     * 显示支持超链接的隐私政策弹窗
     */
    private void showPrivacyDialog() {
        // 带 HTML 标签的隐私文本
        String htmlMessage = "欢迎来到本游戏！<br/><br/>" +
                "为了向您提供更优质的游戏体验与相关服务，我们需要收集和使用您的部分个人信息。<br/>" +
                "在您开启这段禅意之旅前，请知悉：<br/>" +
                "我们将严格遵守相关法律法规，保障您的个人信息安全。<br/>" +
                "我们可能会收集设备信息（如设备型号、操作系统版本）、网络信息、游戏日志等，用于游戏功能实现、服务优化、安全保障、广告投放（如适用）及合规要求。<br/><br/>" +
                "您可以查阅我们的<a href=\"https://mindwordplay.cn/ysxyb\">《隐私政策》</a>了解详细信息，" +
                "包括我们如何收集、使用、存储和保护您的信息，以及您的相关权利。<br/><br/>" +
                "您也需要同意我们的<a href=\"https://mindwordplay.cn/yhxyb\">《用户协议》</a>以使用本游戏。<br/><br/>" +
                "请您仔细阅读并理解以上内容。您的同意对我们至关重要。";

        // 创建 TextView 来显示 HTML 内容
        TextView textView = new TextView(this);
        textView.setPadding(48, 32, 48, 32);
        textView.setTextSize(14);
        textView.setLineSpacing(1.2f, 1.2f);
        // 将 HTML 内容设置到 TextView 中
        textView.setText(Html.fromHtml(htmlMessage, Html.FROM_HTML_MODE_LEGACY));
        // 使链接可点击
        textView.setMovementMethod(LinkMovementMethod.getInstance());

        // 将 TextView 放入 ScrollView，支持滚动
        ScrollView scrollView = new ScrollView(this);
        scrollView.addView(textView);

        // 构建 AlertDialog
        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setTitle("隐私政策与用户协议");
        builder.setView(scrollView);
        builder.setPositiveButton("同意", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialog, int which) {
                SharedPreferences prefs = getSharedPreferences("PrivacyPrefs", MODE_PRIVATE);
                prefs.edit().putBoolean("isPrivacyAgreed", true).apply();
                startUnityActivity();
            }
        });
        builder.setNegativeButton("拒绝", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialog, int which) {
                finish();
                System.exit(0);
            }
        });
        builder.setCancelable(false);
        builder.show();
    }

    /**
     * 启动 UnityPlayerActivity
     */
    private void startUnityActivity() {
        Intent intent = new Intent(this, UnityPlayerActivity.class);
        startActivity(intent);
        finish();
    }
}