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
    /// The feed Ad.
    /// </summary>
    public class FeedAd : NativeAd
    {
        /// <summary>
        /// Set the video Ad listener.
        /// </summary>
        public void SetVideoAdListener(IVideoAdListener listener) { }

        public void SetFeedAdInteractionListener(IFeedAdInteractionListener listener) { }

        public void SetFeedAdDislikeListener(IDislikeInteractionListener dislikeInteractionListener) { }

        public void ShowFeedAd(float x, float y) { }
    }
}


#endif
