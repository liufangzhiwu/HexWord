using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ButterflyGarden : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Button closeBtn;
    [SerializeField] private GameObject garden;
    
    // Start is called before the first frame update
    private void Start()
    {
        closeBtn.AddClickAction(()=>Destroy(gameObject));
    }

    protected  void OnEnable()
    {
        CheckGarden();
    }

    private void CheckGarden()
    {
        for (int i = 0; i < garden.transform.childCount; i++)
        {
            Transform item = garden.transform.GetChild(i);
            if (GameDataManager.Instance.ButterflyData.gardens.Contains(i + 1)) 
            {
                item.GetChild(1).gameObject.SetActive(false);   // 未拥有
                if(GameDataManager.Instance.ButterflyData.currGarden == i+1)
                    item.GetChild(0).gameObject.SetActive(true);    // 已拥有
                else
                    item.GetChild(0).gameObject.SetActive(false);    // 已拥有
                
                // 每个按钮独立副本
                int id = i + 1;   // 关键：局部变量，闭包独立
                Button btn = item.gameObject.AddComponent<Button>();
                btn.AddClickAction(() =>
                {
                    GameDataManager.Instance.ButterflyData.SelectGarden(id);
                    // 向 butterflyHome 脚本发送一个事件选取蝶园改变的事件
                    EventDispatcher.instance.TriggerChangeButterflyGarden();
                    FreshGarden();
                });
            }
            else
            {
                item.GetChild(1).gameObject.SetActive(true);
                item.GetChild(0).gameObject.SetActive(false);
            }
        }
        
    }
    
    // 刷新场景
    private void FreshGarden()
    {
        for (int i = 0; i < garden.transform.childCount; i++)
        {
            if(GameDataManager.Instance.ButterflyData.currGarden == i+1)
                garden.transform.GetChild(i).GetChild(0).gameObject.SetActive(true);    // 已拥有
            else
                garden.transform.GetChild(i).GetChild(0).gameObject.SetActive(false);    // 已拥有
        }
    }

    // 解锁新场景
    public void UnlockGarden(int gardenId)
    {
        MessageSystem.Instance.ShowTip("播放场景解锁特效");

        StartCoroutine(ProcessGardenFlow(gardenId));
    }

    private IEnumerator ProcessGardenFlow(int gardenId)
    {
        Transform item = garden.transform.GetChild(gardenId -1 );
        item.GetChild(1).gameObject.SetActive(true);   // 未拥有
        item.GetChild(0).gameObject.SetActive(false);
        // 先将场景列表中属于 gardenId 置会加锁，再播放解锁特效
        yield return new WaitForSeconds(0.5f);
        // 刷新场景
        FreshGarden();
        yield return new WaitForSeconds(0.2f);
        // 抛出事件让主页跟换
        EventDispatcher.instance.TriggerChangeButterflyGarden();
        Destroy(gameObject); // 关闭
    }
    protected  void OnDisable()
    {
        // nothing
    }
    
}
