using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using Unity.Passport.Runtime;
using Unity.Passport.Runtime.Model;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using Passport;

namespace Unity.Passport.Runtime.UI
{
    public class PassportUIControllerLight : PassportUIBase
    {
        public WebViewController webView;
        public GameObject tosPanel;
        public GameObject loginPanel;
        public GameObject reLoginPanel;
        public GameObject phoneLoginPanel;
        public GameObject realnameAuthenticationPanel;
        public GameObject loadingPanel;
        public TMP_Dropdown dropDown;

        [Tooltip("成功发送验证码后按钮禁用时长")] public float verifyCodeButtonDisabledTime = 59f;

        // 填写框相关的 UI
        public GameObject phoneNumber;

        public GameObject verifyCode;

        // 按钮相关
        public GameObject acceptTosButton;

        public GameObject refuseTosButton;

        // 协议面板
        // 协议内容
        public GameObject tosContent;

        // 协议标题
        public GameObject tosTitle;

        public GameObject tosTitlePortrait;

        // 公司名称、图标列表
        public List<Image> companyIconList = new List<Image>();
        public List<RectTransform> companyIconRectList = new List<RectTransform>();
        public List<GameObject> companyNameList = new List<GameObject>();
        public GameObject applePrefab;
        public GameObject phonePrefab;
        public GameObject wechatPrefab;
        public GameObject qqPrefab;
# if LOGIN_TAPTAP
        public GameObject tapTapPrefab;
#endif
# if LOGIN_HYKB
        public GameObject hykbPrefab;
#endif
        public Sprite reLoginWechatSprite;
        public Sprite reLoginQQSprite;
        public Sprite reLoginPhoneSprite;
        public Sprite reLoginAppleSprite;
# if LOGIN_TAPTAP
        public Sprite reLoginTapTapSprite;
#endif
# if LOGIN_HYKB
        public Sprite reLoginHykbSprite;
#endif
        public Sprite sendVerifyCodeButtonSprite;
        public Sprite sendVerifyCodeButtonDisabledSprite;
        public Image verifyCodeButtonImage;
        public GameObject loginByWechatButton;

        public int companyIconHeight = 30;

        // 实名认证填写框
        public TMP_InputField nameInput;
        public TMP_InputField idInput;
        public Button verifyCodeButton;
        public TextMeshProUGUI verifyCodeButtonText;
        public TMP_InputField phoneNumberInput;
        public TMP_InputField verifyCodeInput;
        public TOSLink linksText;
        public List<GameObject> jumpToReLoginPanelButtons; // 跳转到重新登录面板按钮列表
        private const string CallingAreaCode = "+86";
        private PassportTos _tos = new PassportTos();
        private UserStatus _status = 0;
        private bool _countDownOn;
        private float _remainTime;
        private string PhoneNumber => CallingAreaCode + phoneNumberInput.text;
        private List<UserLoginInfo> _userList;
        private int _selectedUserIndex;
        private bool _needAcceptTosError;
        private UserStatus CurrentUserStatus => _status;

        private LoginType _currentLoginType = LoginType.None;

        // 挂载到对象上时，或者在脚本组件上右键点击 Reset 时执行。当引用丢失时，可以便捷地通过 Reset 找回。
        public void Reset()
        {
#if UNITY_EDITOR
            tosPanel = transform.Find("TOSPanel").gameObject;
            loginPanel = transform.Find("LoginPanel").gameObject;
            reLoginPanel = transform.Find("ReLoginPanel").gameObject;
            phoneLoginPanel = transform.Find("PhoneLoginPanel").gameObject;
            realnameAuthenticationPanel = transform.Find("RealnameAuthenticationPanel").gameObject;
            loadingPanel = transform.Find("LoadingPanel").gameObject;
            dropDown = transform.Find("ReLoginPanel/HistoryUsers/Dropdown").gameObject.GetComponent<TMP_Dropdown>();

            phoneNumber = transform.Find("PhoneLoginPanel/PhoneLogin/PhoneNumber").gameObject;
            verifyCode = transform.Find("PhoneLoginPanel/PhoneLogin/VerifyCode").gameObject;

            acceptTosButton = transform.Find("TOSPanel/Footer/Buttons/Accept").gameObject;
            refuseTosButton = transform.Find("TOSPanel/Footer/Buttons/Refuse").gameObject;

            tosContent = transform.Find("TOSPanel/Body/ScrollView/Viewport/Text").gameObject;
            tosTitle = transform.Find("TOSPanel/LandscapeHeader/Title").gameObject;
            tosTitlePortrait = transform.Find("TOSPanel/PortraitHeader/Title").gameObject;


            companyIconList.Add(transform.Find("TOSPanel/LandscapeHeader/Title/CompanyIcon").gameObject
                .GetComponent<Image>());
            companyIconList.Add(transform.Find("TOSPanel/PortraitHeader/CompanyIcon").gameObject.GetComponent<Image>());

            companyIconList.Add(
                transform.Find("LoginPanel/Header/Content/CompanyIcon").gameObject.GetComponent<Image>());
            companyIconList.Add(
                transform.Find("ReLoginPanel/Header/Content/CompanyIcon").gameObject.GetComponent<Image>()
            );
            companyIconList.Add(
                transform.Find("PhoneLoginPanel/Header/Content/CompanyIcon").gameObject.GetComponent<Image>()
            );
            companyIconList.Add(
                transform.Find("RealnameAuthenticationPanel/Header/CompanyIcon").gameObject.GetComponent<Image>()
            );


            companyIconRectList.Add(transform.Find("TOSPanel/LandscapeHeader/Title/CompanyIcon").gameObject
                .GetComponent<RectTransform>());
            companyIconRectList.Add(transform.Find("TOSPanel/PortraitHeader/CompanyIcon").gameObject
                .GetComponent<RectTransform>());
            companyIconRectList.Add(transform.Find("LoginPanel/Header/Content/CompanyIcon").gameObject
                .GetComponent<RectTransform>());
            companyIconRectList.Add(transform.Find("ReLoginPanel/Header/Content/CompanyIcon").gameObject
                .GetComponent<RectTransform>()
            );
            companyIconRectList.Add(
                transform.Find("PhoneLoginPanel/Header/Content/CompanyIcon").gameObject.GetComponent<RectTransform>()
            );
            companyIconRectList.Add(
                transform.Find("RealnameAuthenticationPanel/Header/CompanyIcon").gameObject
                    .GetComponent<RectTransform>()
            );

            companyNameList.Add(transform.Find("LoginPanel/Header/Content/Text").gameObject);
            companyNameList.Add(transform.Find("ReLoginPanel/Header/Content/Text").gameObject);
            companyNameList.Add(transform.Find("PhoneLoginPanel/Header/Content/Text").gameObject);
            companyNameList.Add(
                transform.Find("RealnameAuthenticationPanel/Header/Title").gameObject
            );

            verifyCodeButton = verifyCode.GetComponentInChildren<Button>();
            verifyCodeButtonText = verifyCodeButton.GetComponentInChildren<TextMeshProUGUI>();
            phoneNumberInput = phoneNumber.GetComponentInChildren<TMP_InputField>();
            verifyCodeInput = verifyCode.GetComponentInChildren<TMP_InputField>();

            linksText = transform.Find("TOSPanel/Links").gameObject.GetComponentInChildren<TOSLink>();

            applePrefab =
                AssetDatabase.LoadAssetAtPath(
                    "Assets/PassportLoginSDK/Resources/Prefabs/LoginWays/Apple.prefab",
                    typeof(GameObject)
                ) as GameObject;
            phonePrefab =
                AssetDatabase.LoadAssetAtPath(
                    "Assets/PassportLoginSDK/Resources/Prefabs/LoginWays/Phone.prefab",
                    typeof(GameObject)
                ) as GameObject;
            wechatPrefab =
                AssetDatabase.LoadAssetAtPath(
                    "Assets/PassportLoginSDK/Resources/Prefabs/LoginWays/Wechat.prefab",
                    typeof(GameObject)
                ) as GameObject;

            qqPrefab =
                AssetDatabase.LoadAssetAtPath(
                    "Assets/PassportLoginSDK/Resources/Prefabs/LoginWays/QQ.prefab",
                    typeof(GameObject)
                ) as GameObject;
# if LOGIN_TAPTAP
            tapTapPrefab =
                AssetDatabase.LoadAssetAtPath(
                    "Assets/PassportLoginSDK/Resources/Prefabs/LoginWays/TapTap.prefab",
                    typeof(GameObject)
                ) as GameObject;
#endif
# if LOGIN_HYKB
            hykbPrefab =
                AssetDatabase.LoadAssetAtPath(
                    "Assets/PassportLoginSDK/Resources/Prefabs/LoginWays/Hykb.prefab",
                    typeof(GameObject)
                ) as GameObject;
#endif
            nameInput = transform
                .Find("RealnameAuthenticationPanel/Authentication/Name/Input")
                .gameObject.GetComponentInChildren<TMP_InputField>();
            idInput = transform
                .Find("RealnameAuthenticationPanel/Authentication/ID/Input")
                .gameObject.GetComponentInChildren<TMP_InputField>();


            reLoginWechatSprite = AssetDatabase.LoadAssetAtPath(
                "Assets/PassportLoginSDK/Resources/Sprites/ReLoginWechat.png",
                typeof(Sprite)
            ) as Sprite;

            reLoginQQSprite = AssetDatabase.LoadAssetAtPath(
                "Assets/PassportLoginSDK/Resources/Sprites/ReLoginQQ.png",
                typeof(Sprite)
            ) as Sprite;

            reLoginPhoneSprite = AssetDatabase.LoadAssetAtPath(
                "Assets/PassportLoginSDK/Resources/Sprites/ReLoginPhone.png",
                typeof(Sprite)
            ) as Sprite;
            reLoginAppleSprite = AssetDatabase.LoadAssetAtPath(
                "Assets/PassportLoginSDK/Resources/Sprites/ReLoginApple.png",
                typeof(Sprite)
            ) as Sprite;
# if LOGIN_TAPTAP
            reLoginTapTapSprite = AssetDatabase.LoadAssetAtPath(
                "Assets/PassportLoginSDK/Resources/Sprites/ReLoginTapTap.png",
                typeof(Sprite)
            ) as Sprite;
#endif
# if LOGIN_HYKB
            reLoginHykbSprite = AssetDatabase.LoadAssetAtPath(
                "Assets/PassportLoginSDK/Resources/Sprites/ReLoginHykb.png",
                typeof(Sprite)
            ) as Sprite;
#endif
            sendVerifyCodeButtonSprite = AssetDatabase.LoadAssetAtPath(
                    "Assets/PassportLoginSDK/Resources/Sprites/SendButtonBackground.png",
                    typeof(Sprite))
                as Sprite;

            sendVerifyCodeButtonDisabledSprite = AssetDatabase.LoadAssetAtPath(
                    "Assets/PassportLoginSDK/Resources/Sprites/SendButtonDisabledBackground.png",
                    typeof(Sprite))
                as Sprite;

            verifyCodeButtonImage = verifyCodeButton.GetComponent<Image>();
            loginByWechatButton = transform.Find("LoginPanel/MainWays/Wechat").gameObject;

            jumpToReLoginPanelButtons.Add(transform.Find("LoginPanel/Header/JumpToReLogin").gameObject);
            jumpToReLoginPanelButtons.Add(transform.Find("PhoneLoginPanel/Header/JumpToReLogin").gameObject);
            webView = transform.Find("WebViewController").gameObject.GetComponent<WebViewController>();
#endif
        }

        // Start is called before the first frame update
        public override async Task Init()
        {
            // 初始化变量
            _remainTime = verifyCodeButtonDisabledTime;

            // 关闭所有面板
            loginPanel.SetActive(false);
            tosPanel.SetActive(false);
            reLoginPanel.SetActive(false);
            realnameAuthenticationPanel.SetActive(false);
            phoneLoginPanel.SetActive(false);
            
            try
            {
                loadingPanel.SetActive(true);
                // 调用 SDK
                var userInfo = await PassportLoginSDK.Init(true);
                if (userInfo != null)
                {
                    UIMessage.Show($"{userInfo.Name} 登录中...");
                    // 手动调用 Login，以显示提示消息
                    await PassportLoginSDK.Identity.Login(userInfo);
                }

                loadingPanel.SetActive(false);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
                HandleLoginError(e, LoginType.LoginByUserInfo);
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

        // Update is called once per frame
        void Update()
        {
            if (_countDownOn)
            {
                CountDown();
            }
        }

        private void ShowLoginPanel()
        {
            loginPanel.SetActive(true);
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
#if UNITY_WEBGL
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
#if UNITY_WEBGL
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
            loadingPanel.SetActive(true);
            if (oauthProviderID != OauthProviderType.WechatWeb)
            {
                UIMessage.Show("授权中...");
            }

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
            }

            if (oauthProviderID == OauthProviderType.WechatWeb)
            {
                // 扫码完成
                webView.Close();
#if UNITY_WEBGL
                passportWebWechatLoginComplete();
#endif
            }

            if (oauthProviderID == OauthProviderType.QQweb)
            {
                webView.Close();
            }

            loadingPanel.SetActive(false);
        }

        private void DisableVerifyCodeButton()
        {
            verifyCodeButton.interactable = false;
            verifyCodeButtonImage.sprite = sendVerifyCodeButtonDisabledSprite;
        }

        private void EnableVerifyCodeButton()
        {
            verifyCodeButton.interactable = true;
            verifyCodeButtonImage.sprite = sendVerifyCodeButtonSprite;
        }

        public void ShowPhoneLoginPanel()
        {
            // 重置倒计时逻辑
            ResetCountDown();
            // 清空填写框
            phoneNumberInput.text = "";
            verifyCodeInput.text = "";

            // 切换到手机登录界面
            loginPanel.SetActive(false);
            tosPanel.SetActive(false);
            reLoginPanel.SetActive(false);
            realnameAuthenticationPanel.SetActive(false);
            phoneLoginPanel.SetActive(true);
        }

        private void SendVerifyCodeCallback(bool success, string reason)
        {
            verifyCodeButton.GetComponent<ButtonController>().StopLoading();

            if (success)
            {
                // 按钮置灰倒计时
                DisableVerifyCodeButton();
                _countDownOn = true;
                // 提示验证码发送成功
                UIMessage.Show("验证码已发送");
            }
            else
            {
                // 恢复按钮
                EnableVerifyCodeButton();
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

            // 按钮置灰
            DisableVerifyCodeButton();
            verifyCodeButton.GetComponent<ButtonController>().Loading();

            try
            {
                await PassportLoginSDK.Identity.SendVerifyCode(PhoneNumber);
            }
            catch (PassportException e)
            {
                SendVerifyCodeCallback(e.Code == ErrorCode.Default, e.ErrorMessage);
                return;
            }

            SendVerifyCodeCallback(true, "");
        }

        /// <summary>
        /// 调用 sdk 进行手机号登录
        /// </summary>
        public async void LoginByPhone()
        {
            string code = verifyCodeInput.text;

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
                HandleLoginError(e, LoginType.None);
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

                loadingPanel.SetActive(true);
                await PassportLoginSDK.Identity.VerifyRealName(realName, id);
                loadingPanel.SetActive(false);

                // 认证成功后，执行以下逻辑
                UpdateStatus(UserStatus.RealName, true);
                ShowPanel();
            }
            catch (PassportException e)
            {
                loadingPanel.SetActive(false);
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

        private void ResetCountDown()
        {
            _remainTime = verifyCodeButtonDisabledTime;
            _countDownOn = false; // 结束计时
            EnableVerifyCodeButton();
            verifyCodeButtonText.text = "发送验证码";
        }

        /// <summary>
        /// 发送验证码后开始倒计时
        /// /// </summary>
        private void CountDown()
        {
            _remainTime -= Time.deltaTime;
            if (_remainTime <= 0)
            {
                ResetCountDown();
            }
            else
            {
                verifyCodeButtonText.text =
                    $"<color=#9E9E9E> {Math.Ceiling(_remainTime)} 秒 </color>";
            }
        }

        /// <summary>
        /// 展示协议面板
        /// </summary>
        private async void ShowTosPanel()
        {
            // 按钮置灰
            acceptTosButton.GetComponent<Button>().interactable = false;
            refuseTosButton.GetComponent<Button>().interactable = false;

            // 加载中
            loadingPanel.SetActive(true);

            // 获取协议信息
            try
            {
                _tos = await PassportLoginSDK.Identity.GetTos();

                // 先清空信息
                linksText.Clear();

                try
                {
                    // 如果协议信息为空，说明用户没有配置，则跳过该步骤
                    if (_tos == null)
                    {
                        loadingPanel.SetActive(false);
                        UpdateStatus(UserStatus.Tos, true);
                        ShowPanel();
                        return;
                    }

                    var tosInfo = _tos;
                    // 应用信息填入
                    // 如果有协议鏈接才填入
                    linksText.Init(tosInfo);

                    // 更新内容、名称
                    tosContent.GetComponent<TextMeshProUGUI>().text = tosInfo.Content;
                    tosTitle.GetComponent<TextMeshProUGUI>().text = tosInfo.Name;
                    tosTitlePortrait.GetComponent<TextMeshProUGUI>().text = tosInfo.Name;


                    // 协议加载成功后，按钮才可点击
                    acceptTosButton.GetComponent<Button>().interactable = true;
                    refuseTosButton.GetComponent<Button>().interactable = true;
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    UIMessage.Show("请求协议信息出错", MessageType.Error);
                }


                loadingPanel.SetActive(false);
                tosPanel.SetActive(true);
            }
            catch (PassportException e)
            {
                UIMessage.Show(e);
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
            realnameAuthenticationPanel.SetActive(false);
            loginPanel.SetActive(false);
            loadingPanel.SetActive(false);

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
                ShowLoginPanel();
            }
            else if ((CurrentUserStatus & UserStatus.RealName) != UserStatus.RealName)
            {
                ShowRealnamePanel();
            }
            else
            {
                // 已完成所有环节
                OnCallback(PassportEvent.Completed);
            }
        }

        /// <summary>
        /// 显示实名认证面板
        /// </summary>
        private void ShowRealnamePanel()
        {
            // 清空填写框
            nameInput.text = "";
            idInput.text = "";

            realnameAuthenticationPanel.SetActive(true);
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
            List<string> loginPanelWays = new List<string>();

            var enabledIDProviderList = PassportLoginSDK.Identity.GetEnabledIDProviderList();

            // 根据平台判断是否使用 web 版本（微信、QQ）
#if UNITY_WEBGL || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR
            // 使用web版本
            if (enabledIDProviderList.ContainsKey(OauthProviderType.WechatWeb))
            {
                phoneLoginPanelWays.Add("Wechat");
                loginByWechatButton.SetActive(true);
            }
            else
            {
                loginByWechatButton.SetActive(false);
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
                loginByWechatButton.SetActive(true);
            }
            else
            {
                loginByWechatButton.SetActive(false);
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
                loginByWechatButton.SetActive(true);
            }
            else
            {
                loginByWechatButton.SetActive(false);
            }
            if (enabledIDProviderList.ContainsKey(OauthProviderType.QQ))
            {
                phoneLoginPanelWays.Add("QQ");
            }
#endif

#if UNITY_IOS || UNITY_EDITOR || UNITY_WEBGL
            if (enabledIDProviderList.ContainsKey(OauthProviderType.Apple))
            {
                phoneLoginPanelWays.Add("Apple");
                loginPanelWays.Add("Apple");
            }
#endif

            if (enabledIDProviderList.ContainsKey(OauthProviderType.QQ))
            {
                phoneLoginPanelWays.Add("QQ");
                loginPanelWays.Add("QQ");
            }

            SetupWays(phoneLoginPanel, phoneLoginPanelWays);
            SetupWays(loginPanel, loginPanelWays);
        }

        private void SetupWays(GameObject panel, List<string> currentWays)
        {
            Transform ways = panel.transform.Find("OtherWays/Ways");

            if (!currentWays.Any())
            {
                var otherWays = panel.transform.Find("OtherWays").gameObject;
                otherWays.SetActive(false);
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
            reLoginPanel.SetActive(true);
        }

        public async void ReLogin()
        {
            // 重新登录
            // 用户选中了某个已经登录过的账号

            loadingPanel.SetActive(true);
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

            loadingPanel.SetActive(false);
        }

        public void DeleteAllLocalInfoForTest()
        {
            PlayerPrefs.DeleteAll();
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
                        else if (user.OauthProvider == OauthProviderType.Wechat || user.OauthProvider == OauthProviderType.WechatWeb)
                        {
                            tmpSprite = reLoginWechatSprite;
                        }
                        else if (user.OauthProvider == OauthProviderType.QQ || user.OauthProvider == OauthProviderType.QQweb)
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
    }
}