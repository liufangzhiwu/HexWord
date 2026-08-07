namespace ByteDance.Union.Mediation
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public sealed class MediationAdEcpmInfo
    {

        public Dictionary<string, string> customData;// only Android
        public string sdkName;
        public string customSdkName;
        public string slotId;
        public string levelTag;
        public string ecpm;
        public int reqBiddingType;
        public string errorMsg;
        public string requestId;
        public string ritType;
        public string segmentId;
        public string channel;
        public string subChannel;
        public string abTestId;
        public string scenarioId;

        public override string ToString()
        {
            string customDataStr = "";
            if (customData != null)
            {
                foreach (KeyValuePair<string,string> pair in customData)
                {
                    customDataStr += (pair.Key + ": " + pair.Value + " ");
                }
            }
            return "MediationAdEcpmInfo: sdkName:" + sdkName + ", customSdkName:" + customSdkName
                + ", slotId:" + slotId + ", levelTag:" + levelTag + ", ecpm:" + ecpm + 
                ", reqBiddingType:" + reqBiddingType + ", errorMsg:" + errorMsg
                + ", requestId:" + requestId + ", ritType:" + ritType + ", segmentId:" + segmentId +
                ", channel:" + channel + ", subChannel:" + subChannel
                + ", abTestId:" + abTestId + ", scenarioId:" + scenarioId + ", customData:" + customDataStr;
        }
    }
}