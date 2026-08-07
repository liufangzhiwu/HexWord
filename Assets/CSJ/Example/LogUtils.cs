using System.Collections.Generic;
using ByteDance.Union.Mediation;
using UnityEngine;

/**
 * 日志打印工具类
 */
public class LogUtils
{
    
    public static void LogMediationAdEcpmInfo(MediationAdEcpmInfo info, string methodName)
    {
        string customDataStr = "";
        if (info.customData != null)
        {
            foreach (KeyValuePair<string,string> pair in info.customData)
            {
                customDataStr += (pair.Key + ": " + pair.Value + " ");
            }
        }
        Debug.Log("CSJM_Unity " + "Example " + methodName + 
                  ",\nsdkName: " + info.sdkName +
                  ",\ncustomSdkNam: " + info.customSdkName +
                  ",\nslotId: " + info.slotId +
                  ",\nlevelTag: " + info.levelTag +
                  ",\necpm: " + info.ecpm +
                  ",\nreqBiddingType: " + info.reqBiddingType +
                  ",\nerrorMsg: " + info.errorMsg +
                  ",\nrequestId: " + info.requestId +
                  ",\nritType: " + info.ritType +
                  ",\nsegmentId: " + info.segmentId +
                  ",\nchannel: " + info.channel +
                  ",\nsubChannel: " + info.subChannel +
                  ",\nabTestId: " + info.abTestId +
                  ",\nscenarioId: " + info.scenarioId +
                  ",\ncustomData: " + customDataStr
                  );
    }

    public static void LogAdLoadInfo(MediationAdLoadInfo item)
    {
        Debug.Log("CSJM_Unity " + "Example " + "GetAdLoadInfo" + 
                  ",\nmediationRit: " + item.mediationRit +
                  ",\nadnName: " + item.adnName +
                  ",\nadType: " + item.adType +
                  ",\nerrCode: " + item.errCode +
                  ",\nerrMsg: " + item.errMsg +
                  ",\nerrUserInfo: " + item.errUserInfo
                  );
    }
}