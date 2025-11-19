using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Random = UnityEngine.Random;


public class CustomFlyInManager : MonoBehaviour
{
    public static CustomFlyInManager  Instance;
    [HideInInspector] public GameObject GoldObj;
    [HideInInspector] public GameObject GoldPrefab;
    private float BizerValue = 3.0f;
    
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GoldPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "GameGole");
    }
    
    public void FlyInGold(Transform start,Action call=null,int count=5)
    {
        Vector3 scale = start.localScale;
        bool isaudio=true;
        if(count>=5) scale=new Vector3(0.85f,0.85f,0.85f);
        if (count == 1)
        {
            scale=new Vector3(0.65f,0.65f,0.65f);
            isaudio = false;
        }
        //BizerValue = Random.Range(1.3f, 4.5f);
        StartCoroutine(FlyInValueGold(start,count,scale,call,isaudio));
    }

    IEnumerator FlyInValueGold(Transform start,int count,Vector3 scale,Action call,bool isaudio)
    {
        for (int i = 0; i < count; i++)
        {
            float s = 0.55f - i * 0.01f;
            yield return new WaitForSeconds(0.085f);
            if (i<4&&isaudio)
                AudioManager.Instance.PlaySoundEffect("filyGold");
            StartCoroutine(FlyInGoldCoroutine(start,GoldObj.transform,GoldPrefab,true,null,scale,s));
        }
        yield return new WaitForSeconds(0.35f);
        call?.Invoke();
    }

    public void FlyIn(Transform start,Transform target,GameObject effect,Action call,float duration=0f)
    {
        StartCoroutine(FlyInCoroutine(start,target,effect,call,duration));
    }
    
    public void FlyIn(Vector3 start,Vector3 target,GameObject effect,Action call)
    {
        StartCoroutine(FlyVectorToEnd(start,target,effect,true,call));
    }
    
    private IEnumerator FlyVectorToEnd(Vector3 start,Vector3 target,GameObject gold,bool isCurve,Action call,float duration=0.35f)
    {
        Vector3 endPosition = target; // 设置起始位置
        
        if (isCurve)
        {
            var midPos = (endPosition + start) / 2;
            var BezierMidPos = (midPos + start) / 2 + Vector3.left * 50;
            //var MidEndPos = (midPos + endPosition) / 2 + Vector3.right *0.78f;
            Vector3[] MovePoints = CreatTwoBezierCurve(start,endPosition,BezierMidPos).ToArray();
            
            // 计算目标旋转（例如，设置一个随机旋转或特定旋转）
            // 计算目标旋转（仅 Z 轴旋转）
            Vector3 targetRotation = new Vector3(0, 0, Random.Range(60,240)); // 沿 Z 轴旋转 360 度

            // 移动和旋转
            Sequence sequence = DOTween.Sequence();
    
            sequence.Append(gold.transform.DOLocalPath(MovePoints, duration).SetEase(Ease.InCubic));
            sequence.Join(gold.transform.DORotate(targetRotation, duration).SetEase(Ease.Linear));
    
            sequence.OnComplete(() =>
            {
                call?.Invoke();
            });
        }
        else
        {
            gold.transform.DOMove(endPosition,duration).SetEase(Ease.InCubic).OnComplete(() =>
            {
                call?.Invoke();
                // 确保元素最终位置在目标位置
            });
        }
        yield return new WaitForSeconds(5.0f);
    }
    
    private IEnumerator FlyInGoldCoroutine(Transform start,Transform target,GameObject gold,bool isCurve,Action call,Vector3 scale,float duration=0.45f)
    {
        GameObject Gold = Instantiate(gold,SystemManager.Instance._uiRoot);
        Gold.transform.position = start.position; // 设置起始位置
        Gold.transform.localScale = scale;
       
        Vector3 endPosition = target.position; // 设置起始位置

        // 计算距离
        float distance = Vector3.Distance(start.position, endPosition);
        float speed = 20.0f; // 例如：每秒移动2个单位
        duration = distance / speed;
        if(duration<0.45f) duration = 0.45f;
        
        // 根据距离计算移动时长
        Debug.LogWarning("金币运动 距离："+distance+"时长"+duration);
        Color color = Gold.GetComponent<Image>().color; // 获取当前颜色
        color.a = 0; // 设置透明度为 0
        Gold.GetComponent<Image>().color = color;
        Gold.GetComponent<Image>().DOFade(1, 0.2f);
        
        if (isCurve)
        {
            var midPos = (endPosition + start.position) / 2;
            var BezierMidPos = (midPos + start.position) / 2 + Vector3.up * 2;
            //var MidEndPos = (midPos + endPosition) / 2 + Vector3.right *0.78f;
            Vector3[] MovePoints = CreatTwoBezierCurve(start.position,endPosition,BezierMidPos).ToArray();
            Gold.transform.DOPath(MovePoints, duration).SetEase(Ease.Linear).OnComplete(() =>
            {
                call?.Invoke();
                // 确保元素最终位置在目标位置
            });
        }
        else
        {
            Gold.transform.DOMove(endPosition,duration).SetEase(Ease.Linear).OnComplete(() =>
            {
                call?.Invoke();
                // 确保元素最终位置在目标位置
            });
        }
        
        Gold.transform.DOScale(new Vector3(0.78f,0.78f,0), duration);
        yield return new WaitForSeconds(0.2f);
        AudioManager.Instance.TriggerVibration();
        AudioManager.Instance.PlaySoundEffect("filyGold");
        yield return new WaitForSeconds(duration);
        Gold.GetComponent<Image>().DOFade(0, 0.1f).OnComplete(() =>
        {
            Gold.gameObject.SetActive(false);
        });
        
        yield return new WaitForSeconds(5.0f);
        Destroy(Gold.gameObject);
    }
    
    /// <summary>
    ///二阶贝塞尔,nultiple光滑度
    /// </summary>
    public List<Vector3> CreatTwoBezierCurve(Vector3 startPoint, Vector3 endPoint, Vector3 middlePoint, int nultiple = 5)
    {
        List<Vector3> allPoints = new List<Vector3>();
        for (int i = 0; i < nultiple; i++)
        {
            float tempPercent = (float)i / (float)nultiple;
            float dis1 = Vector3.Distance(startPoint, middlePoint);
            Vector3 point1 = startPoint + Vector3.Normalize(middlePoint - startPoint) * dis1 * tempPercent;
            float dis2 = Vector3.Distance(middlePoint, endPoint);
            Vector3 point2 = middlePoint + Vector3.Normalize(endPoint - middlePoint) * dis2 * tempPercent;
            float dis3 = Vector3.Distance(point1, point2);
            Vector3 linePoint = point1 + Vector3.Normalize(point2 - point1) * dis3 * tempPercent;
            allPoints.Add(linePoint);
        }
        allPoints.Add(endPoint);
        return allPoints;
    }
    
    private IEnumerator FlyInCoroutine(Transform start,Transform target,GameObject effect,Action call,float duration=0f)
    {
        GameObject Effect = Instantiate(effect);
        Effect.transform.position = start.position; // 设置起始位置
        Effect.gameObject.SetActive(true);
        Vector3 endPosition = target.position; // 设置起始位置
        // 计算距离
        float distance = Vector3.Distance(start.position, endPosition);

        // 根据距离计算移动时长
        // 根据距离计算移动时长
        if(duration<0.2f)  duration = distance / 30f;

        if(duration<0.45f) duration = 0.45f;
        Debug.LogWarning("提示道具粒子效果运动 距离："+distance+"时长"+duration);
        
        var midPos = (endPosition + start.position) / 2;
        var BezierMidPos = (midPos + start.position) / 2 + Vector3.right * 2;
        //var MidEndPos = (midPos + endPosition) / 2 + Vector3.right *0.78f;
        Vector3[] MovePoints = CreatTwoBezierCurve(start.position,endPosition,BezierMidPos).ToArray();
        
        Effect.transform.DOPath(MovePoints, duration).SetEase(Ease.Linear).OnComplete(() =>
        {
            call?.Invoke();
            // 确保元素最终位置在目标位置
            Effect.gameObject.SetActive(false);
        });
        
        //Effect.transform.DOMove(endPosition,duration).SetEase(Ease.Linear).OnComplete(() =>
        //{
        //    call?.Invoke();
        //    // 确保元素最终位置在目标位置
        //   
        //});
       
       yield return new WaitForSeconds(5.0f);
       Destroy(Effect.gameObject);
    }
}


