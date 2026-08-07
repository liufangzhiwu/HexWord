using System;
using UnityEngine;

#if UNITY_ANDROID
namespace ByteDance.Union
{
    public class RewardBundleModel : IRewardBundleModel
    {
        ///服务器回调错误码，int型
        private static string REWARD_EXTRA_KEY_ERROR_CODE = "reward_extra_key_error_code";

        ///服务器回调错误信息，string型
        private static string REWARD_EXTRA_KEY_ERROR_MSG = "reward_extra_key_error_msg";

        ///开发者平台配置激励名称，string型
        private static string REWARD_EXTRA_KEY_REWARD_NAME = "reward_extra_key_reward_name";

        ///开发者平台配置激励数量，int型
        private static string REWARD_EXTRA_KEY_REWARD_AMOUNT = "reward_extra_key_reward_amount";

        /// 建议奖励百分比，float型
        /// 基础奖励为1，进阶奖励为0.1,0.2，开发者自行换算
        private static string REWARD_EXTRA_KEY_REWARD_PROPOSE = "reward_extra_key_reward_propose";

        // gromore激励服务端验证相关信息
        private const string GM_IS_SERVER_SIDE_VERIFY = "isGroMoreServerSideVerify";
        private const string GM_EXTRA = "gromoreExtra";
        private const string GM_TRANS_ID = "transId";
        private const string GM_REASON = "reason";
        private const string GM_ERROR_CODE = "errorCode";
        private const string GM_ERROR_MSG = "errorMsg";
        private const string GM_ADN_NAME = "adnName";
        private const string GM_ECPM = "ecpm";


        private RewardBundleModel(int serverErrorCode, string serverErrorMsg, string rewardName, float rewardAmount,
            float rewardPropose, bool isGromoreServersideVerify, string gromoreExtra, string transId, int reason,
            int errCode, string errMsg, string adnName, string ecpm)
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

        public static RewardBundleModel Create(AndroidJavaObject extraInfo)
        {
            if (extraInfo == null)
            {
                return null;
            }
            try
            {
                var serverErrorCode = extraInfo.Call<int>("getInt", REWARD_EXTRA_KEY_ERROR_CODE);
                Debug.Log("CSJM_Unity "+"serverErrorCode : " + serverErrorCode);
                var serverErrorMsg = extraInfo.Call<string>("getString", REWARD_EXTRA_KEY_ERROR_MSG);
                Debug.Log("CSJM_Unity "+"serverErrorMsg : " + serverErrorMsg);
                var rewardName = extraInfo.Call<string>("getString", REWARD_EXTRA_KEY_REWARD_NAME);
                Debug.Log("CSJM_Unity "+"rewardName : " + rewardName);
                var rewardAmount = extraInfo.Call<float>("getFloat", REWARD_EXTRA_KEY_REWARD_AMOUNT);
                Debug.Log("CSJM_Unity "+"rewardAmount : " + rewardAmount);
                var rewardPropose = extraInfo.Call<float>("getFloat", REWARD_EXTRA_KEY_REWARD_PROPOSE);
                Debug.Log("CSJM_Unity "+"rewardPropose : " + rewardPropose);

                var isGromoreVerify = extraInfo.Call<bool>("getBoolean", GM_IS_SERVER_SIDE_VERIFY);
                Debug.Log("CSJM_Unity "+"isGromoreVerify : " + isGromoreVerify);
                var gromoreExtra = extraInfo.Call<string>("getString", GM_EXTRA);
                Debug.Log("CSJM_Unity "+"gromoreExtra : " + gromoreExtra);
                var transId = extraInfo.Call<string>("getString", GM_TRANS_ID);
                Debug.Log("CSJM_Unity "+"transId : " + transId);
                var reason = extraInfo.Call<int>("getInt", GM_REASON);
                Debug.Log("CSJM_Unity "+"reason : " + reason);
                var errorCode = extraInfo.Call<int>("getInt", GM_ERROR_CODE);
                Debug.Log("CSJM_Unity "+"errorCode : " + errorCode);
                var errorMsg = extraInfo.Call<string>("getString", GM_ERROR_MSG);
                Debug.Log("CSJM_Unity "+"errorMsg : " + errorMsg);
                var adnName = extraInfo.Call<string>("getString", GM_ADN_NAME);
                Debug.Log("CSJM_Unity "+"adnName : " + adnName);
                var ecpm = extraInfo.Call<string>("getString", GM_ECPM);
                Debug.Log("CSJM_Unity "+"ecpm : " + ecpm);

                return new RewardBundleModel(serverErrorCode, serverErrorMsg, rewardName, rewardAmount, rewardPropose,
                    isGromoreVerify, gromoreExtra, transId, reason, errorCode, errorMsg, adnName, ecpm);
            }
            catch (Exception e)
            {
                Debug.Log("CSJM_Unity RewardBundleModel.Create error: " + e.ToString());
                return null;
            }
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