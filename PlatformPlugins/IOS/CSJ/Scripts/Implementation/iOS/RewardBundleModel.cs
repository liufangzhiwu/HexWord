using System;
using UnityEngine;

#if UNITY_IOS
namespace ByteDance.Union
{
    public class RewardBundleModel : IRewardBundleModel
    {

        public RewardBundleModel(int serverErrorCode,
            string serverErrorMsg,
            string rewardName,
            float rewardAmount,
            float rewardPropose,
            bool isGromoreServersideVerify,
            string gromoreExtra,
            string transId,
            int reason,
            int errCode,
            string errMsg,
            string adnName,
            string ecpm)
        {
            ServerErrorCode = serverErrorCode;
            ServerErrorMsg = serverErrorMsg;
            RewardName = rewardName;
            RewardAmount = rewardAmount;
            RewardPropose = rewardPropose;

            GMIsServerSideVerify = isGromoreServersideVerify;
            GMExtra = gromoreExtra;
            GMTransId = transId;
            GMReason = reason;
            GMErrorCode = errCode;
            GMErrorMsg = errMsg;
            GMAdnName = adnName;
            GMEcpm = ecpm;
        }

        public int ServerErrorCode { get; }
        public string ServerErrorMsg { get; }
        public string RewardName { get; }
        public float RewardAmount { get; }
        public float RewardPropose { get; }
        // 是否是GroMore服务端奖励验证
        public bool GMIsServerSideVerify { get; }
        // gromore服务端奖励验证透传信息
        public string GMExtra { get; }
        // gromore服务端验证adnName
        public string GMAdnName { get; }
        // gromore服务端验证transId
        public string GMTransId { get; }
        // gromore服务端验证reason
        public int GMReason { get; }
        // gromore服务端验证errorCode
        public int GMErrorCode { get; }
        // gromore服务端验证errorMsg
        public string GMErrorMsg { get; }
        // 本次广告展示的ecpm
        public string GMEcpm { get; }

        public override string ToString()
        {
            return $"ServerErrorCode : {ServerErrorCode}, ServerErrorMsg:{ServerErrorMsg}, RewardName:{RewardName}," +
                   $" RewardAmount:{RewardAmount}, RewardPropose:{RewardPropose}," +
                   $" isGromoreServersideVerify:{GMIsServerSideVerify}, GMExtra: {GMExtra}, GMAdnName: {GMAdnName}," +
                   $" GMTransId:{GMTransId}, GMReason:{GMReason}, GMErrCode:{GMErrorCode}, GMErrMsg:{GMErrorMsg}," +
                   $" ecpm:{GMEcpm}";
        }
    }
}
#endif