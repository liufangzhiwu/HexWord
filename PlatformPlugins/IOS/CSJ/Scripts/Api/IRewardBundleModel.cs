namespace ByteDance.Union
{
    public interface IRewardBundleModel
    {
        ///获得服务器验证的错误码
        int ServerErrorCode { get; }

        ///获得服务器验证的错误信息
        string ServerErrorMsg { get; }

        /// 获得开发者平台配置的奖励名称
        string RewardName { get; }

        /// 获得开发者平台配置的奖励数量
        float RewardAmount { get; }

        /// 获得此次奖励建议发放的奖励比例
        float RewardPropose { get; }

        // 是否是GroMore服务端奖励验证
        bool GMIsServerSideVerify { get; }
        // gromore服务端奖励验证透传信息
        string GMExtra { get; }
        // gromore服务端验证adnName
        string GMAdnName { get; }
        // gromore服务端验证transId
        string GMTransId { get; }
        // gromore服务端验证reason
        int GMReason { get; }
        // gromore服务端验证errorCode
        int GMErrorCode { get; }
        // gromore服务端验证errorMsg
        string GMErrorMsg { get; }
        // 本次广告展示的ecpm
        string GMEcpm { get; }
    }
}