namespace ByteDance.Union.Mediation
{
    public sealed class MediationAdLoadInfo
    {

        public string mediationRit { set; get; }
        public string adnName { set; get; }
        public string adType { set; get; }
        public int errCode { set; get; }
        public string errMsg { set; get; }
        public string errUserInfo { set; get; }

        public override string ToString()
        {
            return "MediationAdLoadInfo: mediationRit:" + mediationRit + ", adnName:" + adnName
                + ", adType:" + adType + ", errCode:" + errCode + ", errMsg:" + errMsg +
                ", errUserInfo:" + errUserInfo;
        }
    }

}