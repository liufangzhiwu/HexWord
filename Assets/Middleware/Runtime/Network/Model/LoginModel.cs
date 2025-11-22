
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;


[Serializable]
public class LoginRequest
{
    public string deviceId;
    public string platform;
    public string idfa;
    public string version;
    public string language;
}

[Serializable]
public class LoginResponse
{
    public string token;
    public int expiresIn; // 过期时间，单位秒
    public string uid;
    public Dictionary<string, Object> abtest; // A/B测试参数
}

[Serializable]
public class LogoutRequest
{
    public string gameData;
}

[Serializable]
public class GetGameDataResponse
{
    public string gameData;
    public int createdTime;
    public int updatedTime;
}

[Serializable]
public class UserProfile
{
    public int uid;
    public string nickname;
    public string avatar;
    public string zen_level;
}
