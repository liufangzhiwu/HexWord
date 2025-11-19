using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyUIAnimator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Passport.Runtime.UI
{
    public class InputPlaceholderMobile : MonoBehaviour {
        public string placeholderText;
        private TMP_InputField _input;
        private static GameObject _instance = null;
        private static InputPlaceholderMobilePanel _inputPlaceholderMobilePanel;
        private static RectTransform _frame;
        private static Image _mask;
        private static readonly Vector2 FrameStartPos = new(0, 108);
        private static readonly Vector2 FrameEndPos = new(0, -32);
        private const float Duration = 0.2f;
        private bool _isActivated = false;
        
#if UNITY_ANDROID || UNITY_IOS
        private void Awake()
        {
            // 没有设置 placeholderText 则无视
            if (placeholderText == "") return;
            
            // 创建实例
            if (_instance == null)
            {
                GameObject prefab = (GameObject)Resources.Load("InputPlaceholderMobilePanel");
                // 挂载到场景中
                _instance = Instantiate(prefab);
                _frame = _instance.transform.Find("Frame").gameObject.GetComponent<RectTransform>();
                _mask = _instance.transform.Find("Mask").gameObject.GetComponent<Image>();
                _instance.SetActive(false);
                _inputPlaceholderMobilePanel = _instance.GetComponent<InputPlaceholderMobilePanel>();
            }
            
            _input = gameObject.GetComponent<TMP_InputField>();
            // 没有 InputField 组件则无视
            if (_input == null) return;
            
            _input.onSelect.AddListener(HandleOnSelect);
            _input.onDeselect.AddListener(HandleOnDeselect);
        }
        #endif
        
        public async void Hide()
        {
            if (!_isActivated) return; // 已经收起
            UIAnimator.Move(_frame, FrameEndPos, FrameStartPos, Duration).Play();
            UIAnimator.FadeOut(_mask, Duration).Play();
            _isActivated = false;
            await Task.Delay((int)Duration * 1000);
            _instance.SetActive(false);
        }

        private void Show()
        {
            if (_isActivated) return; // 已经显示
            _instance.SetActive(true);
            UIAnimator.Move(_frame, FrameStartPos, FrameEndPos, Duration).Play();
            UIAnimator.FadeIn(_mask, Duration).Play();
            _isActivated = true;
        }

        private void HandleOnSelect(string text)
        {
            _inputPlaceholderMobilePanel.PlaceholderText = placeholderText;
            Show();
        }
        
        private void HandleOnDeselect(string text)
        {
            Hide();
        }
    }
}