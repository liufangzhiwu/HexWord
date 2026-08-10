//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)

namespace ByteDance.Union
{
    /// <summary>
    /// The draw feed Ad.
    /// </summary>
    public sealed class DrawFeedAd : FeedAd
    {
        /// <summary>
        /// Support whether this draw feed can interrupt video during play.
        /// </summary>
        public void SetCanInterruptVideoPlay(bool support) { }
    }

}

#endif
