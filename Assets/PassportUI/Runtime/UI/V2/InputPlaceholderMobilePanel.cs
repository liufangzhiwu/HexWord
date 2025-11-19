using UnityEngine;
using TMPro;
namespace Unity.Passport.Runtime.UI
{
    public class InputPlaceholderMobilePanel : MonoBehaviour
    {
        public TextMeshProUGUI Text;

        public string PlaceholderText
        {
            set => Text.text = value;
        }
    }
}
