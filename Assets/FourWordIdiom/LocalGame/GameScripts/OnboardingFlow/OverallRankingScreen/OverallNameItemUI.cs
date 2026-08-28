using UnityEngine;
using UnityEngine.UI;

public class OverallNameItemUI : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button itemButton;

    // 大厂规范：颜色通常配置在统一样式表里，这里为演示直接用变量
    [Header("Colors")]
    [SerializeField] private Color completedBgColor = Color.white;
    [SerializeField] private Color currentBgColor = new Color(0.98f, 0.89f, 0.65f); // 类似图片中的黄色
    [SerializeField] private Color lockedBgColor = new Color(0.8f, 0.85f, 0.9f, 0.8f); // 类似图片中的浅蓝色
    
    private ZenPathData _data;

    public void Init(ZenPathData data)
    {
        _data = data;
        nameText.text = MultilingualManager.Instance.GetString(data.NameKey,"hudie"); // 多语言支持

        RefreshUIState();

        // 绑定点击事件
        itemButton.onClick.RemoveAllListeners();
        itemButton.onClick.AddListener(OnItemClicked);
    }

    private void RefreshUIState()
    {
        switch (_data.State)
        {
            case ZenPathState.Completed:
                backgroundImage.color = completedBgColor;
                nameText.color = new Color(0.3f, 0.3f, 0.3f); 
                break;
            case ZenPathState.Current:
                backgroundImage.color = currentBgColor;
                nameText.color = new Color(0.8f, 0.4f, 0.1f);
                break;
            case ZenPathState.Locked:
                backgroundImage.color = lockedBgColor;
                nameText.color = new Color(0.5f, 0.6f, 0.7f);
                break;
        }
    }

    private void OnItemClicked()
    {
        if (_data.State == ZenPathState.Locked)
        {
            Debug.Log("未解锁，不可点击");
            return;
        }
        // 处理正常的点击逻辑，例如触发事件派发
        Debug.Log($"Clicked on {_data.NameKey}");
    }
}