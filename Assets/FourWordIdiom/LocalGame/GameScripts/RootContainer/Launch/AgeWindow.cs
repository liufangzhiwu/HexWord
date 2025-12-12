using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AgeWindow : MonoBehaviour
{
    [SerializeField] private Text _titleText;
    [SerializeField] private HyperlinkText _descriptionText;
    [SerializeField] private Button _button;

    // Start is called before the first frame update
    private void Start()
    {
        _descriptionText.text = MultilingualManager.Instance.GetString("AgeDescription");
        _descriptionText.onHyperlinkClick = OnClickText;
        _button.AddClickAction(()=> Destroy(gameObject));
    }
    
    void OnClickText(string url)
    {
        Debug.Log("点击"+url);
        Application.OpenURL(url);
    }
    
}
