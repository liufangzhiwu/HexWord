using UnityEngine;
using UnityEngine.UI;

namespace Unity.Passport.Runtime.UI
{
    public class ButtonController : MonoBehaviour
    {
        public GameObject button, loadingIcon;
        private bool _loading = false;

        // Update is called once per frame
        private void FixedUpdate()
        {
            if (_loading)
            {
                loadingIcon.transform.Rotate(0.0f, 0.0f, 5.0f, Space.World);
            }
        }

        public void Loading()
        {
            button.GetComponent<Button>().interactable = false;
            loadingIcon.SetActive(true);
            _loading = true;
        }

        public void StopLoading()
        {
            button.GetComponent<Button>().interactable = true;
            loadingIcon.SetActive(false);
            _loading = false;
        }
    }
}