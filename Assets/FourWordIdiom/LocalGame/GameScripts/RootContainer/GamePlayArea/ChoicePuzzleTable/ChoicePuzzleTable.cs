using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 重构版选定词面板控制器 - 增强差异化功能
/// 新增特性：
/// 1. 多状态动画效果（成功/失败/警告）
/// 2. 可配置的颜色主题
/// 3. 弹性缩放动画
/// 4. 粒子反馈效果
/// 5. 单词拼写动画
/// </summary>
public class ChoicePuzzleTable : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private CanvasGroup container;
    [SerializeField] private Text selectedLettersText;
    //[SerializeField] private Image backgroundImage;
    //[SerializeField] private ParticleSystem successParticles;
    //[SerializeField] private ParticleSystem errorParticles;

    [Header("动画设置")]
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float initialShakeAmplitude = 8f;
    [SerializeField] private float amplitudeDecayFactor = 0.8f;
    [SerializeField] private float initialShakeTime = 0.09f;
    [SerializeField] private float durationDecayFactor = 0.7f;
    [SerializeField] private int shakeCount = 5;
    [SerializeField] private float charRevealDelay = 0.05f;

    private Vector3 originalScale;
    private Coroutine currentAnimation;
    private string currentPuzzle = "";
    private bool isWordValid = true;

    private void Awake()
    {
        originalScale = container.transform.localScale;
        container.transform.localScale = originalScale * 0.8f;
    }

    public void Initialize()
    {
        container.alpha = 0f;
        container.gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        EventDispatcher.instance.OnShowSelectedPuzzle += OnShowSelectedPuzzle;
        EventDispatcher.instance.OnPlayChoicePuzzle += HandleNonePuzzleSelected;
    }

    private void OnDisable()
    {
        EventDispatcher.instance.OnShowSelectedPuzzle -= OnShowSelectedPuzzle;
        EventDispatcher.instance.OnPlayChoicePuzzle -= HandleNonePuzzleSelected;
    }

    private void OnShowSelectedPuzzle(string puzzle)
    {       

        currentPuzzle = puzzle;
        container.alpha = 1f;        

        // 重置位置和缩放
        (container.transform as RectTransform).anchoredPosition = Vector2.zero;
        container.transform.localScale = originalScale * 0.9f;
        container.transform.DOScale(originalScale, 0.1f).SetEase(Ease.OutBack);

        // 播放字符逐个显示动画
        RevealWord(puzzle);
    }

    /// <summary>
    /// 逐个显示字符的动画
    /// </summary>
    private void RevealWord(string word)
    {
        selectedLettersText.text = "";
        isWordValid = true;

        foreach (char c in word)
        {
            selectedLettersText.text += c;
            // 轻微缩放效果
            selectedLettersText.transform.localScale = Vector3.one * 1.2f;
            selectedLettersText.transform.DOScale(1f, 0.1f);
            //yield return new WaitForSeconds(charRevealDelay);
        }
    }

    private void HandleNonePuzzleSelected(List<int[]> gridCellPositions, bool playAudio)
    {
        ShakeAndFadeOut();
        if (playAudio)
        {
            AudioManager.Instance.PlaySoundEffect("xuanzhecuowu");
        }
    }

    public bool CheckPuzzleFound(string puzzle)
    {
        if (StageController.Instance.CurStageData.FoundTargetPuzzles != null)
            return StageController.Instance.CurStageData.FoundTargetPuzzles.Contains(puzzle);
        return false;
    }

    /// <summary>
    /// 显示成功状态
    /// </summary>
    public void ShowSuccess()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        //backgroundImage.color = successColor;

        // 弹性动画
        //container.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0f), 0.5f, 3, 0.5f);

        // 播放粒子效果
        //if (successParticles != null)
        //{
        //    successParticles.Play();
        //}
        container.DOFade(0f, fadeDuration).SetEase(Ease.Linear);
        container.transform.DOScale(originalScale * 0.8f, fadeDuration * 0.8f);
        // 延迟淡出
        //DOVirtual.DelayedCall(1.2f, () => FadeOut(fadeDuration));
    }

    public void FadeOut(float time)
    {
        container.DOFade(0f, time).SetEase(Ease.Linear);
        container.transform.DOScale(originalScale * 0.8f, time * 0.8f);
    }

    public void ShakeAndFadeOut()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        currentAnimation = StartCoroutine(ShakeRoutine());
    }

    public void Reset()
    {
        container.alpha = 0f;
        container.transform.localScale = originalScale * 0.8f;
    }

    /// <summary>
    /// 显示警告状态（单词存在但未完全选择）
    /// </summary>
    public void ShowWarning()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        //backgroundImage.color = warningColor;
        container.transform.DOShakePosition(0.3f, new Vector3(5f, 0f, 0f), 10, 90, false, true);

        // 闪烁效果
        selectedLettersText.DOColor(new Color(1f, 0.9f, 0.3f, 1f), 0.2f).SetLoops(4, LoopType.Yoyo);
    }

    private IEnumerator ShakeRoutine()
    {
        isWordValid = false;
       
        float initialPosition = container.transform.localPosition.x;      

        for (int i = 0; i < shakeCount; i++)
        {
            float amplitude = initialShakeAmplitude * Mathf.Pow(amplitudeDecayFactor, i); // 计算当前抖动幅度
            float duration = initialShakeTime * Mathf.Pow(durationDecayFactor, i); // 计算当前移动时间
            float targetX = initialPosition + (i % 2 == 0 ? -amplitude : amplitude); // 计算目标X位置

            //Debug.LogError("移动位置: " + targetX + " 时间: " + duration);

            container.transform.DOLocalMoveX(targetX, duration).SetEase(Ease.Linear); // 使用线性缓动函数（可选）
            yield return new WaitForSeconds(duration);
        }
        
        FadeOut(fadeDuration);     

    }
}