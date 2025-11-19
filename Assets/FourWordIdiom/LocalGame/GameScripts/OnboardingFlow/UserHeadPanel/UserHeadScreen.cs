using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UserHeadScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button comfirmBtn; // 确认按钮
    [SerializeField] private Image headIcon; // 确认按钮
   
    [SerializeField] private InputField NameText; //标题文本
    [SerializeField] private Text HeaderText; //标题文本
    [SerializeField] private Text litterTitleText; // 小标题文本
    [SerializeField] private Text comfirmText; //
    [SerializeField] private Button HeadItemBtn;                    
    [SerializeField] private Transform HeadItemParent;         
    
    private Dictionary<int ,GameObject> Headitems = new Dictionary<int ,GameObject>();
   
    protected void Start()
    {
       InitHeadIconList();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        UpdateHeadIcon();
        UpdateHeadName();

        UpdateHeadIconList(true);
        HeaderText.text = MultilingualManager.Instance.GetString("CharacterInfoTitle");
        litterTitleText.text= MultilingualManager.Instance.GetString("CharacterInfoAvatar");
    }
    
    private void UpdateHeadName()
    {
        //if (string.IsNullOrEmpty(GameDataManager.instance.UserData.UserName))
        {
            //GameDataManager.instance.UserData.UserName = FishInfoController.Instance.GeneratePlayerName();
        }
        //NameText.text = GameDataManager.instance.UserData.UserName;
    }

    private void UpdateHeadIcon()
    {
        //headIcon.sprite = LoadheadIcon("head"+GameDataManager.instance.UserData.UserHeadId);
    }

    private void InitHeadIconList()
    {
        // int headid = GameDataManager.instance.UserData.UserHeadId;
        // for (int i = 0; i < 25; i++)
        // {
        //     int index = i;
        //     GameObject HeadItemObj = Instantiate(HeadItemBtn.gameObject, HeadItemParent);
        //     HeadItemObj.GetComponent<Image>().sprite = LoadheadIcon("head"+i);
        //     HeadItemObj.gameObject.SetActive(true);
        //     if (headid == i)
        //     {
        //         HeadItemObj.transform.GetChild(0).gameObject.SetActive(true);
        //         if (headid >= 1)
        //         {
        //             HeadItemObj.transform.SetSiblingIndex(2);
        //         }
        //     }
        //     else
        //     {
        //         HeadItemObj.transform.GetChild(0).gameObject.SetActive(false);
        //     }
        //     HeadItemObj.GetComponent<Button>().onClick.AddListener(()=>ClickHeadItemBtn(index));
        //     Headitems.Add(index, HeadItemObj);
        // }
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
        comfirmBtn.AddClickAction(OnClickComfirmBtn); // 绑定关闭按钮事件
    }

    private void ClickHeadItemBtn(int index)
    {
        //GameDataManager.instance.UserData.UserHeadId = index;
        UpdateHeadIcon();
        UpdateHeadIconList();

        //ThinkManager.instance.Event_changerolehead();
    }
    
    private void UpdateHeadIconList(bool show = false)
    {
        // int headid = GameDataManager.instance.UserData.UserHeadId;
        // for (int i = 0; i < Headitems.Count; i++)
        // {
        //     int index = i;
        //     GameObject HeadItemObj = Headitems[index];
        //     if (headid == i)
        //     {
        //         HeadItemObj.transform.GetChild(0).gameObject.SetActive(true);
        //         if (headid >= 1&&show)
        //         {
        //             HeadItemObj.transform.SetSiblingIndex(2);
        //         }
        //     }
        //     else
        //     {
        //         HeadItemObj.transform.GetChild(0).gameObject.SetActive(false);
        //     }
        // }
    }

    private void OnClickComfirmBtn()
    {
        string name = NameText.text;

        if (!string.IsNullOrEmpty(name))
        {
            // if (MultilingualManager.Instance.ContainsForbiddenWords(name))
            // {
            //     string tips= MultilingualManager.Instance.GetString("CharacterInfoTips01");
            //     if (tips.Contains("\\n"))
            //     {
            //         tips = tips.Replace("\\n", "\n");
            //     }
            //     MessageSystem.Instance.ShowTip(tips, false);
            // }
            // else
            // {
            //     // GameDataManager.instance.UserData.isChangeUserName = true;
            //     // GameDataManager.instance.UserData.UserName = name;
            //     //ThinkManager.instance.SetRoleName(GameDataManager.instance.UserData.UserName);
            //     OnCloseBtn();
            // }
        }
        else
        {
            OnCloseBtn();
        }
    }
 
    private void OnCloseBtn()
    {
        //EventDispatcher.instance.TriggerChangeHeadIconUpdateEvent();
        base.Close(); // 隐藏面板
    }
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }
}
