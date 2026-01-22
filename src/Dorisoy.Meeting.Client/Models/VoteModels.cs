using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 投票信息
/// </summary>
public class Vote : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _question = string.Empty;
    private string _creatorId = string.Empty;
    private string _creatorName = string.Empty;
    private DateTime _createdTime;
    private bool _isClosed;
    private int? _selectedOptionIndex;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 投票ID
    /// </summary>
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 投票问题
    /// </summary>
    public string Question
    {
        get => _question;
        set { _question = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 创建者ID
    /// </summary>
    public string CreatorId
    {
        get => _creatorId;
        set { _creatorId = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 创建者名称
    /// </summary>
    public string CreatorName
    {
        get => _creatorName;
        set { _creatorName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime
    {
        get => _createdTime;
        set { _createdTime = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 是否已关闭
    /// </summary>
    public bool IsClosed
    {
        get => _isClosed;
        set { _isClosed = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 当前用户选中的选项索引
    /// </summary>
    public int? SelectedOptionIndex
    {
        get => _selectedOptionIndex;
        set { _selectedOptionIndex = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 投票选项列表
    /// </summary>
    public ObservableCollection<VoteOption> Options { get; set; } = new();

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 投票选项
/// </summary>
public class VoteOption : INotifyPropertyChanged
{
    private int _index;
    private string _text = string.Empty;
    private int _voteCount;
    private bool _isSelected;
    private double _percentage;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 选项索引
    /// </summary>
    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 选项文本
    /// </summary>
    public string Text
    {
        get => _text;
        set { _text = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 投票数
    /// </summary>
    public int VoteCount
    {
        get => _voteCount;
        set 
        { 
            _voteCount = value; 
            OnPropertyChanged(); 
        }
    }

    /// <summary>
    /// 是否被当前用户选中
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 投票百分比
    /// </summary>
    public double Percentage
    {
        get => _percentage;
        set { _percentage = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 投票人列表
    /// </summary>
    public ObservableCollection<VoteVoter> Voters { get; set; } = new();

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 投票人信息
/// </summary>
public class VoteVoter : INotifyPropertyChanged
{
    private string _peerId = string.Empty;
    private string _displayName = string.Empty;
    private DateTime _voteTime;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 用户ID
    /// </summary>
    public string PeerId
    {
        get => _peerId;
        set { _peerId = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 投票时间
    /// </summary>
    public DateTime VoteTime
    {
        get => _voteTime;
        set { _voteTime = value; OnPropertyChanged(); }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 投票输入项（用于编辑界面）
/// </summary>
public class VoteOptionInput : INotifyPropertyChanged
{
    private int _index;
    private string _text = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 选项索引
    /// </summary>
    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); OnPropertyChanged(nameof(Label)); }
    }

    /// <summary>
    /// 选项标签
    /// </summary>
    public string Label => $"选项 {Index + 1}";

    /// <summary>
    /// 选项文本
    /// </summary>
    public string Text
    {
        get => _text;
        set { _text = value; OnPropertyChanged(); }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 投票提交请求
/// </summary>
public class VoteSubmitRequest
{
    /// <summary>
    /// 投票ID
    /// </summary>
    public string VoteId { get; set; } = string.Empty;

    /// <summary>
    /// 选中的选项索引
    /// </summary>
    public int OptionIndex { get; set; }

    /// <summary>
    /// 投票人ID
    /// </summary>
    public string VoterId { get; set; } = string.Empty;

    /// <summary>
    /// 投票人名称
    /// </summary>
    public string VoterName { get; set; } = string.Empty;
}

/// <summary>
/// 投票创建请求
/// </summary>
public class VoteCreateRequest
{
    /// <summary>
    /// 投票问题
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// 选项列表
    /// </summary>
    public List<string> Options { get; set; } = new();

    /// <summary>
    /// 创建者ID
    /// </summary>
    public string CreatorId { get; set; } = string.Empty;

    /// <summary>
    /// 创建者名称
    /// </summary>
    public string CreatorName { get; set; } = string.Empty;
}

/// <summary>
/// 投票通知
/// </summary>
public class VoteNotification
{
    /// <summary>
    /// 通知类型：Created, Updated, Deleted, VoteSubmitted
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 投票信息
    /// </summary>
    public Vote? Vote { get; set; }

    /// <summary>
    /// 投票ID（删除时使用）
    /// </summary>
    public string? VoteId { get; set; }
}
