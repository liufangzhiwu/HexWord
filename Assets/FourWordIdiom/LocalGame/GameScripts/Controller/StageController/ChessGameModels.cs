using System.Collections.Generic;

// ==========================================
// 关卡枚举
// ==========================================
public enum LevelModes
{
    Normal,
    Hard,
    ExtraHard,
}

// ==========================================
// 连击与反馈配置
// ==========================================
/// <summary>
/// 连击配置数据结构
/// </summary>
public class ComboConfig
{
    public string State;      // 加减分状态 (add, reduce)
    public int Combo;         // 连击状态
    public int Num;           // 加减分数值
    public int TimeLag;       // 时间窗口（秒）
}

/// <summary>
/// 局内正向反馈配置数据 (鼓励词)
/// </summary>
public class PraiseConfig
{
    public int FeedbackID;      // 反馈ID / 触发条件
    public int Priority;        // 优先级 (用于多条反馈同时满足时，决定播哪条)
    public int BannerStyle;     // 表现样式 (对应预制体或UI动效的类型)
    public float Probability;     // 触发概率 (通常是 0-100 的百分比，或是 0-10000 的万分比)
    public float TimeWindow;    // 时间窗口 (秒，用于控制反馈的有效时间或冷却间隔)
    public string[] TextLoop;   // 轮播文案 (解析后变成多语言Key的数组)
}

// ==========================================
// 拓展玩法机制配置 (冰块、花朵、树叶)
// ==========================================
public class Interval
{
    public int Mode;    // 关卡模式: 1=困难,2=极难
    public int Degree;  // 难度: 0=轻度,1=中度,2=重度
    public int Start;   // 开始关卡
    public int End;     // 结束关卡, 叹号表示后续所有关卡
}

/// <summary>
/// 冰块玩法配置
/// </summary>
public class IceConfig
{
    public bool IsOpen;     // 是否开启
    public int FirstLevel;   // 首次出现的关卡是
    public int FirstDegree;  // 首次的难度
    public Dictionary<int, int> Degree; // 难度配置 {难度级别:数量}
    public Dictionary<int, int> Fixed;  // 固定关卡配置  {关卡id,级别degree}
    public List<Interval> CycleLevels;   // 循环关卡配置
}

/// <summary>
/// 根据叶子的收集数量发放奖励
/// </summary>
public class LeafReward
{
    public int Number;   // 叶子数量
    public int Type;    // 奖励类型
    public int Value;   // 数量
}

/// <summary>
/// 叶子玩法配置
/// </summary>
public class LeafConfig
{
    public bool IsOpen;
    public int FirstLevel;   // 首次出现的
    public List<int> CycleLevels; // 循环关卡, 每个位数匹配出现
    public List<LeafReward> Rewards; // 奖励列表
}

/// <summary>
/// 花朵玩法配置
/// </summary>
public class FlowerConfig
{
    public bool IsOpen;
    public int FirstLevel;
    public int FirstDegree;
    public int InitNumber;      // 初始消除最近花朵数量
    public int MinNumber;       // 最小消除最近花朵数量
    public Dictionary<int, int> Degree; // 难度配置 {难度级别:数量}
    public Dictionary<int, int> Fixed;  // 固定关卡配置  {关卡id,级别degree}
    public List<Interval> CycleLevels;  // 循环关卡配置
}

/// <summary>
/// 答题环境上下文，用于正反馈判定
/// </summary>
public struct PraiseContext
{
    public bool IsFirstWord;        // 是否是本关解开的第一个词
    public int InitialLettersCount; // 该词初始自带的字数
    public int ErrorsOnThisWord;    // 答对这个词之前，在这个词上判错的次数
    public int WordsRemaining;      // 答对该词后，关卡还剩下的词数
    public int CurrentCombo;        // 当前连击数
    public int TotalErrorsInLevel; // 本关卡累计的总错误次数
}