#if UNITY_huawei
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Collections.Generic;
using HuaweiService;
using HuaweiService.Account;
using Middleware;
using Newtonsoft.Json;
using Exception = HuaweiService.Exception;

namespace Middleware
{
    public class Constant {
        public static  int IS_LOG = 1;
        //login
        public static  int REQUEST_SIGN_IN_LOGIN = 1002;
        //login by code
        public static  int REQUEST_SIGN_IN_LOGIN_CODE = 1003;
        //independent sign in
        public static  int REQUEST_SIGN_IN_LOGIN_INDEPENDENT = 1004;
    }
    public class Account_huaweiandroid : IAccounts
    {
        public string UserId { get; set; }
        public bool IsLogin { get; set; } = false;
        string teamPlayerId = string.Empty;
        string thirdOpenId = "";
        
        public AuthAccount CurrentAuthAccount { get; private set; }
        private AccountAuthParams mAuthParam;
        private AccountAuthService mAuthService;
        // 定义回调委托
        public Action<bool, AuthAccount> OnLoginComplete;
        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                var callback = new AccountCallback();
                callback.setCallback(MyOnActivityResultCallback);
                AccountActivity.setCallback(callback);
                
                mAuthParam = new AccountAuthParamsHelper().setAccessToken().setUid().setAuthorizationCode().setId().setIdToken().setProfile().setCarrierId().createParams();
                mAuthService=AccountAuthManager.getService(new UnityPlayerActivity(), mAuthParam);
                AccountActivity.setAuthParam(mAuthParam);
            });
        }


        public void Login(bool isShowLoginPanel = false)
        {
            if (mAuthService == null)
            {
                Debug.LogError("Huawei AuthService not initialized!");
                OnLoginComplete?.Invoke(false, null);
                return;
            }
            Debug.Log("开始静默登录...");
            var task = mAuthService.silentSignIn();
            // 添加成功监听器
            task.addOnSuccessListener(new HmsSuccessListener<AuthAccount>((authAccount) =>
            {
                Debug.Log("静默登录成功!");
                HandleLoginSuccess(authAccount);
            }));
            // 添加失败监听器
            task.addOnFailureListener(new HmsFailureListener((e) =>
            {
                Debug.LogWarning("静默登录失败，尝试拉起登录界面...");
                StartSignInActivity();
            }));
        }
        // 拉起华为登录界面
        private void StartSignInActivity()
        {
            try 
            {
                // 获取登录 Intent
                AccountActivity.setIntent("signIn");
                AccountActivity.setRequestCode(Constant.REQUEST_SIGN_IN_LOGIN);
                AccountActivity.start(new UnityPlayerActivity());
            }
            catch (System.Exception ex)
            {
                Debug.LogError("拉起登录界面失败: " + ex.Message);
                OnLoginComplete?.Invoke(false, null);
            }
        }
        public void Logout()
        {

        }

        public void VerifyPlayer()
        {

        }
        
        public void MyOnActivityResultCallback(int requestCode, int resultCode,AndroidJavaObject obj)
        {
            var data = new Intent { obj = obj };
            if (requestCode == Constant.REQUEST_SIGN_IN_LOGIN || requestCode == Constant.REQUEST_SIGN_IN_LOGIN_CODE)
            {
                var authAccountTask = AccountAuthManager.parseAuthResultFromIntent(data);
                if (authAccountTask.isSuccessful()) {
                    Debug.Log("显式登录成功!");
                    var authAccount = new AuthAccount();
                    HandleLoginSuccess(authAccount);
                }else{
                    Debug.LogError("显式登录失败 (User Cancelled or Error)");
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        Game.self.ShowLoginErrorPanel();
                        OnLoginComplete?.Invoke(false, null);
                    });
                }
            }else if (requestCode == Constant.REQUEST_SIGN_IN_LOGIN_INDEPENDENT)
            {
                var authAccountTask = AccountAuthManager.parseAuthResultFromIntent(data);
                if (authAccountTask.isSuccessful()) {
                    Debug.Log("隐式登录成功!");
                    var authAccount = new AuthAccount();
                    HandleLoginSuccess(authAccount);
                }else{
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        StartSignInActivity();
                    });
                }
            }
        }
        // 统一处理登录成功逻辑
        private void HandleLoginSuccess(AuthAccount authAccount)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                CurrentAuthAccount = authAccount;
                UserId = authAccount.getOpenId();
                IsLogin = true;
                
                Debug.Log($"ID Token: {authAccount.getIdToken()}");
                
                OnLoginComplete?.Invoke(true, authAccount);
            });
        }
        
        // public void getInfo()
        // {
        //     var token = mAuthAccount.getAccessToken();
        //     var displayName = mAuthAccount.getDisplayName();
        //     var account = mAuthAccount.getAccount(new Context());
        //     var email = mAuthAccount.getEmail();
        //     var fName = mAuthAccount.getFamilyName();
        //     var gName = mAuthAccount.getGivenName();
        //     var scope = mAuthAccount.getAuthorizedScopes();
        //     var idToken = mAuthAccount.getIdToken();
        //     var avatarUri =mAuthAccount.getAvatarUri();
        //     var authorizationCode =mAuthAccount.getAuthorizationCode();
        //     var serviceCountryCode = mAuthAccount.getServiceCountryCode();
        //     var unionId =mAuthAccount.getUnionId();
        //     var openId =mAuthAccount.getOpenId();
        //     var uid =mAuthAccount.getUid();
        //     
        //     var accountFlag =mAuthAccount.getAccountFlag();
        //     var carrierId =mAuthAccount.getCarrierId();
        //     string msg = ($"getInfo :\n token{token}\n displayName{displayName}\n account{account}\n email{email}\n fName{fName}\n gName{gName}\n scope{scope}\n idToken{idToken}\n" +
        //                           $" avatarUri{avatarUri}\n authorizationCode{authorizationCode}\n serviceCountryCode{serviceCountryCode}\n unionId{unionId}\n openId{openId}\n uid{uid}\n accountFlag{accountFlag}\n carrierId{carrierId}\n");
        // }
    }
    
    public class HmsSuccessListener<T>:OnSuccessListener{
        public SuccessCallBack<T> CallBack;
        public HmsSuccessListener(SuccessCallBack<T> c){
            CallBack = c;
        }
        public void onSuccess(T arg0)
        {
            if(CallBack != null)
            {
                CallBack.Invoke(arg0);
            }
        }
        
        public override void onSuccess(AndroidJavaObject arg0){
            if(CallBack !=null)
            {
                Type type = typeof(T);
                IHmsBase ret = (IHmsBase)Activator.CreateInstance(type);
                ret.obj = arg0;
                CallBack.Invoke((T)ret);
            }
        }
    }
    
    public class HmsFailureListener:OnFailureListener{
        public FailureCallBack CallBack;
        public HmsFailureListener(FailureCallBack c){
            CallBack = c;
        }
        public override void onFailure(Exception arg0){
            if(CallBack !=null){
                CallBack.Invoke(arg0);
            }
        }
    }
}

#endif
