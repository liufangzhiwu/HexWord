/// <summary>
/// 字块选中状态枚举
/// </summary>
public enum TileSelectionState
{
    /// <summary> 未选中任何字块 </summary>
    None,
    /// <summary> 已开始选中并完成选择 </summary>
    Selected
}

/// <summary>
/// 字块数据类 - 表示拼图游戏中的单个字块元素
/// </summary>
public class PuzzleTile
{
    #region 公开属性
    
    /// <summary> 字块所在行索引 (从0开始) </summary>
    public int Row { get; private set; }
    
    /// <summary> 字块所在列索引 (从0开始) </summary>
    public int Column { get; private set; }

    public int Layer;// 新增层级属性

    /// <summary> 字块显示的字母/文字 </summary>
    public char Letter { get; set; }
    
    /// <summary> 字块是否为空(可放置状态) </summary>
    public bool IsEmpty { get;  set; }
    
    /// <summary> 关联的字块视图组件 </summary>
    public TileView TileView { get; set; }

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建字块实例
    /// </summary>
    /// <param name="row">行索引</param>
    /// <param name="column">列索引</param>
    /// <param name="letter">显示字母</param>
    public PuzzleTile(int row, int column,int layer, char letter)
    {
        this.Row = row;
        this.Column = column;
        this.Layer = layer;
        this.Letter = letter;
        this.IsEmpty = false;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 将字块设为空状态
    /// </summary>
    public void SetAsEmpty()
    {
        this.Letter = '\0';
        this.TileView = null;
        this.IsEmpty = true;
    }

    #endregion
}
