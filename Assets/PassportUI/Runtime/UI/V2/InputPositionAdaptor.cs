using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Unity.Passport.Runtime.UI
{
    public class InputPositionAdaptor : MonoBehaviour
    {
        // 需要检查的 input 列表
        public List<TMP_InputField> detectList;
        private Vector3 _startPosition;
        private List<RectTransform> _inputRectTransformList = new();
        private int _activatedIndex = -1;
        private float _deltaY; // y 方向位移距离

        // Start is called before the first frame update
        // void Start()
        // {
        //     // 监听
        //     int count = detectList.Count;
        //     for (int i = 0; i < count; i += 1)
        //     {
        //         var input = detectList[i];
        //         var ii = i;
        //         input.onSelect.AddListener((string text) => { HandleSelect(ii); });
        //         input.onDeselect.AddListener((string text) => { HandleDeselect(); });
        //         _inputRectTransformList.Add(input.gameObject.GetComponent<RectTransform>());
        //     }
        // }

        private void HandleSelect(int index)
        {
            _activatedIndex = index;
        }

        private void HandleDeselect()
        {
            _activatedIndex = -1;
            // 恢复到初始位置
            if (_deltaY > 0)
            {
                var pos = transform.position;
                transform.position = new Vector3(pos.x, pos.y - _deltaY, pos.z);
                _deltaY = 0; // 确保只重置一次
            }
        }

        // private void Update()
        // {
        //     if (_activatedIndex > -1 && detectList[_activatedIndex].isFocused)
        //     {
        //         UpdatePosition();
        //     }
        // }

        private void UpdatePosition()
        {
            // 获取 input 下边缘 y 坐标
            var inputRectTransform = _inputRectTransformList[_activatedIndex];
            Vector3[] inputCorners = new Vector3[4];
            inputRectTransform.GetWorldCorners(inputCorners);
            var inputY = inputCorners[0].y;

            // 获取屏幕高度
            var screenHeight = Screen.height;

            // 获取键盘上边缘 y 坐标
            var area = TouchScreenKeyboard.area;
            var keyboardY = area.height;
            
            if (keyboardY > 0 && keyboardY > inputY)
            {
                // 需要移动的情况
                // 如果屏幕剩余空间不足，则少移动
                _deltaY = Math.Min(keyboardY - inputY, screenHeight - keyboardY);

                if (_deltaY > 0)
                {
                    // 只处理向上移动的情况
                    var thisTransform = transform;
                    var pos = thisTransform.position;
                    thisTransform.position = new Vector3(pos.x, pos.y + _deltaY, pos.z);
                }
                else
                {
                    _deltaY = 0;
                }

            }

            // 只处理一次
            _activatedIndex = -1;
        }
    }
}