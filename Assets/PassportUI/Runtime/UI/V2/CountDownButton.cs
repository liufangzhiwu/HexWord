using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System;

namespace Unity.Passport.Runtime.UI
{
    public class CountDownButton : MonoBehaviour
    {
        public GameObject loadingIcon;
        public Button button;
        public TextMeshProUGUI text;

        [Tooltip("成功发送验证码后按钮禁用时长")]
        public float disabledTime = 59f;
        
        private float _remainTime;
        private bool _countDownOn;
        private bool _loading;
        
        public void Reset()
        {
            loadingIcon = transform.Find("Text/Loading").gameObject;
            button = gameObject.GetComponent<Button>();
            text = button.GetComponentInChildren<TextMeshProUGUI>();
        }

        // Start is called before the first frame update
        void Start()
        {
            // 初始化变量
            _remainTime = disabledTime;
        }

        private void FixedUpdate()
        {
            if (_loading)
            {
                loadingIcon.transform.Rotate(0.0f, 0.0f, 5.0f, Space.World);
            }
            if (_countDownOn)
            {
                CountDown();
            }
        }

        public void StartCountDown()
        {
            _countDownOn = true;
            button.interactable = false;
        }

        public void Loading()
        {
            button.interactable = false;
            loadingIcon.SetActive(true);
            _loading = true;
        }

        public void StopLoading()
        {
            button.interactable = true;
            loadingIcon.SetActive(false);
            _loading = false;
        }
        
        public void ResetCountDown()
        {
            _remainTime = disabledTime;
            _countDownOn = false; // 结束计时
            button.interactable = true;
            text.text = "发送验证码";
        }

        private void CountDown()
        {
            _remainTime -= Time.deltaTime;
            if (_remainTime <= 0)
            {
                ResetCountDown();
            }
            else
            {
                text.text =
                    $"<color=#9E9E9E>重新发送验证码（ {Math.Ceiling(_remainTime)} 秒） </color>";
            }
        }
    }
}
