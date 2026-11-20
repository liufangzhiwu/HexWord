using System;
using System.Collections;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class TaskTable : MonoBehaviour
{
    public Button TaskBtn;
    public GameObject LockObj; //挂载锁头物体
    [SerializeField] private Image TaskOver;
    public GameObject taskEffect;
    public Text taskTime;
    [SerializeField] private Image taskyezi;
    [SerializeField] private GameObject TaskClaim;

    private void Start()
    {
        InitButton();
    }
    
    private void InitButton()
    {
        TaskBtn.AddClickAction(OnTaskClick);
    }
    
    private void OnTaskClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.DailyTasksScreen);
        
        AchievementManager.Instance.DisableFinishAchieveTable();
    }
    
    public void CheckTasksScreen()
    {
        if (!DailyTaskManager.Instance.IsOpen()) return;
        
            DailyTaskManager.Instance.OnDailyTaskBtnUI += InitTaskBtnUI;                
            TaskBtn.gameObject.SetActive(true);
            // ==============================
            // 👉 新增：每日任务的精准防蹭分逻辑 (与限时、捕鱼保持绝对一致)
            // ==============================
            bool isJustUnlocked = false;
        
            // 针对拼字模式 (levelMode == 2) 进行防蹭分
            if (GameDataManager.Instance.UserData.levelMode == 2) 
            {
                int playedStage = ChessStageController.Instance.CurrStageData.StageId; // 刚打完的关卡
                int unlockStage = AppGameSettings.UnlockRequirements.DailyMissions;    // 每日任务解锁关卡
            
                // 判定逻辑：只有当玩家刚好打完门槛前一关，且底层进度刚好迈入了解锁关卡，才是“刚解锁的新手”
                if (playedStage == unlockStage - 1 && GameDataManager.Instance.UserData.CurrentChessStage == unlockStage)
                {
                    isJustUnlocked = true;
                    Debug.Log($"[每日任务防蹭分] 刚打完第{playedStage}关新鲜解锁每日任务，绝不将本局进度带入！");
                }
            }
        
            // 如果是被抓包的新鲜解锁玩家，只刷新UI，直接 return 绝不加分！
            if (isJustUnlocked)
            {
                InitTaskBtnUI(); 
                return;
            }
            if (GameDataManager.Instance.UserData.levelMode == 1)
            {
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedFindWord,StageHexController.Instance.LimitPuzzlecount);

            }else if (GameDataManager.Instance.UserData.levelMode == 2)
            {
                int puzzlecount = ChessStageController.Instance.LimitPuzzleCount;
                Debug.Log("找出的词语数量: "+puzzlecount );
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedFindWord,puzzlecount);
            }
            if (GameDataManager.Instance.UserData.levelMode == 3)
            {
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedFindWord,StageHexController.Instance.LimitPuzzlecount);
            }
            
            if(DailyTaskManager.Instance.UpdatetaskItem.Count > 0)
                StartCoroutine(ShowTaskWordAnim());
    }
    
    /// <summary>
    /// 播放任务进度更新动画
    /// </summary>
    IEnumerator ShowTaskWordAnim()
    { 
        yield return new WaitForSeconds(1f);
        GameObject taskye = Instantiate(taskyezi.gameObject,taskyezi.transform);
        taskye.transform.localScale=new Vector3(0.8f,0.8f,0.8f);
        taskye.transform.SetAsLastSibling();
        taskye.transform.localPosition=new Vector3(165f,165f,0f);
        CanvasGroup canvas = taskye.GetComponent<CanvasGroup>();
        if (canvas == null)
        {
            canvas = taskye.AddComponent<CanvasGroup>();
        }
        canvas.alpha = 0f;
        var midPos = (taskyezi.transform.localPosition + taskye.transform.localPosition) / 2;
        var BezierMidPos = (midPos + taskye.transform.localPosition) / 2 + Vector3.left * 50;
        Vector3[] MovePoints = CustomFlyInManager.Instance.CreatTwoBezierCurve(taskye.transform.localPosition,taskyezi.transform.localPosition,BezierMidPos).ToArray();
        
        canvas.DOFade(1, 0.3f).OnComplete(() =>
        {
            taskye.transform.DOLocalPath(MovePoints, 0.5f).OnComplete(() =>
            {
                taskEffect.gameObject.SetActive(true);
                Destroy(taskye);
                InitTaskBtnUI();   
            });
            
            canvas.DOFade(1, 0.4f).OnComplete(() =>
            {
                AudioManager.Instance.PlaySoundEffect("levelOverLimitwordAward");
                taskyezi.transform.DOScale(new Vector3(1.2f,1.15f,1.15f), 0.3f).OnComplete(() =>
                {
                    taskEffect.gameObject.SetActive(false);
                    taskyezi.transform.DOScale(Vector3.one, 0.2f);
                });
            });
        });
    }
    
    /// <summary>
    /// 结算页「每日任务」入口三态（与主页 PrimaryInterface.CheckTasksBtnUI 同一口径）：
    ///   1) 进度（六边形 / 拼字取大）未到展示关卡 → 整个按钮隐藏；
    ///   2) 到了展示关卡但还没解锁 → 显示带锁态（锁头 + 「第31关」，图标半透明、不可点）；
    ///   3) 已解锁 → 正常显示。
    /// ★ bug101：以前 StageFinishView 用 UnlockButton 按「解锁关卡」直接显隐，
    ///   进度卡在「展示关卡～解锁关卡」之间时，结束页完全没有任务按钮，回主页却有，故统一到这里。
    /// </summary>
    /// <param name="showLevel">展示关卡（AppGameSettings.ShowRequirements.DailyMissionsShow）</param>
    public void RefreshEntryState(int showLevel)
    {
        if (TaskBtn == null || GameDataManager.Instance == null || GameDataManager.Instance.UserData == null) return;

        int progress = Mathf.Max(GameDataManager.Instance.UserData.CurrentHexStage,
                                 GameDataManager.Instance.UserData.CurrentChessStage);
        bool isOpen = DailyTaskManager.Instance.IsOpen();

        // 1. 显隐：未到展示关卡直接隐藏
        bool isShow = progress > showLevel || isOpen;
        TaskBtn.gameObject.SetActive(isShow);
        if (!isShow) return;

        // 2. 锁定态：锁头 + 半透明图标 + 不可点
        bool isLocked = !isOpen;
        TaskBtn.interactable = !isLocked;
        if (LockObj != null) LockObj.SetActive(isLocked);

        if (TaskBtn.transform.childCount > 0)
        {
            Image icon = TaskBtn.transform.GetChild(0).GetComponent<Image>();
            if (icon != null)
            {
                Color c = icon.color;
                c.a = isLocked ? 0.5f : 1f;
                icon.color = c;
            }
        }

        // 3. 文案与领取态：未解锁分支会写「第31关」并铺底板
        InitTaskBtnUI();

        // 解锁瞬间报一声（与 ChessFinishView.UnlockButton 的表现一致）
        if (!isLocked && progress == AppGameSettings.UnlockRequirements.DailyMissions)
            AudioManager.Instance.PlaySoundEffect("BtnUnlock");
    }

    private void InitTaskBtnUI()
    {
        if (!DailyTaskManager.Instance.IsOpen())
        {
            taskTime.gameObject.SetActive(true);
            taskTime.text = $"第{AppGameSettings.UnlockRequirements.DailyMissions}关";
            
            if (taskTime.transform.parent != null)
            {
                Image bg = taskTime.transform.parent.GetComponent<Image>();
                if (bg != null) bg.enabled = true;
            }
            
            TaskClaim.gameObject.SetActive(false);
            TaskOver.gameObject.SetActive(false);
            return;
        }
        
        if (!DailyTaskManager.Instance.IsAllComplete())
        {
            if (!DailyTaskManager.Instance.IsClaim())
            {
                if (TaskClaim.activeSelf)
                {
                    TaskClaim.gameObject.SetActive(false);
                    TaskClaim.GetComponent<CanvasGroup>().alpha = 0;
                }
            }
            else
            {
                TaskClaim.gameObject.SetActive(true);
                TaskClaim.GetComponent<CanvasGroup>().DOFade(1,0.2f);
            }
            TaskOver.gameObject.SetActive(false);
        }
        else
        {
            TaskClaim.gameObject.SetActive(false);
            TaskOver.gameObject.SetActive(true);
        }
    }
    
    
    public void UpdateTimeDisplay(string time)
    {
        if (!string.IsNullOrEmpty(time))
        {
            taskTime.text = time; // 更新文本
        }
    }

}
