using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dorisoy.Meeting.Client.Models;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// 投票窗口
/// </summary>
public partial class VoteWindow : FluentWindow, INotifyPropertyChanged
{
    private readonly string _peerId;
    private readonly string _peerName;
    private readonly bool _isHost;
    private Vote? _currentVote;
    private string _questionText = string.Empty;
    private bool _showEditPanel;
    private bool _showVoters;
    private bool _showResults;
    private bool _hasVoted;
    private int? _selectedOptionIndex;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 投票创建事件
    /// </summary>
    public event Action<Vote>? VoteCreated;

    /// <summary>
    /// 投票提交事件
    /// </summary>
    public event Action<string, int>? VoteSubmitted;

    /// <summary>
    /// 投票删除事件
    /// </summary>
    public event Action<string>? VoteDeleted;

    /// <summary>
    /// 投票更新事件
    /// </summary>
    public event Action<Vote>? VoteUpdated;

    #region Properties

    /// <summary>
    /// 是否为主持人
    /// </summary>
    public bool IsHost => _isHost;

    /// <summary>
    /// 是否为参与者（非主持人）
    /// </summary>
    public bool IsParticipant => !_isHost;

    /// <summary>
    /// 问题文本
    /// </summary>
    public string QuestionText
    {
        get => _questionText;
        set { _questionText = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 选项输入列表
    /// </summary>
    public ObservableCollection<VoteOptionInput> OptionInputs { get; } = new();

    /// <summary>
    /// 是否可以删除选项
    /// </summary>
    public bool CanRemoveOption => OptionInputs.Count > 2;

    /// <summary>
    /// 当前投票
    /// </summary>
    public Vote? CurrentVote
    {
        get => _currentVote;
        set 
        { 
            _currentVote = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(HasVote));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(CanSubmitVote));
        }
    }

    /// <summary>
    /// 是否有投票
    /// </summary>
    public bool HasVote => _currentVote != null;

    /// <summary>
    /// 是否显示编辑面板
    /// </summary>
    public bool ShowEditPanel
    {
        get => _showEditPanel;
        set { _showEditPanel = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 是否显示投票人
    /// </summary>
    public bool ShowVoters
    {
        get => _showVoters;
        set { _showVoters = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 是否显示结果
    /// </summary>
    public bool ShowResults
    {
        get => _showResults;
        set { _showResults = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 是否已投票
    /// </summary>
    public bool HasVoted
    {
        get => _hasVoted;
        set 
        { 
            _hasVoted = value; 
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSubmitVote));
        }
    }

    /// <summary>
    /// 是否可以提交投票
    /// </summary>
    public bool CanSubmitVote => !_isHost && HasVote && !HasVoted;

    /// <summary>
    /// 是否显示空状态
    /// </summary>
    public bool ShowEmptyState => !_isHost && !HasVote;

    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="peerId">当前用户ID</param>
    /// <param name="peerName">当前用户名</param>
    /// <param name="isHost">是否为主持人</param>
    /// <param name="existingVote">已存在的投票（参与者打开时）</param>
    public VoteWindow(string peerId, string peerName, bool isHost, Vote? existingVote = null)
    {
        _peerId = peerId;
        _peerName = peerName;
        _isHost = isHost;

        InitializeComponent();
        DataContext = this;

        // 初始化选项输入
        InitializeOptionInputs();

        // 设置视图状态
        if (_isHost)
        {
            ShowEditPanel = true;
            ShowResults = true;
        }
        else
        {
            ShowEditPanel = false;
            ShowResults = false;
        }

        // 如果有已存在的投票
        if (existingVote != null)
        {
            SetVote(existingVote);
        }
    }

    /// <summary>
    /// 初始化选项输入
    /// </summary>
    private void InitializeOptionInputs()
    {
        OptionInputs.Clear();
        OptionInputs.Add(new VoteOptionInput { Index = 0, Text = string.Empty });
        OptionInputs.Add(new VoteOptionInput { Index = 1, Text = string.Empty });
        OnPropertyChanged(nameof(CanRemoveOption));
    }

    /// <summary>
    /// 设置投票（接收广播的投票）
    /// </summary>
    public void SetVote(Vote vote)
    {
        CurrentVote = vote;
        
        // 检查当前用户是否已投票
        foreach (var option in vote.Options)
        {
            var voter = option.Voters.FirstOrDefault(v => v.PeerId == _peerId);
            if (voter != null)
            {
                HasVoted = true;
                option.IsSelected = true;
                _selectedOptionIndex = option.Index;
                break;
            }
        }

        // 主持人显示结果
        if (_isHost)
        {
            ShowResults = true;
            ShowEditPanel = false;
        }
    }

    /// <summary>
    /// 更新投票结果
    /// </summary>
    public void UpdateVoteResults(Vote updatedVote)
    {
        if (CurrentVote == null || CurrentVote.Id != updatedVote.Id) return;

        // 更新选项
        foreach (var updatedOption in updatedVote.Options)
        {
            var option = CurrentVote.Options.FirstOrDefault(o => o.Index == updatedOption.Index);
            if (option != null)
            {
                option.VoteCount = updatedOption.VoteCount;
                option.Percentage = updatedOption.Percentage;
                option.Voters.Clear();
                foreach (var voter in updatedOption.Voters)
                {
                    option.Voters.Add(voter);
                }
            }
        }
    }

    #region Event Handlers

    /// <summary>
    /// 添加选项
    /// </summary>
    private void AddOption_Click(object sender, RoutedEventArgs e)
    {
        if (OptionInputs.Count >= 10)
        {
            MessageBox.Show("最多只能添加10个选项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OptionInputs.Add(new VoteOptionInput { Index = OptionInputs.Count, Text = string.Empty });
        OnPropertyChanged(nameof(CanRemoveOption));
    }

    /// <summary>
    /// 删除选项
    /// </summary>
    private void RemoveOption_Click(object sender, RoutedEventArgs e)
    {
        if (OptionInputs.Count <= 2)
        {
            MessageBox.Show("至少需要2个选项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OptionInputs.RemoveAt(OptionInputs.Count - 1);
        
        // 重新编号
        for (int i = 0; i < OptionInputs.Count; i++)
        {
            OptionInputs[i].Index = i;
        }
        
        OnPropertyChanged(nameof(CanRemoveOption));
    }

    /// <summary>
    /// 创建投票
    /// </summary>
    private void CreateVote_Click(object sender, RoutedEventArgs e)
    {
        // 验证
        if (string.IsNullOrWhiteSpace(QuestionText))
        {
            MessageBox.Show("请输入投票问题", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var validOptions = OptionInputs.Where(o => !string.IsNullOrWhiteSpace(o.Text)).ToList();
        if (validOptions.Count < 2)
        {
            MessageBox.Show("请至少输入2个有效选项", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 创建投票对象
        var vote = new Vote
        {
            Id = Guid.NewGuid().ToString(),
            Question = QuestionText.Trim(),
            CreatorId = _peerId,
            CreatorName = _peerName,
            CreatedTime = DateTime.Now,
            IsClosed = false
        };

        for (int i = 0; i < validOptions.Count; i++)
        {
            vote.Options.Add(new VoteOption
            {
                Index = i,
                Text = validOptions[i].Text.Trim(),
                VoteCount = 0,
                Percentage = 0
            });
        }

        // 设置当前投票
        CurrentVote = vote;
        ShowEditPanel = false;

        // 触发事件
        VoteCreated?.Invoke(vote);
    }

    /// <summary>
    /// 保存投票
    /// </summary>
    private void SaveVote_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentVote == null) return;
        VoteUpdated?.Invoke(CurrentVote);
        MessageBox.Show("投票已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// 关闭窗口
    /// </summary>
    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 显示/隐藏投票人
    /// </summary>
    private void ShowVoters_Click(object sender, RoutedEventArgs e)
    {
        ShowVoters = !ShowVoters;
    }

    /// <summary>
    /// 编辑投票
    /// </summary>
    private void EditVote_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentVote == null) return;

        // 将当前投票内容填充到编辑区域
        QuestionText = CurrentVote.Question;
        OptionInputs.Clear();
        for (int i = 0; i < CurrentVote.Options.Count; i++)
        {
            OptionInputs.Add(new VoteOptionInput 
            { 
                Index = i, 
                Text = CurrentVote.Options[i].Text 
            });
        }

        ShowEditPanel = true;
        OnPropertyChanged(nameof(CanRemoveOption));
    }

    /// <summary>
    /// 删除投票
    /// </summary>
    private void DeleteVote_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentVote == null) return;

        var result = MessageBox.Show("确定要删除这个投票吗？", "确认删除", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            var voteId = CurrentVote.Id;
            
            // 重置状态
            CurrentVote = null;
            QuestionText = string.Empty;
            InitializeOptionInputs();
            ShowEditPanel = true;
            ShowVoters = false;

            // 触发删除事件
            VoteDeleted?.Invoke(voteId);
        }
    }

    /// <summary>
    /// 选项点击
    /// </summary>
    private void VoteOption_Click(object sender, MouseButtonEventArgs e)
    {
        if (HasVoted || _isHost) return;

        if (sender is Border border && border.DataContext is VoteOption option)
        {
            SelectOption(option.Index);
        }
    }

    /// <summary>
    /// 单选框点击
    /// </summary>
    private void RadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (HasVoted || _isHost) return;

        if (sender is RadioButton radio && radio.DataContext is VoteOption option)
        {
            SelectOption(option.Index);
        }
    }

    /// <summary>
    /// 选择选项
    /// </summary>
    private void SelectOption(int optionIndex)
    {
        if (CurrentVote == null) return;

        // 清除其他选项的选中状态
        foreach (var opt in CurrentVote.Options)
        {
            opt.IsSelected = opt.Index == optionIndex;
        }
        
        _selectedOptionIndex = optionIndex;
    }

    /// <summary>
    /// 提交投票
    /// </summary>
    private void SubmitVote_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentVote == null || !_selectedOptionIndex.HasValue)
        {
            MessageBox.Show("请选择一个选项", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 标记已投票
        HasVoted = true;
        ShowResults = true;

        // 添加到投票人列表（本地）
        var option = CurrentVote.Options.FirstOrDefault(o => o.Index == _selectedOptionIndex.Value);
        if (option != null)
        {
            option.VoteCount++;
            option.Voters.Add(new VoteVoter
            {
                PeerId = _peerId,
                DisplayName = _peerName,
                VoteTime = DateTime.Now
            });

            // 重新计算百分比
            var totalVotes = CurrentVote.Options.Sum(o => o.VoteCount);
            foreach (var opt in CurrentVote.Options)
            {
                opt.Percentage = totalVotes > 0 ? (double)opt.VoteCount / totalVotes * 100 : 0;
            }
        }

        // 触发提交事件
        VoteSubmitted?.Invoke(CurrentVote.Id, _selectedOptionIndex.Value);
    }

    #endregion

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
