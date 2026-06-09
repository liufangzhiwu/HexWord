using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StimulateRuleConfig
{
    /**
     * 鼓励横幅类型
     */
    public BannerType[] BannerTypes;

    /**
     * 鼓励标题概率结算类型, 只有横幅类型是1和2才显示
     */
    public int TitleRate;

    /**
     * 激励文案概率
     * 结算类型, 只有横幅类型是1和2才显示
     */
    public int StimulateRate;

    /**
     * 是否全屏撒花
     */
    public bool ScatterFlowers;

    /**
     * 展示优先级
     */
    public int Priority;

    /**
     * 文案类型
     * 1.禅意分百分比关联
     * 2.叶子全收集
     * 3.极速通关
     * 4.完美收集
     * 5.新记录
     * 6.困难关无压力
     */
    public int Type;

    /**
     * 禅意百分比触发区间
     * 0->大于等于, 1->小于
     */
    public int[] ZenPercent;

    /**
     * 标题文案
     */
    public string TitleKey;

    /**
     * 激励语文案
     */
    public string PhraseKey;

    /**
     * 文案图标文件名
     */
    public string EmojiKey;

    /**
     * 长激励语文案
     */
    public string LongTextKey;
}

/***
 * 横幅配置
 */
public class BannerType
{
    /**
     * 横幅编号
     */
    public int Number;

    /**
     * 显示概率
     */
    public int Rate;
}