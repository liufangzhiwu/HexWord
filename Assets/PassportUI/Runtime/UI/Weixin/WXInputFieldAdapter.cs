using TMPro;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_WEIXINMINIGAME && !UNITY_EDITOR
using WeChatWASM;
#endif
using UnityEngine.EventSystems;

namespace Unity.Passport.Runtime.UI
{
    [RequireComponent(typeof(TMP_InputField))]
    public class WXInputFieldAdapter : MonoBehaviour, IPointerClickHandler, IPointerExitHandler
    {
        private TMP_InputField _inputField;
        private bool _isShowKeyboard = false;
        public UnityEvent onWxInputComplete;
        public int maxLength = 30;

        private void Start()
        {
            _inputField = GetComponent<TMP_InputField>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ShowKeyboard();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_inputField.isFocused)
            {
                HideKeyboard();
            }
        }
        
#if UNITY_WEIXINMINIGAME && !UNITY_EDITOR
        private void OnInput(OnKeyboardInputListenerResult v)
        {
            if (_inputField.isFocused)
            {
                _inputField.text = v.value;
            }
        }
#endif
        
#if UNITY_WEIXINMINIGAME && !UNITY_EDITOR
        private void OnConfirm(OnKeyboardInputListenerResult v)
        {
            // 输入法confirm回调
            HideKeyboard();
            onWxInputComplete.Invoke();
        }
#endif

#if UNITY_WEIXINMINIGAME && !UNITY_EDITOR
        private void OnComplete(OnKeyboardInputListenerResult v)
        {
            // 输入法complete回调
            HideKeyboard();
        }
#endif
        private void ShowKeyboard()
        {
            
            if (_isShowKeyboard) return;
#if UNITY_WEIXINMINIGAME && !UNITY_EDITOR
     
            WX.ShowKeyboard(new ShowKeyboardOption()
            {
                defaultValue = _inputField.text,
                maxLength = maxLength,
                confirmType = "go"
            });

            //绑定回调
            WX.OnKeyboardConfirm(this.OnConfirm);
            WX.OnKeyboardComplete(this.OnComplete);
            WX.OnKeyboardInput(this.OnInput);
#endif
            _isShowKeyboard = true;
        }

        private void HideKeyboard()
        {
            if (!_isShowKeyboard) return;
#if UNITY_WEIXINMINIGAME && !UNITY_EDITOR

            WX.HideKeyboard(new HideKeyboardOption());
            //删除掉相关事件监听
            WX.OffKeyboardInput(this.OnInput);
            WX.OffKeyboardConfirm(this.OnConfirm);
            WX.OffKeyboardComplete(this.OnComplete);
#endif
            _isShowKeyboard = false;
        }
    }
}