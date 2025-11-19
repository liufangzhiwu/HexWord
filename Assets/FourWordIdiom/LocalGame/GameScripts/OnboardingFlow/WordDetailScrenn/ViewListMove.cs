using UnityEngine;
using Knivt.Tools.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class ViewListMove : UICyclicScrollList<WordDetailTable, string>, IEndDragHandler, IBeginDragHandler,IDragHandler
{    
    public WordDetailScreen WordPanel;
    public LevelWordDetail levelWordPanel;
    Vector2 BeginPos;
    Vector2 EndPos;
    private string[] datas;

    public void InitList(List<string> wordData)
    {      
        datas = new string[wordData.Count];
        for (int i = 0; i < datas.Length; i++)
        {
            int j = i % wordData.Count;
            datas[i] = wordData[j];
        }
        Initlize(datas);
    }
    
    protected override void ResetCellData(WordDetailTable cell, string data, int dataIndex)
    {
        cell.gameObject.SetActive(true);
        cell.SetText(data);
        //if (LevelManager.Instance.IsEnterVocabulary)
        //{
        //    cell.GetComponent<Transform>().localScale=Vector3.one;
        //}
        //else
        //{
        //    cell.GetComponent<Transform>().localScale=new Vector3(0.82f,0.82f,0.82f);
        //}
        //cell.UpdateDisplay(data.iconSprite, data.name);
        //ResetCellShow(LevelManager.Instance.WordData.PageIndex);
    }
  
    public void OnEndDrag(PointerEventData eventData)
    {
        var pos = eventData.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, pos, Camera.main, out EndPos);
        if (Mathf.Abs(EndPos.x - BeginPos.x) < 10) return;
        if (EndPos.x < BeginPos.x + 10)
        {
            if(WordPanel != null) 
                WordPanel.MovePage(false);
            if(levelWordPanel != null)
                levelWordPanel.MovePage(false);
        }
        else if (EndPos.x > BeginPos.x - 10)
        {
            if (WordPanel != null)
                WordPanel.MovePage(true);
            if (levelWordPanel != null)
                levelWordPanel.MovePage(true);          
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var pos = eventData.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, pos, Camera.main, out BeginPos);
    }

    public void OnDrag(PointerEventData eventData)
    {
        var pos = eventData.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, pos, Camera.main, out EndPos);
        if (Mathf.Abs(EndPos.x - BeginPos.x) < 10) return;

        if (WordPanel != null)
        {
            if (WordPanel.curPage == 1 && EndPos.x > BeginPos.x + 50)
            {
                return;
            }
            if (WordPanel.curPage == datas.Length && EndPos.x < BeginPos.x - 50)
            {
                return;
            }

            float x = WordPanel.width * -(WordPanel.curPage - 1) + (EndPos.x - BeginPos.x);
            WordPanel.ParentMovePos(x);
        }   
        
        if (levelWordPanel != null)
        {
            if (levelWordPanel.curPage == 1 && EndPos.x > BeginPos.x + 50)
            {
                return;
            }
            if (levelWordPanel.curPage == datas.Length && EndPos.x < BeginPos.x - 50)
            {
                return;
            }

            float x = levelWordPanel.width * -(levelWordPanel.curPage - 1) + (EndPos.x - BeginPos.x);
            levelWordPanel.ParentMovePos(x);
        }
            
        
    }
}