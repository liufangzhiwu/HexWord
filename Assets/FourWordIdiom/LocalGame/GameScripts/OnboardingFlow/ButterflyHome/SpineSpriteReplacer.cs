using System;
using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using Spine.Unity.AttachmentTools; // 🔥 必须引用这个
using UnityEngine;
using UnityEngine.UI;

public class SpineSpriteReplacer : MonoBehaviour
{
    [SerializeField] private SkeletonGraphic skeletonGraphic;
    private bool _isReady = false; // 标记是否换装完成
    // Start is called before the first frame update
    [SerializeField] private Image wingL;
    [SerializeField] private Image wingR;
    [SerializeField] private Image bodyI;
    private void Awake()
    {
        skeletonGraphic = GetComponent<SkeletonGraphic>();
        skeletonGraphic.color = new Color(1, 1, 1, 0);
    }

    private void OnEnable()
    {
        transform.localScale = Vector3.one;
    }

    private void Start()
    {
        if (skeletonGraphic == null) 
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        // 核心修复：检查 Spine 是否有效，如果未初始化，强制初始化
        if (skeletonGraphic != null)
        {
            if (!skeletonGraphic.IsValid)
            {
                skeletonGraphic.Initialize(false);
            }

            // 再次检查 AnimationState 是否存在，防止初始化失败导致的报错
            if (skeletonGraphic.AnimationState != null)
            {
                skeletonGraphic.AnimationState.Data.DefaultMix = 0.2f;
            }
        }
    }

    // private void OnEnable()
    // {
    //     // Application.logMessageReceived += HandleLog;
    //     // if (skeletonGraphic != null)
    //     // {
    //     //     skeletonGraphic.OverrideTexture = null;
    //     //     // 设为透明，等待 InitRoutine 里的 Repack 完成后再变白
    //     //     skeletonGraphic.color = new Color(1, 1, 1, 0); 
    //     // }
    // }
    
    /// <summary>
    /// 外部调用这个方法，传入图片
    /// </summary>
    public void InitializeButterfly(Sprite body, Sprite wing)
    {
        wingL.sprite = wing;
        wingR.sprite = wing;
        bodyI.sprite = body;
        // StartCoroutine(InitRoutine(body, wing));
    }
    public bool IsReady() => _isReady;
    private IEnumerator InitRoutine(Sprite body, Sprite wing)
    {     
        // 1. 强制初始化
        if (!skeletonGraphic.IsValid)
        {
            skeletonGraphic.Initialize(true); 
        }
   
        // 2. 🔥 死等材质球出现！
        // 第一次运行时，Initialize 后可能需要一点时间才能把默认材质加载出来
        // 如果这时候去换装，skeletonGraphic.material 是 null，必挂。
        float waitTime = 0;
        while (skeletonGraphic.material == null && waitTime < 1.0f)
        {
            yield return null;
            waitTime += Time.deltaTime;
        }
        // 如果等了1秒还是空的，手动赋值一个防崩溃
        if (skeletonGraphic.material == null)
        {
            Debug.LogWarning("材质球加载超时，手动创建 fallback 材质");
            skeletonGraphic.material = new Material(Shader.Find("Spine/SkeletonGraphic"));
        }
        // 3. 初始隐身
        skeletonGraphic.color = new Color(1, 1, 1, 0);
        // 1. 等待一帧，确保 Spine 初始化完毕
        yield return null; 
        // 2. 执行换装 (这里是你之前的核心逻辑，不要用 Image.sprite = ...)
        // 这里会产生那次“卡顿”，但因为我们还没显示出来，玩家感觉不到
        DoSpineRepackLogic(body, wing); 

        // 3. 换装完毕，把蝴蝶显示出来 (淡入或者直接显示)
        skeletonGraphic.color = Color.white; 
        _isReady = true;
    }
    public void DoSpineRepackLogic(Sprite body, Sprite wing)
    {
        Debug.Log(">>> 查看贴图 " + body +" " + wing );
        if (skeletonGraphic == null) skeletonGraphic = GetComponent<SkeletonGraphic>();
        try
        {
            wingL.sprite = wing;
            wingR.sprite = wing;
            bodyI.sprite = body;
            var skeleton = skeletonGraphic.Skeleton;
            if (skeletonGraphic.material == null) 
            {
                // 手动加载一个默认材质，防止传 null 进去崩溃
                // skeletonGraphic.material = Resources.Load<Material>("SkeletonGraphicDefault"); 
                // 或者直接创建一个新的
                skeletonGraphic.material = new Material(Shader.Find("Spine/SkeletonGraphic"));
            }
            
            Skin customSkin = new Skin("CombinedSkin");
            if (skeleton.Data.DefaultSkin != null)
            {
                customSkin.AddSkin(skeleton.Data.DefaultSkin);
            }

            Material originalMat = skeletonGraphic.material;
            Material sourceMat;
            if (originalMat != null && originalMat.shader != null)
            {
                sourceMat = new Material(originalMat);
            }
            else
            {
                // 万一原来的材质真的丢了（极少见），才做这个保底 fallback
                Debug.LogWarning("原材质丢失，尝试手动查找 Shader");
                sourceMat = new Material(Shader.Find("Spine/SkeletonGraphic"));
            }
            if (sourceMat == null)
            {
                Debug.LogError(">>> 致命错误: sourceMat 是空的！第一次运行材质还没生成？");
                // 尝试强制修复
                sourceMat = new Material(Shader.Find("Spine/SkeletonGraphic"));
            }
            ApplyToSkin(customSkin, "shengti", body, sourceMat);
            ApplyToSkin(customSkin, "chibang", wing, sourceMat);
            ApplyToSkin(customSkin, "chibang2", wing, sourceMat);
            Debug.Log(">>> 步骤3: 附件映射完毕");
            
            Skin repackedSkin = customSkin.GetRepackedSkin("FinalSkin", sourceMat, out Material runtimeMat,
                out Texture2D runtimeTexture, 1024);
            // 🔥🔥🔥 核心修复：强制设置 Shader 关键字 🔥🔥🔥
            // if (runtimeMat != null)
            // {
            //     // 因为你用的是 Unity 原生 Sprite (Straight Alpha) 换装
            //     // 而 Spine 默认材质通常是 Premultiply Alpha
            //     // 混合在一起容易出问题，这里强制开启 "直线 Alpha" 模式
            //     runtimeMat.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
            //
            //     // 如果用了 CanvasGroup (比如透明度渐变)，也要开启这个
            //     runtimeMat.EnableKeyword("_CANVAS_GROUP_COMPATIBLE");
            // }
            skeleton.SetSkin(repackedSkin);
            skeleton.SetSlotsToSetupPose();
            skeletonGraphic.OverrideTexture = runtimeTexture;
            skeletonGraphic.material = runtimeMat;
            // 1. 告诉 Canvas，材质变了
            skeletonGraphic.SetMaterialDirty();
            // 2. 告诉 Canvas，顶点/网格变了
            skeletonGraphic.SetVerticesDirty();
            skeletonGraphic.Update(0);
            Debug.Log(">>> 蝴蝶整体换装成功！");
        }
        catch (Exception e)
        {
            Debug.LogError($">>> 换装崩溃！原因: {e.Message}\n堆栈: {e.StackTrace}");
        }
    }

    private void ApplyToSkin(Skin targetSkin, string slotName, Sprite newSprite, Material mat)
    {
        if (newSprite == null) return;
        
        var skeleton = skeletonGraphic.Skeleton;
        Slot slot = skeleton.FindSlot(slotName);
        Attachment template = slot.Attachment;
        if (template == null)
        {
            template = skeleton.Data.DefaultSkin.GetAttachment(slot.Data.Index, slotName);
            if (template == null)
            {
                Debug.LogWarning($"插槽 {slotName} 没有模板附件，跳过替换");
                return;
            }
        }

        Attachment newAttachment = template.GetRemappedClone(newSprite, mat);
        // 🔥🔥🔥 核心修复：强制设为不透明白色 🔥🔥🔥
        // 无论原图是不是透明的，新图都必须显示出来
        if (newAttachment is RegionAttachment region)
        {
            region.SetColor(Color.white); // 强制白色
            // region.UpdateOffset(); // 刷新偏移，防止变形
            region.UpdateRegion();
        }
        else if (newAttachment is MeshAttachment mesh)
        {
            mesh.SetColor(Color.white); // 强制白色
            // mesh.UpdateUVs(); // 刷新UV
            mesh.UpdateRegion();
        }
        targetSkin.SetAttachment(slot.Data.Index, slotName, newAttachment);
    }
    
    // string myLog = "";
    //
    // void OnDisable() { Application.logMessageReceived -= HandleLog; }
    // void HandleLog(string logString, string stackTrace, LogType type)
    // {
    //     if (type == LogType.Error || type == LogType.Exception)
    //     {
    //         myLog += logString + "\n";
    //     }
    // }
    // void OnGUI()
    // {
    //     GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 3.0f); // 放大字体
    //     GUI.Label(new Rect(10, 10, 500, 800), myLog);
    // }
}
