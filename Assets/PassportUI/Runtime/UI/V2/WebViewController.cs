using UnityEngine;
using UnityEngine.Events;

namespace Unity.Passport.Runtime.UI
{
    public class WebViewController : MonoBehaviour
    {
        private WebViewObject _webViewObject;
        public GameObject webViewHeader;
        public GameObject webViewBackground; 
        public UnityEvent onClose, onCancel;

        void Start()
        {
            _webViewObject = (new GameObject("WebViewObject")).AddComponent<WebViewObject>();
            _webViewObject.Init(
                cb: (msg) => { },
                err: (msg) => { },
                httpErr: (msg) => { },
                started: (msg) => { },
                hooked: (msg) => { },
                cookies: (msg) => { },
                ld: (msg) => { }
            );

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            _webViewObject.bitmapRefreshCycle = 1;
#endif
        }

        public void Close()
        {
            // webview逻辑已完成
            SetVisibility(false);
            onClose.Invoke();
        }

        public void Cancel()
        {
            // webview逻辑未完成
            SetVisibility(false);
            onCancel.Invoke();
        }

        private void SetVisibility(bool visibility)
        {
#if !UNITY_WEBGL
            // WEBGL没有_webViewObject
            _webViewObject.SetVisibility(visibility);
#endif
            webViewBackground.SetActive(visibility);
        }

        public int GetHeadedPixelHeight()
        {
            RectTransform rt = webViewHeader.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            int headerHeight = (int)(corners[1].y - corners[0].y);
            return headerHeight;
        }

        public void Open(string url)
        {
            // 设置样式
            int headerHeight = GetHeadedPixelHeight();

            _webViewObject.SetMargins(0, headerHeight, 0, 0);
            _webViewObject
                .SetTextZoom(
                    100); // android only. cf. https://stackoverflow.com/questions/21647641/android-webview-set-font-size-system-default/47017410#47017410

            SetVisibility(true);

#if !UNITY_WEBPLAYER && !UNITY_WEBGL
            if (url.StartsWith("http")) {
                _webViewObject.LoadURL(url.Replace(" ", "%20"));
            }
#endif
        }
    }
}