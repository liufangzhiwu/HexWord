using System;
using System.Collections.Generic;

namespace ByteDance.Union
{
    // 广告事件监听， 目前只有授权事件定义，后续会扩展
    public interface ITTAdInteractionListener
    {
        public void OnAdEvent(int code, Dictionary<string, Object> map);
    }
}
