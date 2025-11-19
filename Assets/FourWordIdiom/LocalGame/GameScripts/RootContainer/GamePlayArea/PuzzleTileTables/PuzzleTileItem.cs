using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 词字块类，负责管理字符块的显示与交互
/// </summary>
public class PuzzleTileItem : MonoBehaviour
{
    [Header("Puzzle Tile")] 
    [SerializeField] private GameObject PuzzleRightObj;
    [SerializeField] private GameObject lineObj;
    [SerializeField] private GameObject background; // 空白字块对象
    [SerializeField] private GameObject tipsTextObj; // 硬币对象
    [SerializeField] private GameObject PuzzleTextObj; // 硬币对象
     public List<Text> TextPuzzles; // 显示文本        
     public List<Text> TextTipsPuzzles; // 显示文本
     public List<GameObject> coinObjects; // 显示文本  
  
    private bool isHintShown;
    [HideInInspector] public string currentPuzzle;

    private Button wordbutton;

    private void Start()
    {
        wordbutton = PuzzleRightObj.GetComponent<Button>();      
        wordbutton.AddClickAction(DisplayWordDetailPanel);
    }

    private void DisplayWordDetailPanel()
    {
        if (!string.IsNullOrEmpty(currentPuzzle))
        {
            StageController.Instance.IsEnterVocabulary = true;
            UpdateLevelData();
            SystemManager.Instance.ShowPanel(PanelType.LevelWordDetail);
            AudioManager.Instance.PlaySoundEffect("ShowUI");
        }
    }

    private void UpdateLevelData()
    {
        StageController.Instance.PuzzleData.CurPuzzle = currentPuzzle;
        if (!GameDataManager.instance.UserData.GetWordVocabulary().LevelWords.Contains(currentPuzzle))
        {
            GameDataManager.instance.UserData.AddStagePuzzle(currentPuzzle);
        }
        int wordIndex = GameDataManager.instance.UserData.GetWordVocabulary().LevelWords.IndexOf(currentPuzzle);
        StageController.Instance.PuzzleData.IsVocabularyPuzzle = true;
        StageController.Instance.IsEnterVocabulary = true;
        StageController.Instance.IsEnterPuzzle = true;
        StageController.Instance.PuzzleData.PageIndex = wordIndex + 1;
    }


    #region Public Methods

    public void UpdateCharacter(char letter, int id)
    {           
        TextPuzzles[id].text = letter.ToString();
        isHintShown = false;
    }

    public void SetPuzzleData(string Puzzle)
    {
        currentPuzzle = Puzzle; // 设置当前字
        wordbutton= PuzzleRightObj.GetComponent<Button>();      
        tipsTextObj.gameObject.SetActive(false);
        PuzzleTextObj.gameObject.SetActive(false);
        background.SetActive(true); // 显示空白字块
        wordbutton.enabled = false;
        PuzzleRightObj.GetComponent<Image>().DOFade(0, 0);
      
        for (int i = 0; i < Puzzle.Length; i++)
        {
            var letter = Puzzle[i];
            UpdateCharacter(letter, i);
            TextTipsPuzzles[i].text = letter.ToString();
            TextTipsPuzzles[i].gameObject.SetActive(false);
            if(i<Puzzle.Length-1)
                coinObjects[i].gameObject.SetActive(false);
        }        
    }

    public void HideBlock()
    {           
        background.SetActive(true); // 显示空白字块
    }

    public void ShowBlock()
    {           
        background.SetActive(false); // 隐藏空白字块
        PuzzleTextObj.gameObject.SetActive(true);
        wordbutton.enabled = true;
        PuzzleRightObj.GetComponent<Image>().DOFade(1, 0);
        foreach (Text text in TextPuzzles)
        {
            text.gameObject.SetActive(true);
        }
        
        foreach (GameObject coin in coinObjects)
        {
            coin.gameObject.SetActive(false);
        }     
    }

    public void DisplayHint(bool showWithCoin = true,bool showeffect=false)
    {           
        if (!showWithCoin)
        {
            tipsTextObj.gameObject.SetActive(false);
            //blockObject.SetActive(true);
            //Effect_Puzzle.gameObject.SetActive(showeffect);
            //blockObject.GetComponent<CanvasGroup>().DOFade(1,0.3f).OnComplete(() =>
            //{
            //    Effect_Puzzle.gameObject.SetActive(false);
            //}); // 显示字块
        }
        isHintShown = true;
    }

    public void RevealHint(int index, bool useAnimation = false)
    {
        // 标记提示已显示
        isHintShown = true;

        // 确保索引有效
        if (index < 0 || index >= TextTipsPuzzles.Count)
        {
            Debug.LogWarning($"Invalid hint index: {index}");
            tipsTextObj.SetActive(true);
            return;
        }

        if (useAnimation)
        {
            StartCoroutine(PlayRevealAnimation(index));
        }
        else
        {
            for (int i = 0; i <= index; i++) 
            {
                if (i > 0)
                {
                    coinObjects[i - 1].gameObject.SetActive(false);
                }
                // 即时显示提示
                //ShowHintImmediately(i);
            }
          
        }
    }


    private void ShowHintImmediately(int index)
    {
        tipsTextObj.SetActive(true);
        TextTipsPuzzles[index].gameObject.SetActive(true);

        // 添加简单效果增强视觉反馈
        var text = TextTipsPuzzles[index];
        text.color = new Color(text.color.r, text.color.g, text.color.b, 0);
        text.DOFade(1f, 0.15f);
    }


    private IEnumerator PlayRevealAnimation(int index)
    {
        // 1. 使用差异化特效资源名称
        const string effectBundle = "useritems";
        const string effectAsset = "ToolTipsEffect"; // 修改资源名称

        // 2. 异步加载优化
        var loadOperation = AdvancedBundleLoader.SharedInstance.LoadGameObject(effectBundle, effectAsset);      

        // 3. 特效实例化与定位
        var effectInstance = Instantiate(loadOperation, transform.parent);
        effectInstance.transform.position = TextTipsPuzzles[index].transform.position;

        // 4. 添加随机旋转增加差异化
        effectInstance.transform.Rotate(0, 0, UnityEngine.Random.Range(-5, 5));

        // 5. 文字显示动画序列
        var sequence = DOTween.Sequence();

        // 第一阶段：特效展示期间
        sequence.AppendInterval(0.2f); // 比原版稍短的等待

        // 6. 文字显示带动画效果
        sequence.AppendCallback(() => {
            tipsTextObj.SetActive(true);
            TextTipsPuzzles[index].gameObject.SetActive(true);

            // 添加缩放动画
            var textTransform = TextTipsPuzzles[index].transform;
            textTransform.localScale = Vector3.zero;
            textTransform.DOScale(Vector3.one, 0.3f)
                .SetEase(Ease.OutBack);
        });

        // 7. 特效自动销毁计时器
        sequence.AppendInterval(3.5f); // 比原版更早销毁特效
        sequence.AppendCallback(() => {
            if (effectInstance != null)
            {
                effectInstance.transform.DOScale(Vector3.zero, 0.2f)
                    .OnComplete(() => Destroy(effectInstance));
            }
        });

        yield return sequence.WaitForCompletion();

        // 8. 确保特效被清理
        if (effectInstance != null && effectInstance.activeInHierarchy)
        {
            Destroy(effectInstance);
        }
    }
   

    public void ShowText(Action callback)
    {
        lineObj.gameObject.SetActive(false);
        tipsTextObj.gameObject.SetActive(false);
        wordbutton.enabled = true;
        PuzzleRightObj.GetComponent<Image>().DOFade(1, 0.2f).OnComplete(() =>
        {
            PuzzleTextObj.gameObject.SetActive(true);
            callback?.Invoke();
        });
        PuzzleRightObj.transform.DOScale(0.98f, 0.2f).OnComplete(() =>
        {
            PuzzleRightObj.transform.DOScale(1.15f, 0.2f).OnComplete(() => 
            {
                PuzzleRightObj.transform.DOScale(1f, 0.2f);
            });
        });       
        //AnimateRelatedTiles();
    }
    
    /// <summary>
    /// 显示蝴蝶道具提示的成语
    /// </summary>
    public IEnumerator ButterflyTips(int id=0,float delay=0.16f)
    {
        tipsTextObj.gameObject.SetActive(true);
    
        for (int i =0 ; i <currentPuzzle.Length ; i++)
        {
            if (id == 0)
            {
                int index = currentPuzzle.Length-1 - i;
                if(index<=2)
                    coinObjects[index].gameObject.SetActive(true);
            }
            else 
            {
                if (i <= id - 1)
                {
                    coinObjects[i].gameObject.SetActive(false);
                }
                else
                {
                    coinObjects[i].gameObject.SetActive(true);
                }
            }
         
            yield return new WaitForSeconds(delay);
        }
      
        if (id == 0)
        {
            TextTipsPuzzles[id].gameObject.SetActive(true);
        }
      
        for (int i =0 ; i <id ; i--)
        {
            TextTipsPuzzles[i].gameObject.SetActive(true);
            yield return new WaitForSeconds(delay);
        }
    }
    
    public void ShowCoinFly(int index, bool isfly)
    {
        //coinObject.transform.DOLocalMoveY(87, 0.2f);
        //coinObjects[index].transform.SetAsLastSibling();
        if (isfly)
        {
            Transform coin = coinObjects[index-1].transform;
            coin.gameObject.SetActive(false);
            CustomFlyInManager.Instance.FlyInGold(coin,() =>
            {
                //coinObject.transform.DOLocalMoveY(0,0);
            },1);
        }
    }

    public void OnDisable()
    {
        //Effect_Puzzle.gameObject.SetActive(false);
    }

    #endregion
}
