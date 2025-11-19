using TMPro;
using UnityEngine;
using Unity.Passport.Runtime.Model;
using UnityEngine.UI;

namespace Unity.Passport.Runtime.UI
{
    public class TOSPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI content;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private RectTransform contentTransform;
        [SerializeField] private GameObject linksObj;
        [SerializeField] private TextMeshProUGUI titlePortrait;
        [SerializeField] private Button acceptTosButton;
        [SerializeField] private Button refuseTosButton;

        public void Show(TosReference tosRef)
        {
            content.text = tosRef.Content;
            contentTransform.anchoredPosition = new Vector2(contentTransform.anchoredPosition.x, 0);
            title.text = tosRef.Name;
            gameObject.SetActive(true);
        }
        
        public void Init(PassportTos tos)
        {
            if (tos == null)
            {
                gameObject.SetActive(false);
                return;
            }
            content.text = tos.Content;
            contentTransform.anchoredPosition = new Vector2(contentTransform.anchoredPosition.x, 0);
            title.text = tos.Name;
            titlePortrait.text = tos.Name;
            // 设置 Links
            linksObj.GetComponent<TOSLink>().Init(tos);
            gameObject.SetActive(true);
        }

        public void Loading(bool loading)
        {
            var interactable = !loading;
            acceptTosButton.interactable = interactable;
            refuseTosButton.interactable = interactable;
        }
    }
}
