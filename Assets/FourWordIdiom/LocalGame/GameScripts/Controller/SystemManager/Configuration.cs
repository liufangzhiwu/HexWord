
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Configuration")]
public class Configuration : ScriptableObject
{
    [System.Serializable]
    public struct ViewsData
    {
        [StringInList(typeof(PanelType), "AvailableViews")]
        public string panelName;
        [StringInList(typeof(UIPanelLayer), "GetPanelLayers")]
        public string panelLayer;            
        public string bundleName;
        public string spriteAtlasName;
        public GameObject prefab;
    }
    
    public List<ViewsData> viewsData;

    #region Helper
    public ViewsData GetViewsData(string name)
    {
        var data = viewsData.Find(c => c.panelName == name);
        if(string.IsNullOrEmpty(data.panelName))
        {
            Debug.LogError($"[PanelConfig]data not found! name: {name}");
        }
        return data;
    }
    #endregion
}


