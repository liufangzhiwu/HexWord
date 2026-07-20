using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance; // 单例实例
    // 分离管理：音乐（流式）和音效（短片段）
    private AudioSource musicSource;
    private Dictionary<string, AudioClip> soundClipCache = new Dictionary<string, AudioClip>();
    private List<AudioSource> activeSoundSources = new List<AudioSource>();
    
    public AudioSource audioSPrefab; 
    private ObjectPool audioSourcePool;
    
    float normalVolume = 0.25f; // 正常音量
    
    private void Awake()
    {
        // 确保只有一个 AudioManager 实例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 在场景切换时不销毁
        }

        // 动态创建 AudioSource 组件
        musicSource = gameObject.AddComponent<AudioSource>();
        // 初始化对象池
        audioSourcePool = new ObjectPool(audioSPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "audio_pool"));
    }

    // 预加载音效
    public IEnumerator PreloadCommonSounds(string[] soundNames)
    {
        foreach (var name in soundNames)
        {
            if (!soundClipCache.ContainsKey(name))
            {
                AudioClip clip = AdvancedBundleLoader.SharedInstance.LoadAudioClip("musics", name);
                if (clip != null)
                {
                    soundClipCache[name] = clip;
                    Debug.Log($"预加载音效: {name}, 长度: {clip.length:F2}s");
                }
                yield return null; // 每帧加载一个，避免卡顿
            }
        }
    }
    
    // 播放音效（支持并发）
    public void PlaySoundEffect(string clipName, float time = 0, float volume = 0.25f)
    {
        if (!soundClipCache.TryGetValue(clipName, out AudioClip clip))
        {
            //Debug.LogWarning($"音效未预加载: {soundName}");
            clip = AdvancedBundleLoader.SharedInstance.LoadAudioClip("musics", clipName);
            soundClipCache.Add(clipName, clip);
            //return;
        }
        
        // 获取可用的AudioSource
        AudioSource source = GetAvailableAudioSource();
        if (source == null)
        {
            Debug.LogWarning("没有可用的AudioSource播放音效");
            return;
        }
        
        // 设置并播放
        source.clip = clip;
        source.volume = GameDataManager.Instance.UserData.IsSoundOn ? volume : 0;
        source.pitch = 1;
        source.loop = false;
        source.Play();
        
        // 自动回收
        StartCoroutine(ReleaseAudioSourceAfterPlay(source));
    }
    
    private AudioSource GetAvailableAudioSource()
    {
        // 1. 从对象池获取
        PoolObject poolObj = audioSourcePool.GetObject<PoolObject>();
        if (poolObj != null)
        {
            AudioSource source = poolObj.GetComponent<AudioSource>();
            activeSoundSources.Add(source);
            return source;
        }
        
        // 2. 对象池为空，检查是否有播放完毕的
        for (int i = activeSoundSources.Count - 1; i >= 0; i--)
        {
            AudioSource src = activeSoundSources[i];
            if (src != null && !src.isPlaying)
            {
                // 重置并重用
                src.Stop();
                src.clip = null;
                return src;
            }
        }
        
        // 3. 创建新的（最后手段）
        Debug.LogWarning("创建新的AudioSource，考虑增大对象池");
        GameObject newObj = Instantiate(audioSPrefab.gameObject, transform);
        AudioSource newSource = newObj.GetComponent<AudioSource>();
        activeSoundSources.Add(newSource);
        return newSource;
    }

    private void OnEnable()
    {
        // 预加载背景音乐
        string[] musicNames =
        {
            "music", "Button","ShowUI","Puzzle1","Puzzle2","Puzzle3" ,"Puzzle4","lianci",
            "chongzhidaoju","EnterStage","PassStage","SignWater","ciright","xuanzhecuowu"
        }; // 替换为实际的音乐名称
        StartCoroutine(PreloadCommonSounds(musicNames));
        
        StartCoroutine(PlayMusic(1.5f)); // 初始化音乐开关

        ApplyCriticalFixes();
    }
    
    private void ApplyCriticalFixes()
    {
        
        //修复1：确保使用正确的音频配置
        FixAudioConfiguration();
        
        
        // 修复4：监控和自动恢复
        StartCoroutine(AudioHealthMonitor());
    }
    
    private void FixAudioConfiguration()
    {
        // 这是最关键的一步！修正音频配置
        AudioConfiguration config = AudioSettings.GetConfiguration();
        
        // 针对华为设备的特定配置
        if (SystemInfo.deviceModel.Contains("HUAWEI"))
        {
            // 华为设备需要更大的缓冲区和特定的采样率
            config.dspBufferSize = 2048;  // 非常重要！
            config.sampleRate = 48000;    // 使用48kHz
            config.numVirtualVoices = 16; // 减少虚拟声音
            config.numRealVoices = 8;     // 减少真实声音
        }
        else
        {
            config.dspBufferSize = 1024;
            config.sampleRate = 44100;
        }
        
        // 应用配置
        if (!AudioSettings.Reset(config))
        {
            Debug.Log("音频配置重置失败！");
        }
        else
        {
            Debug.Log($"音频配置: 缓冲区={config.dspBufferSize}, " +
                      $"采样率={config.sampleRate / 1000}kHz");
        }
    }
    
    
    private IEnumerator AudioHealthMonitor()
    {
        float lastCountTime = Time.time;
        int lastFrameCount = Time.frameCount;
        
        while (true)
        {
            yield return new WaitForSeconds(0.5f); // 每500ms检查一次
            
            // 检查处理速度是否正常
            float timeDelta = Time.time - lastCountTime;
            int frameDelta = Time.frameCount - lastFrameCount;
            
            // 正常情况下，500ms应该处理约22帧（45FPS）
            if (frameDelta < 10) // 如果帧数太少
            {
                Debug.Log($"音频处理可能被阻塞: {timeDelta:F1}s内只处理了{frameDelta}帧");
                
                // 应急措施：降低音质
                StartCoroutine(EmergencyAudioRecovery());
            }
            
            lastCountTime = Time.time;
            lastFrameCount = Time.frameCount;
        }
    }
    
    private IEnumerator EmergencyAudioRecovery()
    {
        // 临时降低音频质量
        QualitySettings.SetQualityLevel(0, true);
        
        yield return new WaitForSeconds(1.0f);
        
        // 恢复
        QualitySettings.SetQualityLevel(2, true);
    }
    
    private IEnumerator PlayMusic(float transitionTime = 0.1f)
    {             
        yield return new WaitForSeconds(transitionTime);
        
        if (!GameDataManager.Instance.UserData.IsMusicOn)
        {
            musicSource.Stop(); // 如果音乐关闭，停止播放
        }
        else
        {
            PlayBackgroundMusic("music"); // 播放默认音乐
        }
    }
    
    public void ToggleMusic()
    {   
        if (!GameDataManager.Instance.UserData.IsMusicOn)
        {
            musicSource.Stop(); // 如果音乐关闭，停止播放
        }
        else
        {
            PlayBackgroundMusic("music"); // 播放默认音乐
        }
    }

    private IEnumerator ReleaseAudioSourceAfterPlay(AudioSource source)
    {
        // 等待音频播放完成 + 额外缓冲时间
        float waitTime = source.clip.length + 0.1f;
        yield return new WaitForSeconds(waitTime);
        
        // 停止并回收
        if (source != null && source.isPlaying)
        {
            source.Stop();
        }
        
        // 返回对象池
        PoolObject poolObj = source.GetComponent<PoolObject>();
        if (poolObj != null)
        {
            audioSourcePool.ReturnObjectToPool(poolObj);
            activeSoundSources.Remove(source);
        }
    }
    
    // 播放背景音乐（确保连续）
    public void PlayBackgroundMusic(string musicName)
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.priority = 0; // 最高优先级
            musicSource.bypassEffects = true;
        }
        
        // 异步加载但不阻塞播放
        StartCoroutine(LoadAndPlayMusic(musicName));
    }
    
    private IEnumerator LoadAndPlayMusic(string musicName)
    {
        // 如果当前有音乐，淡出
        if (musicSource.isPlaying)
        {
            yield return StartCoroutine(FadeOut(musicSource, 0.5f));
        }
        
        // 加载新音乐
        AudioClip newMusic = AdvancedBundleLoader.SharedInstance.LoadAudioClip("musics", musicName);
        if (newMusic != null)
        {
            musicSource.clip = newMusic;
            musicSource.loop = true;
            musicSource.volume = 0; // 从0开始
            musicSource.Play();
            
            // 淡入
            yield return StartCoroutine(FadeIn(musicSource, 1.0f, normalVolume));
        }
    }
    
    private IEnumerator FadeOut(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            source.volume = Mathf.Lerp(startVolume, 0, t / duration);
            yield return null;
        }
        source.volume = 0;
    }
    
    private IEnumerator FadeIn(AudioSource source, float duration, float targetVolume)
    {
        source.volume = 0;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            source.volume = Mathf.Lerp(0, targetVolume, t / duration);
            yield return null;
        }
        source.volume = targetVolume;
    }
    
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
}