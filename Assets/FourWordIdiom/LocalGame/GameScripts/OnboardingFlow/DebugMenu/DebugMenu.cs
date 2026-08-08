using UnityEngine;
using UnityEngine.UI;
using System.Net.Mail;
using System.Net;
using System;
using System.Collections;
using System.IO;
using Middleware;
using OpenHarmonyKits.Signal;
using UnityEngine.Networking;


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
    [SerializeField] private Button AddStreakWinDays;
    [SerializeField] private Button GetAllHeadIcons;
    [SerializeField] private Button PushTestBtn;
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
        AddStreakWinDays.AddClickAction(OnAddStreakWinDays);
        GetAllHeadIcons.AddClickAction(OnGetAllHeadIcons);
        PushTestBtn.AddClickAction(OnPushTestBtn);
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
        InitBtnData(AddStreakWinDays, "10");
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
    
    private void OnGetAllHeadIcons()
    { 
        for (int i = 25; i < 40; i++)
        {
            GameDataManager.Instance.UserData.AddHeadIcon(i);
        }
    }
    
    private void OnPushTestBtn()
    {
       Game.self.Pushs.Push("成语消：禅意之境","成语消：禅意之境 推送测试！！！！！");
    }
    
         /// <summary>
        /// 发送推送消息（需先获取到Token）
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="body">通知内容</param>
        public void Push(string title, string body)
        {
            if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.PushToken))
            {
                Debug.LogError("PushManager Push Token is null or empty. Please call GetToken() first.");
                return;
            }
            StartCoroutine(SendPostRequest(title, body));
        }
       

        /// <summary>
        /// 向华为Push Kit服务端发送推送请求（模拟服务端行为）
        /// </summary>
        IEnumerator SendPostRequest(string title, string body)
        {
            // 设置用于JWT签名的公私钥（请替换为您自己的密钥）
            OHPushHelper.PrivateKeyPem =
                @"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"
                    .Replace("\n", "");
            OHPushHelper.PublicKeyPem =
                @"MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAvKW1X8pfiVdj4kfrXW4/XnS4e3a8Nw4SgUD+KGGphPe8ekD8eN84yEuOnqzY0u3am1zEytBCZ3Kj0lICof8tkT4+zSOYeSjYobyYva3Hod3W4wha1RlAmzii1yFfJL/VdRC82KcJ2R4SJ8dm9DFDGbXDlmfhFX+wBCo1Lf4eeXoteWJJSe9XpqNO+xuErXmDwcHgKQ3LUZDLE94gSjKZqnjeb9+HSey1VvKH6kP5G9wfXDTq75STj46gEyH4oz/Dui7ljAh/QW5d5EYsC7bwhz0S+TlTy9eRkKaqYHg5Dljbd51BG/qdsfbkdvIhLolMKt1WB/sqGre0OGk1weUtzP9/Kq2921+fxLMesPJMqNKxXadhkVRCoEFPuuXQdMjEgwlRhW6UC9YAlhZB/US+bLDUTDdDiBcQbPHR3F94vPyBG+9hjZ8dzdjNKWK5HP7MPDsKDmRP4aObaZUiNYO1LVT65ZtwoQdWlCXEYRy65+hPUDtdtWto7ufj8XMoPGjP7kdTCsP2bg2B1+A2MJygwi+V0P5MRDSojHTJLkoK6EDCbx+V8/SNCD6f/ptB1s8zFPBItXhVHy3FdQNXsn6Q7tjYg80E5tgxbm9k41i9M2eT168zESJgxp2WUL0FnciylafbngzE1yMImmmg/eIzrrACWo7OdEeegG4UmHBQZwkCAwEAAQ=="
                    .Replace("\n", "");

            // 请求URL（请替换为您自己的项目ID）
            string url = "https://push-api.cloud.huawei.com/v3/101653523862654451/messages:send";
            string aud = "https://oauth-login.cloud.huawei.com/oauth2/v3/token";

            var request = OHPushHelper.GetPushRequest(url, aud, title, body, GameDataManager.Instance.UserData.PushToken);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[PushManager] Push Error: {request.error}");
            }
            else
            {
                Debug.Log($"[PushManager] Push Response: {request.downloadHandler.text}");
            }
        }
    
    private void OnAddStreakWinDays()
    { 
        InputField Stagenumtxt = AddStreakWinDays.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        
        GameDataManager.Instance.UserData._signSaveData.AddDodaySigneDays(value);

        StreakManager.Instance.UpdateWinStreak();
        
        if (value > 30&&GameDataManager.Instance.UserData._signSaveData.winAwardClaims.Count>=3)
            GameDataManager.Instance.UserData._signSaveData.winAwardClaims.Clear();
       
        
        EventDispatcher.instance.TriggerChangeGoldUI(0, false);
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
        //WaterManager.instance.ClearWater();
        DailyTaskManager.Instance.GetTaskSaveData();
        DailyTaskManager.Instance.isResetDailyTask = true;

        //同步到服务器
        GameDataManager.Instance.CommitGameData();
       
        GameDataManager.Instance.UserData.ABName = ABName;
        ChessStageController.Instance.ClearCurrentLevelSave();
    }

    private void AddResetCountClick()
    {
        InputField Stagenumtxt = AddResetToolBtn.GetComponentInChildren<InputField>();
        int value = int.Parse(Stagenumtxt.text);
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, value);
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
            string emailTo = "Lfzw2025@163.com"; // 接收者的邮箱
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



