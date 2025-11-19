using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Spine;
using Spine.Unity;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class FishItem : MonoBehaviour
{
    public Image line;
    [SerializeField] private Image box;
    [SerializeField] private Transform fishtrans;
    [SerializeField] private Transform fishtransparent;
    [SerializeField] private Transform targettrans;
    [SerializeField] private Image ainamebg;
    [SerializeField] private Text userName;
    [SerializeField] private Text userLevel;
    [SerializeField] private Text targetcount;
    [SerializeField] private Image NameImage; 
    public Image wordbg;
    [SerializeField] private Image RankImage;
    private DashCompetition dashparent;
    //[HideInInspector] public FishAISaveData  fishaiSaveData;
    private GameObject fish01Spine;
    private GameObject fish02Spine;
    
    private ObjectPool objectPoolBox; // 对象池实例
    
    private bool curisai;
    private float startx;
    private float distanceX;
    private int offlevel;
    private RectTransform targetRect;
    private RectTransform fishRect;
    private GameObject spinefishitem;
    //[SerializeField] private float moveSpeed = 350f;
    [HideInInspector] public bool isclaim = false;
 
    private void InitSpine()
    {
        if (fish01Spine == null)
        {
            fish01Spine = Resources.Load<GameObject>("Effect_Fish01");
        }
        
        if (fish02Spine == null)
        {
            fish02Spine = Resources.Load<GameObject>("Effect_Fish02");
        }

        if (objectPoolBox == null)
        {
            // 初始化对象池
            objectPoolBox = new ObjectPool(fish01Spine.gameObject, ObjectPool.CreatePoolContainer(transform, "Fish01Pool"));
        }

    }
    
    private void LoadPanelUI()
    { 
        if (UIUtilities.IsiPad())
        {
            Vector3 ipadvetor = new Vector3(1.1f,1.1f,1.1f);
            box.transform.parent.transform.localScale = ipadvetor;
            fishtransparent.parent.transform.localScale = ipadvetor;
            NameImage.transform.localScale = ipadvetor;
            line.sprite = LoadtaskIcon("bigfishline");
            //wordbg.transform.localScale = ipadvetor;    
        }
        else
        {
            fishtransparent.parent.transform.localScale=Vector3.one;
            box.transform.parent.transform.localScale=Vector3.one;
            NameImage.transform.localScale=Vector3.one;
            //wordbg.transform.localScale = Vector3.one;
            line.sprite = LoadtaskIcon("fishline");
        }
    }

    public void SetAiFishData(DashCompetition daparent)
    {
        InitSpine();
        LoadPanelUI();
        curisai = true;
        //fishaiSaveData = data;
        dashparent = daparent;
        UpdateFishLocation();
        //objectPool=bjectPool;
        InitUI();
        
    }

    private void UpdateFishLocation()
    {
        targetRect = targettrans.transform.GetComponent<RectTransform>();
        fishRect = fishtrans.transform.GetComponent<RectTransform>();
        
        if (distanceX <= 0)
        {
            startx = fishRect.anchoredPosition.x;
            //float totalWidth = GetComponent<RectTransform>().rect.width*UIUtilities.GetScreenRatio();
            float totalWidth = transform.parent.GetComponent<RectTransform>().rect.width-40;
            distanceX = totalWidth -targetRect.rect.width-fishRect.rect.width;
            // 计算水平距离（基于锚点坐标系）
            //distanceX = Mathf.Abs( targetRect.anchoredPosition.x-fishRect.anchoredPosition.x);
            Debug.Log("水平距离：" + distanceX);
        }
    }
    
    public void SetUserFishData(DashCompetition daparent)
    {
        InitSpine();
        LoadPanelUI();
        curisai = false;
        dashparent = daparent;
        UpdateFishLocation();
        InitUI();
    }

    public void InitUI()
    {
        if (curisai)
        {
            // FishInfoController.Instance.CheckAIPassLevel(fishaiSaveData.aiid,null);
            //
            // if(spinefishitem==null)
            //     spinefishitem = Instantiate(fish02Spine, fishtransparent);
            //
            // spinefishitem.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "idle", true);
            // spinefishitem.GetComponent<SkeletonAnimation>().DOPlay();
            // //fishSpine = Resources.Load<GameObject>("wightfish");
            // userName.text = fishaiSaveData.ainame;
            // userLevel.text = $"{MultilingualManager.Instance.GetString("Level")} {fishaiSaveData.ailevel}";
            // targetcount.text = fishaiSaveData.Puzzleprogress.ToString();
        }
        else
        {
            if(spinefishitem==null)
                spinefishitem = Instantiate(fish01Spine, fishtransparent);
            
            
            spinefishitem.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "idle", true);
            spinefishitem.GetComponent<SkeletonAnimation>().DOPlay();
          
            //fishIcon.sprite = LoadtaskIcon("rightfish");
            //if (string.IsNullOrEmpty(GameDataManager.instance.UserData.UserName))
            {
                //GameDataManager.instance.UserData.UserName = FishInfoController.Instance.GeneratePlayerName();
                //ThinkManager.instance.SetRoleName(GameDataManager.instance.UserData.UserName);
            }
            //userName.text = GameDataManager.instance.UserData.UserName;
            userLevel.text= $"{MultilingualManager.Instance.GetString("Level")} {GameDataManager.instance.UserData.CurrentStage}"; 
            //targetcount.text = GameDataManager.instance.FishUserSave.Puzzleprogress.ToString();
        }
        ainamebg.gameObject.SetActive(curisai);
       
        isclaim = false;

        userLevel.transform.DOMoveZ(0, 1f).OnComplete(() =>
        {
            FishMove(out float waittime);
            
            userLevel.transform.DOMoveZ(0,waittime-0.4f).OnComplete(() =>
            {     
                //int rank = curisai ?fishaiSaveData.rank:GameDataManager.instance.FishUserSave.rank;
            
                // switch (rank)
                // {
                //     case 1:
                //         wordbg.sprite = LoadtaskIcon("targetred");
                //         RankImage.sprite= LoadtaskIcon("rank1");
                //         RankImage.gameObject.SetActive(true);
                //         break;
                //     case 2:
                //         wordbg.sprite = LoadtaskIcon("targetblue");
                //         RankImage.sprite = LoadtaskIcon("rank2");
                //         RankImage.gameObject.SetActive(true);
                //         break;
                //     case 3:
                //         wordbg.sprite = LoadtaskIcon("targetgreen");
                //         RankImage.sprite = LoadtaskIcon("rank3");
                //         RankImage.gameObject.SetActive(true);
                //         break;
                //     case 4:
                //     case 5:
                //         wordbg.sprite = LoadtaskIcon("targetgrey");
                //         RankImage.gameObject.SetActive(false);
                //         break;
                //
                // }
            });
            
        });
    }

    public void UpdateUI(bool ismove=true)
    {
        float duration = ismove ? 1.0f : 0.5f;
        if (curisai)
        {
            // if (!FishInfoController.Instance.RoundFishIsOver())
            // {
            //     // FishInfoController.Instance.CheckAIPassLevel(fishaiSaveData.aiid,()=>
            //     // {
            //     //     FishMove(out float waittime, true);
            //     //     duration=waittime;
            //     // });
            //     // userLevel.text = $"{MultilingualManager.Instance.GetString("Level")} {fishaiSaveData.ailevel}";
            //     // targetcount.text = fishaiSaveData.Puzzleprogress.ToString();
            // }
            //if(offlevel>fishaiSaveData.ailevel)
            //    FishMove();
        }
        else
        {
            //userName.text = GameDataManager.instance.UserData.UserName;
            userLevel.text = $"{MultilingualManager.Instance.GetString("Level")} {GameDataManager.instance.UserData.CurrentStage}";
            //targetcount.text = GameDataManager.instance.FishUserSave.Puzzleprogress.ToString();     
            // userLevel.transform.DOMoveZ(0, 0.5f).OnComplete(() =>
            // {
                FishMove(out float waittime, ismove);
                duration=waittime;
            //});
        }
        
        userLevel.transform.DOMoveZ(0,duration).OnComplete(() =>
        {
            int rank = 0;
            if (curisai)
            {
                //rank = fishaiSaveData.Puzzleprogress>0? fishaiSaveData.rank:0;
            }
            else
            {
                //rank = GameDataManager.instance.FishUserSave.Puzzleprogress>0? GameDataManager.instance.FishUserSave.rank:0;
            }

            switch (rank)
            {
                case 1:
                    wordbg.sprite = LoadtaskIcon("targetred");
                    RankImage.sprite = LoadtaskIcon("rank1");
                    RankImage.gameObject.SetActive(true);
                    break;
                case 2:
                    wordbg.sprite = LoadtaskIcon("targetblue");
                    RankImage.sprite = LoadtaskIcon("rank2");
                    RankImage.gameObject.SetActive(true);
                    break;
                case 3:
                    wordbg.sprite = LoadtaskIcon("targetgreen");
                    RankImage.sprite = LoadtaskIcon("rank3");
                    RankImage.gameObject.SetActive(true);
                    break;
                case 4:
                case 5:
                case 0:
                    wordbg.sprite = LoadtaskIcon("targetgrey");
                    RankImage.gameObject.SetActive(false);
                    break;
            }               
        });
    }

    private void FishMove(out float waittime,bool isneedmove=true)
    {
        float point = 0, targetx = 0;
        // if (fishaiSaveData != null)
        // {
        //     point= fishaiSaveData.Puzzleprogress /(float) AppGameSettings.FishTargetWordCount;
        // }
        // else
        // {
        //     point= GameDataManager.instance.FishUserSave.Puzzleprogress /(float) AppGameSettings.FishTargetWordCount;
        // }

        if (point > 0&&isneedmove)
        {
            targetx=distanceX*point+startx;
            
            if(Mathf.Approximately(fishRect.anchoredPosition.x, targetx)) {
                Debug.LogWarning("目标位置相同");
                waittime = 0;
                return ;
            }            
            
            SkeletonAnimation fishSpine = spinefishitem.GetComponent<SkeletonAnimation>();
            // 播放动画并记录动画时长
            TrackEntry runTrack = fishSpine.AnimationState.SetAnimation(0, "run", true); // 关键点：设置为不循环
            fishSpine.DOPlay();
                
            // 计算移动所需时间（根据固定速度）
            float currentX = fishRect.anchoredPosition.x;
            float distance = Mathf.Abs(targetx - currentX);
            //float moveDuration = distance / moveSpeed;
            float moveDuration = point <= 0.5f ?1f:2.5f;

            // 开始移动
            fishRect.DOAnchorPosX(targetx, moveDuration).OnComplete(() =>
            {
                // 强制切换到idle动画
                TrackEntry idleTrack= fishSpine.AnimationState.SetAnimation(0, "idle", true);
                fishSpine.DOPlay();                   
            });
            waittime = moveDuration;
            return;
        }
        waittime = 0;
    }
    
    // private float CalculateMoveDuration(float from, float to) {
    //     float distance = Mathf.Abs(to - from);
    //     return distance / moveSpeed;
    // }

    public void FlyBoxToTarget(int rank)
    {
        GameObject start =  dashparent.oneBoxImage.gameObject;
        switch (rank)
        {
            case 1:
                start =  dashparent.oneBoxImage.gameObject;
                break;
            case 2:
                start =  dashparent.twoBoxImage.gameObject;
                break;
            case 3:
                start =  dashparent.threeBoxImage.gameObject;
                //GameDataManager.instance.FishUserSave.isRoundOver = true;
                break;
        }
        
        if (!curisai)
        {
            //GameDataManager.instance.FishUserSave.isRoundOver = true;
            isclaim=true;
        }
        else
        {
            //fishaiSaveData.iscliam = true;
            isclaim=true;
        }
        box.sprite = start.GetComponent<Image>().sprite;
        box.gameObject.SetActive(true);
        box.transform.DOLocalMoveX(-52,0.3f);
        //FlyInManager.Instance.FlyInBox(start.transform, box, start, null);
    } 

    private Sprite LoadtaskIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }

    private void HideBosSpine()
    {
        box.transform.DOLocalMoveX(60,0f);
        box.gameObject.SetActive(false);
        // if (box.childCount>0)
        // {
        //     for (int i = box.childCount - 1; i >= 0; i--)
        //     {
        //         Transform child = box.GetChild(i);
        //         child.gameObject.SetActive(false);
        //         Destroy(child.gameObject);
        //     }
        // }
    }

    public void OnHide()
    {
        // if (string.IsNullOrEmpty(GameDataManager.instance.FishUserSave.roundstarttime))
        // {
        //     fishRect.anchoredPosition = new Vector2(startx, fishRect.anchoredPosition.y);
        //     isclaim = false;
        //     if (curisai&&fishaiSaveData!=null)
        //     {               
        //         fishaiSaveData.iscliam = false;
        //         fishaiSaveData.Puzzleprogress = 0;
        //         fishaiSaveData.rank = 0;
        //     }
        //     else
        //     {
        //         GameDataManager.instance.FishUserSave.Puzzleprogress = 0;
        //         GameDataManager.instance.FishUserSave.rank = 0;
        //     }
        //     
        //     targetcount.text = "0";
        //     wordbg.sprite = LoadtaskIcon("targetgrey");
        //     RankImage.gameObject.SetActive(false);
        //     HideBosSpine();
        // }
        // else
        // {
        //     FishMove(out float waittime, false);
        // }
        
        transform.GetComponent<Animator>().enabled = false;
        wordbg.transform.localScale = Vector3.zero;
        NameImage.GetComponent<RectTransform>().anchoredPosition=new Vector2(-138f,72.1f);
    }
}