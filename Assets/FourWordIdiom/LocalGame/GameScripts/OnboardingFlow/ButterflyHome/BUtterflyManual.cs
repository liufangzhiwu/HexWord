using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

public class BUtterflyManual : UIWindow
{
    [SerializeField] private Text title;
    [SerializeField] private Button backhome;
    [SerializeField] private Button help;
    [SerializeField] private Transform content;

    private SpriteAtlas manualAtlas;
    private void Start()
    {
        title.text = MultilingualManager.Instance.GetString("ButterflyUI04", "hudie");
        backhome.AddVibraClickAction(() =>
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
        if (manualAtlas == null)
        {
            manualAtlas = AdvancedBundleLoader.SharedInstance.LoadAtlas("butterfly_ui", "UI_Butterflymaunal");
        }

        StartCoroutine(UpdateUI());
    }

    private IEnumerator UpdateUI()
    {
        yield return new WaitUntil(() => manualAtlas != null);
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
                    AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas($"star_show_"+ butterflyInfo.Rarity, "Butterfly_UI");
                butterfly.GetComponent<Image>().sprite = manualAtlas.GetSprite(butterflyInfo.ButterflyIcon);
           
                nameParent.GetChild(0).gameObject.SetActive(true);
                nameParent.GetChild(0).GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString(butterflyInfo.Name,"hudie");
                nameParent.GetChild(1).gameObject.SetActive(false);
            }
            else
            {
                starParent.GetComponent<Image>().sprite =
                    AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas($"star_hide_"+ butterflyInfo.Rarity, "Butterfly_UI");
                butterfly.GetComponent<Image>().sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("butterfly_ghost", "Butterfly_UI");
                nameParent.GetChild(0).gameObject.SetActive(false);
                nameParent.GetChild(1).gameObject.SetActive(true);
            }

            butterfly.GetComponent<Image>().enabled = true;
            starParent.GetComponent<Image>().enabled = true;
            starParent.GetComponent<Image>().SetNativeSize();
        }
    }
}
