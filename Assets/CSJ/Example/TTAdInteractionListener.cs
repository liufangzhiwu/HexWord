using System.Collections.Generic;
using ByteDance.Union;
using UnityEngine;

public class TTAdInteractionListener : ITTAdInteractionListener
{
    public void OnAdEvent(int code, Dictionary<string, object> map)
    {
        Debug.Log("CSJM_Unity" + "Example " + ", --==-- OnAdEvent, code: " + code + ", map: " + map.ToString());
    }
}
