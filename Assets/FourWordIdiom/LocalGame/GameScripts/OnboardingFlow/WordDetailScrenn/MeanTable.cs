using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class MeanTable : MonoBehaviour
{ 
    [SerializeField] private Text meanContentText;
    [SerializeField] private Text meanNameText;
   
   public void InitUI(string meanName, string meanContent)
   {
       meanNameText.text = meanName;
       meanContentText.text = meanContent;
       
       StartCoroutine(SetLayoutHorizontal());
    }
   
   public void SetTextLayer()
   {
       StartCoroutine(SetLayoutHorizontal());
   }
   
   private IEnumerator SetLayoutHorizontal()
   {
       yield return new WaitForSeconds(0.01f);
       
       meanContentText.GetComponent<TextPunctuationAdjuster>().AdjustTextPunctuation();
       
       //transform.GetComponent<ContentSizeFitter>().SetLayoutHorizontal();
   }
}


