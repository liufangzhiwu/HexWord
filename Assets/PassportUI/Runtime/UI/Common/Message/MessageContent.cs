using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Passport.Runtime.UI
{
    public class MessageContent : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;

        private float _deltaTime;
        private float _duration = 100;

        void Update()
        {
            _deltaTime += Time.deltaTime;
            if (_deltaTime > _duration)
            {
                Destroy(gameObject);
            }
        }

        public void Show(string message, MessageType type, float duration)
        {
            if (type == MessageType.Error)
            {
                // 修改颜色
                GetComponent<Image>().color = new Color(0.5f, 0.055f, 0.055f, 0.85f);
            }
            text.text = message;
            _deltaTime = 0;
            _duration = duration;
        }
    }
}
