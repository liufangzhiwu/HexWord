
using UnityEngine;

namespace Middleware
{
    public abstract class SingletonMono<T> : MonoBehaviour where T : SingletonMono<T>
{
    private static T _instance;
    private static bool _applicationIsQuitting = false;
    private static readonly object _lock = new object();

    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed. Returning null.");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    // 尝试查找现有实例
                    _instance = FindObjectOfType<T>();
                    
                    if (_instance == null)
                    {
                        // 创建新的GameObject和实例
                        GameObject singletonObject = new GameObject(typeof(T).Name);
                        _instance = singletonObject.AddComponent<T>();
                        
                        DontDestroyOnLoad(singletonObject);
                        Debug.Log($"[Singleton] Created new singleton instance of {typeof(T)}");
                    }
                    else
                    {
                        DontDestroyOnLoad(_instance.gameObject);
                    }
                }
                
                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        // 确保只有一个实例存在
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this as T;
        DontDestroyOnLoad(gameObject);
    }
    
    public virtual void Init()
    {

    }
    

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        // 只有当被销毁的是当前实例时才重置静态变量
        if (_instance == this)
        {
            _applicationIsQuitting = true;
            _instance = null;
        }
    }
}
}