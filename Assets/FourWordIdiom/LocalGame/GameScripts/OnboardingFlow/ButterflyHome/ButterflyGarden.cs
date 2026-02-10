using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ButterflyGarden : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Button closeBtn;
    [SerializeField] private GameObject garden;
    
    // Start is called before the first frame update
    private void Awake()
    {
        closeBtn.AddClickAction(()=>Destroy(gameObject));
        if (title is null)
        {
            title = GetComponentInChildren<Text>();
        }
        title.text = MultilingualManager.Instance.GetString("ButterflyUI03", "hudie");
        
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
        Debug.Log("ButterflyGarden Awake");
    }
    
    // 刷新场景
    private void FreshGarden()
    {
        for (int i = 0; i < garden.transform.childCount; i++)
        {
            if(GameDataManager.Instance.ButterflyData.currGarden == i+1)
                garden.transform.GetChild(i).GetChild(0).gameObject.SetActive(true);    // 已拥有
            else
                garden.transform.GetChild(i).GetChild(0).gameObject.SetActive(false);    // 未拥有
        }
    }

    // 解锁新场景
    public void UnlockGarden(int gardenId, Action<bool> callback)
    {
        gameObject.SetActive(true);
        StartCoroutine(ProcessGardenFlow(gardenId, callback));
    }

    private IEnumerator ProcessGardenFlow(int gardenId, Action<bool> callback)
    {
        Transform item = garden.transform.GetChild(gardenId -1 );
        GameObject checkGo = item.GetChild(0).gameObject;
        GameObject lockGo = item.GetChild(1).gameObject;   // 锁
        checkGo.SetActive(false);
        lockGo.SetActive(true);
        Image lockImg = lockGo.transform.GetChild(0).GetComponent<Image>();
        // 先将场景列表中属于 gardenId 置会加锁，再播放解锁特效
        yield return new WaitForSeconds(1.2f);
        yield return lockImg.DOFade(0, 1.5f).WaitForCompletion();
        lockImg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("unlock", "butterflyhome");
        GameObject effectPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("butterflyhome", "FX_ButterflyUnlock");
        GameObject effectInstance = Instantiate(effectPrefab, lockGo.transform);
        effectInstance.transform.localPosition = Vector3.zero;
        effectInstance.transform.localScale = Vector3.one;
        ParticleSystem particle = effectInstance.GetComponent<ParticleSystem>();
        float fxDuration = (particle != null) ? particle.main.duration : 2.0f;
        // ParticleSystemRenderer renderer = effectInstance.GetComponent<ParticleSystemRenderer>();
        // Material unBF = AssetBundleLoader.SharedInstance.LoadMaterialResource("materials", "unlockGrandeBF");       
        // renderer.material = unBF;
        yield return lockImg.DOFade(1, 0.5f).WaitForCompletion();
        yield return new WaitForSeconds(2.5f);
        Destroy(particle, fxDuration + 0.5f);
        lockGo.SetActive(false); // 彻底隐藏遮罩层
        FreshGarden();
        // // 抛出事件让主页跟换
        EventDispatcher.instance.TriggerChangeButterflyGarden();
        callback?.Invoke(true); // 解锁动画完成
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject); // 关闭
    }
    protected  void OnDisable()
    {
        // nothing
    }
    
}
