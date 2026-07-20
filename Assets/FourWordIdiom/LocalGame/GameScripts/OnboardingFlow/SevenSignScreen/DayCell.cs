using System;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ==================== 日期格子脚本 ====================

public class DayCell : MonoBehaviour
{
    [SerializeField] public Text basedayText;
    [SerializeField] private Text passdayText;
    [SerializeField] private Text dayText;
    [SerializeField] public Image signMark;        // 签到标记（对勾、高亮等）
    [SerializeField] public Image bgImage;         // 背景（可区分是否当前月）
    public int dayindex;         // 背景（可区分是否当前月）

    public void Setup(int dayNumber, bool isSigned)
    {
        string daystr= dayNumber<10 ? "0"+dayNumber : dayNumber.ToString();

        dayindex = dayNumber;
        basedayText.text = daystr;
        passdayText.text = daystr;
        dayText.text = daystr;
        signMark.gameObject.SetActive(isSigned);
        bgImage.color = Color.white; // 显示背景
        bgImage.gameObject.SetActive(false);
        basedayText.gameObject.SetActive(true);
        transform.GetComponent<Animator>().enabled = false;
    }
    
    /// <summary>
    /// 标记为“过去的日期”：背景可见（可自定义颜色）
    /// </summary>
    public void SetLastDate()
    {
        bgImage.gameObject.SetActive(true);
    }
    
    
    public void SetSignedState(bool isSigned)
    {
        signMark.enabled = true;
        signMark.gameObject.SetActive(isSigned);
        bgImage.color = Color.white; // 显示背景
    }
    
    public void SetHasSigned(int dayNumber)
    {
        dayindex = dayNumber;
        string daystr= dayNumber<10 ? "0"+dayNumber : dayNumber.ToString();
        basedayText.text = daystr;
        passdayText.text = daystr;
        dayText.text = daystr;
        signMark.gameObject.SetActive(true);
        signMark.enabled = false;
        bgImage.gameObject.SetActive(false);
    }

    public void SetEmpty()
    {
        dayindex = -1;
        basedayText.text = "";
        dayText.text = "";
        passdayText.text = "";
        signMark.gameObject.SetActive(false);
        bgImage.color = Color.clear; // 隐藏背景
        transform.GetComponent<Animator>().enabled = false;
    }
}