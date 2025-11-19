using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Unity.Passport.Runtime.UI
{
    public class ConfirmDeleteUser : MonoBehaviour
    {
        [SerializeField] public UnityEvent<int> onConfirmed;
        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(DeleteSelf);
        }

        void DeleteSelf()
        {
            int index = transform.parent.GetSiblingIndex() - 1;
            onConfirmed.Invoke(index);
        }
    }
}