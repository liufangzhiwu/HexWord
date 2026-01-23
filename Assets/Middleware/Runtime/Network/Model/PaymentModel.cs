using System;

namespace Middleware.Network.Model
{
    [Serializable]
    public class WechatPayParams
    {
        public string timeStamp;
        public string nonceStr;
        public string package;
        public string signType;
        public string paySign;
    }

    // 对应后端返回的 data
    [Serializable]
    public class CreateOrderResponse
    {
        public string order_id; // 你的 orders 表主键
        public WechatPayParams pay_params; // 传给 wx.requestPayment 的参数
    }
}