using UnityEngine;
using UnityEngine.UI;

public class OverallTabManager : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Toggle toggle;
        public Image bg;          // 背景 Image（Target Graphic 那张）
        public Text label;        // TMP 就换成 TextMeshProUGUI
    }

    public Tab[] tabs;
    public Sprite selectedSprite;     // cate_active
    public Sprite unselectedSprite;   // cate_normal
    public Color normalColor   = Color.white; 
    public Color selectedColor =  new Color(0.149f, 0.298f, 0.486f, 1f);
    public int normalSize = 60;
    public int selectedSize = 70;
    // 🌟 必须改成 Awake！赶在 OverallRankingScreen 的 OnEnable 之前注册好监听
    private void Awake()
    {
        // 国际化赋值
        if (tabs.Length >= 3)
        {
            tabs[0].label.text = MultilingualManager.Instance.GetString("world", "hudie");
            tabs[1].label.text = MultilingualManager.Instance.GetString("Monthly", "hudie");
            tabs[2].label.text = MultilingualManager.Instance.GetString("Worthy", "hudie");
        }

        // 1. 注册视觉刷新的回调
        for (int i = 0; i < tabs.Length; i++)
        {
            Tab t = tabs[i];
            
            // 只要 Toggle 状态发生变化，就会自动触发外观改变
            t.toggle.onValueChanged.AddListener(on => Refresh(t, on));
            
            // 2. 初始化时，直接强刷一次当前外观，防止预制体默认状态不对
            Refresh(t, t.toggle.isOn);
        }
        
    }

    void Refresh(Tab t, bool on)
    {
        if (t.bg    != null) t.bg.sprite    = on ? selectedSprite : unselectedSprite;
        if (t.label != null)
        {
            t.label.color  = on ? selectedColor  : normalColor;
            t.label.fontSize  = on ? selectedSize  : normalSize;
        }
      
    }
}