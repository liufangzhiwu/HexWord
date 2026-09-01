using System;
using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using Spine.Unity.AttachmentTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;


public class ZenLevelState
{
    public int Id;
    public string Code;
    public string Name;
    public string UpProportion;
    public string DownProportion;
    public int MinScore;
    public int MaxScore;
}
// ==========================================
// 🌟 1. 新增：可视化密度配置类 (让策划/美术在面板上自己配)
// ==========================================
[Serializable]
public class RankDensityConfig
{
    [Tooltip("该配置适用的最大段位索引 (例如填 2，表示 0,1,2 段位都用这个配置)")]
    public int maxTierIndex;
    
    [Tooltip("在这个阶段是否要显示主荷花？")]
    public bool showMainFlower;
    
    [Tooltip("在这个阶段，需要强制隐藏的 Spine 插槽名字列表")]
    public string[] hiddenSlots;
}
public class ZenRankLevelItem : MonoBehaviour
{
    [SerializeField] private Text LevelText;
    [SerializeField] private Text ScoreText;
    [Header("背景牌配置")]
    [SerializeField] private Image nameBoardBg;         // 🌟 挂载段位名字背后的那张底牌 Image
    [SerializeField] private Sprite unlockedBoardSprite;// 🌟 亮色背景牌图片
    [SerializeField] private Sprite lockedBoardSprite;  // 🌟 暗色背景牌图片
    [Header("荷花挂载点与状态配置")] 
    [SerializeField] private Transform hehuaTransform;  // 未解锁的剪影也挂载一起吧
    [SerializeField] private GameObject lockedLeafShadow;   // 🌟 美术给的荷叶阴影剪纸图
    
    [Header("🌟 场景进化模拟器配置 (让美术自己填)")]
    [Tooltip("请按段位索引从低到高填写配置")]
    public List<RankDensityConfig> densityConfigs = new List<RankDensityConfig>()
    {
        // 阶段1：入门 (0~2) - 隐藏虫子、高层叶子
        new RankDensityConfig { maxTierIndex = 2, showMainFlower = false, hiddenSlots = new string[] { "chong1", "chong2", "chong3", "chong4", "chong5", "chong6", "chong7", "chong8",  "5",  "7",  } },
        // 阶段2：修行 (3~5) - 长荷花，隐藏虫子、高层叶子
        new RankDensityConfig { maxTierIndex = 5, showMainFlower = true, hiddenSlots = new string[] { "chong1", "chong2", "chong3", "chong4", "chong5", "chong6", "chong7", "chong8", "7",  } },
        // 阶段3：精进 (6~8) - 保留两只蝴蝶，隐藏部分高层叶
        new RankDensityConfig { maxTierIndex = 8, showMainFlower = true, hiddenSlots = new string[] { "chong3", "chong4", "chong5", "chong6", "chong7", "chong8",  } },
        // 阶段4：圆满 (9+) - 全开
        new RankDensityConfig { maxTierIndex = 99, showMainFlower = true, hiddenSlots = new string[] { } }
    };
    
    // 记录当前是否持有着荷花实体
    private GameObject currentHehua;

    private RectTransform rectTransform;
    private float screenWidth;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (densityConfigs == null || densityConfigs.Count == 0)
        {
            densityConfigs = new List<RankDensityConfig>()
            {
                new RankDensityConfig { maxTierIndex = 2, showMainFlower = true, hiddenSlots = new string[] { "chong1", "chong2", "chong3", "chong4", "chong5", "chong6", "chong7", "chong8","5", "7",  } },
                new RankDensityConfig { maxTierIndex = 5, showMainFlower = true, hiddenSlots = new string[] { "chong1", "chong2", "chong3", "chong4", "chong5", "chong6", "chong7", "chong8", "7",  } },
                new RankDensityConfig { maxTierIndex = 8, showMainFlower = true, hiddenSlots = new string[] { "chong3", "chong4", "chong5", "chong6", "chong7", "chong8",   } },
                new RankDensityConfig { maxTierIndex = 99, showMainFlower = true, hiddenSlots = new string[] { } }
            };
            Debug.Log("已强制恢复被 Unity 吞掉的默认场景配置！");
        }
    }
 
    public void SetLevelInfo(ZenLevelState state)
    {
        // LevelText.text = state.Name.ToString();
        LevelText.text = MultilingualManager.Instance.GetString(state.Code) ?? state.Name.ToString();
        ScoreText.text = "禅意  " + state.MinScore.ToString() + " - " + state.MaxScore.ToString();
        MeasureScreenWidth();
        SetItemWidthToScreenWidth();
    }
    // 🌟 2. 新增：控制荷花的动态显示与回收
    public void UpdateHehuaVisibility(bool shouldShow, bool isUnlocked, int tierIndex, ObjectPool hehuaPool)
    {
        nameBoardBg.sprite = isUnlocked ? unlockedBoardSprite : lockedBoardSprite;

        if (!shouldShow)
        {
            lockedLeafShadow.SetActive(false);
            RecycleHehua();
            return;
        }
        
            if (isUnlocked)
            {
                lockedLeafShadow.SetActive(false);
                // 如果需要显示，且当前还没有荷花，就从池子里拿一个
                if (currentHehua == null)
                {
                    // 解锁了,展示spine动画, 并替换spine的图片与荷花预制体中的荷花
                    currentHehua = hehuaPool.GetObject();
                    currentHehua.transform.SetParent(hehuaTransform, false);
                    currentHehua.transform.localPosition = Vector3.zero;
                    currentHehua.transform.localScale = Vector3.one;
                }

                SkeletonGraphic skeletonGraphic = currentHehua.GetComponent<SkeletonGraphic>();
                // 修复 Spine 变紫红色的 Shader 问题
                if (skeletonGraphic != null)
                {
                    if (skeletonGraphic.material != null)
                    {
                        // 🌟 核心修复：强行让 Unity 在当前环境重新找一次这个 Shader 并赋上去！
                        skeletonGraphic.material.shader = Shader.Find("Spine/SkeletonGraphic");
                    }

                    // ==========================================
                    // 2. 计算图片索引
                    // ==========================================
                    // 荷花索引：1 到 12 (tierIndex 从 0 开始，所以 +1)
                    int flowerIndex = tierIndex + 1;

                    // 荷叶索引：4个图循环分摊给12个段位 (0%4=0->1, 1%4=1->2... 4%4=0->1)
                    int leafIndex = (tierIndex % 4) + 1;

                    Sprite newFlowerSprite =
                        AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("hehua_" + flowerIndex);
                    Image flowerImg = FindImageInChildren(currentHehua.transform, "hehua01");
                    flowerImg.sprite = newFlowerSprite;
                    // ==========================================
                    // 3. 动态替换 Spine 图片
                    // ==========================================
                    // 假设你把荷花的图片打包在了一个叫 "ZenAtlas" 的图集里

                    // Sprite newLeafSprite =
                    //     AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("lotus_leaf_" + leafIndex);
                    // if (newLeafSprite != null)
                    // 🌟 核心新增：调用段位密度控制器，动态隐藏/显示场景元素！
                    ApplyRankDensity(skeletonGraphic, tierIndex, flowerImg);
                }
            }
        else{
            RecycleHehua();
            lockedLeafShadow.SetActive(true);
        }
    }

    /// <summary>
    /// 安全回收荷花
    /// </summary>
    private void RecycleHehua()
    {
        if (currentHehua != null)
        {
            ObjectPool.ReturnObjectToPool(currentHehua);
            currentHehua = null;
        }
    }
    /// <summary>
    /// 递归查找子物体中符合名字前缀的 Image
    /// </summary>
    private Image FindImageInChildren(Transform parent, string namePrefix)
    {
        // 拿到所有的 Image 组件
        Image[] images = parent.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            // 只要名字包含预设的字符串（比如 "hehua"）就认为是目标
            if (img.gameObject.name.Contains(namePrefix))
            {
                return img;
            }
        }
        return null;
    }
    // ==========================================
    // 🌟 2. 重写：让代码完全读取面板配置，不再写死任何逻辑！
    // ==========================================
    private void ApplyRankDensity(SkeletonGraphic skeletonGraphic, int tierIndex, Image flowerImg)
    {
        var skeleton = skeletonGraphic.Skeleton;

        // 1. 每次先重置回默认状态（全显示），防止对象池复用时状态错乱
        skeleton.SetSlotsToSetupPose(); 

        // 2. 如果没有配配置，直接全展示，防报错
        if (densityConfigs == null || densityConfigs.Count == 0) return;

        // 3. 遍历我们在面板上填的配置
        foreach (var config in densityConfigs)
        {
            // 找到第一个符合当前段位区间的配置
            if (tierIndex <= config.maxTierIndex)
            {
                // A. 控制荷花的显隐
                if (flowerImg != null) flowerImg.enabled = config.showMainFlower;

                // B. 控制插槽的隐藏
                if (config.hiddenSlots != null && config.hiddenSlots.Length > 0)
                {
                    foreach (var slotName in config.hiddenSlots)
                    {
                        var slot = skeleton.FindSlot(slotName);
                        if (slot != null) slot.Attachment = null;
                    }
                }
                
                // 找到符合条件的就立刻结束，不往下执行了
                break; 
            }
        }
    }
    
    /// <summary>
    /// 运行时测量屏幕宽度（像素）
    /// </summary>
    private void MeasureScreenWidth()
    {
        // 方案A：往上找 Canvas 的真实宽度（最稳妥）
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            screenWidth = rootCanvas.GetComponent<RectTransform>().rect.width;
        }
        else
        {
            RectTransform parentRect = transform.parent.parent.GetComponent<RectTransform>();

            Canvas.ForceUpdateCanvases();
            screenWidth = parentRect.rect.width;
        }

        // 🌟 终极兜底防线：如果算出来还是 0 或太小，强行赋一个默认宽度（比如 1080）
        if (screenWidth <= 10f)
        {
            screenWidth = 1242f; // 替换成你 UI 设计图的参考宽度
            Debug.LogWarning("真机UI未初始化完成，使用兜底宽度: " + screenWidth);
        }
        // LayoutElement layoutElement = GetComponent<LayoutElement>();
        // layoutElement.preferredWidth = ;   // 2048 px
    }

    /// <summary>
    /// 把 Item 宽度设置为屏幕宽度
    /// </summary>
    private void SetItemWidthToScreenWidth()
    {
        // ① Item 宽度 = 屏幕宽度
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, screenWidth);
        // float scale = UIUtilities.GetScreenRatio();
        // rectTransform.localScale = new Vector3(scale, scale, 1);
    }
}