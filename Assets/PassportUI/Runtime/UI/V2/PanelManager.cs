using EasyUIAnimator;
using UnityEngine;
using TMPro;

namespace Unity.Passport.Runtime.UI
{
    public class PanelManager : MonoBehaviour
    {
        // 播放动画
        public RectTransform authenticationInput;
        public RectTransform authenticationButton;

        private readonly Vector2 _inputStartPosition = new Vector2(784,-96f);
        private readonly Vector2 _buttonStartPosition = new Vector2(1702,-200f);
        
        private bool _shown;

        public TMP_InputField nameInput;
        public TMP_InputField idInput;
        
        private const int IDRequiredLength = 18;

        private bool idCompleted = false;
        private bool nameCompleted = false;

        private void HandleNameChange(string text)
        {
            if (text != "")
            {
                OnNameComplete();
            }
        }

        private void HandleIDChange(string text)
        {
            // 检查位数大于身份证位数即可
            if (text.Length >= IDRequiredLength)
            {
                OnIdComplete();
            }
        }

        private void HandleNameEditEnd(string text)
        {
            if (text != "")
            {
                OnNameComplete();
            }
        }

        private void HandleIdEditEnd(string text)
        {
            if (text != "")
            {
                OnIdComplete();
            }
        }

        private void OnNameComplete()
        {
            nameCompleted = true;
            if (idCompleted)
            {
                ShowRealNameButton();
            }
        }

        private void OnIdComplete()
        {
            idCompleted = true;
            if (nameCompleted)
            {
                ShowRealNameButton();
            }
        }

        public void Open()
        {
            // 添加事件监听
            nameInput.onValueChanged.AddListener(HandleNameChange);
            idInput.onValueChanged.AddListener(HandleIDChange);
            nameInput.onEndEdit.AddListener(HandleNameEditEnd);
            idInput.onEndEdit.AddListener(HandleIdEditEnd);

            // 位置回归
            authenticationInput.anchoredPosition = _inputStartPosition;
            authenticationButton.anchoredPosition = _buttonStartPosition;
            
            gameObject.SetActive(true);
            // 自动对焦
            if (!TouchScreenKeyboard.isSupported)
            {
                nameInput.ActivateInputField();
            }
        }

        public void Close()
        {
            gameObject.SetActive(false);
            
            // 重置统计数据
            _shown = false;
            nameCompleted = false;
            idCompleted = false;
            
            // 清空 input 框
            nameInput.text = "";
            idInput.text = "";
        }
        
        private void ShowRealNameButton()
        {
            if (_shown) return;
            
            // 移除监听
            nameInput.onValueChanged.RemoveListener(HandleNameChange);
            idInput.onValueChanged.RemoveListener(HandleIDChange);
            nameInput.onEndEdit.RemoveListener(HandleNameEditEnd);
            idInput.onEndEdit.RemoveListener(HandleIdEditEnd);
            
            _shown = true;
            // 播放动画
            UIAnimator.Move(authenticationInput, _inputStartPosition, new Vector2(664,-96f), 0.3f).SetModifier(Modifier.CubIn).Play(); 
            UIAnimator.Move(authenticationButton, _buttonStartPosition, new Vector2(1308,-200f), 0.3f).SetModifier(Modifier.CubIn).Play();
        }
    }
}