using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PersonInfoScreen : UIWindow
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
    private int newHeadIonIndex = 0;
   
    protected void Start()
    {
       //InitHeadIconList();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");

       
        HeaderText.text = MultilingualManager.Instance.GetString("CharacterInfoTitle");
        litterTitleText.text= MultilingualManager.Instance.GetString("CharacterInfoAvatar");
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,false);
        newHeadIonIndex=GameDataManager.Instance.UserData.UserHeadId;
        UpdateHeadIconList(true);
        UpdateHeadIcon();
        UpdateHeadName();
     
        int gethead= GameDataManager.Instance.UserData._getAnimalsHeadIcons.Count;
        int totalheads=25+gethead;

        if (totalheads > Headitems.Count)
        {
            InitHeadIconList();
        }
        
    }
    
    private void UpdateHeadName()
    {
        if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.UserName))
        {
            GameDataManager.Instance.UserData.UserName = FishInfoController.Instance.GeneratePlayerName();
        }
        NameText.text = GameDataManager.Instance.UserData.UserName;
    }

    private void UpdateHeadIcon()
    {
        headIcon.sprite = LoadheadIcon("head"+newHeadIonIndex);
    }

    private void InitHeadIconList()
    {
        int headid = GameDataManager.Instance.UserData.UserHeadId;
        int gethead= GameDataManager.Instance.UserData._getAnimalsHeadIcons.Count;
        int totalheads=25+gethead;
        for (int i = 0; i < totalheads; i++)
        {
            int index = i;
            int iconindex = i;
            if (Headitems.ContainsKey(iconindex))
            {
                // GameObject item = Headitems[iconindex];
                // item.GetComponent<Button>().onClick.AddListener(()=>ClickHeadItemBtn(index,iconindex));
                continue;
            }
          
            GameObject HeadItemObj = Instantiate(HeadItemBtn.gameObject, HeadItemParent);
            
            HeadItemObj.gameObject.SetActive(true);
              
            int spriteindex =i;

            if (i >= 25)
            {
                HeadItemObj.transform.SetSiblingIndex(3);
                int index2 = totalheads - i-1;
                int getid= GameDataManager.Instance.UserData._getAnimalsHeadIcons[index2];
                iconindex=getid;
                spriteindex = getid;
                HeadItemObj.GetComponent<Image>().sprite = LoadheadIcon("head"+getid);
            }
            else
            {
                HeadItemObj.GetComponent<Image>().sprite = LoadheadIcon("head"+i);
            }
            
            if (headid == spriteindex)
            {
                HeadItemObj.transform.GetChild(0).gameObject.SetActive(true);
            } else
            {
                HeadItemObj.transform.GetChild(0).gameObject.SetActive(false);
            }
            
            HeadItemObj.GetComponent<Button>().onClick.AddListener(()=>ClickHeadItemBtn(index,iconindex));

            Headitems.Add(spriteindex,HeadItemObj);
        }
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
        comfirmBtn.AddClickAction(OnClickComfirmBtn); // 绑定关闭按钮事件
    }

    private void ClickHeadItemBtn(int index,int iconindex)
    {
        newHeadIonIndex = iconindex;
        UpdateHeadIcon();
        UpdateHeadIconList();

        AnalyticMgr.HeadChange();
    }
    
    private void UpdateHeadIconList(bool show = false)
    {
        List<GameObject> array = Headitems.Values.ToList();
        
        for (int i = 0; i < Headitems.Values.Count; i++)
        {
            GameObject HeadItemObj = array[i];
            string spritename=HeadItemObj.GetComponent<Image>().sprite.name.Replace("head","");
            spritename=spritename.Replace("(Clone)","");
            int iconindex =int.Parse(spritename);
            
            if (newHeadIonIndex == iconindex)
            {
                HeadItemObj.transform.GetChild(0).gameObject.SetActive(true);
            }
            else
            {
                HeadItemObj.transform.GetChild(0).gameObject.SetActive(false);
            }
        }
    }

    private void OnClickComfirmBtn()
    {
        string name = NameText.text;
        
        GameDataManager.Instance.UserData.UserHeadId = newHeadIonIndex;

        if (!string.IsNullOrEmpty(name))
        {
            if (MultilingualManager.Instance.ContainsForbiddenWords(name))
            {
                string tips= MultilingualManager.Instance.GetString("CharacterInfoTips01");
                if (tips.Contains("\\n"))
                {
                    tips = tips.Replace("\\n", "\n");
                }
                MessageSystem.Instance.ShowTip(tips, false);
            }
            else
            {
                GameDataManager.Instance.UserData.isChangeUserName = true;
                GameDataManager.Instance.UserData.UserName = name;
                AnalyticMgr.NameChange(GameDataManager.Instance.UserData.UserName);
                GameDataManager.Instance.CommitGameData();
                OnCloseBtn();
            }
        }
        else
        {
            OnCloseBtn();
        }
    }
 
    private void OnCloseBtn()
    {
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
        EventDispatcher.instance.TriggerChangeHeadIconUpdateEvent();
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
