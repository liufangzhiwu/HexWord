using System;
using System.Collections;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Events;
using WebGLSupport;

namespace Unity.Passport.Runtime.UI
{
    public class CodeInput : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField inputField;
        public UnityEvent onComplete;
        [SerializeField]
        private Transform codeBlocks;

        private readonly List<CodeInputComponent> _codeBlockList = new();
        private int _childCount;
        private int _currentIndex; // 当前输入项的 index

        private bool _invokeManually = false;
        
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool passportWebPlatformIsMobile();
#endif
        
        void Awake()
        {
            // 初始化，设置事件监听
            _childCount = codeBlocks.childCount;
            
            for (int i = 0; i < _childCount; i += 1)
            {
                var child = codeBlocks.GetChild(i).gameObject.GetComponent<CodeInputComponent>();
                _codeBlockList.Add(child);
            }
            
            // 手动触发：点击输入法的"完成"按钮，才触发验证码校验
            // 仅在移动端 webgl 开启
            #if UNITY_WEBGL && !UNITY_EDITOR
            _invokeManually = passportWebPlatformIsMobile();
            #endif
            
            
            inputField.onValueChanged.AddListener((e) =>
            {
                HandleValueChanged(e);
                Debug.Log($"inputField.onValueChanged inputField.onValueChanged {e}");
            });
            inputField.onSelect.AddListener(HandleSelect);
            inputField.onDeselect.AddListener(HandleDeselect);
            if (_invokeManually)
            {
                inputField.onEndEdit.AddListener(HandleEditEnd);
            }
            // 根据展示组件的个数，设置 input 框字数限制
            inputField.characterLimit = _childCount;
        }

        private void OnEnable()
        {
            ClearAndActivate();
        }
        
        public void ClearAndActivate()
        {
            // 清空输入值
            inputField.text = "";
            
            // 自动对焦
            if (!TouchScreenKeyboard.isSupported)
            {
                inputField.ActivateInputField();
            }
        }
        
        void HandleValueChanged(string text)
        {
            Fill(text);
        }

        void HandleSelect(string text)
        {
            if (_currentIndex < _childCount)
            {
                _codeBlockList[_currentIndex].ActivateInputField();
            }
        }

         void HandleDeselect(string text)
        {
            for (int i = 0; i < _childCount; i += 1)
            {
                _codeBlockList[i].DeactivateInputField();
            }
        }

        /// <summary>
        /// 输入完成时，若长度已经到达要求，触发 onComplete 事件
        /// </summary>
        void HandleEditEnd(string text)
        {
            if (text.Length >= _childCount)
            {
                onComplete.Invoke();
            }
        }

        /// <summary>
        /// 填入
        /// </summary>
        /// <param name="str"></param>
        private void Fill(string str)
        {
            Debug.Log($"Fill {str}");
            for (int i = 0; i < _childCount; i += 1)
            {
                _codeBlockList[i].text = i < str.Length ? str.Substring(i, 1): "";
                _codeBlockList[i].DeactivateInputField();
            }
            // 记录填充长度
            _currentIndex = Math.Min(_childCount, str.Length); 
            
            // 设置闪动的光标
            if (str.Length < _childCount)
            {
                _codeBlockList[str.Length].ActivateInputField();
            }
            // 已经将所有子项填满
            if (str.Length >= _childCount)
            {
                if (!_invokeManually)
                {
                    onComplete.Invoke();
                }
            }
        }
        
        public string GetValue()
        {
            return inputField.text;
        }
    }
}