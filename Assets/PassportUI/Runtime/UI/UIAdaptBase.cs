using UnityEngine;
using System;

namespace Unity.Passport.Runtime.UI
{
    [Serializable]
    public class RectTransformAdaptConfig
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public float width;
        public float left;
    }
    public class UIAdaptBase: MonoBehaviour
    {
        public RectTransformAdaptConfig portraitConfig;
        public RectTransformAdaptConfig landscapeConfig;
        public RectTransform _rectTransform;
        public GameObject portraitObj;
        public GameObject landscapeObj;

        public void Start()
        { 
            UIAdaptManager.Instance.Register(this);
        }

        public void Reset()
        {
            _rectTransform = GetComponent<RectTransform>();

        }
        
        public void OnDestroy()
        {
            if (null == UIAdaptManager.Instance) {
                return;
            }
            UIAdaptManager.Instance.Remove(this);
        }
        
        public void OnLandscape()
        {
            // _rectTransform.anchorMin = landscapeConfig.anchorMin;
            // _rectTransform.anchorMax = landscapeConfig.anchorMax;
            if (landscapeConfig.width != 0)
            {
                float height = _rectTransform.sizeDelta[1];
                _rectTransform.sizeDelta =new Vector2(landscapeConfig.width, height);
            }

            if (landscapeConfig.left != 0)
            {
                float bottom = _rectTransform.offsetMin[1];
                _rectTransform.offsetMin = new Vector2(landscapeConfig.left, bottom );
            }

            if (portraitObj)
            {
                portraitObj.SetActive(false);
            }

            if (landscapeObj)
            {
                landscapeObj.SetActive(true);
            }
        }

        public void OnPortrait()
        {
            if (portraitConfig.width != 0)
            {
                float height = _rectTransform.sizeDelta[1];
                _rectTransform.sizeDelta =new Vector2(portraitConfig.width, height);
            }
            
            if (portraitConfig.left != 0)
            {
                float bottom = _rectTransform.offsetMin[1];
                _rectTransform.offsetMin = new Vector2(portraitConfig.left ,bottom );
            }
            
            if (portraitObj)
            {
                portraitObj.SetActive(true);
            }

            if (landscapeObj)
            {
                landscapeObj.SetActive(false);
            }
        }
    }
}