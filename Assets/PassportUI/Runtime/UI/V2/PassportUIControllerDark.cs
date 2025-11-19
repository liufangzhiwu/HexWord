using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq;
using Unity.Passport.Runtime.Model;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using Passport;

namespace Unity.Passport.Runtime.UI
{
    public class PassportUIControllerDark : PassportUIBase
    {
        [Header("主面板")] [SerializeField] private WebViewController webView;
        [SerializeField] private GameObject tosPanel;
        [SerializeField] private GameObject reLoginPanel;
        [SerializeField] private GameObject phoneLoginPanel;
        [SerializeField] private GameObject verifyCodePanel;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private PanelManager realNamePanel;

        [Header("公司信息")] [SerializeField] private List<Image> companyIconList = new(); // 公司图标
        [SerializeField] private List<RectTransform> companyIconRectList = new();
        [SerializeField] private List<GameObject> companyNameList = new(); // 公司名称

        [Header("历史账号面板")] [SerializeField] private TMP_Dropdown dropDown;
        [SerializeField] private List<GameObject> jumpToReLoginPanelButtons; // 跳转到重新登录面板按钮列表
        [SerializeField] private GameObject reLoginTosLink; // 协议外链

        [Header("登录按钮预制件")] [SerializeField] private GameObject applePrefab;
        [SerializeField] private GameObject phonePrefab;
        [SerializeField] private GameObject wechatPrefab;
        [SerializeField] private GameObject qqPrefab;
#if LOGIN_TAPTAP
        [SerializeField] private GameObject tapTapPrefab;
#endif
#if LOGIN_HYKB
        [SerializeField] private GameObject hykbPrefab;
#endif

        [SerializeField] private Sprite reLoginWechatSprite;
        [SerializeField] private Sprite reLoginPhoneSprite;
        [SerializeField] private Sprite reLoginAppleSprite;
        [SerializeField] private Sprite reLoginQQSprite;
#if LOGIN_TAPTAP
        [SerializeField] private Sprite reLoginTapTapSprite;
#endif
#if LOGIN_HYKB
        [SerializeField] private Sprite reLoginHykbSprite;
#endif
        private int companyIconHeight = 120;

        [Header("实名认证面板")]
        // 实名认证填写框
        [SerializeField]
        private TMP_InputField nameInput;

        [SerializeField] private TMP_InputField idInput;
        [SerializeField] private TMP_InputField phoneNumberInput;
        [SerializeField] private CodeInput verifyCodeInput;

        [SerializeField] private CountDownButton verifyCodeButton;
        [SerializeField] private TextMeshProUGUI verifyCodeHint;

        private const string CallingAreaCode = "+86";
        private PassportTos _tos = new();
        private UserStatus _status = 0;
        private string PhoneNumber => CallingAreaCode + phoneNumberInput.text;
        private List<UserLoginInfo> _userList;
        private int _selectedUserIndex;
        private bool _needAcceptTosError;
        private UserStatus CurrentUserStatus => _status;
        private LoginType _currentLoginType = LoginType.None;

        // Start is called before the first frame update
        public override async Task Init()
        {
            // 关闭所有面板
            tosPanel.SetActive(false);
            reLoginPanel.SetActive(false);
            realNamePanel.Close();
            phoneLoginPanel.SetActive(false);

            try
            {
                Loading(true);
                // 调用 SDK
                var userInfo = await PassportLoginSDK.Init(true);
                if (userInfo != null)
                {
                    UIMessage.Show($"{userInfo.Name} 登录中...");
                    // 手动调用 Login，以显示提示消息
                    await PassportLoginSDK.Identity.Login(userInfo);
                }

                Loading(false);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
                HandleLoginError(e, LoginType.LoginByUserInfo);
                Loading(false);
            }

            // 根据 app 在 uos 上的配置，更新 UI 显示：
            // 获取公司信息
            GetIDDomainInfo();
            // 设置其他登录方式
            SetupOtherWays();

            if (_currentLoginType == LoginType.None)
            {
                // 初始化用户状态并展示对应的面板
                await InitUserStatus();
            }
            
            if (!invokeLoginManually)
            {
                ShowPanel();
            }
        }

        public override void Login()
        {
            ShowPanel();
        }

        public void LoginByWechat()
        {
            // 条件编译
#if UNITY_IOS || UNITY_WEBGL || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR

#if UNITY_IOS
            // iOS 中非 iPad，仍是走微信跳转的逻辑
            var identifier = SystemInfo.deviceModel;
            if (!identifier.StartsWith("iPad"))
            {
                Login(OauthProviderType.Wechat);
                return;
            }
#endif

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                var wechatConfig = PassportLoginSDK.Identity.GetWechatWebLoginConfig();
                var config = new PassportWebWechatConfig()
                {
                    AppID = wechatConfig.AppID,
                    Scope = wechatConfig.Scope,
                    State = wechatConfig.State,
                    RedirectUri = wechatConfig.RedirectUri,
                    HeaderHeight = webView.GetHeadedPixelHeight().ToString(),
                    UnityContainerId = Config.UnityContainerId,
                };

                passportWebWechatLogin(JsonUtility.ToJson(config));
                webView.Open(""); // 只是开启 header

#elif UNITY_STANDALONE_WIN
                var url = PassportLoginSDK.Identity.GetWechatLoginQRCodeUrl();
                Application.OpenURL(url);
#else
                var url = PassportLoginSDK.Identity.GetWechatLoginQRCodeUrl();
                webView.Open(url);
#endif

                // 等待扫码结果
                Login(OauthProviderType.WechatWeb);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
                ShowPanel();
            }
#else
            Login(OauthProviderType.Wechat);
#endif
        }

        public void CancelWechatWebLogin()
        {
            PassportLoginSDK.Identity.CancelWebLogin();
            ShowPanel();
#if UNITY_WEBGL && !UNITY_EDITOR
            passportWebWechatLoginComplete();
#endif
        }

        public Sprite GetSprite(Byte[] bytes)
        {
            //先创建一个Texture2D对象，用于把流数据转成Texture2D
            Texture2D texture = new Texture2D(10, 10);
            texture.LoadImage(bytes); //流数据转换成Texture2D
            //创建一个Sprite,以Texture2D对象为基础
            Sprite sp = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
            return sp;
        }

        private async void Login(OauthProviderType oauthProviderID)
        {
            Loading(true);
            UIMessage.Show("授权中...");
            try
            {
                var loginResult = await PassportLoginSDK.Identity.Login(oauthProviderID, true);
                UIMessage.Show("授权成功");
                OnLoginComplete(loginResult);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
                HandleLoginError(e, LoginType.None);
                ShowPanel();
            }

            if (oauthProviderID == OauthProviderType.WechatWeb)
            {
                // 扫码完成
                webView.Close();
#if UNITY_WEBGL && !UNITY_EDITOR
                passportWebWechatLoginComplete();
#endif
            }

            if (oauthProviderID == OauthProviderType.QQweb)
            {
                webView.Close();
            }

            Loading(false);
        }

        public void ShowPhoneLoginPanel()
        {
            // 重置倒计时逻辑
            verifyCodeButton.ResetCountDown();
            // 清空填写框
            phoneNumberInput.text = "";

            // 切换到手机登录界面
            tosPanel.SetActive(false);
            reLoginPanel.SetActive(false);
            realNamePanel.Close();
            verifyCodePanel.SetActive(false);
            phoneLoginPanel.SetActive(true);
            
            // 自动对焦
            if (!TouchScreenKeyboard.isSupported)
            {
                phoneNumberInput.ActivateInputField();
            }
        }

        private void SendVerifyCodeCallback(bool success, string reason, string phoneNumber)
        {
            verifyCodeButton.StopLoading();

            if (success)
            {
                // 按钮置灰倒计时
                verifyCodeButton.StartCountDown();
                // 提示验证码发送成功
                UIMessage.Show("验证码已发送");
                verifyCodeHint.text = $"验证码已发送至 {phoneNumber}，请输入验证码";

                // TODO: 修改验证码面板的提示文案

                // 展示输入验证码面板
                phoneLoginPanel.SetActive(false);
                verifyCodePanel.SetActive(true);
            }
            else
            {
                // 恢复按钮
                // EnableVerifyCodeButton();
                // 提示验证码发送失败原因
                UIMessage.Show(reason, MessageType.Error);
            }
        }

        /// <summary>
        /// 发送验证码
        /// </summary>
        public async void SendVerifyCode()
        {
            if (PhoneNumber == CallingAreaCode)
            {
                UIMessage.Show("手机号不能为空", MessageType.Error);
                return;
            }

            // 按钮置为 loading 状态
            verifyCodeButton.Loading();

            try
            {
                await PassportLoginSDK.Identity.SendVerifyCode(PhoneNumber);
            }
            catch (PassportException e)
            {
                SendVerifyCodeCallback(e.Code == ErrorCode.Default, e.ErrorMessage, PhoneNumber);
                return;
            }

            SendVerifyCodeCallback(true, "", PhoneNumber);
        }

        /// <summary>
        /// 调用 sdk 进行手机号登录
        /// </summary>
        public async void LoginByPhone()
        {
            string code = verifyCodeInput.GetValue();

            if (code == "")
            {
                UIMessage.Show("验证码不能为空", MessageType.Error);
                return;
            }

            if (PhoneNumber == CallingAreaCode)
            {
                UIMessage.Show("手机号不能为空", MessageType.Error);
                return;
            }

            try
            {
                var loginResult = await PassportLoginSDK.Identity.Login(PhoneNumber, code, true);
                OnLoginComplete(loginResult);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
                
                if (e.Code == ErrorCode.InvalidArgumentPassportVerificationCodeInvalid)
                {
                    // 验证码不正确
                    return;
                }
                HandleLoginError(e, LoginType.None);
                ShowPanel();
            }
        }

        /// <summary>
        /// 进行实名认证
        /// </summary>
        public async void VerifyRealName()
        {
            try
            {
                // 调用 sdk 进行实名认证
                string realName = nameInput.text;
                string id = idInput.text;

                Loading(true);
                await PassportLoginSDK.Identity.VerifyRealName(realName, id);
                Loading(false);

                // 认证成功后，执行以下逻辑
                UpdateStatus(UserStatus.RealName, true);
                ShowPanel();
            }
            catch (PassportException e)
            {
                Loading(false);
                UIMessage.Show(e);
            }
        }

        private async void OnLoginComplete(LoginResult result)
        {
            // 登录失败
            if (result.ErrorMessage != null)
            {
                UIMessage.Show(result.ErrorMessage.Message, MessageType.Error);
                UpdateStatus(UserStatus.LoggedIn, false);
                return;
            }

            UIMessage.Show("登录成功");
            OnCallback(PassportEvent.LoggedIn);

            await CheckUserStatusAfterLoggedIn();
            ShowPanel();
        }

        private async void SetTosLinks()
        {
            var tosInfo = await PassportLoginSDK.Identity.GetTos();
            _tos = tosInfo;
            reLoginTosLink.GetComponent<TOSLink>().Init(tosInfo, "已阅读并同意：", true);
        }

        /// <summary>
        /// 展示协议面板
        /// </summary>
        private async void ShowTosPanel()
        {
            var tosPanelController = tosPanel.GetComponent<TOSPanel>();
            tosPanelController.Loading(true);
            // 加载中
            Loading(true);

            // 获取协议信息
            try
            {
                _tos = await PassportLoginSDK.Identity.GetTos();
                tosPanelController.Init(_tos);

                // 如果协议信息为空，说明用户没有配置，则跳过该步骤
                if (_tos == null)
                {
                    Loading(false);
                    UpdateStatus(UserStatus.Tos, true);
                    ShowPanel();
                    return;
                }

                tosPanelController.Loading(false);
                Loading(false);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
                tosPanelController.Loading(false);
            }
        }

        /// <summary>
        /// 同意协议
        /// </summary>
        /// <returns></returns>
        public async void AcceptTos()
        {
            // 点击同意协议按钮
            try
            {
                await PassportLoginSDK.Identity.AcceptTos(_tos);
                // 成功
                UpdateStatus(UserStatus.Tos, true);
                if (_currentLoginType == LoginType.LoginByUserInfo)
                {
                    var loginResult = await PassportLoginSDK.Identity.Login();
                    OnLoginComplete(loginResult);
                }
                else if (_currentLoginType == LoginType.ReLogin)
                {
                    ReLogin();
                }
                else
                {
                    ShowPanel();
                }

            }
            catch (PassportException e)
            {
                // 失败
                UpdateStatus(UserStatus.Tos, false);
                UIMessage.Show(e);
            }
        }

        /// <summary>
        /// 根据状态码决定展示的面板
        /// </summary>
        private void ShowPanel()
        {
            phoneLoginPanel.SetActive(false);
            tosPanel.SetActive(false);
            reLoginPanel.SetActive(false);
            realNamePanel.Close();
            Loading(false);
            verifyCodePanel.SetActive(false);

            if ((CurrentUserStatus & UserStatus.ReLogin) == UserStatus.ReLogin)
            {
                ShowReLoginPanel();
                UpdateStatus(UserStatus.ReLogin, false);
            }
            else if ((CurrentUserStatus & UserStatus.Tos) != UserStatus.Tos)
            {
                ShowTosPanel();
            }
            else if ((CurrentUserStatus & UserStatus.LoggedIn) != UserStatus.LoggedIn)
            {
                ShowPhoneLoginPanel();
            }
            else if ((CurrentUserStatus & UserStatus.RealName) != UserStatus.RealName)
            {
                realNamePanel.Open();
            }
            else
            {
                // 已完成所有环节
                OnCallback(PassportEvent.Completed);
            }
        }

        /// <summary>
        /// 更新公司名称和图标
        /// </summary>
        private async void GetIDDomainInfo()
        {
            try
            {
                IDDomainResult info = await PassportSDK.Identity.GetIDDomainInfo();
                // 更新公司名称和图标
                foreach (var title in companyNameList)
                {
                    title.GetComponent<TextMeshProUGUI>().text = info.IDDomain.Name;
                }

                LoadCompanyIcon(info.IDDomain.IconUrl);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
            }
        }

        private void UpdateStatus(UserStatus subStatus, bool additive)
        {
            // additive：叠加指定位置的完成态
            if (additive)
            {
                _status |= subStatus;
            }
            else
            {
                _status &= ~subStatus;
            }
        }

        public override void Logout()
        {
            PassportLoginSDK.Identity.Logout();

            // 修改状态
            UpdateStatus(UserStatus.ReLogin, true);
            UpdateStatus(UserStatus.LoggedIn, false);

            ShowPanel();
            OnCallback(PassportEvent.LoggedOut);
        }

        private void SetupOtherWays()
        {
            List<string> phoneLoginPanelWays = new List<string>();

            var enabledIDProviderList = PassportLoginSDK.Identity.GetEnabledIDProviderList();
            
            // 根据平台判断是否使用 web 版本（微信、QQ）
#if UNITY_WEBGL || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR
            // 使用web版本
            if (enabledIDProviderList.ContainsKey(OauthProviderType.WechatWeb))
            {
                phoneLoginPanelWays.Add("Wechat");
            }

            if (enabledIDProviderList.ContainsKey(OauthProviderType.QQweb))
            {
                phoneLoginPanelWays.Add("QQ");
            }

#elif UNITY_IOS
            // iOS 需要区分 ipad 和非 iPad
            var identifier = SystemInfo.deviceModel;
            if ((identifier.StartsWith("iPad") && enabledIDProviderList.ContainsKey(OauthProviderType.WechatWeb)) ||
                (!identifier.StartsWith("iPad") && enabledIDProviderList.ContainsKey(OauthProviderType.Wechat)))
            {
                // 满足以下任一条件则添加按钮
                // 1. iPad 平台且配置了 WechatWeb
                // 2. 非 iPad 平台且配置了 Wechat
                phoneLoginPanelWays.Add("Wechat");
            }
            if ((identifier.StartsWith("iPad") && enabledIDProviderList.ContainsKey(OauthProviderType.QQweb)) ||
                (!identifier.StartsWith("iPad") && enabledIDProviderList.ContainsKey(OauthProviderType.QQ)))
            {
                // 满足以下任一条件则添加按钮
                // 1. iPad 平台且配置了 QqWeb
                // 2. 非 iPad 平台且配置了 Qq
                phoneLoginPanelWays.Add("QQ");
            }
#else
            // 使用非 web 版本
            if (enabledIDProviderList.ContainsKey(OauthProviderType.Wechat))
            {
                phoneLoginPanelWays.Add("Wechat");
            }
            if (enabledIDProviderList.ContainsKey(OauthProviderType.QQ))
            {
                phoneLoginPanelWays.Add("QQ");
            }
#endif

#if UNITY_IOS || UNITY_EDITOR
            if (enabledIDProviderList.ContainsKey(OauthProviderType.Apple))
            {
                phoneLoginPanelWays.Add("Apple");
            }
#endif
#if LOGIN_TAPTAP
            if (enabledIDProviderList.ContainsKey(OauthProviderType.TapTap))
            {
                phoneLoginPanelWays.Add("TapTap");
            }
#endif
#if LOGIN_HYKB
            if (enabledIDProviderList.ContainsKey(OauthProviderType.Hykb))
            {
                phoneLoginPanelWays.Add("Hykb");
            }
#endif
            
            SetupWays(phoneLoginPanel, phoneLoginPanelWays);
        }

        private void SetupWays(GameObject panel, List<string> currentWays)
        {
            Transform ways = panel.transform.Find("OtherWays/Ways");

            if (!currentWays.Any())
            {
                var otherWays = panel.transform.Find("OtherWays").gameObject;
                otherWays.SetActive(false);
                phoneLoginPanel.GetComponent<VerticalLayoutGroup>().padding.bottom = 228;
                return;
            }

            foreach (string way in currentWays)
            {
                switch (way)
                {
                    case "Apple":
                    {
                        GameObject appleBtn = Instantiate(
                            applePrefab,
                            new Vector3(0, 0, 0),
                            Quaternion.identity,
                            ways.transform
                        );

                        appleBtn.GetComponent<Button>().onClick.AddListener(() => Login(OauthProviderType.Apple));

                        break;
                    }

                    case "Phone":
                    {
                        GameObject phoneBtn = Instantiate(
                            phonePrefab,
                            new Vector3(0, 0, 0),
                            Quaternion.identity,
                            ways.transform
                        );

                        phoneBtn.GetComponent<Button>().onClick.AddListener(ShowPhoneLoginPanel);

                        break;
                    }

                    case "Wechat":
                    {
                        GameObject wechatBtn = Instantiate(
                            wechatPrefab,
                            new Vector3(0, 0, 0),
                            Quaternion.identity,
                            ways.transform
                        );
                        wechatBtn.GetComponent<Button>().onClick.AddListener(LoginByWechat);

                        break;
                    }

                    case "QQ":
                    {
                        GameObject qqBtn = Instantiate(
                            qqPrefab,
                            new Vector3(0, 0, 0),
                            Quaternion.identity,
                            ways.transform
                        );
                        qqBtn.GetComponent<Button>().onClick.AddListener(LoginByQQ);
                        break;
                    }
#if LOGIN_TAPTAP
                    case "TapTap":
                    {
                        GameObject tapBtn = Instantiate(
                            tapTapPrefab,
                            new Vector3(0, 0, 0),
                            Quaternion.identity,
                            ways.transform
                        );
                        tapBtn.GetComponent<Button>().onClick.AddListener(LoginByTapTap);
                        break;
                    }    
#endif
#if LOGIN_HYKB
                    case "Hykb":
                    {
                        GameObject hykbBtn = Instantiate(
                            hykbPrefab,
                            new Vector3(0, 0, 0),
                            Quaternion.identity,
                            ways.transform
                        );
                        hykbBtn.GetComponent<Button>().onClick.AddListener(LoginByHykb);
                        break;
                    }    
#endif
                    default:
                    {
                        UIMessage.Show("未知的登录渠道", MessageType.Error);
                        break;
                    }
                }
            }
        }

        private async Task InitUserStatus()
        {
            try
            {
                // 检查是否同意最新协议
                var needAcceptTos = await PassportLoginSDK.Identity.CheckUserNeedAcceptTos();
                UpdateStatus(UserStatus.Tos, !needAcceptTos);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
            }

            try
            {
                // 检查登录状态
                var loggedIn = PassportLoginSDK.Identity.CheckIsLoggedIn();
                UpdateStatus(UserStatus.LoggedIn, loggedIn);

                if (loggedIn)
                {
                    var userInfo = await PassportSDK.Identity.GetUserProfileInfo();
                    var comma = string.IsNullOrEmpty(userInfo.Name) ? "" : "，";
                    UIMessage.Show($"{userInfo.Name}{comma}欢迎进入游戏!", MessageType.Info, 3f);
                    OnCallback(PassportEvent.LoggedIn);
                    await CheckUserStatusAfterLoggedIn();
                }
                else
                {
                    // 没有登录，检查是否有历史账号
                    UpdateUserList();
                    if (_userList.Count > 0)
                    {
                        UpdateStatus(UserStatus.ReLogin, true);
                    }
                }
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
            }
        }

        private void ShowReLoginPanel()
        {
            GetHistoryUserList();
            SetTosLinks();
            reLoginPanel.SetActive(true);
        }

        /// <summary>
        /// 重新登录：用户选中某个已经登录过的账号
        /// </summary>
        public async void ReLogin()
        {
            if (!reLoginTosLink.GetComponent<TOSLink>().Check())
            {
                UIMessage.Show("请先同意协议再继续登录", MessageType.Error);
                return;
            }

            // 同意协议
            try
            {
                await PassportLoginSDK.Identity.AcceptTos(_tos);
                // 成功
                UpdateStatus(UserStatus.Tos, true);
            }
            catch (PassportException e)
            {
                // 失败
                UpdateStatus(UserStatus.Tos, false);
                UIMessage.Show(e);
            }

            Loading(true);
            try
            {
                var loginResult = await PassportLoginSDK.Identity.Login(_userList[_selectedUserIndex]);
                OnLoginComplete(loginResult);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
                HandleLoginError(e, LoginType.ReLogin);
                ShowPanel();
            }

            Loading(false);
        }

        /// <summary>
        /// 拒绝用户协议
        /// </summary>
        public void RejectTos()
        {
            tosPanel.SetActive(false);
            OnCallback(PassportEvent.RejectedTos);
        }

        IEnumerator DownSprite(string url)
        {
            // 检查 URL 是否合法
            string pattern = "([^\\s]+(\\.(?i)(jpg|png|jpeg))$)";

            if (!Regex.IsMatch(url, pattern))
            {
                Debug.Log("无效的公司图标链接，请检查上传文件的格式，仅支持 jpg、jpeg 和 png 格式。");
                yield break;
            }

            UnityWebRequest wr = new UnityWebRequest(url);
            DownloadHandlerTexture texD1 = new DownloadHandlerTexture(true);
            wr.downloadHandler = texD1;

            yield return wr.SendWebRequest();

            if (wr.error == null)
            {
                var width = texD1.texture.width;
                var height = texD1.texture.height;
                Texture2D tex = texD1.texture;

                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                // transform.GetComponent<Image>().sprite = sprite;
                LoadSprite(sprite, width, height);
            }
        }

        private void LoadCompanyIcon(string url)
        {
            StartCoroutine(DownSprite(url));
        }

        private void LoadSprite(Sprite sprite, float width, float height)
        {
            for (var i = 0; i < companyIconList.Count; i += 1)
            {
                var icon = companyIconList[i];
                icon.sprite = sprite;
                var rect = companyIconRectList[i];
                var companyIconWidth = width / height * companyIconHeight;
                rect.sizeDelta = new Vector2(companyIconWidth, companyIconHeight);
            }
        }

        /// <summary>
        /// 更新历史用户列表，并更新跳转到「重新登录面板」的按钮展示状态
        /// </summary>
        private void UpdateUserList()
        {
            _userList = PassportLoginSDK.Identity.GetHistoryUserList();

            foreach (var button in jumpToReLoginPanelButtons)
            {
                // Count != 0 展示按钮；Count = 0 不展示按钮
                button.SetActive(_userList.Count != 0);
            }
        }

        private void GetHistoryUserList()
        {
            UpdateUserList();

            List<TMP_Dropdown.OptionData> users = new List<TMP_Dropdown.OptionData>();
            foreach (var user in _userList)
            {
                Sprite tmpSprite = reLoginWechatSprite;

                // 找到不同登录方式对应的 sprite
                switch (user.LoginMethod)
                {
                    case PassportLoginMethod.Phone:
                        tmpSprite = reLoginPhoneSprite;
                        break;
                    case PassportLoginMethod.Oauth:
                        if (user.OauthProvider == OauthProviderType.Apple)
                        {
                            tmpSprite = reLoginAppleSprite;
                        }
                        else if (user.OauthProvider == OauthProviderType.Wechat ||
                                 user.OauthProvider == OauthProviderType.WechatWeb)
                        {
                            tmpSprite = reLoginWechatSprite;
                        }
                        else if (user.OauthProvider == OauthProviderType.QQ ||
                                 user.OauthProvider == OauthProviderType.QQweb)
                        {
                            tmpSprite = reLoginQQSprite;
                        }
#if LOGIN_TAPTAP
                        else if (user.OauthProvider == OauthProviderType.TapTap)
                        {
                            tmpSprite = reLoginTapTapSprite;
                        }
#endif
#if LOGIN_HYKB
                        else if (user.OauthProvider == OauthProviderType.Hykb)
                        {
                            tmpSprite = reLoginHykbSprite;
                        }
#endif

                        break;
                }
                
#if UNITY_2023_2_OR_NEWER
                var tmp = new TMP_Dropdown.OptionData(user.Name, tmpSprite, new Color());
#else
                var tmp = new TMP_Dropdown.OptionData(user.Name, tmpSprite);
#endif
                users.Add(tmp);
            }
            
            dropDown.ClearOptions();
            dropDown.AddOptions(users);
            _selectedUserIndex = users.Count - 1;
            dropDown.value = _selectedUserIndex;
        }

        public void RemoveUserAt(int index)
        {
            PassportLoginSDK.Identity.RemoveUserFromHistoryList(_userList[index]);
            // _userList.RemoveAt(index);

            GetHistoryUserList();

            dropDown.Hide();
            dropDown.Show();

            // 清空了所有历史用户，则启用「使用其他账号登录」逻辑
            if (!_userList.Any())
            {
                LoginByOtherAccount();
            }
        }

        public void OnHistoryUserSelected(int index)
        {
            _selectedUserIndex = index;
        }

        /// <summary>
        /// 使用其它方式登录
        /// </summary>
        public void LoginByOtherAccount()
        {
            // 重置所有状态
            UpdateStatus(UserStatus.ReLogin, false);
            UpdateStatus(UserStatus.Tos, false);
            UpdateStatus(UserStatus.LoggedIn, false);
            UpdateStatus(UserStatus.RealName, false);
            ShowPanel();
        }

        /// <summary>
        /// 登录后检查用户状态
        /// </summary>
        private async Task CheckUserStatusAfterLoggedIn()
        {
            UpdateStatus(UserStatus.LoggedIn, true);
            UpdateStatus(UserStatus.ReLogin, false);

            // 登录成功后，检查协议同意状态
            try
            {
                // 检查是否同意最新协议
                var needAcceptTos = await PassportLoginSDK.Identity.CheckUserNeedAcceptTos();
                UpdateStatus(UserStatus.Tos, !needAcceptTos);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
            }

            // 登录成功后检查实名认证状态
            try
            {
                var needVerifyRealName = PassportLoginSDK.Identity.CheckNeedVerifyRealName();
                UpdateStatus(UserStatus.RealName, !needVerifyRealName);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
            }
        }

        private void HandleLoginError(PassportException e, LoginType loginType)
        {
            switch (e.Code)
            {
                case ErrorCode.FailedPreconditionPassportNeedAcceptTos:
                    _currentLoginType = loginType;
                    UpdateStatus(UserStatus.Tos, false);
                    break;
                case ErrorCode.UnauthenticatedPassportRefreshTokenExpired:
                    _currentLoginType = LoginType.None; // 需要使用新账号重新登录
                    UpdateStatus(UserStatus.ReLogin, false);
                    break;
            }
        }

        public void LoginByQQ()
        {
            // 条件编译
#if UNITY_IOS || UNITY_WEBGL || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR

#if UNITY_IOS
            // iOS 中非 iPad，仍是走跳转的逻辑
            var identifier = SystemInfo.deviceModel;
            if (!identifier.StartsWith("iPad"))
            {
                Login(OauthProviderType.QQ);
                return;
            }
#endif

            try
            {
                string url = PassportLoginSDK.Identity.GetQQWebLoginQRCodeUrl();
#if UNITY_STANDALONE_WIN || UNITY_WEBGL && !UNITY_EDITOR
                Application.OpenURL(url);
#else
                webView.Open(url);
#endif

                // 等待扫码结果
                Login(OauthProviderType.QQweb);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
                ShowPanel();
            }
#else
            Login(OauthProviderType.QQ);
#endif
        }
#if LOGIN_TAPTAP
        public void LoginByTapTap()
        {
            Login(OauthProviderType.TapTap);
        }
        
#endif
#if LOGIN_HYKB
        public void LoginByHykb()
        {
            Login(OauthProviderType.Hykb);
        }
        
#endif
        
        private void Loading(bool loading)
        {
            loadingPanel.SetActive(loading);
        }
    }
}