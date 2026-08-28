using System.Collections;
using System.Collections.Generic;
using Knivt.Tools.UI;
using UnityEngine;

public class MainRankingList : UICyclicScrollList<OverallRankItem, OverallRankState>
{
    // 记录当前是月榜还是总榜，以便在刷新时传入正确的参数
    public bool IsMonthly { get; set; } 

    protected override void ResetCellData(OverallRankItem cell, OverallRankState state, int dataIndex)
    {
        // 调用你原来的转换逻辑
        cell.gameObject.SetActive(true);
        cell.SetRankInfo(state, IsMonthly, false);
    }
    
}
