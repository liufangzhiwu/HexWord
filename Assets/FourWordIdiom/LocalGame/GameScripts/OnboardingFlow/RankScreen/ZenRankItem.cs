using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ZenRankState
{
    public int Rank;
    public int Avatar;
    public string Name;
    public string Level;
    public int Score;
    public int Reward;
}

public class ZenRankItem : MonoBehaviour
{
    [SerializeField] private GameObject Rank;
    [SerializeField] private Image Avatar;
    [SerializeField] private Text Name;
    [SerializeField] private GameObject Level;
    [SerializeField] private Text Score;
    [SerializeField] private Text ZenTitle;
    private void Start()
    {
        ZenTitle.text = MultilingualManager.Instance.GetString("ZenValue");
    }

    public void SetRankInfo(ZenRankState state)
    {
        Text RankText = Rank.GetComponentInChildren<Text>(true);
        Image RankIcon = Rank.GetComponentInChildren<Image>(true);
        RankText.gameObject.SetActive(false);
        RankIcon.gameObject.SetActive(false);
        Level.transform.GetChild(0).gameObject.SetActive(false);
        Level.transform.GetChild(1).gameObject.SetActive(false);
        switch (state.Rank)
        {
            case 1:
                RankIcon.gameObject.SetActive(true);
                RankIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Rankicon1");
                Level.transform.GetChild(0).GetComponentInChildren<Image>().sprite = LoadBox(state.Rank);
                Level.transform.GetChild(0).gameObject.SetActive(true);
                break;
            case 2:
                RankIcon.gameObject.SetActive(true);
                RankIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Rankicon2");
                Level.transform.GetChild(0).GetComponentInChildren<Image>().sprite = LoadBox(state.Rank);
                Level.transform.GetChild(0).gameObject.SetActive(true);
                break;
            case 3:
                RankIcon.gameObject.SetActive(true);
                RankIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Rankicon3");
                Level.transform.GetChild(0).GetComponentInChildren<Image>().sprite = LoadBox(state.Rank);
                Level.transform.GetChild(0).gameObject.SetActive(true);
                break;
            default:
                RankText.gameObject.SetActive(true);
                RankText.text = state.Rank.ToString();
                if (state.Reward > 0)
                {
                    Level.transform.GetChild(1).GetComponentInChildren<Text>().text = $"×{state.Reward}";
                    Level.transform.GetChild(1).gameObject.SetActive(true);
                }
                break;
        }
        Avatar.sprite = LoadheadIcon(state.Avatar);
        Name.text = state.Name;
        Score.text = state.Score.ToString();
    }

    private Sprite LoadheadIcon(int idx)
    {
        return AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("head" + idx);
    }
    private Sprite LoadBox(int idx)
    {
        return AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("RankBox" + idx);
    }
}
