using System.Collections;
using System.Collections.Generic;
using Knivt.Tools.UI;
using UnityEngine;

public struct HallOfFameGroupData
{
    public string Date;
    public List<MonthlyTopPlayer> TopPlayers;
}

public class HallOfFameList : UICyclicScrollList<MonthlyRankingRecord, HallOfFameGroupData>
{
    protected override void ResetCellData(MonthlyRankingRecord cell, HallOfFameGroupData data, int dataIndex)
    {
        cell.gameObject.SetActive(true);
        cell.SetRecordData(data.Date, data.TopPlayers);
    }
}
