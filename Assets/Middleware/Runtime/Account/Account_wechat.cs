using UnityEngine;
using System;
using System.Collections.Generic;
using Middleware;
using Newtonsoft.Json;
using WeChatWASM;

namespace Middleware
{
    public class Account_wechat : IAccounts
    {
        public WXUserInfo userInfo;
        
        public string UserId { get; set; }
        public bool IsLogin { get; set; }
        public bool IsAuthorized { get; set; }

        private bool infoFlag = false;
        public void Init(float delay)
        {
            WX.InitSDK((code) =>
            {
                Debug.Log("微信初始化 Init WxSDK code: " + code);
                // CheckPrivacySetting(null);
            });
        }

        private void CheckPrivacySetting(Action onSuccess)
        {
            WX.GetPrivacySetting(new GetPrivacySettingOption()
            {
                success = (res) =>
                {
                    if (res.needAuthorization)
                    {
                        
                    }
                    else
                    {
                        Debug.Log("隐私协议已授权");
                        onSuccess?.Invoke();
                    }
                },
                fail = (err) =>
                {
                    Debug.Log("检查隐私配置失败: " + err.errMsg);
                }
            });
        }

        private void LoaderWxMess()
        {
            WX.GetPrivacySetting(new GetPrivacySettingOption()
            {
                success = (res) =>
                {
                    if (res.needAuthorization)
                    {
                        WX.RequirePrivacyAuthorize(new RequirePrivacyAuthorizeOption()
                        {
                            success = (res) =>
                            {
                                Debug.Log("同意隐私协议：" + JsonUtility.ToJson(res, true));
                                this.GetScopeInfoSetting();
                                this.infoFlag = true;
                            },
                            fail = (err) =>
                            {
                                Debug.Log("拒绝隐私协议:" + JsonUtility.ToJson(res, true));
                            }
                        });
                    }
                    IsAuthorized = !res.needAuthorization;
                },
                fail = (err) =>{Debug.Log("隐私协议出现错误！" + err.errMsg);},
                complete = (res) =>
                {
                    // 处理询问隐私协议失败或之前已经同意但未授权用户信息的情况
                    if (!this.infoFlag)
                    {
                        this.GetScopeInfoSetting();
                    }
                    else
                    {
                        IsAuthorized = true;
                    }
                    Debug.Log("隐私协议执行完成！");
                }
            });
        }

        private void GetScopeInfoSetting()
        {
            WX.GetSetting(new GetSettingOption()
            {
                success = (res) =>
                {
                    IsAuthorized = true;
                    Debug.Log("获取用户信息授权情况成功: " + JsonUtility.ToJson(res.authSetting, true));
                    // 判断用户信息的授权情况
                    if (!res.authSetting.ContainsKey("scope.userInfo") || !res.authSetting["scope.userInfo"])
                    {
                        // 3.1 未授权，创建授权按钮区
                        // 需引导用户点击所创建的区域，这里的做法是将开始游戏的按钮放在该区域
                        this.CreateUserInfoButton();
                    }
                    else
                    {
                        // 3.2 已授权，直接获取用户信息
                        // this.GetUserInfo();
                        // 这里也可以先不获取，留到点击开始游戏按钮再获取，但没必要，先获取后存起来即可
                      
                    }
                },
                fail = (err) =>
                {
                    IsAuthorized = false;
                    Debug.Log("获取用户信息授权情况失败：" + JsonUtility.ToJson(err, true));
                }
            });
        }
        
        /// <summary>
        /// 创建用户信息授权点击区域
        /// </summary>
        private void CreateUserInfoButton()
        {
            Debug.Log("create userinfo button area");
            WXUserInfoButton btn = WX.CreateUserInfoButton(0, 0, Screen.width, Screen.height, "zh_CN", false);
            // 监听授权区域的点击
            btn.OnTap((res) =>
            {
                Debug.Log("click userinfo btn: " + JsonUtility.ToJson(res, true));
                if (res.errCode == 0)
                {
                    // 用户已允许获取个人信息，返回的 res.userInfo 即为用户信息
                    Debug.Log("userinfo: " + JsonUtility.ToJson(res.userInfo, true));
                    // 将用户信息存入成员变量，以待后用
                    this.userInfo = res.userInfo;
                    // 展示，只是为了测试看到
                    // this.ShowUserInfo(res.userInfo.avatarUrl, res.userInfo.nickName);
                }
                else
                {
                    Debug.Log("用户拒绝获取个人信息");
                }
                // 最后隐藏授权区域，防止阻塞游戏继续
                btn.Hide();
                Debug.Log("已隐藏热区");
            });
        }
        //该函数在用户授权后调用就行
        private void GetWXLoginCode(Action<string> onCodeReceived)
        {
            Debug.Log("****** GetCode ");
            LoginOption loginOption = new LoginOption();
            loginOption.complete = (e) => 
            {
                Debug.Log("****** e.complete " + e);
            };
            loginOption.success = ((e) =>
            {
                //成功获取到用户Code
                string code = e.code;
                IsLogin = true;
                Debug.Log("微信登录返回的code-> " + code);
                onCodeReceived?.Invoke(code);
                
            });
            loginOption.fail = ((e) =>
            {
                IsLogin = false;
                Debug.Log("****** e.errMsg " + e.errMsg);
            });
            WX.Login(loginOption);
        }
        /// <summary>
        /// 调用Api获取用户信息
        /// </summary>
        private void GetUserInfo()
        {
            WX.GetUserInfo(new GetUserInfoOption()
            {
                lang = "zh_CN",
                success = (res) =>
                {
                    Debug.Log("获取用户信息成功(API): " + JsonUtility.ToJson(res.userInfo, true));
                    // 将用户信息存入成员变量，或存入云端，方便后续使用
                    this.userInfo = this.ConvertUserInfo(res.userInfo);

                    // this.ShowUserInfo(res.userInfo.avatarUrl, res.userInfo.nickName);
                },
                fail = (err) =>
                {
                    Debug.Log("获取用户信息失败(API): " + JsonUtility.ToJson(err, true));
                }
            });
        }
        
        /// <summary>
        /// 将UserInfo对象转为WXUserInfo
        /// ps: 不知为何，相同结构要搞两个对象
        /// </summary>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        WXUserInfo ConvertUserInfo(UserInfo userInfo)
        {
            return new WXUserInfo()
            {
                nickName = userInfo.nickName,
                avatarUrl = userInfo.avatarUrl,
                country = userInfo.country,
                province = userInfo.province,
                city = userInfo.city,
                language = userInfo.language,
                gender = (int)userInfo.gender
            };
        }
        public void Login(Action<string> callback)
        {
            Debug.Log("调用了登录方法");
            // GetWXLoginCode(callback);
            WX.GetPrivacySetting(new GetPrivacySettingOption()
            {
                success = (privacyRes) =>
                {
                    if (privacyRes.needAuthorization)
                    {
                        WX.RequirePrivacyAuthorize(new RequirePrivacyAuthorizeOption()
                        {
                            success = (authRes) =>
                            {
                                ExecuteWxLogin(callback);
                            },
                            fail = (err) => 
                            {
                                Debug.LogError("用户拒绝了隐私协议: " + err.errMsg);
                                callback?.Invoke(null); // 返回空表示失败
                            }
                        });
                    }
                    else
                    {
                        // 3. 不需要授权（之前同意过），直接登录
                        ExecuteWxLogin(callback);
                    }
                },fail = (err) =>
                {
                    Debug.LogError("检查隐私配置失败: " + err.errMsg);
                    callback?.Invoke(null);
                }
            });
        }

        private void ExecuteWxLogin(Action<string> callback)
        {
            WX.Login(new LoginOption()
            {
                success = (res) =>
                {
                    if (!string.IsNullOrEmpty(res.code))
                    {
                        callback?.Invoke(res.code);
                    }
                    else
                    {
                        Debug.LogError("登录失败，Code为空");
                        callback?.Invoke(null);
                    }
                },
                fail = (err) =>
                {
                    Debug.LogError("微信登录接口失败: " + err.errMsg);
                    callback?.Invoke(null);
                }
            });
        }
        public void Logout()
        {
            
        }

        public void VerifyPlayer()
        {
            Debug.Log("调用了校验隐私协议方法！");
            LoaderWxMess();
        }
    }
}