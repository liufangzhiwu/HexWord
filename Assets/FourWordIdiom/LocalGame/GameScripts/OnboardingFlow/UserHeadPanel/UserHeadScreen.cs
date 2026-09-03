using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UserHeadScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button comfirmBtn; // 确认按钮
    [SerializeField] private Button headBtn; // 确认按钮
    [SerializeField] private Button headBorderBtn; // 确认按钮
    [SerializeField] private Image headIcon; // 确认按钮
    [SerializeField] private Image headBorderIcon; // 确认按钮
  
    [SerializeField] private Image headBorderRedPoint; // 头像框红点
    [SerializeField] private Image redPointHeadImage; // 头像红点
   
    [SerializeField] private InputField NameText; //标题文本
    [SerializeField] private Text HeaderText; //标题文本
    [SerializeField] private Text litterTitleText; // 小标题文本
    [SerializeField] private Text comfirmText; //
    [SerializeField] private Button HeadItemBtn;                    
    [SerializeField] private Button HeadBorderItemBtn;                    
    [SerializeField] private Transform HeadItemParent;         
    [SerializeField] private Transform HeadBorderItemParent;         
    [SerializeField] private GameObject HeadItemList;         
    [SerializeField] private GameObject HeadBorderItemList;  
    
    [SerializeField] private GameObject HeadChoiceItem;         
    [SerializeField] private GameObject HeadBorderChoiceItem;         

    
    private Dictionary<int ,GameObject> Headitems = new Dictionary<int ,GameObject>();
    private Dictionary<int ,GameObject> HeadBorderitems = new Dictionary<int ,GameObject>();
    private int newHeadIonIndex = 0;
    private int newHeadBorderIonIndex = 0;
   
    protected void Start()
    {
        InitHeadIconList();
        InitHeadBorderIconList();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");

       
        HeaderText.text = MultilingualManager.Instance.GetString("CharacterInfoTitle");
        litterTitleText.text= MultilingualManager.Instance.GetString("CharacterInfoAvatar");
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
        newHeadIonIndex=GameDataManager.Instance.UserData.UserHeadId;
        newHeadBorderIonIndex=GameDataManager.Instance.UserData.UserHeadBorderId;
        UpdateHeadIconList(newHeadIonIndex,true);
        UpdateAllHeadBorderIconList(newHeadBorderIonIndex,true);
        
        UpdateHeadIcon();
        UpdateHeadName();
        
        headBorderRedPoint.gameObject.SetActive(GameDataManager.Instance.UserData.isGetNewHeadBorderIcon);
        redPointHeadImage.gameObject.SetActive(GameDataManager.Instance.UserData.isGetNewHeadIcon);
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
        
        int userHeadBorderId = newHeadBorderIonIndex;
        
        headBorderIcon.sprite = LoadheadIcon("AvatarFrameIcon"+userHeadBorderId);
    
    }

    private void InitHeadIconList()
    {
        Headitems.Clear();
        
        int headid = GameDataManager.Instance.UserData.UserHeadId;
        int gethead= GameDataManager.Instance.UserData._getAnimalsHeadIcons.Count;
        int totalheads=25+gethead;
        for (int i = 0; i < totalheads; i++)
        {
            int index = i;
            int iconindex = i;
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
                HeadItemObj.transform.GetChild(0).GetComponent<Image>().sprite = LoadheadIcon("head"+getid);
            }
            else
            {
                HeadItemObj.transform.GetChild(0).GetComponent<Image>().sprite = LoadheadIcon("head"+i);
            }
            
            if (headid == spriteindex)
            {
                HeadItemObj.transform.GetChild(1).gameObject.SetActive(true);
            } else
            {
                HeadItemObj.transform.GetChild(1).gameObject.SetActive(false);
            }
            
            HeadItemObj.GetComponent<Button>().onClick.AddListener(()=>ClickHeadItemBtn(index,iconindex));
            Headitems.Add(index, HeadItemObj);
        }
    }
    
    private void InitHeadBorderIconList()
    {
        HeadBorderitems.Clear();
        int headBorderid = GameDataManager.Instance.UserData.UserHeadBorderId;
        List<AvatarFrameItem> avatarFrameItems= AchievementManager.Instance.GetAllAvatarFrameItems();

        int avatarFrameCount = 7;
        
        for (int i = 0; i < avatarFrameItems.Count; i++)
        {
            int index = i;
            if (index > avatarFrameCount)
            {
                break;
            }
            int iconindex = i+1;
            GameObject HeadBorderItemObj = Instantiate(HeadBorderItemBtn.gameObject, HeadBorderItemParent);
            
            HeadBorderItemObj.gameObject.SetActive(true);
            
            HeadBorderItemObj.transform.GetChild(0).gameObject.SetActive(index <= 3);
            HeadBorderItemObj.GetComponent<Image>().enabled=(index > 3);

            if (index > 3)
            {
                HeadBorderItemObj.GetComponent<Image>().sprite = LoadheadIcon("AvatarFrameIcon"+index);
            }
            else
            {
                HeadBorderItemObj.transform.GetChild(0).GetComponent<Image>().sprite = LoadheadIcon("AvatarFrameIcon"+index);
            }
            
            if (headBorderid == index)
            {
                HeadBorderItemObj.transform.GetChild(1).gameObject.SetActive(true);
            } else
            {
                HeadBorderItemObj.transform.GetChild(1).gameObject.SetActive(false);
            }
            
            bool isUnLock=false;
            
            if (index > 3)
            {
                if (!GameDataManager.Instance.UserData._getHeadBorderIcons.Contains(iconindex))
                {
                    isUnLock = true;
                }
            }
         
            HeadBorderItemObj.transform.GetChild(2).gameObject.SetActive(isUnLock);
          
            HeadBorderItemObj.GetComponent<Button>().onClick.AddListener(()=>ClickHeadBorderItemBtn(index,iconindex));
            
            HeadBorderitems.Add(index, HeadBorderItemObj);
        }
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
        comfirmBtn.AddClickAction(OnClickComfirmBtn); // 绑定关闭按钮事件
        headBtn.onClick.AddListener(ClickHeadBtn);
        headBorderBtn.onClick.AddListener(ClickHeadBorderBtn);
    }


    private void ClickHeadBtn()
    {
        HeadItemList.gameObject.SetActive(true);
        HeadChoiceItem.gameObject.SetActive(true);
        HeadBorderChoiceItem.gameObject.SetActive(false);
        HeadBorderItemList.gameObject.SetActive(false);
        
        GameDataManager.Instance.UserData.isGetNewHeadIcon = false;
        
        redPointHeadImage.gameObject.SetActive(GameDataManager.Instance.UserData.isGetNewHeadIcon);
    }
    
    private void ClickHeadBorderBtn()
    {
        HeadItemList.gameObject.SetActive(false);
        HeadBorderItemList.gameObject.SetActive(true);
        HeadBorderChoiceItem.gameObject.SetActive(true);
        HeadChoiceItem.gameObject.SetActive(false);
        
        GameDataManager.Instance.UserData.isGetNewHeadBorderIcon = false;
        
        headBorderRedPoint.gameObject.SetActive(GameDataManager.Instance.UserData.isGetNewHeadBorderIcon);
    }
    

    private void ClickHeadItemBtn(int index,int iconindex)
    {
        newHeadIonIndex = iconindex;
        UpdateHeadIcon();
        UpdateHeadIconList(index);

        AnalyticMgr.HeadChange();
        
        GameDataManager.Instance.UserData.isGetNewHeadIcon = false;
        
        redPointHeadImage.gameObject.SetActive(GameDataManager.Instance.UserData.isGetNewHeadIcon);
    }
    
    private void ClickHeadBorderItemBtn(int index,int iconindex)
    {
        bool isUnLock=false;
        string tisstr=null;

        if (index > 3)
        {
            if (!GameDataManager.Instance.UserData._getHeadBorderIcons.Contains(iconindex))
            {
                AvatarFrameItem avatarFrameItem=  AchievementManager.Instance.GetAvatarSomeFrameById(iconindex);
                tisstr = avatarFrameItem.unlockTipText;
                isUnLock = true;
            }
        }

        if (isUnLock)
        {
            MessageSystem.Instance.ShowTip(tisstr);
            return;
        }
        
        newHeadBorderIonIndex = index;
        UpdateHeadIcon();
        UpdateHeadBorderIconList(index);
    }
    
    private void UpdateHeadIconList(int headid=0, bool show = false)
    {
        for (int i = 0; i < Headitems.Count; i++)
        {
            int index = i;
            GameObject HeadItemObj = Headitems[index];
            
            string spritename=HeadItemObj.transform.GetChild(0).GetComponent<Image>().sprite.name.Replace("head","");
            spritename=spritename.Replace("(Clone)","");
            int iconindex =int.Parse(spritename);
            
            if (newHeadIonIndex == iconindex)
            {
                HeadItemObj.transform.GetChild(1).gameObject.SetActive(true);
            }
            else
            {
                HeadItemObj.transform.GetChild(1).gameObject.SetActive(false);
            }
        }
    }
    
    private void UpdateAllHeadBorderIconList(int headborderid=0, bool show = false)
    {
        
        for (int i = 0; i < HeadBorderitems.Count; i++)
        {
            int index = i;
            int iconindex = i+1;
            GameObject HeadborderItemObj = HeadBorderitems[index];
            
            if (newHeadBorderIonIndex == index)
            {
                HeadborderItemObj.transform.GetChild(1).gameObject.SetActive(true);
            }
            else
            {
                HeadborderItemObj.transform.GetChild(1).gameObject.SetActive(false);
            }
            
            bool isUnLock=false;
            if (index > 3)
            {
                if (!GameDataManager.Instance.UserData._getHeadBorderIcons.Contains(iconindex))
                {
                    isUnLock = true;
                }
            }
         
            HeadborderItemObj.transform.GetChild(2).gameObject.SetActive(isUnLock);

        }
     
    }
    
    private void UpdateHeadBorderIconList(int headid=0, bool show = false)
    {
        for (int i = 0; i < HeadBorderitems.Count; i++)
        {
            int index = i;
            GameObject HeadItemObj = HeadBorderitems[index];
            
            if (newHeadBorderIonIndex == index)
            {
                HeadItemObj.transform.GetChild(1).gameObject.SetActive(true);
            }
            else
            {
                HeadItemObj.transform.GetChild(1).gameObject.SetActive(false);
            }
        }
    }

    private void OnClickComfirmBtn()
    {
        string name = NameText.text;
        
        GameDataManager.Instance.UserData.UserHeadId = newHeadIonIndex;
        GameDataManager.Instance.UserData.UserHeadBorderId = newHeadBorderIonIndex;

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
        EventDispatcher.instance.TriggerChangeGoldUI(0, false);
        EventDispatcher.instance.TriggerChangeHeadIconUpdateEvent();
        base.Close(); // 隐藏面板
    }
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon,"UserHeadIcons");
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
