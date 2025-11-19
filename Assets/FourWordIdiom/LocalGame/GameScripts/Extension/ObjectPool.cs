using UnityEngine;
using System.Collections;
using System.Collections.Generic;


#region 枚举类型
/// <summary>
/// 池化管理行为模式
/// </summary>
public enum PoolBehaviour
{
    /** 通过GameObject.SetActive控制显示 */
    GameObject,
    /** 通过CanvasGroup属性控制UI元素 */
    CanvasGroup
}
#endregion



public class PoolObject : MonoBehaviour
{
    #region Member Variables

    public bool isInPool;
    public ObjectPool pool;
    public CanvasGroup canvasGroup;

    #endregion
}


/// <summary>
/// 对象池管理系统（支持GameObject和CanvasGroup两种模式）
/// </summary>
public class ObjectPool
{
    #region 成员变量
    /** 预制体模板 */
    private GameObject objectPrefab = null;
    /** 池中所有对象列表 */
    private List<PoolObject> poolObjects = new List<PoolObject>();
    /** 对象池父节点 */
    private Transform parent = null;
    /** 当前池化管理模式 */
    private PoolBehaviour poolBehaviour = PoolBehaviour.GameObject;
    #endregion
       

    #region 公共方法
    /// <summary>
    /// 构造函数初始化对象池
    /// </summary>
    /// <param name="objectPrefab">对象预制体</param>
    /// <param name="parent">父节点</param>
    /// <param name="initcount">初始对象数量</param>
    /// <param name="poolBehaviour">管理模式</param>
    public ObjectPool(GameObject objectPrefab, Transform parent = null, 
        int initcount = 3, PoolBehaviour poolBehaviour = PoolBehaviour.GameObject)
    {
        // **核心初始化逻辑**
        this.objectPrefab = objectPrefab;
        this.parent = parent;
        this.poolBehaviour = poolBehaviour;

        // 预生成初始对象
        for (int i = 0; i < initcount; i++)
        {
            CreateObject();
        }
    }

    /// <summary>
    /// 创建对象池容器（组织场景中的池对象）
    /// </summary>
    public static Transform CreatePoolContainer(Transform containerParent, string name = "pool_container")
    {
        GameObject container = new GameObject(name);
        container.SetActive(true);
        container.transform.SetParent(containerParent);
        container.transform.localScale = Vector3.one;
        return container.transform;
    }

    /// <summary>
    /// 静态方法回收对象到所属池
    /// </summary>
    public static void ReturnObjectToPool(GameObject gameObject)
    {
        // **通过组件验证对象归属**
        PoolObject poolObject = gameObject.GetComponent<PoolObject>();
        if (poolObject == null)
        {
            Debug.LogWarning($"[ObjectPool] {gameObject.name} 不属于任何对象池");
            return;
        }
        poolObject.pool.ReturnObjectToPool(poolObject);
    }

    /// <summary>
    /// 获取可用对象（自动扩容）
    /// </summary>
    /// <returns>可用的游戏对象</returns>
    public GameObject GetObject()
    {
        bool temp;
        return GetObject(out temp);
    }

    /// <summary>
    /// 获取对象并返回是否新建实例
    /// </summary>
    /// <param name="instantiated">是否新建对象</param>
    public GameObject GetObject(out bool instantiated)
    {
        instantiated = false;
        PoolObject poolObject = null;

        // **遍历查找可用对象**
        foreach (var obj in poolObjects)
        {
            if (obj.isInPool)
            {
                poolObject = obj;
                break;
            }
        }

        // 无可用对象时创建新实例
        if (poolObject == null)
        {
            poolObject = CreateObject();
            instantiated = true;
        }

        // 根据模式激活对象
        switch (poolBehaviour)
        {
            case PoolBehaviour.GameObject:
                poolObject.gameObject.SetActive(true); // **标准激活方式**
                break;
            case PoolBehaviour.CanvasGroup:
                var cg = poolObject.canvasGroup;
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true; // **UI元素激活方式**
                break;
        }

        poolObject.isInPool = false;
        return poolObject.gameObject;
    }

    /// <summary>
    /// 获取对象到指定父节点
    /// </summary>
    public GameObject GetObject(Transform parent)
    {
        bool temp;
        return GetObject(parent, out temp);
    }

    /// <summary>
    /// 获取对象到指定父节点并返回是否新建
    /// </summary>
    public GameObject GetObject(Transform parent, out bool instantiated)
    {
        GameObject obj = GetObject(out instantiated);
        obj.transform.SetParent(parent, false); // **设置父节点保持本地坐标**
        return obj;
    }

    /// <summary>
    /// 获取带指定组件的对象（泛型版本）
    /// </summary>
    public T GetObject<T>(Transform parent) where T : Component
    {
        return GetObject(parent).GetComponent<T>();
    }

    /// <summary>
    /// 获取组件对象并返回是否新建
    /// </summary>
    public T GetObject<T>(Transform parent, out bool instantiated) where T : Component
    {
        return GetObject(parent, out instantiated).GetComponent<T>();
    }

    /// <summary>
    /// 获取带组件的对象（无指定父节点）
    /// </summary>
    public T GetObject<T>() where T : Component
    {
        return GetObject().GetComponent<T>();
    }

    /// <summary>
    /// 获取组件对象并返回是否新建
    /// </summary>
    public T GetObject<T>(out bool instantiated) where T : Component
    {
        return GetObject(out instantiated).GetComponent<T>();
    }

    /// <summary>
    /// 批量回收所有对象到池中
    /// </summary>
    public void ReturnAllObjectsToPool()
    {
        foreach (var obj in poolObjects)
        {
            ReturnObjectToPool(obj);
        }
    }

    /// <summary>
    /// 销毁池中所有对象
    /// </summary>
    public void DestroyAllObjects()
    {
        foreach (var obj in poolObjects)
        {
            GameObject.Destroy(obj);
        }
        poolObjects.Clear();
    }
    #endregion

    #region 私有方法
    /// <summary>
    /// 创建新对象并初始化
    /// </summary>
    private PoolObject CreateObject()
    {
        GameObject obj = GameObject.Instantiate(objectPrefab);
        PoolObject poolObject = obj.AddComponent<PoolObject>();
        poolObject.pool = this;

        // 初始化CanvasGroup组件
        if (poolBehaviour == PoolBehaviour.CanvasGroup)
        {
            poolObject.canvasGroup = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();
        }

        poolObjects.Add(poolObject);
        ReturnObjectToPool(poolObject); // **创建后立即回收**
        return poolObject;
    }

    /// <summary>
    /// 回收单个对象到池中
    /// </summary>
    public void ReturnObjectToPool(PoolObject poolObject)
    {
        // **根据模式停用对象**
        switch (poolBehaviour)
        {
            case PoolBehaviour.GameObject:
                poolObject.gameObject.SetActive(false);
                break;
            case PoolBehaviour.CanvasGroup:
                var cg = poolObject.canvasGroup;
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                break;
        }

        poolObject.transform.SetParent(parent, false);
        poolObject.isInPool = true;
    }
    #endregion
}
