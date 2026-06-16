using UnityEngine;
using UnityEngine.UI;
using System.Net.Mail;
using System.Net;
using System;
using System.IO;


public class DebugMenu : UIWindow
{
    [SerializeField] private Button CloseBtn;

    [SerializeField] private Button PassStageBtn; // 一键通关     
    [SerializeField] private Button ReSetGameBtn; //清空存档
    [SerializeField] private Button MailBtn; 

    [SerializeField] private Button AddGoldBtn; //增加金币
    [SerializeField] private Button EnterStageBtn; // 跳关   
    [SerializeField] private Button ChessStageBtn; // 跳关   
    [SerializeField] private Button ChessEBtn; // 拼字E值   
    [SerializeField] private Button AddResetToolBtn; //重置道具
    [SerializeField] private Button AddHintToolBtn; //提示道具
    [SerializeField] private Button AddButterflyToolBtn; //蝴蝶道具
    [SerializeField] private Button setAbBtButton; //设置AB包
    [SerializeField] private Button SeeAdsBtn; //蝴蝶道具
    [SerializeField] private Button FindPuzzleBtn; //蝴蝶道具
    [SerializeField] private Button OnlineTimeBtn; //蝴蝶道具
    [SerializeField] private Button LightLimtBtn; //蝴蝶道具
    [SerializeField] private Button UseButterflyBtn; //蝴蝶道具
    [SerializeField] private Button ShopBuyBtn;
    [SerializeField] private Button AddPupaBtn;
    [SerializeField] private Button AddGoldLeafBtn;
    [SerializeField] private Toggle AutoToggle;

    public InputField EmailText; 
    public Text FPSText; 
    public Text logText; // 用于显示日志信息的 UI 文本 
    private bool isRebuilding = false;
    private string ABName;


    // private float deltaTime;
    // private int frameCount;
    private float totalTime;

    protected override void Awake()
    {
        base.Awake();
        InitializeButtons();
        //detailPanel.SetActive(false); // 隐藏详细信息面板
    }

    protected override void OnEnable()
    {           
        // 注册日志回调
        Application.logMessageReceived += HandleLog;
        HandleLog("","",LogType.Log);
        InitUIData();
    }

    protected void InitializeButtons()
    {
        AutoToggle.onValueChanged.AddListener(OnAutoToggleValueChanged);
        CloseBtn.AddClickAction(OnCloseBtn);
        MailBtn.AddClickAction(SendMail);
        EnterStageBtn.AddClickAction(OnEnterStageClick);
        ChessStageBtn.AddClickAction(OnChessStageClick);
        AddGoldBtn.AddClickAction(OnAddGoldClick);
        AddResetToolBtn.AddClickAction(AddResetCountClick);
        AddHintToolBtn.AddClickAction(AddHintCountClick);
        AddButterflyToolBtn.AddClickAction(AddButterflyCountClick);
        ReSetGameBtn.AddClickAction(OnReSetClick);
        PassStageBtn.AddClickAction(OnPassStageClick);
        SeeAdsBtn.AddClickAction(OnSeeAdsClick);
        FindPuzzleBtn.AddClickAction(OnFindPuzzleClick);
        OnlineTimeBtn.AddClickAction(OnLineTimeTaskClick);
        LightLimtBtn.AddClickAction(OnLightLimitClick);
        UseButterflyBtn.AddClickAction(OnUserButterflyClick);
        ShopBuyBtn.AddClickAction(OnShopBuyClick);
        AddPupaBtn.AddClickAction(OnAddPupaClick);
        ChessEBtn.AddClickAction(OnChessEnergyClick);
        setAbBtButton.AddClickAction(OnSetABBtnClick);
        AddGoldLeafBtn.AddClickAction(OnAddGoldLeafClick);
    }

    private void InitUIData()
    {
        InitBtnData(AddGoldBtn,"100");
        InitBtnData(AddResetToolBtn, "10");
        InitBtnData(AddHintToolBtn, "10");
        InitBtnData(EnterStageBtn, "10");
        InitBtnData(AddButterflyToolBtn, "10");
        InitBtnData(SeeAdsBtn, "10");
        InitBtnData(FindPuzzleBtn, "10");
        InitBtnData(OnlineTimeBtn, "10");
        InitBtnData(LightLimtBtn, "10");
        InitBtnData(UseButterflyBtn, "10");
        InitBtnData(ShopBuyBtn, "10");
        InitBtnData(AddPupaBtn, "10");
        InitBtnData(AddGoldLeafBtn, "10");
        ChessEBtn.GetComponentInChildren<InputField>().text = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue.ToString("0.00");
        ChessStageBtn.GetComponentInChildren<InputField>().text = GameDataManager.Instance.UserData.CurrentChessStage.ToString();
        
        string bagName=GameDataManager.Instance.UserData.ABName=="1"?"B 包":"A 包";
        logText.text = "拼字玩法当前为"+bagName+"\n 其中0为A包 1为B包"; // 清空 UI 文本
    }

    private void OnAutoToggleValueChanged(bool value)
    {
        GameCoreManager.Instance.SetAutoLevelTalbe(value);
    }
    
    
    private void OnSetABBtnClick()
    {
        if (GameDataManager.Instance.UserData.ABName == "1")
        {
            GameDataManager.Instance.UserData.ABName = "0";
        }else 
            GameDataManager.Instance.UserData.ABName = "1";
        
        string bagName=GameDataManager.Instance.UserData.ABName=="1"?"B 包":"A 包";
        ChessStageController.Instance.Initialized();
        ABName = GameDataManager.Instance.UserData.ABName;
        logText.text = "拼字玩法当前为"+bagName+"\n 其中0为A包 1为B包"; // 清空 UI 文本
    }
    
    private void OnAddGoldLeafClick()
    {
        InputField Stagenumtxt = AddGoldLeafBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        GameDataManager.Instance.UserData.UpdateGoldLeaf(value);
        MessageSystem.Instance.ShowTip($"添加成功 {value} 个");
    }

    private void InitBtnData(Button button, string count)
    {
        InputField Stagenumtxt = button.GetComponentInChildren<InputField>();
        string value = Stagenumtxt.text;
        if (string.IsNullOrEmpty(value))
        {
            Stagenumtxt.text = count;
        }
    }
    private void OnAddPupaClick()
    {
        InputField Stagenumtxt = AddPupaBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        GameDataManager.Instance.ButterflyData.AddPupa(value);
        MessageSystem.Instance.ShowTip($"添加成功 {value} 个");
    }
    private void OnShopBuyClick()
    {
        InputField Stagenumtxt = ShopBuyBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedShopBuy,value);
    }
    
    private void OnUserButterflyClick()
    {
        InputField Stagenumtxt = UseButterflyBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseButterflyTool,value);
    }
    
    private void OnLightLimitClick()
    {
        InputField Stagenumtxt = LightLimtBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedLightLimit,value);
    }
    
    private void OnLineTimeTaskClick()
    {
        InputField Stagenumtxt = OnlineTimeBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedOnlineTime,value);
    }

    private void OnSeeAdsClick()
    {
        InputField Stagenumtxt = SeeAdsBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedSeeAds,value);
    }
    
    private void OnFindPuzzleClick()
    {
        InputField Stagenumtxt = FindPuzzleBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedFindWord,value);
        LimitTimeManager.Instance.UpdateLimitProgress(value);
        //GameDataManager.instance.FishUserSave.UpdateFishProgress(value);
    }
    private void OnChessEnergyClick()
    {
        InputField Stagenumtxt = ChessEBtn.GetComponentInChildren<InputField>();
        float value = float.Parse(Stagenumtxt.text);
        GameDataManager.Instance.ChessDynamicHardSave.SetEnergy(value);
        GameDataManager.Instance.ChessDynamicHardSave.SaveData();
    }
    private void OnPassStageClick()
    {
        GameDataManager.Instance.UserData.UpdateHexStage();
        //EventManager.OnChangeLanguageUpdateUI?.Invoke();
        MessageSystem.Instance.ShowTip("通关成功！");
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedPassLevel,1);
    }

    private void OnReSetClick()
    {           
        GameDataManager.Instance.WipeAllGameData();            
        //EventDispatcher.instance.TriggerChangeGoldUI(0,false);
        //EventDispatcher.OnChangeLanguageUpdateUI?.Invoke();
        StageHexController.Instance.LimitPuzzlecount = 0;
        LimitTimeManager.Instance.UpdateLimitTimeBtnUI();
        //AdsManager.Instance.HideBannerAd();
        WaterManager.instance.ClearWater();
        DailyTaskManager.Instance.GetTaskSaveData();
        DailyTaskManager.Instance.isResetDailyTask = true;

        //同步到服务器
        GameDataManager.Instance.CommitGameData();
    }

    private void AddResetCountClick()
    {
        InputField Stagenumtxt = AddResetToolBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.SingleWordTipsttool, value);
        //EventManager.OnChangeLanguageUpdateUI?.Invoke();
        MessageSystem.Instance.ShowTip("重置道具增加成功！");
    }

    private void AddButterflyCountClick()
    {
        InputField Stagenumtxt = AddButterflyToolBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, value);
        //EventManager.OnChangeLanguageUpdateUI?.Invoke();
        MessageSystem.Instance.ShowTip("蝴蝶道具增加成功！");
    }
    
    private void AddHintCountClick()
    {
        InputField Stagenumtxt = AddHintToolBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, value);
        //EventManager.OnChangeLanguageUpdateUI?.Invoke();
        MessageSystem.Instance.ShowTip("提示道具增加成功！");
    }

    private void OnAddGoldClick()
    {
        InputField Stagenumtxt = AddGoldBtn.GetComponentInChildren<InputField>();
        int Stagenum = int.Parse(Stagenumtxt.text);
        GameDataManager.Instance.UserData.UpdateGold(Stagenum);

        MessageSystem.Instance.ShowTip("金币增加成功！");
    }

    private void OnEnterStageClick()
    {
        InputField Stagenumtxt = EnterStageBtn.GetComponentInChildren<InputField>();
        int Stagenum = int.Parse(Stagenumtxt.text);
        
        if (Stagenum < 1)
        {
            MessageSystem.Instance.ShowTip("关卡编号无效");
        }
        
        //设置关卡数据 向前跳转关卡后，进度需要跟关卡同步；向后跳关不需要同步
        if (Stagenum > GameDataManager.Instance.UserData.CurrentHexStage)
        {
            GameDataManager.Instance.UserData.UpdateHexStage(Stagenum,true);
        }
        StageHexController.Instance.IsFirstEnterStage = true;
        StageHexController.Instance.SetStageData(Stagenum);
        StageHexController.Instance.IsGMEnterStage = true;

        OnPlayClick();
        //EventManager.RequestChangeBack(true);
    }
    private void OnChessStageClick()
    {
        InputField Stagenumtxt = ChessStageBtn.GetComponentInChildren<InputField>();
        int Stagenum = int.Parse(Stagenumtxt.text);
        
        if (Stagenum < 1)
        {
            MessageSystem.Instance.ShowTip("关卡编号无效");
        }
        
        //设置关卡数据 向前跳转关卡后，进度需要跟关卡同步；向后跳关不需要同步
        if (Stagenum > GameDataManager.Instance.UserData.CurrentChessStage)
        {
            GameDataManager.Instance.UserData.UpdateChessStage(Stagenum,true);
        }
        ChessStageController.Instance.SetStageData(Stagenum);

        SystemManager.Instance.HidePanel(PanelType.HeaderSection,true,()=> SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea));
        SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        Close();
        //EventManager.RequestChangeBack(true);
    }
    private void OnPlayClick()
    {
        SystemManager.Instance.HidePanel(PanelType.HeaderSection,true,EnterStageClick);
        SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        Close();
    }

    private void EnterStageClick()
    {
        //StageController.Instance.SetStageData(StageController.Instance.CurStageData.StageId);
        SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
        //EventManager.RequestChangeBack(true);
    }
    
    
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (isRebuilding) return; // 如果正在重建，则直接返回

        isRebuilding = true;

        // 添加新日志信息
        LogManager.Instance.logBuilder.AppendLine(logString);
        // 限制文本长度，避免文本过大
        const int maxLogLength = 8000; // 设置最大日志长度
        if (LogManager.Instance.logBuilder.Length > maxLogLength)
        {
            // 删除旧的内容，保留最新的部分
            LogManager.Instance.logBuilder.Remove(0, LogManager.Instance.logBuilder.Length - maxLogLength);
        }

        // 更新 UI 文本
        if (logText != null)
        {
            logText.text = LogManager.Instance.logBuilder.ToString();
        }
        isRebuilding = false;
    }
    
    
       private async void SendMail()
        {        
            // 日志文件路径           
            string logFilePath = Path.Combine(Application.persistentDataPath, "logs/log_0.txt");

            // 设置 SMTP 服务器信息
            string smtpAddress = "smtp.qq.com"; // QQ 邮箱的 SMTP 地址
            int portNumber = 587; // 使用 TLS 的端口号
            bool enableSSL = true; // 是否启用 SSL

            // 发送者和接收者的电子邮件地址
            string emailFrom = "f2608544640@foxmail.com"; // 发送者的QQ邮箱
            string password = "grwdqvewgxwzeagc"; // QQ邮箱的授权码
            string emailTo = "f2608544640@foxmail.com"; // 接收者的邮箱
            string subject = EmailText.text;
            string body = "Please find the attached log file.";

            MessageSystem.Instance.ShowTip("邮件已发送！");
            MailBtn.enabled = false;

            // 创建邮件
            using (MailMessage mail = new MailMessage())
            {
                mail.From = new MailAddress(emailFrom);
                mail.To.Add(emailTo);
                mail.Subject = subject;
                mail.Body = body;
                mail.IsBodyHtml = true; // 如果邮件内容是 HTML 格式，设置为 true

                // 添加日志文件作为附件
                if (File.Exists(logFilePath))
                {
                    Attachment attachment = new Attachment(logFilePath);
                    mail.Attachments.Add(attachment);
                }
                else
                {
                    Console.WriteLine("Log file does not exist.");
                    MailBtn.enabled = true;
                    return;
                }

                // 发送邮件
                using (SmtpClient smtp = new SmtpClient(smtpAddress, portNumber))
                {
                    smtp.Credentials = new NetworkCredential(emailFrom, password);
                    smtp.EnableSsl = enableSSL; // 启用 SSL
                    //try
                    //{
                        await smtp.SendMailAsync(mail);  // 使用异步发送邮件
                    //}
                    //catch (Exception ex)
                    //{
                    //    Console.WriteLine("Error sending email: " + ex.Message);
                    //}
                }
            }

            MailBtn.enabled = true;
        }


    public void ShowDetail(string logEntry)
    {
        //detailText.text = logEntry; // 显示详细信息
        //detailPanel.SetActive(true); // 显示详细信息面板
    }

    public void ClearLogs()
    {
        //LogSystem.Instance.logBuilder.Clear();
        if (logText != null)
        {
            logText.text = string.Empty; // 清空 UI 文本
        }
        //File.WriteAllText(logFilePath, string.Empty); // 清空文件
    }

    public void HideDetailPanel()
    {
        //detailPanel.SetActive(false); // 隐藏详细信息面板
    }

    private void OnCloseBtn()
    {
        SystemManager.Instance.HidePanel(PanelType.DebugMenu);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Application.logMessageReceived -= HandleLog;
    }

}



