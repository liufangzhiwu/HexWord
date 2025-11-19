using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Water2D;

public class WaterManager : MonoBehaviour
{
    public static WaterManager instance;
    public GameObject WaterGame;
    public Water2D_Spawner Water2DSpawner;
    public Action<int> OnWaterProgress;
    public int waterParCount;
    public Camera waterCamera;
    public List<GameObject> lines;
    public GameObject water;
    public GameObject hu;
    public GameObject beizi;
    public GameObject beizikuang;
    public Camera waterqundCamera;
    
    [Header("基准分辨率")]
    private float targetWidth = 9f;   // 基准宽度比例（如16:9中的16）
    private float targetHeight = 16f;   // 基准高度比例（如16:9中的9）

    [Header("像素单位配置")]
    private float pixelsPerUnit = 100f; // 与Sprite的PPU一致
    
    public DateTime StartTime; // 与Sprite的PPU一致

    private void Awake()
    {
        instance = this;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CheckShowWater();
            
        beizikuang.GetComponent<SpriteRenderer>().DOFade(0, 0);
        beizi.GetComponent<SpriteRenderer>().DOFade(0, 0);

        StartTime = DateTime.Now;
        UpdateOrthographicSize();
    }
    
    void UpdateOrthographicSize()
    {
        if (!UIUtilities.IsiPad())
        {
            float baseRatio = UIUtilities.REFERENCE_WIDTH /  UIUtilities.REFERENCE_HEIGHT;

            float curscreenRatio = Screen.width / (float)Screen.height;

            float scale = curscreenRatio / baseRatio;
            float scaleFactor = 3.68f;
            waterCamera.orthographicSize = scaleFactor;
            waterqundCamera.orthographicSize = scaleFactor;
        }
        else
        {
            waterCamera.orthographicSize = 3.68f;
            waterqundCamera.orthographicSize = 3.68f;
        }
        
        // 比较当前比例与基准比例
        // if (screenAspect >= targetAspect)
        // {
        //     // 如果屏幕更宽，以高度为基准，调整宽度
        //     //waterCamera.orthographicSize = Screen.height / (5.5f * pixelsPerUnit);
        //     //waterqundCamera.orthographicSize = Screen.height / (5.5f * pixelsPerUnit);
        // }
        // else
        // {
        //     if (screenAspect == 0.5f)
        //     {
        //         float rate = Mathf.Round(screenAspect * 100f) / 100f;
        //         waterCamera.orthographicSize =  defoutzide+rate*1f;
        //         waterqundCamera.orthographicSize = defoutzide+rate*1f;
        //     }
        //     else
        //     {
        //         // 如果屏幕更高或比例一致，以宽度为基准，调整高度
        //         float scale = targetAspect / screenAspect;
        //         float rate = Mathf.Round(screenAspect * 100f) / 100f;
        //         //waterCamera.orthographicSize = (Screen.height / (22.8f * pixelsPerUnit)) * scale;
        //         //waterqundCamera.orthographicSize = Screen.height / (22.8f * pixelsPerUnit)* scale;
        //         waterCamera.orthographicSize =  defoutzide+rate*1.92f;
        //         waterqundCamera.orthographicSize = defoutzide+rate*1.92f;
        //     }
        // }
    }

    public void CheckShowWater()
    {
        waterParCount = 0;
        if (GameDataManager.instance.UserData.signid > 0)
        {           
            if (GameDataManager.instance.UserData.signid >= 4)
            {
                waterCamera.gameObject.SetActive(false);
                Water2DSpawner.gameObject.SetActive(true);
                WaterGame.SetActive(true);
                water.gameObject.SetActive(true);
                water.transform.DOScaleY(0.75f, 0f);
            }
            else
            {
                //Water2DSpawner.instance.DelayBetweenParticles = 0.001f;
                waterCamera.gameObject.SetActive(false);
                Water2DSpawner.gameObject.SetActive(true);
                WaterGame.SetActive(true);
                PlayerWater(true);
                water.gameObject.SetActive(true);
                float yscale = 0.2f * GameDataManager.instance.UserData.signid;
                water.transform.DOScaleY(yscale, 0f);

                switch (GameDataManager.instance.UserData.signid)
                {
                    case 1:
                        water.transform.DOLocalMoveY(0.32f, 0f);
                        break;
                    case 2:
                        water.transform.DOLocalMoveY(0.52f, 0f);
                        break;
                    case 3:
                        water.transform.DOLocalMoveY(0.63f, 0f);
                        break;
                }
            }

            for (int i = 0; i < GameDataManager.instance.UserData.signid; i++)
            {
                lines[i].gameObject.SetActive(false);
            }
        }
        else
        {
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i].gameObject.SetActive(true);
            }
        }


        beizikuang.GetComponent<SpriteRenderer>().DOFade(0, 0);
        beizi.GetComponent<SpriteRenderer>().DOFade(0, 0);
        hu.GetComponent<SpriteRenderer>().DOFade(0, 0);

    }


    public void WaterShow(bool isShow)
    {
        Water2DSpawner.instance.DelayBetweenParticles = 0.0321f;
          
        if (isShow)
        {
           
            beizikuang.GetComponent<SpriteRenderer>().DOFade(1, 0.3f);
            beizi.GetComponent<SpriteRenderer>().DOFade(1, 0.3f);
            hu.GetComponent<SpriteRenderer>().DOFade(1, 0.3f);
        }
        else
        {
            beizikuang.GetComponent<SpriteRenderer>().DOFade(0, 0.2f);
            beizi.GetComponent<SpriteRenderer>().DOFade(0, 0.2f);
            hu.GetComponent<SpriteRenderer>().DOFade(0, 0.2f);
        }    
            
        Water2DSpawner.transform.DOScale(1f, 0.1f).OnComplete(() =>
        {
            Water2DSpawner.gameObject.SetActive(isShow);
            waterCamera.gameObject.SetActive(isShow);
        });
        WaterGame.SetActive(true);
    }

    public void ShowDaoWater(bool isShow)
    {
        waterqundCamera.gameObject.SetActive(isShow);
    }

    
    public void PlayerWater(bool isenter=false,int value=0)
    {
        if (isenter)
        {
            Water2DSpawner.Spawn(isenter);
        }
        else
        {
            GameDataManager.instance.UserData.UpdateSignid();
            GameDataManager.instance.UserData.UpdateGold(value, false, false,"签到广告获得");

            hu.transform.DOLocalRotate(new Vector3(0f, 0f, 35f), 1.2f, RotateMode.Fast).OnComplete(() =>
            {
                Water2DSpawner.Speed = 2.5f;
                ShowDaoWater(true);
                water.gameObject.SetActive(false);
                Water2DSpawner.Spawn(isenter);
                AudioManager.Instance.PlaySoundEffect("SignWater");                
            });
        }
            
    }
    
    public void WaterPause()
    {
        hu.transform.DOLocalRotate(new Vector3(0f, 0f, 0f), 0.2f, RotateMode.Fast);
        Water2DSpawner.StopSpawning();
    }

    public void ClearWater()
    {
        WaterPause();
        water.gameObject.SetActive(false);
        Water2DSpawner.Restore();
        CheckShowWater();
    }
        
    public void TriggerWaterline(int waterPar)
    {
        waterParCount += waterPar;
        if (!Water2DSpawner.instance.isEnter)
        {
            int waterLine = GameDataManager.instance.UserData.signid-1;
            lines[waterLine].gameObject.SetActive(false);
            OnWaterProgress?.Invoke(waterLine);
        }
        else
        {
            WaterPause();
        }
    }
  
}
