using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Passport.Runtime.UI
{
    public class CodeInputComponent : MonoBehaviour
    {
        // public Color m_CaretColor = new Color(50f / 255f, 50f / 255f, 50f / 255f, 1f);
        public Color normalColor = new Color( 0.78f, 0.78f, 0.78f, 1f);
        public Color highlightColor = new Color( 1f, 1f, 1f, 1f);
        public TextMeshProUGUI textComponent;
        public Image backgroundImage;
        public GameObject caret;
        public float caretBlinkTime = 1.0f;
        private float _deltaTime; // = Time.unscaledTime;
        private bool _caretStatus = false;
        private bool _activated;

        public string text
        {
            set
            {
                textComponent.text = value;
                backgroundImage.color =value == "" ? normalColor: highlightColor;
            }
        }

        public void ActivateInputField()
        {
            _activated = true;
            _caretStatus = true;
            caret.SetActive(_caretStatus);
        }

        public void DeactivateInputField()
        {
            _activated = false;
            _caretStatus = false;
            caret.SetActive(_caretStatus);
        }
        private void LateUpdate()
        {
            if (_activated)
            {
                if (_deltaTime > caretBlinkTime)
                {
                    _deltaTime = 0;
                    _caretStatus = !_caretStatus;
                    caret.SetActive(_caretStatus);
                }
                _deltaTime += Time.deltaTime;
            }
        }
    }
}