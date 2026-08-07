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
    public interface IExpressBannerInteractionListener
    {
        /// <summary>
        /// Invoke when the AdView is renderin succ.
        /// </summary>
        void OnAdViewRenderSucc(float width, float height);

        /// <summary>
        /// Invoke when the AdView is renderin fail .
        /// <param name="code">error code.</param>
        /// <param name="message">rerror message.</param>
        /// </summary>
        void OnAdViewRenderError(int code, string message);

        /// <summary>
        /// Invoke when the Ad is shown.
        /// </summary>
        void OnAdShow();

        /// <summary>
        /// Invoke when the Ad is clicked.
        /// </summary>
        void OnAdClicked();

        /// <summary>
        /// Invoke when the Ad is closed.
        /// </summary>
        void OnAdClose();

        /// <summary>
        /// Invoke when the Ad become Hiddened.
        /// </summary>
        void onAdRemoved();

    }
}
