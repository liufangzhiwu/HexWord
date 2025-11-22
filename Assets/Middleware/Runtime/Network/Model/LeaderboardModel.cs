using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeaderboardEntry
{
    public int user_id;
    public int rank;
    public int avatar;
    public string nickname;
    public int score;
    public string leaderboard_name;
    public string grouping;
    
}
public class LeaderboardRequest
{
    public string boardId;
}

public class LeaderboardResponse
{
    public LeaderboardEntry my;
    public string updated_at;
    public List<LeaderboardEntry> top;
    public List<LeaderboardEntry> middle;
    public List<LeaderboardEntry> bottom;
}