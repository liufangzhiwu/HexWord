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

    private void Start()
    {
        skeletonGraphic.AnimationState.Data.DefaultMix = 0.2f;
    }

    /// <summary>
    /// 外部调用这个方法，传入图片
    /// </summary>
    public void InitializeButterfly(Sprite body, Sprite wing)
    {
        StartCoroutine(InitRoutine(body, wing));
    }
    public bool IsReady() => _isReady;
    private IEnumerator InitRoutine(Sprite body, Sprite wing)
    {
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
        wingL.sprite = wing;
        wingR.sprite = wing;
        bodyI.sprite = body;
        if (skeletonGraphic == null) skeletonGraphic = GetComponent<SkeletonGraphic>();

        var skeleton = skeletonGraphic.Skeleton;
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
        ApplyToSkin(customSkin, "shengti", body, sourceMat);
        ApplyToSkin(customSkin,"chibang", wing, sourceMat);
        ApplyToSkin(customSkin,"chibang2", wing,sourceMat);
        
        Skin repackedSkin = customSkin.GetRepackedSkin("FinalSkin", sourceMat, out Material runtimeMat, out Texture2D runtimeTexture, 1024);
        
        skeleton.SetSkin(repackedSkin);
        skeleton.SetSlotsToSetupPose();
        skeletonGraphic.OverrideTexture = runtimeTexture;
        skeletonGraphic.Update(0);
        Debug.Log("蝴蝶整体换装成功！");
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
    
    string myLog = "";
    void OnEnable() { Application.logMessageReceived += HandleLog; }
    void OnDisable() { Application.logMessageReceived -= HandleLog; }
    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            myLog += logString + "\n";
        }
    }
    void OnGUI()
    {
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 3.0f); // 放大字体
        GUI.Label(new Rect(10, 10, 500, 800), myLog);
    }
}
