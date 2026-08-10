//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

namespace ByteDance.Union
{
    /// <summary>
    /// The listener for splash Ad.
    /// </summary>
    public interface ISplashAdInteractionListener
    {
        /// <summary>
        /// Invoke when the Ad is clicked.
        /// </summary>
        void OnAdClicked(int type);

        /// <summary>
        /// Invoke when the Ad is shown.
        /// </summary>
        void OnAdDidShow(int type);

        void OnAdWillShow(int type);

        /// <summary>
        /// Invoke when the Ad is skipped.
        /// </summary>
        //void OnAdSkip();

        /// <summary>
        /// Invoke when the Ad time over.
        /// </summary>
        //void OnAdTimeOver();

        /// <summary>
        /// Invoke when the Ad close.
        /// The type parameter is the reason for opening and closing the screen
        /// int CLICK_SKIP = 1; //点击跳过
        /// int COUNT_DOWN_OVER = 2; //倒计时结束
        /// int CLICK_JUMP = 3; //点击跳转
        /// int VIDEO_PLAYER_COMPLETE = 4; //视频播放完成 Android独有
        /// </summary>
        void OnAdClose(int type);


    }
}
