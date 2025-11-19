using System.Collections.Generic;
using UnityEngine;

namespace Unity.Passport.Runtime.UI
{
    public class UIAdaptManager : MonoBehaviour
    {
        public static UIAdaptManager Instance = null;
        private List<UIAdaptBase> _adaptBases = new List<UIAdaptBase>();

        private bool _state = false;
        public bool autoRotation = false; // 是否开启了自动旋转
        
        // Start is called before the first frame update
        private void Awake()
        {
            Instance = this;
            _state = IsLandscape();
            OnChange();
        }

        // Update is called once per frame
        void Update()
        {
            if (autoRotation)
            {
                var newState = IsLandscape();
                if (newState != _state)
                {
                    _state = newState;
                    OnChange();
                }
            }
            
        }

        private void OnChange()
        {
            foreach (var adaptBase in _adaptBases)
            {
                if (_state)
                {
                    // 切换到横屏
                    adaptBase.OnLandscape();
                }
                else
                {
                    // 切换到竖屏
                    adaptBase.OnPortrait();
                }
            }
        }

        private bool IsLandscape()
        {
            return Screen.width >= Screen.height;
        }
        
        public void Register(UIAdaptBase adaptBase) {
            if (_adaptBases.Contains(adaptBase)) {
                return;
            }
            _adaptBases.Add(adaptBase);
            OnChange();
        }
        
        public void Remove(UIAdaptBase adaptBase) {
            if (!_adaptBases.Contains(adaptBase)) {
                return;
            }
            _adaptBases.Remove(adaptBase);
        }
    }
}
