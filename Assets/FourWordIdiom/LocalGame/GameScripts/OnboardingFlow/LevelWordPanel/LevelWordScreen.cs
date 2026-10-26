using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


public class LevelWordScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button VocabularyBtn;
    [SerializeField] private Text headTitle;
    [SerializeField] private WordButton wordLevelBtn; // 词语预设
    [SerializeField] private Transform wordsVocabularyParent; // 词语父对象
    private ObjectPool objectPool; // 对象池实例
    private Dictionary<string, WordButton> WordVocabularys = new Dictionary<string, WordButton>();
    //private Dictionary<string, NoteBooks> NoteBooks = new Dictionary<string, NoteBooks>();

    private const float IpadPopupScale = 1.15f; // iPad 下界面放大的倍数
    private const int BaseWordLength = 4;       // 底图按 4 字成语设计

    private RectTransform backUI;            // 界面主体（标题、角按钮、列表都在其下）
    private Vector3 backUIBaseScale;         // 界面主体原始缩放

    // 关内熟语列表：改为按行手动排版（GridLayoutGroup 只能统一行高，含长词时整列表都会被撑高）
    private GridLayoutGroup wordGrid;        // 原始网格（仅用于读取格子尺寸，运行时停用）
    private Vector2 wordGridBaseCellSize;    // 原始格子尺寸
    private Vector2 wordGridBaseSpacing;     // 原始格子间距
    private int wordGridColumnCount = 7;     // 每行列数

    protected override void Awake()
    {
        base.Awake();
        // 初始化对象池
        objectPool = new ObjectPool(wordLevelBtn.gameObject, ObjectPool.CreatePoolContainer(transform, "WordPool"));
        CacheLayout();
    }

    /// <summary>
    /// 缓存界面主体、网格参数与原始缩放，iPad 适配时按倍数计算，重复打开不会累乘
    /// </summary>
    private void CacheLayout()
    {
        backUI = transform.Find("BackUI") as RectTransform;
        if (backUI != null) backUIBaseScale = backUI.localScale;

        RectTransform content = wordsVocabularyParent as RectTransform;
        if (content == null) return;

        wordGrid = content.GetComponent<GridLayoutGroup>();
        if (wordGrid != null)
        {
            wordGridBaseCellSize = wordGrid.cellSize;
            wordGridBaseSpacing = wordGrid.spacing;
            wordGridColumnCount = Mathf.Max(1, wordGrid.constraintCount);
            // 停用统一行高的网格布局，改由 LayoutWordRows 按行计算
            wordGrid.enabled = false;
        }

        // 高度改由排版结果直接写入，关掉自适应高度组件，避免被重置
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        // 顶部拉伸锚点 + 顶部 pivot，滚动区域从顶部向下生长
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.GetComponent<Transform>().localScale = new Vector3(0.95f, 0.95f, 1f);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ApplyPopupScale();
        ShowLevelWord();
        headTitle.text = MultilingualManager.Instance.GetString("LevelWord");
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        VocabularyBtn.gameObject.SetActive(GameDataManager.Instance.UserData.isShowVocabulary);
    }

    /// <summary>
    /// iPad 下界面整体放大 1.15 倍；关闭/生词本按钮与标题大小保持不变（仅随界面等比移动）
    /// </summary>
    private void ApplyPopupScale()
    {
        float scale = UIUtilities.IsiPad() ? IpadPopupScale : 1f;

        if (backUI != null) backUI.localScale = backUIBaseScale * scale;
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
        VocabularyBtn.AddVibraClickAction(ShowWordVocabulary); // 绑定关闭按钮事件
    }

    private void ShowWordVocabulary()
    {
        StageHexController.Instance.IsEnterVocabulary = false;
        ChessStageController.Instance.IsEnterVocabulary = false;
        SystemManager.Instance.ShowPanel(PanelType.WordVocabularyScreen);
        OnHideAnimationEnd();
    }

    /// <summary>
    /// 显示关内熟语
    /// </summary>
    private void ShowLevelWord()
    {
        // 按字数从少到多排列：字数少的排前面，字数多的排最后（字数相同保持原有顺序）
        List<string> levelWords = WordVocabularyManager.SortByWordLength(GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords);
        // 本次列表里最多的字数，用于字号选择
        int maxWordLength = levelWords.Count > 0 ? levelWords.Max(word => string.IsNullOrEmpty(word) ? 0 : word.Length) : 0;

        int i = 1;
        List<WordButton> orderedCards = new List<WordButton>();

        foreach (var word in levelWords)
        {
            WordButton card;
            if (!WordVocabularys.TryGetValue(word, out card))
            {
                // 从对象池获取奖励文字对象
                card = objectPool.GetObject<WordButton>(wordsVocabularyParent);
                card.SetText(word, i, true, maxWordLength);
                WordVocabularys.Add(word, card);
            }
            else
            {
                card.wordData.PageIndex = i;
            }

            orderedCards.Add(card);
            i++;
        }

        LayoutWordRows(orderedCards);
    }

    /// <summary>
    /// 按行排版：每行最多 7 个，行高取该行字数最多的词条高度，词条顶边对齐。
    /// 这样只有含长词的那一行变高，4 字行保持原始高度（避免整列表被一个长词撑高）。
    /// </summary>
    private void LayoutWordRows(List<WordButton> cards)
    {
        RectTransform content = wordsVocabularyParent as RectTransform;
        if (content == null) return;

        // 兜底值：万一网格组件缺失也能正常排版（与预制体原始格子 120×420 / 间距 30×73 一致）
        float cellWidth = wordGridBaseCellSize.x > 0f ? wordGridBaseCellSize.x : 120f;
        float baseHeight = wordGridBaseCellSize.y > 0f ? wordGridBaseCellSize.y : 420f;
        float gapX = wordGridBaseSpacing.x > 0f ? wordGridBaseSpacing.x : 30f;
        float gapY = wordGridBaseSpacing.y > 0f ? wordGridBaseSpacing.y : 73f;
        int columns = Mathf.Max(1, wordGridColumnCount);

        float top = 0f;

        for (int start = 0; start < cards.Count; start += columns)
        {
            int count = Mathf.Min(columns, cards.Count - start);

            // 行高取该行最高的词条
            float rowHeight = 0f;
            for (int j = 0; j < count; j++)
            {
                float height = GetCardHeight(cards[start + j], baseHeight);
                if (height > rowHeight) rowHeight = height;
            }

            for (int j = 0; j < count; j++)
            {
                WordButton card = cards[start + j];
                RectTransform rect = card.transform as RectTransform;
                if (rect == null) continue;

                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.localScale = Vector3.one; // 清掉点击动画可能残留的缩放
                rect.sizeDelta = new Vector2(cellWidth, GetCardHeight(card, baseHeight));
                rect.anchoredPosition = new Vector2(j * (cellWidth + gapX), -top);
            }

            top += rowHeight + gapY;
        }

        if (cards.Count > 0) top -= gapY; // 去掉最后一行多余的间距

        content.sizeDelta = new Vector2(content.sizeDelta.x, top);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    /// <summary>
    /// 词条高度：4 字及以下保持原始格子高度，字数越多越长（每多一字增加四分之一原高）
    /// </summary>
    private static float GetCardHeight(WordButton card, float baseHeight)
    {
        int length = BaseWordLength;
        if (card != null && card.wordData != null && !string.IsNullOrEmpty(card.wordData.CurPuzzle))
            length = card.wordData.CurPuzzle.Length;

        if (length <= BaseWordLength) return baseHeight;
        return baseHeight * length / BaseWordLength;
    }

    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        foreach (var wordbtn in WordVocabularys.Values)
        {
            objectPool.ReturnObjectToPool(wordbtn.GetComponent<PoolObject>()); // 将对象返回到池中
        }
        WordVocabularys.Clear();
    }
}
