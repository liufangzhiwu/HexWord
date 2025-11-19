using UnityEngine;

namespace Unity.Passport.Runtime.UI {
    public class LoadingIcon : MonoBehaviour
    {
        // Update is called once per frame
        private void FixedUpdate()
        {
            transform.Rotate(0.0f, 0.0f, 5.0f, Space.World);
        }
    }
}