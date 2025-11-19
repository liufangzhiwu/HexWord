using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Unity.Passport.Runtime.Model;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.Passport.Runtime.UI
{
    public class TOSLink : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI linkText;
        [SerializeField] private GameObject tosLinkContainer;
        [SerializeField] private GameObject tosReferencePanel;
        [SerializeField] private Toggle tosToggle;

        private List<TosReference> _tosReferences = new();
        public void Init(PassportTos tos, string hint = "点击查看：", bool showMainTos = false)
        {
            Clear();
            if (tos == null)
            {
                tosLinkContainer.SetActive(false);
                return;
            }
            
            // 初始化 _tosReferences
            if (showMainTos)
            {
                _tosReferences.Add(new TosReference()
                {
                    Name = tos.Name,
                    Content = tos.Content
                });
            }
            foreach (var reference in tos.References)
            {
                _tosReferences.Add(reference);
            }
            
            // 生成超链接
            linkText.text = hint;
            var textList = new List<string>();
            for (var i = 0; i < _tosReferences.Count; i += 1)
            {
                var text = _tosReferences[i].Name;
                textList.Add($"<link=\"{i}\"><color=#61B9FF>{text}</color></link>");
            }
            linkText.text += String.Join("，", textList.ToArray());

            // 当列表为空，对象不展示
            tosLinkContainer.SetActive(_tosReferences.Any());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var pos = new Vector3(eventData.position.x, eventData.position.y, 0);
            var linkIndex = TMP_TextUtilities.FindIntersectingLink(linkText, pos, null);
            if (linkIndex == -1) return;
            
            var linkInfo = linkText.textInfo.linkInfo[linkIndex];
            var index = Int32.Parse(linkInfo.GetLinkID());
            var tosRef = _tosReferences[index];
            if (!String.IsNullOrEmpty(tosRef.Content) && tosReferencePanel != null)
            {
                // 弹框跳转
                tosReferencePanel.GetComponent<TOSPanel>().Show(tosRef);
            }
            else
            {
                // 外链跳转
                UIMessage.Show("链接跳转中...");
                Application.OpenURL(@tosRef.Link);
            }
        }

        public void Clear()
        {
            linkText.text = "";
            _tosReferences.Clear();
            if (tosToggle != null)
            {
                tosToggle.isOn = false;
            }
        }

        /// <summary>
        /// 是否通过校验
        /// </summary>
        /// <returns></returns>
        public bool Check()
        {
            return !_tosReferences.Any() || tosToggle.isOn;
        }
    }
}
