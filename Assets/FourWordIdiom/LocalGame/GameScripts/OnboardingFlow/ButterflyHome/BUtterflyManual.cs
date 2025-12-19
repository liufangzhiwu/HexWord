using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BUtterflyManual : UIWindow
{
    [SerializeField] private Button backhome;
    [SerializeField] private Button help;
    [SerializeField] private Transform content;

    private void Start()
    {
        backhome.AddClickAction(() =>
        {
            SystemManager.Instance.HidePanel(PanelType.ButterflyManual);
            SystemManager.Instance.ShowPanel(PanelType.ButterflyHome);
        });
        
        help.AddClickAction(() =>
        {
            SystemManager.Instance.ShowPanel(PanelType.ButterflyGardenHelp);
        });
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        UpdateUI();
    }

    private void UpdateUI()
    {
        List<ButterflyInfo> butterflyInfos = ButterfliesManager.Instance.GetCurrentGardenButterflies();
        if (content.childCount < butterflyInfos.Count)
        {
            int diff = butterflyInfos.Count - content.childCount;
            Transform firstItem = content.GetChild(0);
            for (int i = 0; i < diff; i++)
            {
                Instantiate(firstItem.gameObject, content);
            }
        }else if (content.childCount > butterflyInfos.Count)
        {
            int diff = content.childCount - butterflyInfos.Count;
            for (int i = diff; i > 0; i--)
            {
                Destroy(content.GetChild(content.childCount - i - 1).gameObject);
            }
        }
       
        for (int i = 0; i < butterflyInfos.Count; i++)
        {
            ButterflyInfo butterflyInfo = butterflyInfos[i];
            GameObject item = content.GetChild(i).gameObject;
            item.GetComponent<Image>().enabled = true;
            
            Transform starParent = item.transform.Find("StarIcon");
            Transform butterfly = item.transform.Find("ButterflyParent");
            Transform nameParent = item.transform.Find("NameParent");
            bool isOwn = GameDataManager.Instance.ButterflyData.butterflies.Contains(butterflyInfo.Id);
            if (isOwn)
            {
                starParent.GetComponent<Image>().sprite =
                    AssetBundleLoader.SharedInstance.GetSpriteFromAtlas($"star_show_"+ butterflyInfo.Rarity);
                butterfly.GetComponent<Image>().sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas(butterflyInfo.ButterflyIcon);
           
                nameParent.GetChild(0).gameObject.SetActive(true);
                nameParent.GetChild(0).GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString(butterflyInfo.Name,"hudie");
                nameParent.GetChild(1).gameObject.SetActive(false);
            }
            else
            {
                starParent.GetComponent<Image>().sprite =
                    AssetBundleLoader.SharedInstance.GetSpriteFromAtlas($"star_hide_"+ butterflyInfo.Rarity);
                butterfly.GetComponent<Image>().sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("butterfly_ghost");
                nameParent.GetChild(0).gameObject.SetActive(false);
                nameParent.GetChild(1).gameObject.SetActive(true);
            }

            butterfly.GetComponent<Image>().enabled = true;
            starParent.GetComponent<Image>().enabled = true;
            starParent.GetComponent<Image>().SetNativeSize();
        }
    }
}
