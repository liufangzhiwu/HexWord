using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance; // 单例实例
    private Dictionary<string,AudioSource> musicClips=new Dictionary<string, AudioSource>(); // 音乐片段   
    public AudioSource audioSPrefab; 
    private ObjectPool audioSourcePool; 

    private AudioSource musicSource; // 背景音乐音频源
    
    public float normalVolume = 0.35f; // 正常音量
    public float reducedVolume = 0.1f; // 外部音频播放时的音量
    
    // 声明 iOS 原生方法
#if UNITY_IOS
    [DllImport("__Internal")]
    private static extern void TriggerVibrationWithStyle(int style);
    
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void ConfigureAudioSession();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern bool IsExternalAudioPlaying();
#endif

    private void Awake()
    {
        // 确保只有一个 AudioManager 实例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 在场景切换时不销毁
        }
        else
        {
            Destroy(gameObject); // 销毁重复的实例
        }

        // 动态创建 AudioSource 组件
        musicSource = gameObject.AddComponent<AudioSource>();
        // 初始化对象池
        audioSourcePool = new ObjectPool(audioSPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "audio_pool"));
    }
    
    private void Start()
    {            
        ToggleMusic(); // 初始化音乐开关
        //ToggleSounds(); // 初始化音效开关

        Debug.Log("判断音乐开关是否开启"+GameDataManager.Instance.UserData.IsMusicOn);
        // 如果用户设置允许音乐播放，播放默认音乐
        if (GameDataManager.Instance.UserData.IsMusicOn) 
        {
            PlayMusic("music");
        }       
        
#if UNITY_IOS && !UNITY_EDITOR
    ConfigureAudioSession();
#endif
    }
    
    private void Update()
    {
        bool isExternalAudioPlaying = CheckExternalAudio();
        musicSource.volume = isExternalAudioPlaying ? reducedVolume : normalVolume;
    }
    
    // 检测外部音频是否播放
    public bool CheckExternalAudio()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return IsExternalAudioPlaying();
#else
        return false; // 非 iOS 平台直接返回 false
#endif
    }

    private AudioSource LoadAudioClip(string clipName)
    {
        AudioClip clip;
        if (!musicClips.ContainsKey(clipName))
        {
            clip = AdvancedBundleLoader.SharedInstance.LoadAudioClip("musics", clipName);
            AudioSource sfxSource = audioSourcePool.GetObject<AudioSource>(); // 获取 AudioSource
            sfxSource.volume = GameDataManager.Instance.UserData.IsSoundOn?0.25f:0; // 根据用户设置决定音量
            sfxSource.clip = clip; // 设置要播放的音效
            musicClips.Add(clipName, sfxSource);
        }
        return musicClips[clipName];
    }

    // 播放背景音乐
    public void PlayMusic(string name)
    {
        AudioSource clipsSource = LoadAudioClip(name); // 根据名称查找音效片段
        if (clipsSource == null)
        {
            Debug.LogWarning($"音效 '{name}' 未找到。");
            return;
        }
        musicSource.clip=clipsSource.clip;
        musicSource.volume = 0.35f; // 设置要播放的音乐片段
        musicSource.loop=true; // 播放音乐
        musicSource.Play(); // 播放音乐
    }

    // 根据名称播放音效
    public void PlaySoundEffect(string clipName,float time=0)
    {
        AudioSource sfxSource = LoadAudioClip(clipName); // 根据名称查找音效片段
        if (time > 0)
        {
            AudioClip clips = sfxSource.clip;
            sfxSource = audioSourcePool.GetObject<AudioSource>(); // 获取 AudioSource
            sfxSource.volume = GameDataManager.Instance.UserData.IsSoundOn?0.25f:0; // 根据用户设置决定音量
            sfxSource.clip = clips; // 设置要播放的音效
        }
        else
        {
           
            if (sfxSource == null)
            {
                Debug.LogWarning($"音效 '{clipName}' 未找到。");
                return;
            }
            sfxSource.gameObject.SetActive(true);
        }
       
        // 从对象池中获取 AudioSource
        sfxSource.volume = GameDataManager.Instance.UserData.IsSoundOn?0.25f:0; // 根据用户设置决定音量
        sfxSource.Play(); // 播放音效
        if (time > 0)
        {
            StartCoroutine(ReturnToPoolAfterPlayback(sfxSource,time));
        }
    }

    IEnumerator ReturnToPoolAfterPlayback(AudioSource source,float time)
    {
        //int time = (int)(source.time * 1000);
        //Debug.LogWarning("音效时长"+time);
        yield return new WaitForSeconds(10); 
        audioSourcePool.ReturnObjectToPool(source.GetComponent<PoolObject>()); // 将对象返回到池中
    }      

    // 切换背景音乐的播放状态
    public void ToggleMusic()
    {                
        if (!GameDataManager.Instance.UserData.IsMusicOn)
        {
            musicSource.Stop(); // 如果音乐关闭，停止播放
        }
        else
        {
            PlayMusic("music"); // 播放默认音乐
        }
    }
    
    /*public void Vibrate()
    {
#if UNITY_ANDROID
        // Android 震动逻辑
        Handheld.Vibrate();
#elif UNITY_IOS
    // iOS 震动逻辑
    // iOS 不支持直接震动，需使用震动插件或自定义 API
    // 使用震动插件示例
    Vibration.Vibrate();
#endif
    }*/

    public void TriggerVibration(long milliseconds = 5,int intensity=50,int iointensity=1)
    {
        //if (!GameDataManager.instance.UserData.IsVibrationOn) return;
// #if UNITY_EDITOR
//         // 编辑器模式下的模拟逻辑
//         Debug.Log($"模拟震动：强度 {intensity}，持续时间 {milliseconds}ms");
// #elif UNITY_IOS
//      // iOS 震动逻辑
//         TriggerVibrationWithStyle(1);
// #elif UNITY_ANDROID
//     // Android 平台的震动逻辑
//       AndroidVibration.Vibrate(milliseconds,intensity);
// #endif
    }

    // 切换音效的播放状态
    public void ToggleSounds()
    {
        // 设置全局音量
        //bool volume = SaveSystem.Instance.UserData.isSoundsOn;

        //// 遍历对象池中的所有 AudioSource
        //foreach (var audioSource in audioSourcePool)
        //{
        //    audioSource.mute = volume; // 根据用户设置决定音量
        //}
    }

    // 暂停背景音乐
    public void PauseMusic()
    {
        if (musicSource.isPlaying)
        {
            musicSource.Pause(); // 如果正在播放，暂停音乐
        }
    }

    // 恢复背景音乐
    public void ResumeMusic()
    {
        if (!musicSource.isPlaying)
        {
            musicSource.UnPause(); // 如果未播放，恢复音乐
        }
    }

    // 停止背景音乐
    public void StopMusic()
    {
        musicSource.Stop(); // 停止播放音乐
    }
    
    private AudioSource CreateAudioSource()
    {
        return gameObject.AddComponent<AudioSource>(); // 创建新的 AudioSource
    }

    private void OnGetAudioSource(AudioSource source)
    {
        source.mute = false; // 获取时设置为不静音
    }

    private void OnReleaseAudioSource(AudioSource source)
    {
        source.Stop(); // 释放时停止播放
    }

    private void OnDestroyAudioSource(AudioSource source)
    {
        Destroy(source); // 销毁 AudioSource
    }
    
}
