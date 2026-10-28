using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace MarineSort
{
    /// <summary>
    /// 进度百分比文本。
    /// 独立挂在进度条节点上轮询 Slider.value，不侵入 LoadingController 的动画与加载流程，
    /// 关卡加载逻辑改动时不会连带失效。
    /// </summary>
    public class MarineProgressPercent : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Text label;

        private int _last = -1;

        private void Awake()
        {
            if (slider == null) slider = GetComponent<Slider>();
        }

        private void LateUpdate()
        {
            if (slider == null || label == null) return;

            int pct = Mathf.RoundToInt(Mathf.Clamp01(slider.value) * 100f);
            if (pct == _last) return;

            _last = pct;
            // 固定区域格式：避免某些语言包把数字渲染成非 ASCII 数字
            label.text = pct.ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
