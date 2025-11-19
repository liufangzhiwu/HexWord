using UnityEngine;

namespace Unity.Passport.Runtime.UI
{
    public enum MessageType
    {
        Info,
        Error
    }
    public class UIMessage : MonoBehaviour
    {
        [SerializeField] private GameObject messageContentPrefab;

        private static UIMessage _singleton; // 单例
        private const float DefaultDuration = 3; // 默认展示时长

        [RuntimeInitializeOnLoadMethod]
        public static void Init()
        {
            if (_singleton != null) return;
    
            // 实例化
            var prefab = Resources.Load<GameObject>("Common/MessageUI");

            if (prefab == null) return;
            var instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);
            _singleton = instance.GetComponent<UIMessage>();
        }

        public static void Show(string message, MessageType type = MessageType.Info, float duration = DefaultDuration)
        {
            _singleton.ShowMessage(message, type, duration);
        }
        
        public static void Show(PassportException e, MessageType type = MessageType.Info, float duration = DefaultDuration)
        {
            _singleton.ShowMessage(e.ErrorMessage, type, duration);
        }
        

        private void ShowMessage(string message, MessageType type, float duration)
        {
            var messageInstance = Instantiate(messageContentPrefab, transform);
            var messageContent = messageInstance.GetComponent<MessageContent>();

            messageContent.Show(message, type, duration);
        }
    }
}
