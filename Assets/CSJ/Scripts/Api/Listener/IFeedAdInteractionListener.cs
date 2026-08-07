namespace ByteDance.Union
{
    public interface IFeedAdInteractionListener
    {
        /// <summary>
        /// Invoke when the Ad is clicked.
        /// </summary>
        void OnAdClicked();

        /// <summary>
        /// Invoke when the Ad creative view is clicked.
        /// </summary>
        void OnAdCreativeClick();

        /// <summary>
        /// Invoke when the Ad is shown.
        /// </summary>
        void OnAdShow();
    }
}