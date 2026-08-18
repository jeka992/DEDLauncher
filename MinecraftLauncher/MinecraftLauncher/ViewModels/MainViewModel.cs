using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.VersionMetadata;
using DedLauncher.Helpers;
using DedLauncher.Models;
using DedLauncher.Services;

namespace DedLauncher.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ModService _mods;
    private readonly JavaService _java;
    private readonly SkinService _skins;
    private readonly HttpClient _http;
    private readonly MinecraftPath _minecraftPath;

    private MinecraftLauncher? _launcher;
    private JELoginHandler? _loginHandler;
    private MSession? _session;
    private VersionMetadataCollection? _allVersions;
    private Process? _gameProcess;

    public MainViewModel()
    {
        _mods = new();
        _java = new();
        _skins = new();
        _http = new();
        _minecraftPath = new MinecraftPath(MinecraftPathHelper.GameDir);
        MinecraftPathHelper.Initialize();
        InitCommands();
        InitServerData();
    }

    
    private AccountInfo _account = new();
    public AccountInfo Account { get => _account; set { SetProperty(ref _account, value); OnPropertyChanged(nameof(AccountTypeLabel)); OnPropertyChanged(nameof(ChipName)); OnPropertyChanged(nameof(ChipTypeLabel)); OnPropertyChanged(nameof(CanUseFriendsChat)); OnPropertyChanged(nameof(ChatLockReason)); } }
    private string _accountStatus = "cyr1";
    public string AccountStatus { get => _accountStatus; set => SetProperty(ref _accountStatus, value); }
    public string AccountTypeLabel => Account.AccountType switch
    {
        "msa" => "cyr2",
        _ => "cyr3"
    };
    private string _offlineUsername = "Player";
    public string OfflineUsername { get => _offlineUsername; set => SetProperty(ref _offlineUsername, value); }

    
    private bool _multipleInstances;
    public bool MultipleInstances { get => _multipleInstances; set => SetProperty(ref _multipleInstances, value); }

    private string _postLaunchAction = "keep";
    public string PostLaunchAction { get => _postLaunchAction; set => SetProperty(ref _postLaunchAction, value); }

    private bool _ipv4Only = true;
    public bool Ipv4Only { get => _ipv4Only; set => SetProperty(ref _ipv4Only, value); }

    
    private bool _lowEndMode;
    public bool LowEndMode
    {
        get => _lowEndMode;
        set
        {
            if (SetProperty(ref _lowEndMode, value))
            {
                PerfSettings.LowEndMode = value;
                SaveSettings();
            }
        }
    }

    private bool _softwareRendering;
    public bool SoftwareRendering
    {
        get => _softwareRendering;
        set
        {
            if (SetProperty(ref _softwareRendering, value))
            {
                PerfSettings.SoftwareRendering = value;
                SaveSettings();
            }
        }
    }

    
    
    private const string UpdateChannel = "NeiroDEDmod";

    private string _updateStatus = "cyr4";
    public string UpdateStatus { get => _updateStatus; set => SetProperty(ref _updateStatus, value); }

    public string VersionLabel => App.VersionLabel;

    public string UpdateChannelLink => $"https://t.me/{UpdateChannel}";

    
    
    
    
    
    public async Task CheckForUpdatesAsync(bool manual)
    {
        UpdateStatus = "cyr5";
        try
        {
            var info = await UpdateService.CheckTgAsync(UpdateChannel);
            if (info == null)
            {
                UpdateStatus = "cyr6";
                if (manual) ShowToast("cyr7");
                return;
            }

            if (UpdateService.CompareVersions(info.Version, App.VersionLabel) <= 0)
            {
                UpdateStatus = $"cyr8";
                if (manual) ShowToast($"cyr9");
                return;
            }

            UpdateStatus = $"cyr10";

            if (manual)
            {
                var answer = MessageBox.Show(
                    $"cyr11" +
                    "cyr12" +
                    "cyr13",
                    "cyr14", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Yes && !string.IsNullOrEmpty(info.Url))
                {
                    try { Process.Start(new ProcessStartInfo(info.Url) { UseShellExecute = true }); }
                    catch { }
                }
            }
            else
            {
                ShowToast($"cyr15");
                ConsoleLines.Add($"cyr16");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = "cyr17";
            if (manual)
            {
                Status = $"cyr18";
                ShowToast("cyr19");
            }
        }
    }

    
    private string _discordClientId = "";
    public string DiscordClientId { get => _discordClientId; set => SetProperty(ref _discordClientId, value); }

    private string _discordClientSecret = "";
    public string DiscordClientSecret { get => _discordClientSecret; set => SetProperty(ref _discordClientSecret, value); }

    
    public bool GameDetached { get; set; }
    private bool _autoLogin = true;
    public bool AutoLogin { get => _autoLogin; set => SetProperty(ref _autoLogin, value); }

    private int _fontSize = 13;
    public IReadOnlyList<int> FontSizes { get; } = new[] { 11, 12, 13, 14, 15 };
    public int FontSize
    {
        get => _fontSize;
        set
        {
            if (SetProperty(ref _fontSize, value))
            {
                MainWindow.UpdateUiScale(value);
                SaveSettings();
            }
        }
    }

    
    public List<ThemePalette> ThemesList => ThemeManager.Themes;
    private string _selectedTheme = "Red";
    public string SelectedTheme
    {
        get => _selectedTheme;
        set { if (SetProperty(ref _selectedTheme, value)) ThemeManager.ApplyTheme(value); }
    }

    
    private ImageSource? _skinHeadPreview;
    public ImageSource? SkinHeadPreview
    {
        get => _skinHeadPreview;
        set { SetProperty(ref _skinHeadPreview, value); OnPropertyChanged(nameof(ChipAvatar)); }
    }

    private ImageSource? _accountAvatar;
    public ImageSource? AccountAvatar
    {
        get => _accountAvatar;
        set { SetProperty(ref _accountAvatar, value); OnPropertyChanged(nameof(ChipAvatar)); }
    }

    
    public ImageSource? ChipAvatar => _accountAvatar ?? _skinHeadPreview;

    
    private DiscordAuthService.DiscordUser? _discordProfile;
    public DiscordAuthService.DiscordUser? DiscordProfile
    {
        get => _discordProfile;
        set
        {
            if (SetProperty(ref _discordProfile, value))
            {
                OnPropertyChanged(nameof(ChipName));
                OnPropertyChanged(nameof(ChipTypeLabel));
                OnPropertyChanged(nameof(HasDiscordProfile));
                OnPropertyChanged(nameof(CanUseFriendsChat));
                OnPropertyChanged(nameof(ChatLockReason));
                if (value != null) LoadAccountAvatar(value.AvatarUrl); else AccountAvatar = null;
            }
        }
    }
    public bool HasDiscordProfile => _discordProfile != null;

    
    
    public bool CanUseFriendsChat => _discordProfile != null || Account.AccountType == "msa";

    public string ChatLockReason => _discordProfile != null ? ""
        : (Account.AccountType == "msa" ? ""
            : "cyr20");

    
    public string ChipName => _discordProfile?.Username ?? Account.Username;

    
    public string ChipTypeLabel => _discordProfile != null ? "Discord" : AccountTypeLabel;

    private static string DiscordProfilePath => Path.Combine(MinecraftPathHelper.BaseDir, "discord.json");

    private void LoadDiscordProfile()
    {
        try
        {
            if (File.Exists(DiscordProfilePath))
            {
                var p = JsonSerializer.Deserialize<DiscordAuthService.DiscordUser>(File.ReadAllText(DiscordProfilePath));
                if (p != null) DiscordProfile = p;
            }
        }
        catch { }
    }

    private void SaveDiscordProfile()
    {
        try
        {
            if (_discordProfile == null) { if (File.Exists(DiscordProfilePath)) File.Delete(DiscordProfilePath); return; }
            File.WriteAllText(DiscordProfilePath, JsonSerializer.Serialize(_discordProfile, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public void LogoutDiscord()
    {
        DiscordProfile = null;
        SaveDiscordProfile();
        ShowToast("cyr21");
    }

    

    public ObservableCollection<FriendEntry> Friends { get; } = new();
    public ObservableCollection<FriendEntry> FriendsView { get; } = new();
    public ObservableCollection<FriendEntry> FriendRequests { get; } = new();
    public ObservableCollection<GroupChat> Groups { get; } = new();
    public ObservableCollection<GroupMember> GroupMembers { get; } = new();
    public ObservableCollection<ChatLine> ChatLines { get; } = new();
    public ObservableCollection<ChatLine> GroupChatLines { get; } = new();

    private readonly Dictionary<string, Dictionary<string, GroupMember>> _groupMembersRaw = new();

    private FriendsService? _friends;
    private AdminClientService? _admin;
    private string _myFriendCode = "";
    private string _friendCodeInput = "";
    private string _chatInput = "";
    private string _groupCodeInput = "";
    private string _groupChatInput = "";
    private string _myStatus = "";
    private FriendEntry? _selectedFriend;
    private GroupChat? _selectedGroup;
    private System.Windows.Threading.DispatcherTimer? _presenceTimer;
    private System.Windows.Threading.DispatcherTimer? _friendsPulseTimer;
    private string? _lastPresenceServer;

    public string MyFriendCode { get => _myFriendCode; set => SetProperty(ref _myFriendCode, value); }
    public string FriendCodeInput { get => _friendCodeInput; set => SetProperty(ref _friendCodeInput, value); }
    public string ChatInput
    {
        get => _chatInput;
        set
        {
            if (SetProperty(ref _chatInput, value))
            {
                
                if (!string.IsNullOrEmpty(value) && SelectedFriend != null && _friends != null &&
                    DateTime.UtcNow - _lastTypingSent > TimeSpan.FromSeconds(2))
                {
                    _lastTypingSent = DateTime.UtcNow;
                    _ = _friends.SendTypingAsync(SelectedFriend.Code);
                }
            }
        }
    }
    public string GroupCodeInput { get => _groupCodeInput; set => SetProperty(ref _groupCodeInput, value); }
    public string GroupChatInput { get => _groupChatInput; set => SetProperty(ref _groupChatInput, value); }
    public string MyStatus { get => _myStatus; set { SetProperty(ref _myStatus, value); _ = PublishMyPresenceAsync(); } }

    private bool _soundEnabled = true;
    public bool SoundEnabled { get => _soundEnabled; set => SetProperty(ref _soundEnabled, value); }

    private string _friendSearch = "";
    public string FriendSearch { get => _friendSearch; set { if (SetProperty(ref _friendSearch, value)) RebuildFriendsView(); } }

    private DateTime _lastTypingSent = DateTime.MinValue;

    public FriendEntry? SelectedFriend
    {
        get => _selectedFriend;
        set
        {
            if (SetProperty(ref _selectedFriend, value))
            {
                if (value != null) _selectedGroup = null;
                OnPropertyChanged(nameof(SelectedGroup));
                OnPropertyChanged(nameof(IsFriendChat));
                OnPropertyChanged(nameof(IsGroupChat));
                OnPropertyChanged(nameof(IsNoChat));
                if (value != null) { value.Unread = 0; LoadFriendChat(value.Code); }
                else ChatLines.Clear();
                UpdateUnread();
            }
        }
    }

    public bool IsNoChat => SelectedFriend == null && SelectedGroup == null;

    public GroupChat? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                if (value != null) _selectedFriend = null;
                OnPropertyChanged(nameof(SelectedFriend));
                OnPropertyChanged(nameof(IsFriendChat));
                OnPropertyChanged(nameof(IsGroupChat));
                OnPropertyChanged(nameof(IsNoChat));
                if (value != null) { value.Unread = 0; LoadGroupChat(value.Code); LoadGroupMembers(value.Code); }
                else { GroupChatLines.Clear(); GroupMembers.Clear(); }
                UpdateUnread();
            }
        }
    }

    private void LoadGroupMembers(string groupCode)
    {
        GroupMembers.Clear();
        if (_groupMembersRaw.TryGetValue(groupCode, out var map))
            foreach (var m in map.Values) GroupMembers.Add(m);
    }

    public bool IsFriendChat => SelectedFriend != null && SelectedGroup == null;
    public bool IsGroupChat => SelectedGroup != null;

    private int _totalUnread;
    public int TotalUnread { get => _totalUnread; set { if (SetProperty(ref _totalUnread, value)) OnPropertyChanged(nameof(HasUnread)); } }
    public bool HasUnread => _totalUnread > 0;

    private void UpdateUnread()
    {
        TotalUnread = Friends.Sum(f => f.Unread) + Groups.Sum(g => g.Unread);
    }

    public bool HasFriends => Friends.Count > 0;
    public bool HasFriendRequests => FriendRequests.Count > 0;

    public ICommand AddFriendCmd { get; private set; } = null!;
    public ICommand SendChatCmd { get; private set; } = null!;
    public ICommand NavFriendsCmd { get; private set; } = null!;
    public ICommand CopyCodeCmd { get; private set; } = null!;
    public ICommand CreateGroupCmd { get; private set; } = null!;
    public ICommand JoinGroupCmd { get; private set; } = null!;
    public ICommand SendGroupChatCmd { get; private set; } = null!;
    public ICommand ClearChatCmd { get; private set; } = null!;

    private static string FriendsFilePath => Path.Combine(MinecraftPathHelper.BaseDir, "friends.json");
    private static string GroupsFilePath => Path.Combine(MinecraftPathHelper.BaseDir, "groups.json");
    private static string FriendCodePath => Path.Combine(MinecraftPathHelper.BaseDir, "friendcode.txt");
    private static string ChatsDir => Path.Combine(MinecraftPathHelper.BaseDir, "chats");

    private static string FriendChatFile(string code) => Path.Combine(ChatsDir, $"f_{code}.json");
    private static string GroupChatFile(string code) => Path.Combine(ChatsDir, $"g_{code}.json");

    

    private void LoadFriendChat(string code)
    {
        ChatLines.Clear();
        try
        {
            var f = FriendChatFile(code);
            if (!File.Exists(f)) return;
            var list = JsonSerializer.Deserialize<List<ChatLine>>(File.ReadAllText(f));
            if (list != null) foreach (var l in list.TakeLast(300)) ChatLines.Add(l);
        }
        catch { }
    }

    private void LoadGroupChat(string code)
    {
        GroupChatLines.Clear();
        try
        {
            var f = GroupChatFile(code);
            if (!File.Exists(f)) return;
            var list = JsonSerializer.Deserialize<List<ChatLine>>(File.ReadAllText(f));
            if (list != null) foreach (var l in list.TakeLast(300)) GroupChatLines.Add(l);
        }
        catch { }
    }

    private void SaveFriendChat(string code)
    {
        try
        {
            Directory.CreateDirectory(ChatsDir);
            File.WriteAllText(FriendChatFile(code), JsonSerializer.Serialize(ChatLines.ToList()));
        }
        catch { }
    }

    private void SaveGroupChat(string code)
    {
        try
        {
            Directory.CreateDirectory(ChatsDir);
            File.WriteAllText(GroupChatFile(code), JsonSerializer.Serialize(GroupChatLines.ToList()));
        }
        catch { }
    }

    
    public void ClearChat()
    {
        if (SelectedFriend != null)
        {
            ChatLines.Clear();
            try { File.Delete(FriendChatFile(SelectedFriend.Code)); } catch { }
            ShowToast("cyr22");
        }
        else if (SelectedGroup != null)
        {
            GroupChatLines.Clear();
            try { File.Delete(GroupChatFile(SelectedGroup.Code)); } catch { }
            ShowToast("cyr23");
        }
    }

    private string EnsureFriendCode()
    {
        try
        {
            if (File.Exists(FriendCodePath))
            {
                var c = File.ReadAllText(FriendCodePath).Trim();
                if (c.Length >= 6) return c;
            }
        }
        catch { }

        var code = GenerateFriendCode();
        try { File.WriteAllText(FriendCodePath, code); } catch { }
        return code;
    }

    private static string GenerateFriendCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 6; i++)
            sb.Append(chars[Random.Shared.Next(chars.Length)]);
        return sb.ToString();
    }

    private void LoadFriends()
    {
        Friends.Clear();
        try
        {
            if (File.Exists(FriendsFilePath))
            {
                var list = JsonSerializer.Deserialize<List<FriendEntry>>(File.ReadAllText(FriendsFilePath));
                if (list != null)
                    foreach (var f in list)
                        Friends.Add(f);
            }
        }
        catch { }
        OnPropertyChanged(nameof(HasFriends));
        RebuildFriendsView();
    }

    private void SaveFriends()
    {
        try
        {
            File.WriteAllText(FriendsFilePath, JsonSerializer.Serialize(Friends.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
        OnPropertyChanged(nameof(HasFriends));
        RebuildFriendsView();
    }

    
    private void RebuildFriendsView()
    {
        var q = (_friendSearch ?? "").Trim().ToLower();
        var list = Friends
            .Where(f => string.IsNullOrEmpty(q) || f.DisplayName.ToLower().Contains(q) || f.Code.ToLower().Contains(q))
            .OrderByDescending(f => f.IsOnline)
            .ThenBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FriendsView.Clear();
        foreach (var f in list) FriendsView.Add(f);
    }

    private void LoadGroups()
    {
        Groups.Clear();
        try
        {
            if (File.Exists(GroupsFilePath))
            {
                var list = JsonSerializer.Deserialize<List<GroupChat>>(File.ReadAllText(GroupsFilePath));
                if (list != null)
                    foreach (var g in list)
                        Groups.Add(g);
            }
        }
        catch { }
    }

    private void SaveGroups()
    {
        try
        {
            File.WriteAllText(GroupsFilePath, JsonSerializer.Serialize(Groups.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void StartFriends()
    {
        if (_friends != null) return;

        _myFriendCode = EnsureFriendCode();
        OnPropertyChanged(nameof(MyFriendCode));

        _friends = new FriendsService(_myFriendCode)
        {
            DisplayName = _discordProfile?.Username ?? Account.Username
        };
        _friends.PresenceReceived += OnFriendPresence;
        _friends.MessageReceived += OnFriendMessage;
        _friends.TypingReceived += OnTypingReceived;
        _friends.RequestReceived += OnRequestReceived;
        _friends.RequestAccepted += OnRequestAccepted;
        _friends.InviteReceived += OnInviteReceived;
        _friends.GroupPresence += OnGroupPresence;
        _friends.GroupMessage += OnGroupMessage;

        _ = _friends.StartAsync();

        foreach (var f in Friends.ToList())
            _ = _friends.AddFriendAsync(f.Code);
        foreach (var g in Groups.ToList())
            _ = _friends.JoinGroupAsync(g.Code);

        _presenceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _presenceTimer.Tick += async (s, e) =>
        {
            await PublishMyPresenceAsync();
        };
        _presenceTimer.Start();

        _friendsPulseTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _friendsPulseTimer.Tick += (s, e) =>
        {
            foreach (var f in Friends) { f.Touch(); }
            foreach (var m in GroupMembers) m.Touch();
            RebuildFriendsView();
        };
        _friendsPulseTimer.Start();
    }

    
    
    
    
    private void StartAdmin()
    {
        if (_admin != null) return;
        _admin = new AdminClientService
        {
            Nick = Account.Username,
            AccountType = Account.AccountType == "microsoft" ? "ms" : Account.AccountType,
            McVersion = CurrentProfile?.VersionId ?? "",
            Loader = CurrentProfile?.ModLoader ?? "Vanilla"
        };
        _admin.AnnouncementReceived += (id, text) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowToast($"cyr24");
                ConsoleLines.Add($"cyr25");
            });
        };
        _ = _admin.StartAsync();
    }

    private void SyncAdminProfile()
    {
        if (_admin == null) return;
        _admin.Nick = Account.Username;
        _admin.AccountType = Account.AccountType == "microsoft" ? "ms" : Account.AccountType;
        _admin.McVersion = CurrentProfile?.VersionId ?? "";
        _admin.Loader = CurrentProfile?.ModLoader ?? "Vanilla";
        _ = _admin.PublishStatusAsync();
    }

    private async Task PublishMyPresenceAsync()
    {
        if (_friends == null) return;
        string? server = ReadGamePresence();
        if (server != _lastPresenceServer)
        {
            _lastPresenceServer = server;
            Status = string.IsNullOrEmpty(server) ? "cyr26" : $"cyr27";
        }
        await _friends.PublishPresenceAsync(server, string.IsNullOrWhiteSpace(_myStatus) ? null : _myStatus.Trim());
    }

    private string? ReadGamePresence()
    {
        try
        {
            var path = Path.Combine(GetProfileGameDir(CurrentProfile!), "config", "dedmod-presence.json");
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var server = doc.RootElement.GetProperty("server").GetString();
            return string.IsNullOrEmpty(server) ? null : server;
        }
        catch { return null; }
    }

    private void OnFriendPresence(string code, string name, string? server, string? status, bool online)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var friend = Friends.FirstOrDefault(f => f.Code == code);
            if (friend == null) return;
            if (!string.IsNullOrEmpty(name)) friend.Name = name;
            if (!string.IsNullOrEmpty(status)) friend.Status = status; else friend.Status = null;
            friend.Server = server;
            friend.LastSeen = DateTime.UtcNow;
            friend.LastOnline = DateTime.UtcNow;
            friend.Touch();
            SaveFriends();
            RebuildFriendsView();
        });
    }

    private void OnFriendMessage(string code, string name, string text)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var friend = Friends.FirstOrDefault(f => f.Code == code);
            var sender = string.IsNullOrEmpty(name) ? code : name;
            if (friend != null && SelectedFriend?.Code == code)
            {
                ChatLines.Add(new ChatLine { Sender = sender, Text = text, IsMine = false });
                while (ChatLines.Count > 300) ChatLines.RemoveAt(0);
                SaveFriendChat(code);
            }
            else
            {
                if (friend != null) friend.Unread++;
                UpdateUnread();
                PlayMessageSound();
                ShowToast($"{sender}: {text}");
            }
        });
    }

    private void OnTypingReceived(string code)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var friend = Friends.FirstOrDefault(f => f.Code == code);
            if (friend == null) return;
            friend.IsTyping = true;
            _ = Task.Run(async () =>
            {
                await Task.Delay(3500);
                Application.Current.Dispatcher.Invoke(() => friend.IsTyping = false);
            });
        });
    }

    private void PlayMessageSound()
    {
        if (!SoundEnabled) return;
        _ = Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream(MessageSound.BuildWav());
                using var player = new System.Media.SoundPlayer(ms);
                player.PlaySync();
            }
            catch { }
        });
    }

    private void OnRequestReceived(string code, string name)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Friends.Any(f => f.Code == code)) return;
            if (FriendRequests.Any(r => r.Code == code)) return;
            FriendRequests.Add(new FriendEntry { Code = code, Name = name });
            OnPropertyChanged(nameof(HasFriendRequests));
            ShowToast($"cyr28");
        });
    }

    private void OnRequestAccepted(string code, string name)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Friends.Any(f => f.Code == code)) return;
            var friend = new FriendEntry { Code = code, Name = name };
            Friends.Add(friend);
            SaveFriends();
            _ = _friends!.AddFriendAsync(code);
            ShowToast($"cyr29");
        });
    }

    private void OnInviteReceived(string code, string server)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var friend = Friends.FirstOrDefault(f => f.Code == code);
            var sender = friend?.DisplayName ?? code;

            if (friend != null && SelectedFriend?.Code == code)
            {
                ChatLines.Add(new ChatLine { Sender = sender, IsMine = false, InviteServer = server });
                while (ChatLines.Count > 300) ChatLines.RemoveAt(0);
                SaveFriendChat(code);
            }
            else
            {
                if (friend != null) friend.Unread++;
                UpdateUnread();
                PlayMessageSound();
            }
            ShowToast($"cyr30");
        });
    }

    private void OnGroupPresence(string groupCode, string name)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var group = Groups.FirstOrDefault(g => g.Code == groupCode);
            if (group == null) return;
            TouchGroupMember(groupCode, "", name);
            group.OnlineCount = _groupMembersRaw.TryGetValue(groupCode, out var m)
                ? m.Values.Count(x => x.IsOnline)
                : 1;
        });
    }

    private void OnGroupMessage(string groupCode, string name, string text)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var group = Groups.FirstOrDefault(g => g.Code == groupCode);
            var sender = string.IsNullOrEmpty(name) ? groupCode : name;
            TouchGroupMember(groupCode, "", name);
            if (group != null && SelectedGroup?.Code == groupCode)
            {
                GroupChatLines.Add(new ChatLine { Sender = sender, Text = text, IsMine = false });
                while (GroupChatLines.Count > 300) GroupChatLines.RemoveAt(0);
                SaveGroupChat(groupCode);
            }
            else
            {
                if (group != null) group.Unread++;
                UpdateUnread();
                PlayMessageSound();
                ShowToast($"[{groupCode}] {sender}: {text}");
            }
        });
    }

    private void TouchGroupMember(string groupCode, string code, string name)
    {
        if (!_groupMembersRaw.TryGetValue(groupCode, out var map))
        {
            map = new Dictionary<string, GroupMember>();
            _groupMembersRaw[groupCode] = map;
        }
        var key = string.IsNullOrEmpty(code) ? name : code;
        if (!map.TryGetValue(key, out var member))
        {
            member = new GroupMember { Code = key, Name = name };
            map[key] = member;
            if (SelectedGroup?.Code == groupCode) GroupMembers.Add(member);
        }
        if (!string.IsNullOrEmpty(name)) member.Name = name;
        member.LastSeen = DateTime.UtcNow;
        member.Touch();
    }

    public async Task SendFriendRequestAsync()
    {
        if (!CanUseFriendsChat) { ShowToast("cyr31"); return; }
        var code = FriendCodeInput.Trim().ToUpper();
        if (code.Length < 4) { Status = "cyr32"; return; }
        if (code == _myFriendCode) { ShowToast("cyr33"); return; }
        if (Friends.Any(f => f.Code == code)) { ShowToast("cyr34"); return; }

        StartFriends();
        await _friends!.SendRequestAsync(code);
        FriendCodeInput = "";
        ShowToast("cyr35");
    }

    public async Task AcceptRequestAsync(FriendEntry request)
    {
        if (request == null) return;
        FriendRequests.Remove(request);
        OnPropertyChanged(nameof(HasFriendRequests));
        var friend = new FriendEntry { Code = request.Code, Name = request.Name };
        Friends.Add(friend);
        SaveFriends();
        StartFriends();
        await _friends!.AcceptRequestAsync(request.Code);
        ShowToast($"cyr36");
    }

    public void DeclineRequest(FriendEntry request)
    {
        if (request == null) return;
        FriendRequests.Remove(request);
        OnPropertyChanged(nameof(HasFriendRequests));
    }

    public void RemoveFriend(FriendEntry friend)
    {
        if (friend == null) return;
        _friends?.RemoveFriend(friend.Code);
        Friends.Remove(friend);
        SaveFriends();
        if (SelectedFriend == friend) SelectedFriend = null;
        ShowToast("cyr37");
    }

    public void RenameFriend(FriendEntry friend)
    {
        if (friend == null) return;
        var newName = PromptText("cyr38", friend.DisplayName);
        if (string.IsNullOrWhiteSpace(newName)) return;
        friend.PinnedName = newName.Trim();
        SaveFriends();
    }

    public void CopyFriendCode()
    {
        try
        {
            System.Windows.Clipboard.SetText(MyFriendCode);
            ShowToast("cyr39");
        }
        catch { }
    }

    public async Task InviteFriendAsync(FriendEntry friend)
    {
        if (friend == null) return;
        var server = ReadGamePresence();
        if (string.IsNullOrEmpty(server) || server == "singleplayer")
        {
            ShowToast("cyr40");
            return;
        }
        StartFriends();
        await _friends!.InviteToServerAsync(friend.Code, server);
        ShowToast($"cyr41");
    }

    public async Task SendChatAsync()
    {
        if (!CanUseFriendsChat) { ShowToast("cyr42"); return; }
        var text = ChatInput.Trim();
        if (string.IsNullOrEmpty(text) || SelectedFriend == null) return;
        await _friends!.SendMessageAsync(SelectedFriend.Code, text);
        ChatLines.Add(new ChatLine { Sender = "cyr43", Text = text, IsMine = true });
        while (ChatLines.Count > 300) ChatLines.RemoveAt(0);
        SaveFriendChat(SelectedFriend.Code);
        ChatInput = "";
    }

    public async Task SendGroupChatAsync()
    {
        if (!CanUseFriendsChat) { ShowToast("cyr44"); return; }
        var text = GroupChatInput.Trim();
        if (string.IsNullOrEmpty(text) || SelectedGroup == null) return;
        await _friends!.SendGroupMessageAsync(SelectedGroup.Code, text);
        GroupChatLines.Add(new ChatLine { Sender = "cyr45", Text = text, IsMine = true });
        while (GroupChatLines.Count > 300) GroupChatLines.RemoveAt(0);
        SaveGroupChat(SelectedGroup.Code);
        GroupChatInput = "";
    }

    public async Task CreateGroupAsync()
    {
        var name = PromptText("cyr46", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        var code = GenerateFriendCode();
        var group = new GroupChat { Code = code, Name = name.Trim() };
        Groups.Add(group);
        SaveGroups();
        StartFriends();
        await _friends!.JoinGroupAsync(code);
        ShowToast($"cyr47");
    }

    public async Task JoinGroupAsync()
    {
        var code = GroupCodeInput.Trim().ToUpper();
        if (code.Length < 4) { ShowToast("cyr48"); return; }
        if (Groups.Any(g => g.Code == code)) { ShowToast("cyr49"); return; }
        var group = new GroupChat { Code = code };
        Groups.Add(group);
        SaveGroups();
        StartFriends();
        await _friends!.JoinGroupAsync(code);
        GroupCodeInput = "";
        ShowToast("cyr50");
    }

    public void LeaveGroup(GroupChat group)
    {
        if (group == null) return;
        _friends?.LeaveGroup(group.Code);
        Groups.Remove(group);
        SaveGroups();
        if (SelectedGroup == group) SelectedGroup = null;
        ShowToast("cyr51");
    }

    public void JoinFriendServer(FriendEntry friend)
    {
        if (friend == null || string.IsNullOrEmpty(friend.Server) || friend.Server == "singleplayer") return;

        var address = friend.Server;
        var port = 25565;
        var colon = address.LastIndexOf(':');
        if (colon > 0 && int.TryParse(address[(colon + 1)..], out var p))
        {
            address = address[..colon];
            port = p;
        }

        var server = new ServerEntry { Name = address, Address = address, Port = port, Description = $"cyr52" };
        _ = LaunchAsync(server);
    }

    
    
    
    
    
    public async Task JoinInviteServer(ChatLine line)
    {
        if (line == null || string.IsNullOrEmpty(line.InviteServer)) return;

        var address = line.InviteServer.Trim();
        var port = 25565;
        var colon = address.LastIndexOf(':');
        if (colon > 0 && int.TryParse(address[(colon + 1)..], out var p))
        {
            address = address[..colon];
            port = p;
        }
        if (string.IsNullOrEmpty(address)) return;

        if (IsGameRunning)
        {
            var answer = MessageBox.Show(
                $"cyr53" +
                "cyr54",
                "DED Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            StopGame();
            IsGameRunning = false;
            await Task.Delay(800); 
        }

        var server = new ServerEntry { Name = address, Address = address, Port = port, Description = "cyr55" };
        _ = LaunchAsync(server);
    }

    private static string? PromptText(string title, string initial)
    {
        string? result = null;
        var win = new Window
        {
            Title = title,
            Width = 360,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12)),
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow
        };
        var panel = new StackPanel { Margin = new Thickness(14) };
        var box = new TextBox
        {
            Text = initial,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 10),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
            Padding = new Thickness(8, 5, 8, 5)
        };
        var ok = new Button
        {
            Content = "cyr56",
            Width = 90,
            Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(0xB3, 0x00, 0x00)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        ok.Click += (s, e) => { result = box.Text; win.DialogResult = true; win.Close(); };
        panel.Children.Add(box);
        panel.Children.Add(ok);
        win.Content = panel;
        win.ShowDialog();
        return result;
    }

    private void LoadAccountAvatar(string? url)
    {
        AccountAvatar = null;
        if (string.IsNullOrEmpty(url)) return;
        _ = LoadAvatarAsync(url);
    }

    private async Task LoadAvatarAsync(string url)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            AccountAvatar = bmp;
        }
        catch { AccountAvatar = null; }
    }

    private ImageSource? _capePreview;
    public ImageSource? CapePreview { get => _capePreview; set => SetProperty(ref _capePreview, value); }

    private void LoadCapePreview()
    {
        var path = _skins.GetCapePath(Account.Username);
        if (path == null) { CapePreview = null; return; }
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            CapePreview = bmp;
        }
        catch { CapePreview = null; }
    }

    private ImageSource? _skinFullPreview;
    public ImageSource? SkinFullPreview { get => _skinFullPreview; set => SetProperty(ref _skinFullPreview, value); }

    private string _skinStatus = "cyr57";
    public string SkinStatus { get => _skinStatus; set => SetProperty(ref _skinStatus, value); }

    public ICommand SelectSkinCmd { get; private set; } = null!;
    public ICommand RemoveSkinCmd { get; private set; } = null!;

    private string _deviceCode = "";
    public string DeviceCode { get => _deviceCode; set => SetProperty(ref _deviceCode, value); }
    private string _loginLink = "";
    public string LoginLink { get => _loginLink; set => SetProperty(ref _loginLink, value); }
    private bool _isLoggingIn;
    public bool IsLoggingIn { get => _isLoggingIn; set => SetProperty(ref _isLoggingIn, value); }

    
    private bool _homeActive, _playActive, _modsActive, _consoleActive, _setupActive;
    public bool HomeActive { get => _homeActive; set { SetProperty(ref _homeActive, value); OnPropertyChanged(nameof(HomeActiveTag)); OnPropertyChanged(nameof(IsHomePage)); } }
    public bool PlayActive { get => _playActive; set { SetProperty(ref _playActive, value); OnPropertyChanged(nameof(PlayActiveTag)); OnPropertyChanged(nameof(IsFixedPage)); } }
    public bool ModsActive { get => _modsActive; set { SetProperty(ref _modsActive, value); OnPropertyChanged(nameof(ModsActiveTag)); OnPropertyChanged(nameof(IsFixedPage)); } }
    public bool ConsoleActive { get => _consoleActive; set { SetProperty(ref _consoleActive, value); OnPropertyChanged(nameof(ConsoleActiveTag)); OnPropertyChanged(nameof(IsSecondaryPage)); } }
    public bool SetupActive { get => _setupActive; set { SetProperty(ref _setupActive, value); OnPropertyChanged(nameof(SetupActiveTag)); OnPropertyChanged(nameof(IsSecondaryPage)); } }

    private bool _friendsActive;
    public bool FriendsActive { get => _friendsActive; set { SetProperty(ref _friendsActive, value); OnPropertyChanged(nameof(FriendsActiveTag)); OnPropertyChanged(nameof(IsFixedPage)); } }
    public string FriendsActiveTag => FriendsActive ? "Active" : "";

    
    public bool IsFixedPage => _modsActive || _playActive || _friendsActive;

    
    public bool IsHomePage => _homeActive;

    
    public bool IsSecondaryPage => _consoleActive || _screenshotsActive || _setupActive;

    
    private int _settingsSection;
    public int SettingsSection
    {
        get => _settingsSection;
        set
        {
            if (SetProperty(ref _settingsSection, value))
            {
                OnPropertyChanged(nameof(IsSettingsGeneral));
                OnPropertyChanged(nameof(IsSettingsProfile));
                OnPropertyChanged(nameof(IsSettingsLaunch));
                OnPropertyChanged(nameof(IsSettingsAppearance));
            }
        }
    }
    public bool IsSettingsGeneral => _settingsSection == 0;
    public bool IsSettingsProfile => _settingsSection == 1;
    public bool IsSettingsLaunch => _settingsSection == 2;
    public bool IsSettingsAppearance => _settingsSection == 3;

    public string HomeActiveTag => HomeActive ? "Active" : "";
    public string PlayActiveTag => PlayActive ? "Active" : "";
    public string ModsActiveTag => ModsActive ? "Active" : "";
    public string ConsoleActiveTag => ConsoleActive ? "Active" : "";
    public string SetupActiveTag => SetupActive ? "Active" : "";

    private void NavigateTo(string tab)
    {
        HomeActive = PlayActive = ModsActive = ConsoleActive = SetupActive = ScreenshotsActive = FriendsActive = false;
        switch (tab)
        {
            case "home": HomeActive = true; break;
            case "play": PlayActive = true; break;
            case "mods": ModsActive = true; _ = LoadRecommendedModsAsync(); break;
            case "console": ConsoleActive = true; break;
            case "setup": SetupActive = true; break;
            case "screenshots": ScreenshotsActive = true; LoadScreenshots(); break;
            case "friends": FriendsActive = true; break;
        }
    }

    
    public ObservableCollection<string> VersionIds { get; } = new();
    public ObservableCollection<string> InstalledVersionIds { get; } = new();
    public ObservableCollection<VersionOption> VersionOptions { get; } = new();
    private string _versionFilter = "";
    public string VersionFilter { get => _versionFilter; set { SetProperty(ref _versionFilter, value); FilterVersions(); } }

    private int _versionCategory;
    public int VersionCategory
    {
        get => _versionCategory;
        set { if (SetProperty(ref _versionCategory, value)) FilterVersions(); }
    }

    private string _selectedVersionId = "";
    public string SelectedVersionId { get => _selectedVersionId; set => SetProperty(ref _selectedVersionId, value); }

    private VersionOption? _selectedVersionOption;
    public VersionOption? SelectedVersionOption
    {
        get => _selectedVersionOption;
        set
        {
            if (SetProperty(ref _selectedVersionOption, value) && value != null && CurrentProfile != null)
            {
                CurrentProfile.VersionId = value.McVersion;
            }
        }
    }

    
    public string SelectedLoader
    {
        get => CurrentProfile?.ModLoader ?? "Vanilla";
        set
        {
            if (CurrentProfile == null || CurrentProfile.ModLoader == value) return;
            CurrentProfile.ModLoader = value;
            OnPropertyChanged();
            if (value != "Vanilla" && string.IsNullOrEmpty(CurrentProfile.ModLoaderVersion))
                _ = AutoInstallLoaderAsync();
        }
    }

    public ObservableCollection<string> LoaderChoices { get; } = new() { "Vanilla", "Fabric", "Forge", "OptiFine" };

    private async Task AutoInstallLoaderAsync()
    {
        if (CurrentProfile == null) return;
        EditModLoader = CurrentProfile.ModLoader;
        await InstallModLoaderAsync();
    }

    private void SyncVersionOption()
    {
        if (CurrentProfile == null) return;
        var match = VersionOptions.FirstOrDefault(o => o.McVersion == CurrentProfile.VersionId);
        if (match != null)
            _selectedVersionOption = match;
        else if (VersionOptions.Count > 0)
            _selectedVersionOption = VersionOptions[0];
        OnPropertyChanged(nameof(SelectedVersionOption));
        OnPropertyChanged(nameof(SelectedLoader));
    }

    private void FilterVersions()
    {
        VersionIds.Clear();
        VersionOptions.Clear();
        if (_allVersions == null) return;

        var q = (_versionFilter ?? "").ToLower();
        var all = _allVersions
            .OrderByDescending(v => v.ReleaseTime)
            .ToList();

        foreach (var v in all)
        {
            
            var type = v.Type ?? "release";
            if (_versionCategory == 1 && type != "release") continue;
            if (_versionCategory == 2 && type != "snapshot") continue;
            if (_versionCategory == 3 && type != "old_alpha" && type != "old_beta") continue;

            if (!string.IsNullOrEmpty(q) && !v.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;

            VersionIds.Add(v.Name);
            VersionOptions.Add(new VersionOption(v.Name, type));
        }
    }

    
    public ObservableCollection<LaunchProfile> Profiles { get; } = new();
    private LaunchProfile? _currentProfile;
    public LaunchProfile? CurrentProfile
    {
        get => _currentProfile;
        set
        {
            if (SetProperty(ref _currentProfile, value))
            {
                if (value != null)
                {
                    _mods.GameDir = GetProfileGameDir(value);
                }
                LoadMods();
                LoadInstalledResourcePacks();
                LoadInstalledShaders();
                LoadScreenshots();
                SyncAdminProfile();
            }
        }
    }

    
    
    
    
    private string GetProfileGameDir(LaunchProfile p)
    {
        if (!string.IsNullOrEmpty(p.GameDir))
        {
            if (!Directory.Exists(p.GameDir)) Directory.CreateDirectory(p.GameDir);
            return p.GameDir;
        }
        return MinecraftPathHelper.GameDir;
    }
    public ObservableCollection<string> ModLoaders { get; } = new(new[] { "Vanilla", "Forge", "Fabric", "OptiFine" });
    private string _editProfileVersion = "";
    public string EditProfileVersion { get => _editProfileVersion; set => SetProperty(ref _editProfileVersion, value); }
    private string _editModLoader = "Vanilla";
    public string EditModLoader
    {
        get => _editModLoader;
        set { if (SetProperty(ref _editModLoader, value)) { _ = LoadLoaderVersionsAsync(); } }
    }
    private string _editModLoaderVersion = "";
    public string EditModLoaderVersion { get => _editModLoaderVersion; set => SetProperty(ref _editModLoaderVersion, value); }

    public ObservableCollection<string> AvailableLoaderVersions { get; } = new();
    private string _selectedLoaderVersion = "";
    public string SelectedLoaderVersion
    {
        get => _selectedLoaderVersion;
        set { if (SetProperty(ref _selectedLoaderVersion, value)) { _editModLoaderVersion = value; OnPropertyChanged(nameof(EditModLoaderVersion)); } }
    }

    private async Task LoadLoaderVersionsAsync()
    {
        AvailableLoaderVersions.Clear();
        if (CurrentProfile == null) return;

        try
        {
            if (EditModLoader == "Fabric")
            {
                Status = "cyr58";
                var versions = await _mods.GetFabricLoadersForMcAsync(CurrentProfile.VersionId);
                foreach (var v in versions.Take(30)) AvailableLoaderVersions.Add(v);
                if (AvailableLoaderVersions.Count > 0)
                {
                    SelectedLoaderVersion = AvailableLoaderVersions[0];
                    Status = $"cyr59";
                }
                else Status = "cyr60" + CurrentProfile.VersionId;
            }
            else if (EditModLoader == "Forge")
            {
                Status = "cyr61";
                var versions = await _mods.GetForgeVersionsAsync(CurrentProfile.VersionId);
                foreach (var v in versions.Select(x => x.Version).Take(30)) AvailableLoaderVersions.Add(v);
                if (AvailableLoaderVersions.Count > 0)
                {
                    SelectedLoaderVersion = AvailableLoaderVersions[0];
                    Status = $"cyr62";
                }
                else Status = "cyr63" + CurrentProfile.VersionId;
            }
            else if (EditModLoader == "OptiFine")
            {
                Status = "cyr64";
                var versions = await _mods.GetOptiFineVersionsAsync(CurrentProfile.VersionId);
                foreach (var v in versions.Take(30))
                    AvailableLoaderVersions.Add($"{v.Type}_{v.Patch}");
                if (AvailableLoaderVersions.Count > 0)
                {
                    
                    SelectedLoaderVersion = AvailableLoaderVersions[^1];
                    Status = $"cyr65";
                }
                else Status = "cyr66" + CurrentProfile.VersionId;
            }
        }
        catch (Exception ex) { Status = $"cyr67"; }
    }

    
    public long SystemRamMb => SystemInfo.TotalRamMb;
    public int MaxRamLimitMb => SystemInfo.MaxAllocatableMb;
    public int RecommendedRamMb => SystemInfo.RecommendedRamMb;
    public string RamInfo => $"cyr68";

    public int RamSliderMin => 1024;

    public double RamSliderValue
    {
        get => CurrentProfile?.MaxRamMb ?? RecommendedRamMb;
        set
        {
            if (CurrentProfile == null) return;
            int clamped = (int)Math.Clamp(value, 1024, MaxRamLimitMb);
            CurrentProfile.MaxRamMb = clamped;
            OnPropertyChanged(nameof(RamSliderValue));
        }
    }

    public void ApplyRecommendedRam()
    {
        if (CurrentProfile != null)
        {
            CurrentProfile.MaxRamMb = RecommendedRamMb;
            OnPropertyChanged(nameof(RamSliderValue));
        }
    }

    
    public ObservableCollection<JavaInfo> JavaInstallations { get; } = new();

    
    public ObservableCollection<LaunchHistoryEntry> LaunchHistory { get; } = new();

    
    public ObservableCollection<ServerEntry> ServersList { get; } = new();
    private string _serverInput = "";
    public string ServerInput { get => _serverInput; set => SetProperty(ref _serverInput, value); }
    private bool _serversRefreshing;
    public bool ServersRefreshing { get => _serversRefreshing; set => SetProperty(ref _serversRefreshing, value); }

    
    public ObservableCollection<NewsItem> NewsItems { get; } = new();

    
    public ObservableCollection<ModInfo> InstalledMods { get; } = new();
    public ObservableCollection<ModrinthMod> ModrinthResults { get; } = new();
    public ObservableCollection<CurseForgeMod> CurseForgeResults { get; } = new();
    public int ModsCount => InstalledMods.Count(m => m.Enabled);
    public ObservableCollection<ModInfo> ActiveMods { get; } = new();
    private string _modSearchQuery = "";
    public string ModSearchQuery { get => _modSearchQuery; set => SetProperty(ref _modSearchQuery, value); }

    private string _modSource = "Modrinth";
    public string ModSource
    {
        get => _modSource;
        set { if (SetProperty(ref _modSource, value)) { OnPropertyChanged(nameof(IsModrinthSource)); OnPropertyChanged(nameof(IsCurseForgeSource)); } }
    }
    public bool IsModrinthSource => _modSource == "Modrinth";
    public bool IsCurseForgeSource => _modSource == "CurseForge";

    
    private int _modsSubTab;
    public int ModsSubTab
    {
        get => _modsSubTab;
        set
        {
            if (SetProperty(ref _modsSubTab, value))
            {
                OnPropertyChanged(nameof(IsModsSubTab));
                OnPropertyChanged(nameof(IsResourcePacksSubTab));
                OnPropertyChanged(nameof(IsShadersSubTab));
                OnPropertyChanged(nameof(IsServersSubTab));

                
                switch (value)
                {
                    case 1:
                        if (RpSource == "CurseForge") { if (ResourcePackCfResults.Count == 0) _ = LoadCurseForgeResourcePacksAsync(); }
                        else if (ResourcePackResults.Count == 0) _ = LoadRecommendedResourcePacksAsync();
                        break;
                    case 2:
                        if (ShaderSource == "CurseForge") { if (ShaderCfResults.Count == 0) _ = LoadCurseForgeShadersAsync(); }
                        else if (ShaderResults.Count == 0) _ = LoadRecommendedShadersAsync();
                        break;
                    case 3:
                        LoadServers();
                        break;
                }
            }
        }
    }
    public bool IsModsSubTab => _modsSubTab == 0;
    public bool IsResourcePacksSubTab => _modsSubTab == 1;
    public bool IsShadersSubTab => _modsSubTab == 2;
    public bool IsServersSubTab => _modsSubTab == 3;

    public ObservableCollection<ModrinthMod> ResourcePackResults { get; } = new();    public ObservableCollection<CurseForgeMod> ResourcePackCfResults { get; } = new();
    public ObservableCollection<ModrinthMod> ShaderResults { get; } = new();
    public ObservableCollection<CurseForgeMod> ShaderCfResults { get; } = new();

    public ObservableCollection<InstalledPackItem> InstalledResourcePacks { get; } = new();
    public ObservableCollection<InstalledPackItem> InstalledShaders { get; } = new();

    
    private string _rpSource = "Modrinth";
    public string RpSource
    {
        get => _rpSource;
        set { if (SetProperty(ref _rpSource, value)) { OnPropertyChanged(nameof(IsRpModrinth)); OnPropertyChanged(nameof(IsRpCurseForge)); } }
    }
    public bool IsRpModrinth => _rpSource == "Modrinth";
    public bool IsRpCurseForge => _rpSource == "CurseForge";

    private string _shaderSource = "Modrinth";
    public string ShaderSource
    {
        get => _shaderSource;
        set { if (SetProperty(ref _shaderSource, value)) { OnPropertyChanged(nameof(IsShaderModrinth)); OnPropertyChanged(nameof(IsShaderCurseForge)); } }
    }
    public bool IsShaderModrinth => _shaderSource == "Modrinth";
    public bool IsShaderCurseForge => _shaderSource == "CurseForge";

    
    private bool _showInstalledMods;
    public bool ShowInstalledMods
    {
        get => _showInstalledMods;
        set { if (SetProperty(ref _showInstalledMods, value)) { OnPropertyChanged(nameof(IsModsBrowserView)); OnPropertyChanged(nameof(IsModsInstalledView)); } }
    }
    public bool IsModsBrowserView => !_showInstalledMods;
    public bool IsModsInstalledView => _showInstalledMods;

    private bool _showInstalledRps;
    public bool ShowInstalledRps
    {
        get => _showInstalledRps;
        set { if (SetProperty(ref _showInstalledRps, value)) { OnPropertyChanged(nameof(IsRpBrowserView)); OnPropertyChanged(nameof(IsRpInstalledView)); } }
    }
    public bool IsRpBrowserView => !_showInstalledRps;
    public bool IsRpInstalledView => _showInstalledRps;

    private bool _showInstalledShaders;
    public bool ShowInstalledShaders
    {
        get => _showInstalledShaders;
        set { if (SetProperty(ref _showInstalledShaders, value)) { OnPropertyChanged(nameof(IsShaderBrowserView)); OnPropertyChanged(nameof(IsShaderInstalledView)); } }
    }
    public bool IsShaderBrowserView => !_showInstalledShaders;
    public bool IsShaderInstalledView => _showInstalledShaders;

    
    private int _modOffset;
    private int _modTotalHits;
    public bool HasMoreMods => _modOffset < _modTotalHits;
    private int _rpOffset;
    private int _rpTotalHits;
    public bool HasMoreRps => _rpOffset < _rpTotalHits;
    private int _shaderOffset;
    private int _shaderTotalHits;
    public bool HasMoreShaders => _shaderOffset < _shaderTotalHits;

    
    private const int CfChunkSize = 12;
    private List<CurseForgeMod> _allCfMods = new();
    private List<CurseForgeMod> _allCfResourcePacks = new();
    private List<CurseForgeMod> _allCfShaders = new();
    private int _modCfOffset;
    private int _rpCfOffset;
    private int _shaderCfOffset;
    public bool HasMoreCfMods => _modCfOffset < _allCfMods.Count;
    public bool HasMoreCfResourcePacks => _rpCfOffset < _allCfResourcePacks.Count;
    public bool HasMoreCfShaders => _shaderCfOffset < _allCfShaders.Count;

    
    public ObservableCollection<ScreenshotItem> Screenshots { get; } = new();
    private bool _screenshotsActive;
    public bool ScreenshotsActive
    {
        get => _screenshotsActive;
        set { SetProperty(ref _screenshotsActive, value); OnPropertyChanged(nameof(ScreenshotsActiveTag)); OnPropertyChanged(nameof(IsSecondaryPage)); }
    }
    public string ScreenshotsActiveTag => ScreenshotsActive ? "Active" : "";

    public ICommand NavScreenshotsCmd { get; private set; } = null!;

    
    private int _modViewMode = 3;
    public int ModViewMode
    {
        get => _modViewMode;
        set
        {
            if (SetProperty(ref _modViewMode, value))
            {
                OnPropertyChanged(nameof(ModGridColumns));
                OnPropertyChanged(nameof(IsGridMode));
            }
        }
    }
    public int ModGridColumns => _modViewMode switch
    {
        0 => 1,
        1 => 2,
        2 => 3,
        _ => 4
    };
    public bool IsGridMode => _modViewMode == 3;

    
    public ObservableCollection<string> ConsoleLines { get; } = new();
    private string _cmdInput = "";
    public string CmdInput { get => _cmdInput; set => SetProperty(ref _cmdInput, value); }

    
    private string _status = "cyr69";
    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
                Log(value);
        }
    }

    private void Log(string message)
    {
        void Add(string m)
        {
            ConsoleLines.Add(m);
            while (ConsoleLines.Count > 1000) ConsoleLines.RemoveAt(0);
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (Application.Current.Dispatcher.CheckAccess())
            Add(line);
        else
            Application.Current.Dispatcher.Invoke(() => Add(line));

        
        try
        {
            File.AppendAllText(Path.Combine(MinecraftPathHelper.BaseDir, "launcher.log"), line + Environment.NewLine);
        }
        catch { }
    }
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { if (SetProperty(ref _isBusy, value)) { OnPropertyChanged(nameof(DlIndeterminate)); OnPropertyChanged(nameof(IsDownloading)); } } }
    private bool _isLaunching;
    public bool IsLaunching { get => _isLaunching; set => SetProperty(ref _isLaunching, value); }
    private bool _isGameRunning;
    public bool IsGameRunning { get => _isGameRunning; set => SetProperty(ref _isGameRunning, value); }
    private double _dlProgress;
    private double _dlProgressTarget;
    private System.Windows.Threading.DispatcherTimer? _dlSmoothTimer;

    public double DlProgress { get => _dlProgress; set { if (SetProperty(ref _dlProgress, value)) OnPropertyChanged(nameof(DlIndeterminate)); } }

    
    
    
    
    public void ReportProgress(double pct)
    {
        _dlProgressTarget = Math.Clamp(pct, 0, 100);
        if (_dlProgressTarget >= 99.9)
        {
            _dlSmoothTimer?.Stop();
            DlProgress = 100;
            return;
        }
        if (_dlProgress > _dlProgressTarget)
        {
            
            DlProgress = _dlProgressTarget;
        }
        EnsureSmoothTimer();
    }

    private void EnsureSmoothTimer()
    {
        if (_dlSmoothTimer != null) return;
        _dlSmoothTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PerfSettings.ProgressSmoothIntervalMs)
        };
        _dlSmoothTimer.Tick += (s, e) =>
        {
            var diff = _dlProgressTarget - _dlProgress;
            if (Math.Abs(diff) < 0.4)
            {
                DlProgress = _dlProgressTarget;
                _dlSmoothTimer.Stop();
                return;
            }
            DlProgress += diff * 0.22;
        };
        _dlSmoothTimer.Start();
    }

    private string _dlFile = "";
    public string DlFile { get => _dlFile; set { if (SetProperty(ref _dlFile, value)) { OnPropertyChanged(nameof(IsDownloading)); OnPropertyChanged(nameof(DlIndeterminate)); } } }

    
    public bool IsDownloading => IsBusy && !string.IsNullOrEmpty(DlFile);

    
    public bool DlIndeterminate => IsDownloading && DlProgress <= 0;

    
    private string _toastText = "";
    public string ToastText { get => _toastText; set => SetProperty(ref _toastText, value); }

    private bool _toastVisible;
    public bool ToastVisible { get => _toastVisible; set => SetProperty(ref _toastVisible, value); }

    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    public void ShowToast(string message)
    {
        ToastText = message;
        ToastVisible = true;
        _toastTimer?.Stop();
        _toastTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            ToastVisible = false;
        };
        _toastTimer.Start();
    }

    
    
    
    
    private async void FinishDownload()
    {
        DlProgress = 100;
        DlFile = "cyr70";
        await Task.Delay(900);
        IsBusy = false;
        DlFile = "";
    }

    
    public ICommand NavHomeCmd { get; private set; } = null!;
    public ICommand NavPlayCmd { get; private set; } = null!;
    public ICommand NavModsCmd { get; private set; } = null!;
    public ICommand NavConsoleCmd { get; private set; } = null!;
    public ICommand NavSetupCmd { get; private set; } = null!;
    public ICommand LoginMsCmd { get; private set; } = null!;
    public ICommand LoginOfflineCmd { get; private set; } = null!;
    public ICommand LoginDiscordCmd { get; private set; } = null!;
    public ICommand LaunchCmd { get; private set; } = null!;
    public ICommand StopCmd { get; private set; } = null!;
    public ICommand SaveProfileCmd { get; private set; } = null!;
    public ICommand DeleteProfileCmd { get; private set; } = null!;
    public ICommand NewProfileCmd { get; private set; } = null!;
    public ICommand RenameProfileCmd { get; private set; } = null!;
    public ICommand SearchModsCmd { get; private set; } = null!;
    public ICommand ClearLogsCmd { get; private set; } = null!;
    public ICommand SendCmdCmd { get; private set; } = null!;
    public ICommand InstallLoaderCmd { get; private set; } = null!;
    public ICommand RefreshVersionsCmd { get; private set; } = null!;
    public ICommand DownloadVersionCmd { get; private set; } = null!;
    public ICommand OpenModsFolderCmd { get; private set; } = null!;

    private void InitCommands()
    {
        NavHomeCmd = new RelayCommand(_ => NavigateTo("home"));
        NavPlayCmd = new RelayCommand(_ => NavigateTo("play"));
        NavModsCmd = new RelayCommand(_ => NavigateTo("mods"));
        NavConsoleCmd = new RelayCommand(_ => NavigateTo("console"));
        NavSetupCmd = new RelayCommand(_ => NavigateTo("setup"));
        NavScreenshotsCmd = new RelayCommand(_ => NavigateTo("screenshots"));
        NavFriendsCmd = new RelayCommand(_ => NavigateTo("friends"));
        AddFriendCmd = new RelayCommand(async _ => await SendFriendRequestAsync());
        SendChatCmd = new RelayCommand(async _ => await SendChatAsync());
        CopyCodeCmd = new RelayCommand(_ => CopyFriendCode());
        CreateGroupCmd = new RelayCommand(async _ => await CreateGroupAsync());
        JoinGroupCmd = new RelayCommand(async _ => await JoinGroupAsync());
        SendGroupChatCmd = new RelayCommand(async _ => await SendGroupChatAsync());
        ClearChatCmd = new RelayCommand(_ => ClearChat());
        LoginMsCmd = new RelayCommand(async _ => await LoginMsAsync());
        LoginOfflineCmd = new RelayCommand(_ => LoginOffline());
        LoginDiscordCmd = new RelayCommand(async _ => await LoginDiscordAsync());
        LaunchCmd = new RelayCommand(async _ => await LaunchAsync());
        StopCmd = new RelayCommand(_ => StopGame());
        SaveProfileCmd = new RelayCommand(_ => SaveCurrentProfile());
        DeleteProfileCmd = new RelayCommand(_ => DeleteCurrentProfile());
        NewProfileCmd = new RelayCommand(_ => CreateProfile());
        RenameProfileCmd = new RelayCommand(_ => RenameCurrentProfile());
        SearchModsCmd = new RelayCommand(async _ => await SearchModsAsync());
        ClearLogsCmd = new RelayCommand(_ => ConsoleLines.Clear());
        SendCmdCmd = new RelayCommand(_ => { ConsoleLines.Add($"> {CmdInput}"); CmdInput = ""; });
        InstallLoaderCmd = new RelayCommand(async _ => await InstallModLoaderAsync());
        SelectSkinCmd = new RelayCommand(_ => SelectSkin());
        RemoveSkinCmd = new RelayCommand(_ => RemoveSkin());
        RefreshVersionsCmd = new RelayCommand(async _ => await RefreshVersionsAsync());
        DownloadVersionCmd = new RelayCommand(async _ => await DownloadVersionAsync());
        OpenModsFolderCmd = new RelayCommand(_ => OpenModsFolder());
    }

    private static string ServersFilePath => Path.Combine(MinecraftPathHelper.BaseDir, "servers.json");

    private void InitServerData()
    {
        LoadServers();
        LoadInstalledResourcePacks();
        LoadInstalledShaders();

        NewsItems.Add(new() { Title = "cyr71", Date = "cyr72" });
        NewsItems.Add(new() { Title = "cyr73", Date = "cyr74" });
        NewsItems.Add(new() { Title = "cyr75", Date = "cyr76" });
        NewsItems.Add(new() { Title = "cyr77", Date = "cyr78" });
    }

    

    
    public IEnumerable<ServerEntry> RecentServers =>
        ServersList.Where(s => s.LastPlayed > DateTime.MinValue)
                   .OrderByDescending(s => s.LastPlayed)
                   .Take(8);

    public void LoadServers()
    {
        ServersList.Clear();
        try
        {
            if (File.Exists(ServersFilePath))
            {
                var list = JsonSerializer.Deserialize<List<ServerEntry>>(File.ReadAllText(ServersFilePath));
                if (list != null)
                    foreach (var s in list.OrderByDescending(s => s.LastPlayed))
                        ServersList.Add(s);
            }
        }
        catch { }

        
        var serversDat = Path.Combine(_mods.GameDir, "servers.dat");
        _ = Task.Run(() => NbtServersReader.ReadServersDat(serversDat)).ContinueWith(t =>
        {
            List<NbtServersReader.McServer> mcServers;
            try { mcServers = t.Result; } catch { mcServers = new(); }
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var s in mcServers)
                {
                    if (ServersList.Any(x => x.Address.Equals(s.Ip, StringComparison.OrdinalIgnoreCase) && x.Port == s.Port))
                        continue;
                    var entry = new ServerEntry
                    {
                        Name = s.Name,
                        Address = s.Ip,
                        Port = s.Port,
                        Description = "cyr79"
                    };
                    if (!string.IsNullOrEmpty(s.IconBase64))
                        entry.Icon = DecodeServerIcon(s.IconBase64);
                    ServersList.Add(entry);
                }
                if (ServersList.Count > 0) _ = RefreshServersAsync();
                OnPropertyChanged(nameof(RecentServers));
            });
        });

        if (ServersList.Count > 0) _ = RefreshServersAsync();
        OnPropertyChanged(nameof(RecentServers));
    }

    public void SaveServers()
    {
        try
        {
            File.WriteAllText(ServersFilePath, JsonSerializer.Serialize(ServersList.ToList()));
        }
        catch { }
    }

    public async Task AddServerAsync()
    {
        var input = ServerInput.Trim();
        if (string.IsNullOrEmpty(input)) return;

        var address = input;
        var port = 25565;
        var colonIdx = input.LastIndexOf(':');
        if (colonIdx > 0 && int.TryParse(input[(colonIdx + 1)..], out var parsed))
        {
            address = input[..colonIdx];
            port = parsed;
        }

        if (ServersList.Any(s => s.Address.Equals(address, StringComparison.OrdinalIgnoreCase) && s.Port == port))
        {
            Status = "cyr80";
            ServerInput = "";
            return;
        }

        var entry = new ServerEntry
        {
            Name = address,
            Address = address,
            Port = port,
            Description = ""
        };
        ServersList.Insert(0, entry);
        SaveServers();
        ServerInput = "";
        Status = $"cyr81";
        ShowToast("cyr82");
        OnPropertyChanged(nameof(RecentServers));
        await PingServerAsync(entry);
    }

    public void RemoveServer(ServerEntry server)
    {
        ServersList.Remove(server);
        SaveServers();
        Status = "cyr83";
        ShowToast("cyr84");
        OnPropertyChanged(nameof(RecentServers));
    }

    public async Task JoinServerAsync(ServerEntry server)
    {
        if (server == null) return;
        await LaunchAsync(server);
    }

    public async Task RefreshServersAsync(bool force = false)
    {
        if (ServersRefreshing) return;
        ServersRefreshing = true;
        try
        {
            
            var targets = ServersList
                .Where(s => force || DateTime.UtcNow - s.LastPingTime > TimeSpan.FromSeconds(s.IsOnline ? 45 : 15))
                .ToList();
            await Task.WhenAll(targets.Select(PingServerAsync));
            Status = "cyr85";
        }
        finally { ServersRefreshing = false; }
    }

    private async Task PingServerAsync(ServerEntry server)
    {
        var result = await ServerPinger.PingAsync(server.Address, server.Port);
        server.LastPingTime = DateTime.UtcNow;
        if (!result.Success)
        {
            server.Online = "cyr86";
            server.Ping = "—";
            return;
        }
        server.Online = $"{result.Online}/{result.Max}";
        server.Ping = result.LatencyMs.ToString();
        if (!string.IsNullOrEmpty(result.Description))
            server.Description = result.Description;
    }

    private void ResortServers()
    {
        var ordered = ServersList.OrderByDescending(s => s.LastPlayed).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var idx = ServersList.IndexOf(ordered[i]);
            if (idx != i) ServersList.Move(idx, i);
        }
        OnPropertyChanged(nameof(RecentServers));
    }

    private static ImageSource? DecodeServerIcon(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    

    public void LoadInstalledResourcePacks()
    {
        LoadPacksAsync(_mods.GetResourcePacksDir(), InstalledResourcePacks);
    }

    public void LoadInstalledShaders()
    {
        LoadPacksAsync(_mods.GetShadersDir(), InstalledShaders);
    }

    
    private void LoadPacksAsync(string dir, ObservableCollection<InstalledPackItem> target)
    {
        _ = Task.Run(() =>
        {
            var items = new List<InstalledPackItem>();
            foreach (var file in ScanPackFiles(dir))
                items.Add(BuildPackItem(file));
            return items;
        }).ContinueWith(t =>
        {
            List<InstalledPackItem> items;
            try { items = t.Result; } catch { items = new(); }
            Application.Current.Dispatcher.Invoke(() =>
            {
                target.Clear();
                foreach (var i in items) target.Add(i);
            });
        });
    }

    private static IEnumerable<string> ScanPackFiles(string dir)
    {
        if (!Directory.Exists(dir)) yield break;

        foreach (var f in Directory.GetFiles(dir).OrderByDescending(f => File.GetLastWriteTime(f)))
        {
            var ext = Path.GetExtension(f).ToLower();
            if (ext is ".zip" or ".jar") yield return f;
        }
        foreach (var d in Directory.GetDirectories(dir).OrderByDescending(d => Directory.GetLastWriteTime(d)))
            yield return d;
    }

    private static InstalledPackItem BuildPackItem(string path)
    {
        var isDir = Directory.Exists(path);
        string description = "";
        if (!isDir && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                var meta = zip.GetEntry("pack.mcmeta");
                if (meta != null)
                {
                    using var reader = new StreamReader(meta.Open());
                    using var doc = JsonDocument.Parse(reader.ReadToEnd());
                    if (doc.RootElement.TryGetProperty("pack", out var pack) &&
                        pack.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
                        description = desc.GetString() ?? "";
                }
            }
            catch { }
        }

        long size = isDir
            ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
            : new FileInfo(path).Length;

        return new InstalledPackItem
        {
            FileName = Path.GetFileName(path),
            FilePath = path,
            Description = description,
            SizeText = FormatBytes(size),
            InstalledAt = isDir ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path)
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1 << 30) return $"cyr87";
        if (bytes >= 1 << 20) return $"cyr88";
        if (bytes >= 1 << 10) return $"cyr89";
        return $"cyr90";
    }

    public void RemoveInstalledPack(InstalledPackItem item, bool isShader)
    {
        try
        {
            if (Directory.Exists(item.FilePath)) Directory.Delete(item.FilePath, true);
            else File.Delete(item.FilePath);
            if (isShader) LoadInstalledShaders(); else LoadInstalledResourcePacks();
            Status = $"cyr91";
            ShowToast("cyr92");
        }
        catch (Exception ex) { Status = $"cyr93"; }
    }

    
    
    private bool _isInitializing = true;
    public bool IsInitializing { get => _isInitializing; set => SetProperty(ref _isInitializing, value); }

    private string _initStatus = "cyr94";
    public string InitStatus { get => _initStatus; set => SetProperty(ref _initStatus, value); }

    public async Task InitAsync()
    {
        IsInitializing = true;
        try
        {
            InitStatus = "cyr95";
            LoadSettings();

            InitStatus = "cyr96";
            LoadSavedAccount();
            LoadDiscordProfile();
            LoadLaunchHistory();
            LoadFriends();
            LoadGroups();
            StartFriends();
            StartAdmin();
            HomeActive = true;

            InitStatus = "cyr97";
            var parameters = MinecraftLauncherParameters.CreateDefault(_minecraftPath, _http);
            _launcher = new MinecraftLauncher(parameters);
            _allVersions = await _launcher.GetAllVersionsAsync();

            InitStatus = "cyr98";
            _ = LoadJavaAsync();
            await LoadProfilesAsync();
            await RefreshInstalledVersions();

            InitStatus = "cyr99";
            FilterVersions();
            SyncVersionOption();
            UpdateActiveMods();
            Status = "cyr100";

            
            
            _ = _mods.PrefetchPopularAsync(includeIcons: !_lowEndMode);

            
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                Application.Current.Dispatcher.Invoke(() => _ = CheckForUpdatesAsync(manual: false));
            });
        }
        catch (Exception ex)
        {
            Status = $"cyr101";
            InitStatus = "cyr102";
        }
        finally
        {
            await Task.Delay(250); 
            IsInitializing = false;
        }
    }

    
    public static string SettingsPath => Path.Combine(MinecraftPathHelper.BaseDir, "settings.json");

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var s = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsPath));
                if (s != null)
                {
                    _selectedTheme = s.Theme;
                    OnPropertyChanged(nameof(SelectedTheme));
                    ThemeManager.ApplyTheme(_selectedTheme);

                    _autoLogin = s.AutoLogin;
                    OnPropertyChanged(nameof(AutoLogin));
                    _fontSize = s.FontSize;
                    OnPropertyChanged(nameof(FontSize));
                    MainWindow.UpdateUiScale(_fontSize);
                    _offlineUsername = s.OfflineUsername;
                    OnPropertyChanged(nameof(OfflineUsername));

                    _multipleInstances = s.MultipleInstances;
                    OnPropertyChanged(nameof(MultipleInstances));
                    _postLaunchAction = string.IsNullOrEmpty(s.PostLaunchAction) ? "keep" : s.PostLaunchAction;
                    OnPropertyChanged(nameof(PostLaunchAction));

                    _ipv4Only = s.Ipv4Only;
                    OnPropertyChanged(nameof(Ipv4Only));

                    _lowEndMode = s.LowEndMode ?? PerfSettings.AutoDetectLowEnd();
                    PerfSettings.LowEndMode = _lowEndMode;
                    OnPropertyChanged(nameof(LowEndMode));

                    _softwareRendering = s.SoftwareRendering;
                    PerfSettings.SoftwareRendering = _softwareRendering;
                    OnPropertyChanged(nameof(SoftwareRendering));

                    _discordClientId = s.DiscordClientId;
                    OnPropertyChanged(nameof(DiscordClientId));
                    _discordClientSecret = s.DiscordClientSecret;
                    OnPropertyChanged(nameof(DiscordClientSecret));
                }
            }
        }
        catch { }
    }

    public void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(MinecraftPathHelper.BaseDir);
            var s = new LauncherSettings
            {
                Theme = _selectedTheme,
                AutoLogin = _autoLogin,
                FontSize = _fontSize,
                OfflineUsername = _offlineUsername,
                MultipleInstances = _multipleInstances,
                PostLaunchAction = _postLaunchAction,
                Ipv4Only = _ipv4Only,
                LowEndMode = _lowEndMode,
                SoftwareRendering = _softwareRendering,
                DiscordClientId = _discordClientId,
                DiscordClientSecret = _discordClientSecret
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    
    private static string AccountsFilePath => Path.Combine(MinecraftPathHelper.BaseDir, "accounts.json");

    public ObservableCollection<AccountInfo> Accounts { get; } = new();

    private AccountInfo? _selectedAccount;
    public AccountInfo? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value) && value != null && value.IsLoggedIn)
                ApplyAccount(value);
        }
    }

    private void ApplyAccount(AccountInfo acc)
    {
        _session = acc.IsOffline
            ? new MSession { Username = acc.Username, UUID = acc.Uuid, AccessToken = "0", UserType = "legacy" }
            : new MSession { Username = acc.Username, UUID = acc.Uuid, AccessToken = acc.AccessToken };

        Account = acc;
        SaveAccount();
        AccountStatus = acc.IsOffline ? $"cyr103" : $"MS: {acc.Username}";
        LoadSkinPreview(acc.SkinPath);
        LoadAccountAvatar(acc.AvatarUrl);
        LoadCapePreview();
        SyncAdminProfile();
        Status = $"cyr104";
        ShowToast($"cyr105");
    }

    private void LoadAccounts()
    {
        Accounts.Clear();
        try
        {
            if (File.Exists(AccountsFilePath))
            {
                var list = JsonSerializer.Deserialize<List<AccountInfo>>(File.ReadAllText(AccountsFilePath));
                if (list != null)
                    foreach (var a in list.Where(a => a.IsLoggedIn && a.AccountType != "discord"))
                        Accounts.Add(a);
            }
        }
        catch { }

        
        if (Account.IsLoggedIn && !Accounts.Any(a => a.Uuid == Account.Uuid && a.Username == Account.Username))
        {
            Accounts.Insert(0, Account);
            SaveAccounts();
        }
    }

    private void SaveAccounts()
    {
        try
        {
            File.WriteAllText(AccountsFilePath, JsonSerializer.Serialize(Accounts.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public void RemoveAccount(AccountInfo acc)
    {
        if (acc == null) return;
        var r = MessageBox.Show($"cyr106", "cyr107",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        Accounts.Remove(acc);
        SaveAccounts();

        if (Account.Uuid == acc.Uuid && Account.Username == acc.Username)
        {
            var next = Accounts.FirstOrDefault();
            if (next != null) { SelectedAccount = next; }
            else
            {
                
                LoginOffline("Player");
            }
        }
        Status = $"cyr108";
    }

    private void LoadSavedAccount()
    {
        try
        {
            var p = Path.Combine(MinecraftPathHelper.BaseDir, "account.json");
            if (File.Exists(p))
            {
                Account = JsonSerializer.Deserialize<AccountInfo>(File.ReadAllText(p)) ?? new();
                if (Account.IsLoggedIn)
                {
                    AccountStatus = Account.IsOffline ? $"cyr109" : $"MS: {Account.Username}";

                    
                    if (string.IsNullOrEmpty(Account.SkinPath))
                    {
                        var found = _skins.GetSkinPath(Account.Username);
                        if (found != null)
                        {
                            Account.SkinPath = found;
                            SaveAccount();
                        }
                    }
                    LoadSkinPreview(Account.SkinPath);
                    CapeStatus = _skins.GetCapePath(Account.Username) != null
                        ? $"cyr110"
                        : "cyr111";
                    LoadCapePreview();

                    if (AutoLogin)
                    {
                        if (!Account.IsOffline)
                            _session = new MSession { Username = Account.Username, UUID = Account.Uuid, AccessToken = Account.AccessToken };
                        else
                            _session = new MSession
                            {
                                Username = Account.Username,
                                UUID = Account.Uuid,
                                AccessToken = "0",
                                UserType = "legacy"
                            };
                    }
                }
            }
            else if (AutoLogin)
            {
                
                _session = MSession.CreateOfflineSession("Player");
                Account = new AccountInfo
                {
                    Username = "Player", Uuid = _session.UUID, AccessToken = _session.AccessToken,
                    AccountType = "offline", ExpiresAt = DateTime.MaxValue
                };
                SaveAccount();
                AccountStatus = "cyr112";
            }

            LoadAccounts();
            if (Account.IsLoggedIn)
                _selectedAccount = Accounts.FirstOrDefault(a => a.Uuid == Account.Uuid) ?? Account;
            OnPropertyChanged(nameof(SelectedAccount));
        }
        catch { }
    }

    private void SaveAccount()
    {
        File.WriteAllText(Path.Combine(MinecraftPathHelper.BaseDir, "account.json"),
            JsonSerializer.Serialize(Account, new JsonSerializerOptions { WriteIndented = true }));
        OnPropertyChanged(nameof(AccountTypeLabel));
    }

    private async Task LoginMsAsync()
    {
        IsLoggingIn = true; Status = "cyr113";
        try
        {
            _loginHandler = JELoginHandlerBuilder.BuildDefault();
            _session = await _loginHandler.Authenticate();

            var existing = Accounts.FirstOrDefault(a => !a.IsOffline && a.Uuid == _session.UUID);
            Account = new AccountInfo
            {
                Username = _session.Username,
                Uuid = _session.UUID,
                AccessToken = _session.AccessToken,
                AccountType = "msa",
                ExpiresAt = DateTime.MaxValue
            };

            if (existing == null)
            {
                Accounts.Insert(0, Account);
                SaveAccounts();
                Status = $"cyr114";
            }
            else
            {
                existing.Username = Account.Username;
                existing.AccessToken = Account.AccessToken;
                Account = existing;
                SaveAccounts();
                Status = $"cyr115";
            }

            _selectedAccount = Account;
            OnPropertyChanged(nameof(SelectedAccount));
            ApplyAccount(Account);
            if (_friends != null && _discordProfile == null)
                _friends.DisplayName = Account.Username;
            AccountStatus = $"MS: {Account.Username}";
            ShowToast($"cyr116");
        }
        catch (Exception ex) { AccountStatus = $"cyr117"; }
        finally { IsLoggingIn = false; }
    }

    private void LoginOffline()
    {
        LoginOffline(OfflineUsername);
        OfflineUsername = "";
    }

    
    
    
    
    private async Task LoginDiscordAsync()
    {
        IsLoggingIn = true; Status = "cyr118";
        try
        {
            var auth = new DiscordAuthService(_http);
            var user = await auth.LoginAsync();
            if (user == null)
            {
                AccountStatus = "cyr119";
                return;
            }

            
            
            DiscordProfile = user;
            SaveDiscordProfile();
            if (_friends != null) _friends.DisplayName = user.Username;
            StartFriends();
            Status = $"Discord: {user.Username}";
            ShowToast($"cyr120");
        }
        catch (Exception ex)
        {
            AccountStatus = $"cyr121";
        }
        finally { IsLoggingIn = false; }
    }

    private void LoginOffline(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        var name = username.Trim();

        var existing = Accounts.FirstOrDefault(a => a.IsOffline && a.Username.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _selectedAccount = existing;
            OnPropertyChanged(nameof(SelectedAccount));
            ApplyAccount(existing);
            return;
        }

        string uuid = GenerateOfflineUuid(name);
        _session = new MSession
        {
            Username = name,
            UUID = uuid,
            AccessToken = "0",
            UserType = "legacy"
        };
        Account = new AccountInfo
        {
            Username = name,
            Uuid = uuid,
            AccessToken = "0",
            AccountType = "offline",
            ExpiresAt = DateTime.MaxValue
        };
        Accounts.Insert(0, Account);
        SaveAccounts();
        _selectedAccount = Account;
        OnPropertyChanged(nameof(SelectedAccount));
        ApplyAccount(Account);
        AccountStatus = $"cyr122";
        ShowToast($"cyr123");
    }

    private static string GenerateOfflineUuid(string username)
    {
        var data = System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + username);
        var hash = System.Security.Cryptography.MD5.HashData(data);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var uuid = new Guid(hash);
        return uuid.ToString("N");
    }

    
    private async Task RefreshVersionsAsync()
    {
        if (_launcher == null) return;
        Status = "cyr124";
        try
        {
            _allVersions = await _launcher.GetAllVersionsAsync();
            FilterVersions();
            await RefreshInstalledVersions();
            Status = $"cyr125";
        }
        catch (Exception ex) { Status = $"cyr126"; }
    }

    private Task RefreshInstalledVersions()
    {
        InstalledVersionIds.Clear();
        var versionsDir = Path.Combine(_minecraftPath.BasePath, "versions");
        if (Directory.Exists(versionsDir))
            foreach (var dir in Directory.GetDirectories(versionsDir))
            {
                var name = Path.GetFileName(dir);
                if (File.Exists(Path.Combine(dir, $"{name}.jar")) || File.Exists(Path.Combine(dir, $"{name}.json")))
                    InstalledVersionIds.Add(name);
            }
        return Task.CompletedTask;
    }

    private async Task DownloadVersionAsync()
    {
        if (_launcher == null || string.IsNullOrEmpty(SelectedVersionId) || _session == null) return;
        IsBusy = true; DlProgress = 0; Status = $"cyr127";
        try
        {
            var cts = new CancellationTokenSource();
            var fileProgress = new Progress<CmlLib.Core.Installers.InstallerProgressChangedEventArgs>(p =>
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DlFile = p.Name ?? "";
                    ReportProgress(p.TotalTasks > 0 ? (double)p.ProgressedTasks / p.TotalTasks * 100 : 0);
                    Status = $"{p.Name}";
                }));
            var byteProgress = new Progress<CmlLib.Core.ByteProgress>(p => { });

            var launchOption = new MLaunchOption
            {
                Session = _session,
                MaximumRamMb = CurrentProfile?.MaxRamMb ?? 4096,
                MinimumRamMb = CurrentProfile?.MinRamMb ?? 2048
            };

            await Task.Run(async () =>
            {
                await _launcher.InstallAndBuildProcessAsync(SelectedVersionId, launchOption,
                    fileProgress, byteProgress, cts.Token);
            });

            Application.Current.Dispatcher.Invoke(() => { DlProgress = 100; Status = $"cyr128"; });
            ShowToast($"cyr129");
            await RefreshInstalledVersions();
        }
        catch (Exception ex) { Status = $"cyr130"; }
        finally { FinishDownload(); }
    }

    
    private async Task LaunchAsync(ServerEntry? server = null)
    {
        if (_launcher == null || CurrentProfile == null || _session == null) { Status = "cyr131"; return; }
        if (IsGameRunning && !MultipleInstances)
        {
            Status = "cyr132";
            return;
        }

        
        if (_admin != null && _admin.IsBanned(out var banReason, out var banUntil))
        {
            Status = "cyr133";
            ShowToast(banUntil != null
                ? $"cyr134"
                : $"cyr135");
            return;
        }

        IsLaunching = true; IsGameRunning = true;
        ConsoleLines.Add("cyr136");
        Status = server != null
            ? $"cyr137"
            : $"cyr138";

        try
        {
            
            string versionId = CurrentProfile.VersionId;
            if (CurrentProfile.ModLoader == "Fabric" && !string.IsNullOrEmpty(CurrentProfile.ModLoaderVersion))
                versionId = $"fabric-loader-{CurrentProfile.ModLoaderVersion}-{CurrentProfile.VersionId}";
            else if (CurrentProfile.ModLoader == "Forge" && !string.IsNullOrEmpty(CurrentProfile.ModLoaderVersion))
                versionId = $"{CurrentProfile.VersionId}-forge-{CurrentProfile.ModLoaderVersion}";
            else if (CurrentProfile.ModLoader == "OptiFine" && !string.IsNullOrEmpty(CurrentProfile.ModLoaderVersion))
                versionId = $"{CurrentProfile.VersionId}-OptiFine_{CurrentProfile.ModLoaderVersion}";

            
            var extraJvmArgs = new List<MArgument>(MLaunchOption.DefaultExtraJvmArguments);
            if (!string.IsNullOrEmpty(CurrentProfile.JvmArgs))
                extraJvmArgs.Add(MArgument.FromCommandLine(CurrentProfile.JvmArgs));

            
            if (Ipv4Only)
                extraJvmArgs.Add(new MArgument("-Djava.net.preferIPv4Stack=true"));

            
            var gameDir = GetProfileGameDir(CurrentProfile);
            var extraGameArgs = new List<MArgument>();
            if (!string.Equals(gameDir, MinecraftPathHelper.GameDir, StringComparison.OrdinalIgnoreCase))
                extraGameArgs.Add(MArgument.FromCommandLine($"--gameDir \"{gameDir}\""));

            
            EnsureDedMod();
            EnsureSkin();
            EnsureCape();

            var launchOption = new MLaunchOption
            {
                Session = _session,
                MaximumRamMb = CurrentProfile.MaxRamMb,
                MinimumRamMb = CurrentProfile.MinRamMb,
                FullScreen = CurrentProfile.Fullscreen,
                ScreenWidth = CurrentProfile.WindowWidth,
                ScreenHeight = CurrentProfile.WindowHeight,
                JavaPath = !string.IsNullOrEmpty(CurrentProfile.JavaPath) ? CurrentProfile.JavaPath : null,
                ExtraJvmArguments = extraJvmArgs,
                ExtraGameArguments = extraGameArgs,
                ServerIp = server?.Address,
                ServerPort = server?.Port ?? 25565
            };

            var cts = new CancellationTokenSource();
            var launchDoneTasks = 0;
            var launchTotalTasks = 0;
            var launchMaxPct = 0.0;
            var fileProgress = new Progress<CmlLib.Core.Installers.InstallerProgressChangedEventArgs>(p =>
            {
                if (p.TotalTasks > 0) launchTotalTasks = Math.Max(launchTotalTasks, p.TotalTasks);
                if (p.EventType == CmlLib.Core.Installers.InstallerEventType.Done) launchDoneTasks++;

                
                double pct = launchTotalTasks > 0 ? (double)launchDoneTasks / launchTotalTasks * 100 : 1;
                pct = Math.Max(pct, launchMaxPct);
                launchMaxPct = pct;

                IsBusy = true;
                DlFile = p.Name ?? "";
                ReportProgress(Math.Min(pct, 99.5));
                Status = $"cyr139";
            });
            var byteProgress = new Progress<CmlLib.Core.ByteProgress>(p =>
            {
                IsBusy = true;
            });

            Status = $"cyr140";
            IsBusy = true;
            DlProgress = 1;
            DlFile = "cyr141";

            _gameProcess = await Task.Run(async () =>
                await _launcher.InstallAndBuildProcessAsync(versionId, launchOption,
                    fileProgress, byteProgress, cts.Token));

            
            try
            {
                File.AppendAllText(Path.Combine(MinecraftPathHelper.BaseDir, "launcher.log"),
                    $"[{DateTime.Now:HH:mm:ss}] CMD: {_gameProcess.StartInfo.FileName} {_gameProcess.StartInfo.Arguments}{Environment.NewLine}");
            }
            catch { }

            
            _gameProcess.StartInfo.RedirectStandardOutput = true;
            _gameProcess.StartInfo.RedirectStandardError = true;
            _gameProcess.StartInfo.UseShellExecute = false;

            _gameProcess.OutputDataReceived += (s, e) =>
            { if (e.Data != null) Application.Current.Dispatcher.Invoke(() => { ConsoleLines.Add(e.Data); if (ConsoleLines.Count > 500) ConsoleLines.RemoveAt(0); }); };
            _gameProcess.ErrorDataReceived += (s, e) =>
            { if (e.Data != null) Application.Current.Dispatcher.Invoke(() => { ConsoleLines.Add(e.Data); if (ConsoleLines.Count > 500) ConsoleLines.RemoveAt(0); }); };
            _gameProcess.EnableRaisingEvents = true;
            _gameProcess.Exited += (s, e) => Application.Current.Dispatcher.Invoke(() =>
            {
                IsGameRunning = false;
                Status = "cyr142";
                _admin?.PublishStatusAsync("online", "");
            });

            _gameProcess.Start();
            _gameProcess.BeginOutputReadLine();
            _gameProcess.BeginErrorReadLine();

            IsBusy = false;
            DlProgress = 100;
            DlFile = "";

            CurrentProfile.LastPlayed = DateTime.UtcNow;
            SaveLaunchHistory(CurrentProfile.VersionId, CurrentProfile.ModLoader);
            SaveProfile(CurrentProfile);
            Status = server != null ? $"cyr143" : "cyr144";

            
            _admin?.PublishStatusAsync("playing", server?.AddressLabel ?? "");

            if (server != null)
            {
                server.LastPlayed = DateTime.UtcNow;
                SaveServers();
                ResortServers();
            }

            
            switch (PostLaunchAction)
            {
                case "hide":
                    Application.Current.Dispatcher.Invoke(() => Application.Current.MainWindow.WindowState = WindowState.Minimized);
                    break;
                case "close":
                    GameDetached = true;
                    Application.Current.Dispatcher.Invoke(() => Application.Current.MainWindow.Close());
                    break;
            }
        }
        catch (Exception ex)
        {
            Status = $"cyr145";
            try { File.AppendAllText(Path.Combine(MinecraftPathHelper.BaseDir, "launcher.log"), $"[{DateTime.Now:HH:mm:ss}] EXCEPTION: {ex}{Environment.NewLine}"); } catch { }
            IsGameRunning = false;
            _admin?.PublishStatusAsync("online", "");
        }
        finally { IsLaunching = false; }
    }

    public void StopGame() { if (_gameProcess != null && !_gameProcess.HasExited) { _gameProcess.Kill(); _gameProcess = null; } }

    

    
    
    private void EnsureDedMod()
    {
        try
        {
            if (CurrentProfile == null || !ModService.IsDedModCompatible(CurrentProfile.VersionId))
                return;

            var modsDir = _mods.GetModsDir("");

            
            foreach (var csl in Directory.GetFiles(modsDir, "*CustomSkinLoader*.jar"))
            {
                try { File.Delete(csl); } catch { }
            }

            var jarName = $"ded-mod-1.0.0-{CurrentProfile.VersionId}.jar";
            var src = Path.Combine(AppContext.BaseDirectory, "Assets", jarName);
            if (!File.Exists(src))
                src = Path.Combine(AppContext.BaseDirectory, "Assets", "ded-mod-1.0.0.jar");
            if (!File.Exists(src)) return;

            var dest = Path.Combine(modsDir, jarName);
            if (!File.Exists(dest) || new FileInfo(src).Length != new FileInfo(dest).Length)
                File.Copy(src, dest, true);
        }
        catch { }
    }

    
    private void EnsureSkin()
    {
        try
        {
            var skinFile = !string.IsNullOrEmpty(Account.SkinPath) && File.Exists(Account.SkinPath)
                ? Account.SkinPath
                : _skins.GetSkinPath(Account.Username);
            if (string.IsNullOrEmpty(skinFile)) return;

            var configDir = Path.Combine(GetProfileGameDir(CurrentProfile!), "config");
            Directory.CreateDirectory(configDir);
            var prepared = _skins.PrepareSkin(skinFile);
            File.Copy(prepared, Path.Combine(configDir, "skin.png"), true);
        }
        catch { }
    }

    
    private void EnsureCape()
    {
        try
        {
            var cape = _skins.GetCapePath(Account.Username);
            if (cape == null) return;
            var configDir = Path.Combine(GetProfileGameDir(CurrentProfile!), "config");
            Directory.CreateDirectory(configDir);
            File.Copy(cape, Path.Combine(configDir, "cape.png"), true);
        }
        catch { }
    }

    
    private async Task LoadJavaAsync()
    {
        try { var list = await _java.FindJavaInstallationsAsync(); JavaInstallations.Clear(); foreach (var j in list) JavaInstallations.Add(j); } catch { }
    }

    
    private async Task LoadProfilesAsync()
    {
        Profiles.Clear();
        var dir = MinecraftPathHelper.ProfilesDir;
        if (!Directory.Exists(dir)) { AddDefaultProfile(); return; }
        foreach (var f in Directory.GetFiles(dir, "*.json"))
        {
            try { var p = JsonSerializer.Deserialize<LaunchProfile>(await File.ReadAllTextAsync(f)); if (p != null) Profiles.Add(p); } catch { }
        }
        if (Profiles.Count == 0) AddDefaultProfile();

        
        
        var ordered = Profiles.OrderBy(p => p.CreatedAt).ToList();
        bool first = true;
        foreach (var p in ordered)
        {
            if (first) { first = false; continue; }
            if (string.IsNullOrEmpty(p.GameDir))
            {
                p.GameDir = Path.Combine(MinecraftPathHelper.BaseDir, "profiles", p.Id, "game");
                SaveProfile(p);
            }
        }

        CurrentProfile ??= Profiles.FirstOrDefault();
    }

    private void AddDefaultProfile()
    {
        var p = new LaunchProfile { Name = "cyr146", VersionId = "1.21.1", MaxRamMb = 3072, MinRamMb = 2048 };
        Profiles.Add(p); SaveProfile(p); CurrentProfile = p;
    }

    private string _profileNameInput = "";
    public string ProfileNameInput { get => _profileNameInput; set => SetProperty(ref _profileNameInput, value); }

    private void CreateProfile()
    {
        var name = ProfileNameInput.Trim();
        if (string.IsNullOrEmpty(name)) { Status = "cyr147"; return; }
        if (Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { Status = "cyr148"; return; }

        var p = new LaunchProfile { Name = name, VersionId = SelectedVersionId ?? "1.21.1", MaxRamMb = 4096, MinRamMb = 2048 };
        
        p.GameDir = Path.Combine(MinecraftPathHelper.BaseDir, "profiles", p.Id, "game");
        Profiles.Add(p); SaveProfile(p); CurrentProfile = p;
        ProfileNameInput = "";
        CopyBaseAssetsToProfile(p);
        LoadMods();
        Status = $"cyr149";
        ShowToast("cyr150");
    }

    
    
    
    
    public void CopyModsToCurrentProfile()
    {
        if (CurrentProfile == null) return;
        CopyBaseAssetsToProfile(CurrentProfile);
        LoadMods();
        LoadInstalledResourcePacks();
        LoadInstalledShaders();
        ShowToast("cyr151");
    }

    private void CopyBaseAssetsToProfile(LaunchProfile p)
    {
        try
        {
            var dst = GetProfileGameDir(p);
            if (string.Equals(Path.GetFullPath(dst), Path.GetFullPath(MinecraftPathHelper.GameDir),
                    StringComparison.OrdinalIgnoreCase)) return; 

            var copied = 0;
            foreach (var sub in new[] { "mods", "resourcepacks", "shaderpacks" })
            {
                var srcDir = Path.Combine(MinecraftPathHelper.GameDir, sub);
                if (!Directory.Exists(srcDir)) continue;
                var dstDir = Path.Combine(dst, sub);
                Directory.CreateDirectory(dstDir);
                foreach (var f in Directory.GetFiles(srcDir))
                {
                    var dest = Path.Combine(dstDir, Path.GetFileName(f));
                    if (!File.Exists(dest))
                    {
                        File.Copy(f, dest, true);
                        copied++;
                    }
                }
            }
            if (copied > 0) Status = $"cyr152";
        }
        catch { }
    }

    private void RenameCurrentProfile()
    {
        if (CurrentProfile == null) return;
        var name = ProfileNameInput.Trim();
        if (string.IsNullOrEmpty(name)) { Status = "cyr153"; return; }
        if (Profiles.Any(p => p != CurrentProfile && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { Status = "cyr154"; return; }

        var oldFile = Path.Combine(MinecraftPathHelper.ProfilesDir, $"{CurrentProfile.Name}.json");
        if (File.Exists(oldFile)) { try { File.Delete(oldFile); } catch { } }

        CurrentProfile.Name = name;
        SaveProfile(CurrentProfile);

        
        var idx = Profiles.IndexOf(CurrentProfile);
        if (idx >= 0) Profiles[idx] = CurrentProfile;

        ProfileNameInput = "";
        Status = $"cyr155";
        ShowToast("cyr156");
    }

    private void SaveCurrentProfile() { if (CurrentProfile != null) { SaveProfile(CurrentProfile); Status = "cyr157"; } }

    private void DeleteCurrentProfile()
    {
        if (CurrentProfile == null || Profiles.Count <= 1) return;
        var r = MessageBox.Show($"cyr158", "cyr159", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        var path = Path.Combine(MinecraftPathHelper.ProfilesDir, $"{CurrentProfile.Name}.json");
        if (File.Exists(path)) File.Delete(path);
        Profiles.Remove(CurrentProfile);
        CurrentProfile = Profiles.FirstOrDefault();
    }

    public void SaveProfile(LaunchProfile p)
    {
        Directory.CreateDirectory(MinecraftPathHelper.ProfilesDir);
        File.WriteAllText(Path.Combine(MinecraftPathHelper.ProfilesDir, $"{p.Name}.json"),
            JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
    }

    
    public void ImportMods(string[] files) { if (CurrentProfile != null) { foreach (var f in files) _mods.InstallMod(f, CurrentProfile.Id); LoadMods(); } }
    public void RemoveMod(ModInfo mod) { _mods.RemoveMod(mod.FilePath); InstalledMods.Remove(mod); UpdateActiveMods(); OnPropertyChanged(nameof(ModsCount)); ShowToast("cyr160"); }
    public void RefreshInstalledMods() => LoadMods();

    
    
    
    
    public void SetModEnabled(ModInfo mod, bool enabled)
    {
        try
        {
            var newPath = _mods.SetModEnabled(mod, enabled);
            if (newPath != null) mod.FilePath = newPath;
            mod.Enabled = enabled;
            UpdateActiveMods();
            OnPropertyChanged(nameof(ModsCount));
            Status = enabled
                ? $"cyr161"
                : $"cyr162";
            ShowToast(enabled ? "cyr163" : "cyr164");
        }
        catch (Exception ex) { Status = $"cyr165"; }
    }

    

    
    
    
    
    
    public void InstallDroppedFiles(string[] paths)
    {
        if (CurrentProfile == null) { Status = "cyr166"; return; }
        var installed = new List<string>();

        foreach (var path in paths)
        {
            try
            {
                var name = Path.GetFileName(path);

                
                if (Directory.Exists(path))
                {
                    var dirTarget = GetDirectoryTarget(path);
                    if (dirTarget == null) { Status = $"cyr167"; continue; }
                    var dirDest = Path.Combine(dirTarget, name);
                    if (Directory.Exists(dirDest)) { Status = $"cyr168"; continue; }
                    CopyDirectoryRecursive(path, dirDest);
                    installed.Add($"cyr169");
                    continue;
                }

                var ext = Path.GetExtension(path).ToLower();

                
                if (ext == ".png")
                {
                    var (valid, w, h) = _skins.ValidateSkin(path);
                    if (!valid) { Status = $"cyr170"; continue; }
                    var prepared = _skins.PrepareSkin(path);
                    var savedPath = _skins.SaveSkin(prepared, Account.Username);
                    Account.SkinPath = savedPath;
                    SaveAccount();
                    LoadSkinPreview(savedPath);
                    SkinStatus = $"cyr171";
                    installed.Add($"cyr172");
                    continue;
                }

                if (ext is not (".jar" or ".zip")) { Status = $"cyr173"; continue; }

                var kind = ClassifyArchive(path, ext);
                var destFolder = kind switch
                {
                    "resourcepack" => _mods.GetResourcePacksDir(),
                    "shader" => _mods.GetShadersDir(),
                    _ => _mods.GetModsDir(CurrentProfile.Id)
                };
                var kindLabel = kind switch
                {
                    "resourcepack" => "cyr174",
                    "shader" => "cyr175",
                    _ => "cyr176"
                };

                var dest = Path.Combine(destFolder, name);
                if (File.Exists(dest)) { Status = $"cyr177"; continue; }
                File.Copy(path, dest);
                installed.Add($"{name} → {kindLabel}");
            }
            catch (Exception ex) { Status = $"cyr178"; }
        }

        LoadMods();
        LoadInstalledResourcePacks();
        LoadInstalledShaders();

        Status = installed.Count > 0
            ? $"cyr179"
            : "cyr180";
        if (installed.Count > 0)
            ShowToast($"cyr181");
    }

    private string? GetDirectoryTarget(string dir)
    {
        if (File.Exists(Path.Combine(dir, "pack.mcmeta"))) return _mods.GetResourcePacksDir();
        if (Directory.Exists(Path.Combine(dir, "shaders"))) return _mods.GetShadersDir();
        return null;
    }

    private static string ClassifyArchive(string path, string ext)
    {
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(path);
            if (zip.GetEntry("fabric.mod.json") != null ||
                zip.GetEntry("META-INF/mods.toml") != null ||
                zip.GetEntry("mcmod.info") != null)
                return "mod";
            if (zip.GetEntry("pack.mcmeta") != null) return "resourcepack";
            if (zip.Entries.Any(e => e.FullName.StartsWith("shaders/", StringComparison.OrdinalIgnoreCase)))
                return "shader";
        }
        catch { }
        return ext == ".jar" ? "mod" : "resourcepack";
    }

    private static void CopyDirectoryRecursive(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            var target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private void LoadMods()
    {
        if (CurrentProfile == null)
        {
            InstalledMods.Clear();
            UpdateActiveMods();
            OnPropertyChanged(nameof(ModsCount));
            return;
        }
        var profileId = CurrentProfile.Id;
        
        
        _ = Task.Run(() => _mods.GetInstalledMods(profileId)).ContinueWith(t =>
        {
            List<ModInfo> mods;
            try { mods = t.Result; } catch { mods = new(); }
            Application.Current.Dispatcher.Invoke(() =>
            {
                InstalledMods.Clear();
                foreach (var m in mods) InstalledMods.Add(m);
                UpdateActiveMods();
                OnPropertyChanged(nameof(ModsCount));
                OnPropertyChanged(nameof(OptimizationInstalled));
                OnPropertyChanged(nameof(OptimizationButtonText));
            });
        });
    }

    private void UpdateActiveMods()
    {
        ActiveMods.Clear();
        foreach (var m in InstalledMods.Where(m => m.Enabled).Take(5)) ActiveMods.Add(m);
    }

    public async Task SearchModsAsync()
    {
        if (string.IsNullOrWhiteSpace(ModSearchQuery) || CurrentProfile == null)
        {
            Status = "cyr182";
            return;
        }
        IsBusy = true; Status = $"cyr183";
        try
        {
            if (ModSource == "CurseForge")
            {
                var results = await _mods.SearchCurseForgeAsync(ModSearchQuery, CurrentProfile.VersionId);
                _allCfMods = results;
                _modCfOffset = 0;
                CurseForgeResults.Clear();
                foreach (var r in results.Take(CfChunkSize)) CurseForgeResults.Add(r);
                _modCfOffset = CurseForgeResults.Count;
                OnPropertyChanged(nameof(HasMoreCfMods));
                _ = LoadCurseForgeIconsAsync(CurseForgeResults.ToList());
                Status = CurseForgeResults.Count > 0
                    ? $"cyr184"
                    : $"cyr185";
            }
            else
            {
                var loader = CurrentProfile.ModLoader == "Vanilla" ? "" : CurrentProfile.ModLoader.ToLower();
                var (results, total) = await _mods.SearchModrinthPageAsync(ModSearchQuery, "mod", CurrentProfile.VersionId, loader, 30, 0);
                ModrinthResults.Clear();
                foreach (var r in results) ModrinthResults.Add(r);
                _modOffset = results.Count; _modTotalHits = total;
                OnPropertyChanged(nameof(HasMoreMods));
                _ = LoadModrinthIconsAsync(ModrinthResults.ToList());
                Status = ModrinthResults.Count > 0
                    ? $"cyr186"
                    : $"cyr187";
            }
        }
        catch (Exception ex) { Status = $"cyr188"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    
    
    
    public async Task LoadMoreModsAsync()
    {
        if (IsBusy || ModSource != "Modrinth" || !HasMoreMods) return;
        IsBusy = true;
        try
        {
            var loader = CurrentProfile?.ModLoader == "Vanilla" ? "" : CurrentProfile?.ModLoader.ToLower() ?? "";
            var (results, total) = await _mods.SearchModrinthPageAsync(ModSearchQuery, "mod", CurrentProfile?.VersionId ?? "", loader, 30, _modOffset);
            foreach (var r in results) ModrinthResults.Add(r);
            _modOffset += results.Count; _modTotalHits = total;
            OnPropertyChanged(nameof(HasMoreMods));
            _ = LoadModrinthIconsAsync(results);
            Status = $"cyr189";
        }
        catch { }
        finally { IsBusy = false; DlFile = ""; }
    }

    private async Task LoadRecommendedModsAsync()
    {
        if (ModrinthResults.Count > 0 || CurseForgeResults.Count > 0) return;
        IsBusy = true; Status = "cyr190";
        try
        {
            var (results, total) = await _mods.SearchModrinthPageAsync("", "mod", "", "", 24, 0);
            ModrinthResults.Clear();
            foreach (var r in results) ModrinthResults.Add(r);
            _modOffset = results.Count; _modTotalHits = total;
            OnPropertyChanged(nameof(HasMoreMods));
            _ = LoadModrinthIconsAsync(ModrinthResults.ToList());
            Status = ModrinthResults.Count > 0
                ? $"cyr191"
                : "cyr192";
        }
        catch (Exception ex) { Status = $"cyr193"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    public async Task InstallCurseForgeModAsync(CurseForgeMod mod)
    {
        if (CurrentProfile == null) return;
        IsBusy = true; Status = $"cyr194";
        try
        {
            var files = await _mods.GetCurseForgeFilesAsync(mod.Id);
            var file = files.FirstOrDefault(f => f.GameVersions.Contains(CurrentProfile.VersionId)) ?? files.FirstOrDefault();
            if (file == null) { Status = "cyr195"; return; }

            var progress = new Progress<DownloadProgress>(p =>
                Application.Current.Dispatcher.Invoke(() => { DlFile = p.FileName; ReportProgress(p.Percentage); }));
            await _mods.DownloadCurseForgeFileAsync(file, CurrentProfile.Id, progress);
            Status = $"cyr196";
            ShowToast("cyr197");
            LoadMods();
        }
        catch (Exception ex) { Status = $"cyr198"; }
        finally { FinishDownload(); }
    }

    public void SetModSource(string source)
    {
        if (ModSource == source) return;
        ModSource = source;
        if (source == "CurseForge" && CurseForgeResults.Count == 0)
            _ = LoadCurseForgeModsAsync();
        else if (source == "Modrinth" && ModrinthResults.Count == 0)
            _ = LoadRecommendedModsAsync();
    }

    private async Task LoadCurseForgeModsAsync()
    {
        IsBusy = true; Status = "cyr199";
        try
        {
            var results = await _mods.SearchCurseForgeAsync("", CurrentProfile?.VersionId ?? "");
            _allCfMods = results;
            _modCfOffset = 0;
            CurseForgeResults.Clear();
            foreach (var r in results.Take(CfChunkSize)) CurseForgeResults.Add(r);
            _modCfOffset = CurseForgeResults.Count;
            OnPropertyChanged(nameof(HasMoreCfMods));
            _ = LoadCurseForgeIconsAsync(CurseForgeResults.ToList());
            Status = CurseForgeResults.Count > 0
                ? $"cyr200"
                : "cyr201";
        }
        catch (Exception ex) { Status = $"cyr202"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    
    
    
    
    public async Task LoadMoreCurseForgeModsAsync()
    {
        if (IsBusy || !HasMoreCfMods) return;
        try
        {
            var chunk = _allCfMods.Skip(_modCfOffset).Take(CfChunkSize).ToList();
            foreach (var r in chunk) CurseForgeResults.Add(r);
            _modCfOffset += chunk.Count;
            OnPropertyChanged(nameof(HasMoreCfMods));
            _ = LoadCurseForgeIconsAsync(chunk);
            Status = $"cyr203";
        }
        catch { }
    }

    public void OpenModsFolder()
    {
        if (CurrentProfile == null) return;
        var dir = _mods.GetModsDir(CurrentProfile.Id);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{dir}\"", UseShellExecute = true });
            Status = "cyr204";
        }
        catch (Exception ex) { Status = $"cyr205"; }
    }

    public void RunDiagnostics()
    {
        Log("cyr206");
        Log($"cyr207");
        Log($"cyr208");
        Log($"cyr209");
        Log($"cyr210");
        Log($"cyr211");
        foreach (var j in JavaInstallations.Take(5))
            Log($"  Java: {j.Version} ({j.Vendor})");
        Log($"cyr212");
        foreach (var v in InstalledVersionIds.Take(10))
            Log($"cyr213");
        Log($"cyr214");
        Log($"cyr215");
        Log("cyr216");
    }

    private static System.Threading.SemaphoreSlim? _iconGate;
    private static System.Threading.SemaphoreSlim IconGate =>
        _iconGate ??= new System.Threading.SemaphoreSlim(PerfSettings.IconParallelism);

    private async Task LoadModrinthIconsAsync(List<ModrinthMod> mods)
    {
        await Parallel.ForEachAsync(mods, async (mod, _) =>
        {
            if (string.IsNullOrEmpty(mod.IconUrl)) return;
            await IconGate.WaitAsync();
            try
            {
                var icon = await _mods.LoadIconAsync(mod.IconUrl);
                if (icon != null)
                    Application.Current.Dispatcher.Invoke(() => mod.Icon = icon);
            }
            catch { }
            finally { IconGate.Release(); }
        });
    }

    private async Task LoadCurseForgeIconsAsync(List<CurseForgeMod> mods)
    {
        await Parallel.ForEachAsync(mods, async (mod, _) =>
        {
            if (string.IsNullOrEmpty(mod.ThumbnailUrl)) return;
            await IconGate.WaitAsync();
            try
            {
                var icon = await _mods.LoadIconAsync(mod.ThumbnailUrl);
                if (icon != null)
                    Application.Current.Dispatcher.Invoke(() => mod.Icon = icon);
            }
            catch { }
            finally { IconGate.Release(); }
        });
    }

    public void SearchCategory(string category)
    {
        switch (category)
        {
            case "cyr217": ModSearchQuery = ""; _ = (ModSource == "CurseForge" ? LoadCurseForgeModsAsync() : LoadRecommendedModsAsync()); break;
            case "cyr218": ModSearchQuery = "optimization"; break;
            case "cyr219": ModSearchQuery = "tech"; break;
            case "cyr220": ModSearchQuery = "magic"; break;
            case "cyr221": ModSearchQuery = "ui hud"; break;
        }
        if (category != "cyr222")
            _ = SearchModsAsync();
    }

    

    public void SetRpSource(string source)
    {
        if (RpSource == source) return;
        RpSource = source;
        if (source == "CurseForge" && ResourcePackCfResults.Count == 0)
            _ = LoadCurseForgeResourcePacksAsync();
        else if (source == "Modrinth" && ResourcePackResults.Count == 0)
            _ = LoadRecommendedResourcePacksAsync();
    }

    public void SetShaderSource(string source)
    {
        if (ShaderSource == source) return;
        ShaderSource = source;
        if (source == "CurseForge" && ShaderCfResults.Count == 0)
            _ = LoadCurseForgeShadersAsync();
        else if (source == "Modrinth" && ShaderResults.Count == 0)
            _ = LoadRecommendedShadersAsync();
    }

    private async Task LoadRecommendedResourcePacksAsync()
    {
        IsBusy = true; Status = "cyr223";
        try
        {
            var (results, total) = await _mods.SearchModrinthPageAsync("", "resourcepack", "", "", 24, 0);
            ResourcePackResults.Clear();
            foreach (var r in results) ResourcePackResults.Add(r);
            _rpOffset = results.Count; _rpTotalHits = total;
            OnPropertyChanged(nameof(HasMoreRps));
            _ = LoadModrinthIconsAsync(ResourcePackResults.ToList());
            Status = $"cyr224";
        }
        catch (Exception ex) { Status = $"cyr225"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    private async Task LoadRecommendedShadersAsync()
    {
        IsBusy = true; Status = "cyr226";
        try
        {
            var (results, total) = await _mods.SearchModrinthPageAsync("", "shader", "", "", 24, 0);
            ShaderResults.Clear();
            foreach (var r in results) ShaderResults.Add(r);
            _shaderOffset = results.Count; _shaderTotalHits = total;
            OnPropertyChanged(nameof(HasMoreShaders));
            _ = LoadModrinthIconsAsync(ShaderResults.ToList());
            Status = $"cyr227";
        }
        catch (Exception ex) { Status = $"cyr228"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    private async Task LoadCurseForgeResourcePacksAsync()
    {
        IsBusy = true; Status = "cyr229";
        try
        {
            var results = await _mods.SearchCurseForgeByCategoryAsync("", ModService.CfCategoryResourcePacks);
            _allCfResourcePacks = results;
            ResourcePackCfResults.Clear();
            foreach (var r in results.Take(CfChunkSize)) ResourcePackCfResults.Add(r);
            _rpCfOffset = ResourcePackCfResults.Count;
            OnPropertyChanged(nameof(HasMoreCfResourcePacks));
            _ = LoadCurseForgeIconsAsync(ResourcePackCfResults.ToList());
            Status = $"cyr230";
        }
        catch (Exception ex) { Status = $"cyr231"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    public async Task LoadMoreCurseForgeResourcePacksAsync()
    {
        if (IsBusy || !HasMoreCfResourcePacks) return;
        IsBusy = true;
        try
        {
            var chunk = _allCfResourcePacks.Skip(_rpCfOffset).Take(CfChunkSize).ToList();
            foreach (var r in chunk) ResourcePackCfResults.Add(r);
            _rpCfOffset += chunk.Count;
            OnPropertyChanged(nameof(HasMoreCfResourcePacks));
            _ = LoadCurseForgeIconsAsync(chunk);
            Status = $"cyr232";
        }
        catch { }
        finally { IsBusy = false; DlFile = ""; }
    }

    private async Task LoadCurseForgeShadersAsync()
    {
        IsBusy = true; Status = "cyr233";
        try
        {
            var results = await _mods.SearchCurseForgeByCategoryAsync("", ModService.CfCategoryShaders);
            _allCfShaders = results;
            ShaderCfResults.Clear();
            foreach (var r in results.Take(CfChunkSize)) ShaderCfResults.Add(r);
            _shaderCfOffset = ShaderCfResults.Count;
            OnPropertyChanged(nameof(HasMoreCfShaders));
            _ = LoadCurseForgeIconsAsync(ShaderCfResults.ToList());
            Status = $"cyr234";
        }
        catch (Exception ex) { Status = $"cyr235"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    public async Task LoadMoreCurseForgeShadersAsync()
    {
        if (IsBusy || !HasMoreCfShaders) return;
        IsBusy = true;
        try
        {
            var chunk = _allCfShaders.Skip(_shaderCfOffset).Take(CfChunkSize).ToList();
            foreach (var r in chunk) ShaderCfResults.Add(r);
            _shaderCfOffset += chunk.Count;
            OnPropertyChanged(nameof(HasMoreCfShaders));
            _ = LoadCurseForgeIconsAsync(chunk);
            Status = $"cyr236";
        }
        catch { }
        finally { IsBusy = false; DlFile = ""; }
    }

    public async Task SearchResourcePacksAsync()
    {
        if (string.IsNullOrWhiteSpace(ModSearchQuery)) return;
        IsBusy = true; Status = $"cyr237";
        try
        {
            if (RpSource == "CurseForge")
            {
                var results = await _mods.SearchCurseForgeByCategoryAsync(ModSearchQuery, ModService.CfCategoryResourcePacks);
                ResourcePackCfResults.Clear();
                foreach (var r in results) ResourcePackCfResults.Add(r);
                _ = LoadCurseForgeIconsAsync(ResourcePackCfResults.ToList());
                Status = $"cyr238";
            }
            else
            {
                var (results, total) = await _mods.SearchModrinthPageAsync(ModSearchQuery, "resourcepack", "", "", 30, 0);
                ResourcePackResults.Clear();
                foreach (var r in results) ResourcePackResults.Add(r);
                _rpOffset = results.Count; _rpTotalHits = total;
                OnPropertyChanged(nameof(HasMoreRps));
                _ = LoadModrinthIconsAsync(ResourcePackResults.ToList());
                Status = $"cyr239";
            }
        }
        catch (Exception ex) { Status = $"cyr240"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    public async Task LoadMoreResourcePacksAsync()
    {
        if (IsBusy || !HasMoreRps) return;
        IsBusy = true;
        try
        {
            var (results, total) = await _mods.SearchModrinthPageAsync(ModSearchQuery, "resourcepack", "", "", 30, _rpOffset);
            foreach (var r in results) ResourcePackResults.Add(r);
            _rpOffset += results.Count; _rpTotalHits = total;
            OnPropertyChanged(nameof(HasMoreRps));
            _ = LoadModrinthIconsAsync(results);
            Status = $"cyr241";
        }
        catch { }
        finally { IsBusy = false; DlFile = ""; }
    }

    public async Task SearchShadersAsync()
    {
        if (string.IsNullOrWhiteSpace(ModSearchQuery)) return;
        IsBusy = true; Status = $"cyr242";
        try
        {
            if (ShaderSource == "CurseForge")
            {
                var results = await _mods.SearchCurseForgeByCategoryAsync(ModSearchQuery, ModService.CfCategoryShaders);
                ShaderCfResults.Clear();
                foreach (var r in results) ShaderCfResults.Add(r);
                _ = LoadCurseForgeIconsAsync(ShaderCfResults.ToList());
                Status = $"cyr243";
            }
            else
            {
                var (results, total) = await _mods.SearchModrinthPageAsync(ModSearchQuery, "shader", "", "", 30, 0);
                ShaderResults.Clear();
                foreach (var r in results) ShaderResults.Add(r);
                _shaderOffset = results.Count; _shaderTotalHits = total;
                OnPropertyChanged(nameof(HasMoreShaders));
                _ = LoadModrinthIconsAsync(ShaderResults.ToList());
                Status = $"cyr244";
            }
        }
        catch (Exception ex) { Status = $"cyr245"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    public async Task LoadMoreShadersAsync()
    {
        if (IsBusy || !HasMoreShaders) return;
        IsBusy = true;
        try
        {
            var (results, total) = await _mods.SearchModrinthPageAsync(ModSearchQuery, "shader", "", "", 30, _shaderOffset);
            foreach (var r in results) ShaderResults.Add(r);
            _shaderOffset += results.Count; _shaderTotalHits = total;
            OnPropertyChanged(nameof(HasMoreShaders));
            _ = LoadModrinthIconsAsync(results);
            Status = $"cyr246";
        }
        catch { }
        finally { IsBusy = false; DlFile = ""; }
    }

    public async Task InstallResourcePackAsync(ModrinthMod item)
    {
        if (CurrentProfile == null) return;
        IsBusy = true; Status = $"cyr247";
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
                Application.Current.Dispatcher.Invoke(() => { DlFile = p.FileName; ReportProgress(p.Percentage); }));
            await _mods.DownloadModrinthToFolderAsync(item, CurrentProfile.VersionId, "minecraft",
                _mods.GetResourcePacksDir(), progress);
            Status = $"cyr248";
            ShowToast("cyr249");
            LoadInstalledResourcePacks();
        }
        catch (Exception ex) { Status = $"cyr250"; }
        finally { FinishDownload(); }
    }

    public async Task InstallCurseForgeResourcePackAsync(CurseForgeMod mod)
    {
        IsBusy = true; Status = $"cyr251";
        try
        {
            var files = await _mods.GetCurseForgeFilesAsync(mod.Id);
            var file = files.FirstOrDefault(f => f.GameVersions.Contains(CurrentProfile?.VersionId ?? "")) ?? files.FirstOrDefault();
            if (file == null) { Status = "cyr252"; return; }
            var progress = new Progress<DownloadProgress>(p =>
                Application.Current.Dispatcher.Invoke(() => { DlFile = p.FileName; ReportProgress(p.Percentage); }));
            await _mods.DownloadCurseForgeFileToFolderAsync(file, _mods.GetResourcePacksDir(), progress);
            Status = $"cyr253";
            ShowToast("cyr254");
            LoadInstalledResourcePacks();
        }
        catch (Exception ex) { Status = $"cyr255"; }
        finally { FinishDownload(); }
    }

    public async Task InstallShaderAsync(ModrinthMod item)
    {
        if (CurrentProfile == null) return;
        IsBusy = true; Status = $"cyr256";
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
                Application.Current.Dispatcher.Invoke(() => { DlFile = p.FileName; ReportProgress(p.Percentage); }));
            await _mods.DownloadModrinthToFolderAsync(item, CurrentProfile.VersionId, "iris",
                _mods.GetShadersDir(), progress);
            Status = $"cyr257";
            ShowToast("cyr258");
            LoadInstalledShaders();
        }
        catch (Exception ex) { Status = $"cyr259"; }
        finally { FinishDownload(); }
    }

    public async Task InstallCurseForgeShaderAsync(CurseForgeMod mod)
    {
        IsBusy = true; Status = $"cyr260";
        try
        {
            var files = await _mods.GetCurseForgeFilesAsync(mod.Id);
            var file = files.FirstOrDefault(f => f.GameVersions.Contains(CurrentProfile?.VersionId ?? "")) ?? files.FirstOrDefault();
            if (file == null) { Status = "cyr261"; return; }
            var progress = new Progress<DownloadProgress>(p =>
                Application.Current.Dispatcher.Invoke(() => { DlFile = p.FileName; ReportProgress(p.Percentage); }));
            await _mods.DownloadCurseForgeFileToFolderAsync(file, _mods.GetShadersDir(), progress);
            Status = $"cyr262";
            ShowToast("cyr263");
            LoadInstalledShaders();
        }
        catch (Exception ex) { Status = $"cyr264"; }
        finally { FinishDownload(); }
    }

    

    public void LoadScreenshots()
    {
        Screenshots.Clear();
        try
        {
            var dir = Path.Combine(_mods.GameDir, "screenshots");
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.GetFiles(dir, "*.png")
                         .OrderByDescending(f => File.GetLastWriteTime(f)).Take(60))
            {
                var item = new ScreenshotItem
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    TakenAt = File.GetLastWriteTime(file)
                };
                Screenshots.Add(item);
                _ = LoadScreenshotThumbnailAsync(item);
            }
        }
        catch { }
    }

    
    private async Task LoadScreenshotThumbnailAsync(ScreenshotItem item)
    {
        try
        {
            var bmp = await Task.Run(() =>
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 320;
                image.UriSource = new Uri(item.FilePath);
                image.EndInit();
                image.Freeze();
                return image;
            });
            item.Thumbnail = bmp;
        }
        catch { }
    }

    public void OpenScreenshot(ScreenshotItem item)
    {
        try
        {
            Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
        }
        catch { }
    }

    public void OpenScreenshotsFolder()
    {
        try
        {
            var dir = Path.Combine(MinecraftPathHelper.GameDir, "screenshots");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{dir}\"", UseShellExecute = true });
        }
        catch { }
    }

    public async Task InstallModrinthByIndexAsync(int idx)
    {
        if (idx < 0 || idx >= ModrinthResults.Count || CurrentProfile == null) return;
        var mod = ModrinthResults[idx];
        await InstallModrinthAutoAsync(mod);
    }

    private string ProfileLoader()
    {
        if (CurrentProfile == null) return "fabric";
        return CurrentProfile.ModLoader == "Vanilla" ? "fabric" : CurrentProfile.ModLoader.ToLower();
    }

    
    
    
    
    private async Task InstallModrinthAutoAsync(ModrinthMod mod)
    {
        if (CurrentProfile == null) return;
        var loader = ProfileLoader();

        IsBusy = true;
        Status = $"cyr265";
        List<ModrinthVersion> filtered;
        try
        {
            filtered = await _mods.GetModrinthVersionsAsync(mod.ProjectId, CurrentProfile.VersionId, loader);
        }
        catch { filtered = new(); }
        finally { IsBusy = false; DlFile = ""; }

        if (filtered.Count > 0)
        {
            
            await DownloadModVersionAsync(mod, filtered[0]);
            return;
        }

        
        await ShowModVersionsAsync(mod);
    }

    
    public async Task ShowModVersionsAsync(ModrinthMod mod)
    {
        if (CurrentProfile == null) return;
        var loader = ProfileLoader();

        IsBusy = true;
        Status = $"cyr266";
        List<ModrinthVersion> filtered;
        List<ModrinthVersion> all;
        try
        {
            filtered = await _mods.GetModrinthVersionsAsync(mod.ProjectId, CurrentProfile.VersionId, loader);
            all = filtered.Count > 0
                ? filtered
                : await _mods.GetModrinthVersionsAsync(mod.ProjectId, "", "");
        }
        catch { filtered = new(); all = new(); }
        finally { IsBusy = false; DlFile = ""; }

        if (all.Count == 0)
        {
            Status = "cyr267";
            ShowToast("cyr268");
            return;
        }

        ModrinthVersion? recommended = filtered.Count > 0 ? filtered[0] : null;
        var choices = all.Select(v => new VersionPickerWindow.Choice
        {
            Title = v.VersionNumber,
            Sub = (v.GameVersions.Count > 0
                    ? "MC " + string.Join(", ", v.GameVersions.Take(3))
                    : "cyr269")
                  + (v.Loaders.Count > 0 ? "  ·  " + string.Join("/", v.Loaders) : ""),
            Recommended = v == recommended,
            Tag = v
        }).ToList();
        int recIdx = choices.FindIndex(c => c.Recommended);

        var picked = VersionPickerWindow.ShowPick(
            $"cyr270",
            "cyr271",
            choices, recIdx);
        if (picked?.Tag is not ModrinthVersion version) return;

        await DownloadModVersionAsync(mod, version);
    }

    
    public async Task ShowCurseForgeVersionsAsync(CurseForgeMod mod)
    {
        if (CurrentProfile == null) return;

        IsBusy = true;
        Status = $"cyr272";
        List<CurseForgeFile> files;
        try
        {
            files = await _mods.GetCurseForgeFilesAsync(mod.Id);
        }
        catch { files = new(); }
        finally { IsBusy = false; DlFile = ""; }

        if (files.Count == 0)
        {
            Status = "cyr273";
            ShowToast("cyr274");
            return;
        }

        var profileVersion = CurrentProfile.VersionId;
        var choices = files.Select(f => new VersionPickerWindow.Choice
        {
            Title = f.DisplayName,
            Sub = f.GameVersions.Count > 0
                ? "MC " + string.Join(", ", f.GameVersions.Take(4))
                : f.FileName,
            Recommended = f.GameVersions.Contains(profileVersion),
            Tag = f
        }).ToList();
        int recIdx = choices.FindIndex(c => c.Recommended);

        var picked = VersionPickerWindow.ShowPick(
            $"cyr275",
            "cyr276",
            choices, recIdx);
        if (picked?.Tag is not CurseForgeFile file) return;

        IsBusy = true; DlProgress = 0;
        Status = $"cyr277";
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
                Application.Current.Dispatcher.Invoke(() => { DlFile = p.FileName; ReportProgress(p.Percentage); Status = $"cyr278"; }));
            await _mods.DownloadCurseForgeFileAsync(file, CurrentProfile.Id, progress);
            Status = $"cyr279";
            ShowToast("cyr280");
            LoadMods();
        }
        catch (Exception ex) { Status = $"cyr281"; }
        finally { FinishDownload(); }
    }

    private async Task DownloadModVersionAsync(ModrinthMod mod, ModrinthVersion version)
    {
        if (CurrentProfile == null) return;
        IsBusy = true; DlProgress = 0;
        Status = $"cyr282";
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
                Application.Current.Dispatcher.Invoke(() => { DlFile = p.FileName; ReportProgress(p.Percentage); Status = $"cyr283"; }));
            await _mods.DownloadModrinthVersionAsync(version, _mods.GetModsDir(CurrentProfile.Id), progress);
            Status = $"cyr284";
            ShowToast("cyr285");
            LoadMods();
        }
        catch (Exception ex) { Status = $"cyr286"; }
        finally { FinishDownload(); }
    }

    private async Task InstallModLoaderAsync()
    {
        if (CurrentProfile == null || EditModLoader == "Vanilla")
        {
            Status = "cyr287";
            return;
        }

        IsBusy = true; Status = $"cyr288";
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
                Application.Current.Dispatcher.Invoke(() => { DlFile = p.FileName; Status = $"cyr289"; }));

            string loaderVersion = EditModLoaderVersion;
            string sodiumNote = "";

            if (EditModLoader == "Fabric")
            {
                
                if (string.IsNullOrWhiteSpace(loaderVersion))
                {
                    Status = "cyr290";
                    loaderVersion = await _mods.GetLatestFabricLoaderAsync(CurrentProfile.VersionId) ?? "";
                }
                if (string.IsNullOrEmpty(loaderVersion))
                {
                    Status = "cyr291" + CurrentProfile.VersionId;
                    return;
                }
                await _mods.InstallFabricAsync(CurrentProfile.VersionId, loaderVersion, progress);

                var extras = new List<string>();

                
                try
                {
                    Status = "cyr292";
                    var sodiumOk = await _mods.DownloadModrinthProjectAsync(
                        "AANobbMI", CurrentProfile.VersionId, "fabric",
                        _mods.GetModsDir(CurrentProfile.Id), progress);
                    LoadMods();
                    if (sodiumOk) extras.Add("Sodium");
                }
                catch { }

                
                if (ModService.IsDedModCompatible(CurrentProfile.VersionId))
                {
                    try { EnsureDedMod(); extras.Add("DED Mod"); } catch { }
                }

                sodiumNote = extras.Count > 0 ? " + " + string.Join(" + ", extras) : "";
            }
            else if (EditModLoader == "Forge")
            {
                if (string.IsNullOrWhiteSpace(loaderVersion))
                {
                    Status = "cyr293";
                    return;
                }
                await _mods.InstallForgeAsync(CurrentProfile.VersionId, loaderVersion, progress);
            }
            else if (EditModLoader == "OptiFine")
            {
                if (string.IsNullOrWhiteSpace(loaderVersion))
                {
                    Status = "cyr294";
                    var versions = await _mods.GetOptiFineVersionsAsync(CurrentProfile.VersionId);
                    if (versions.Count == 0)
                    {
                        Status = "cyr295" + CurrentProfile.VersionId;
                        return;
                    }
                    
                    var latest = versions[^1];
                    loaderVersion = $"{latest.Type}_{latest.Patch}";
                }
                await _mods.InstallOptiFineAsync(CurrentProfile.VersionId, loaderVersion, progress);
            }

            CurrentProfile.ModLoader = EditModLoader;
            CurrentProfile.ModLoaderVersion = loaderVersion;
            SaveProfile(CurrentProfile);
            await RefreshInstalledVersions();
            Status = $"cyr296";
            if (EditModLoader == "Fabric" && sodiumNote.Contains("+ Sodium"))
                ShowToast("cyr297");
        }        catch (Exception ex) { Status = $"cyr298"; }
        finally { IsBusy = false; DlFile = ""; }
    }

    private void SaveLaunchHistory(string version, string loader)
    {
        LaunchHistory.Insert(0, new LaunchHistoryEntry
        {
            Version = string.IsNullOrEmpty(loader) || loader == "Vanilla" ? $"Minecraft {version}" : $"{loader} {version}",
            Time = DateTime.Now
        });
        if (LaunchHistory.Count > 20) LaunchHistory.RemoveAt(LaunchHistory.Count - 1);

        try
        {
            var json = JsonSerializer.Serialize(LaunchHistory.Take(10));
            File.WriteAllText(Path.Combine(MinecraftPathHelper.BaseDir, "history.json"), json);
        }
        catch { }
    }

    private void LoadLaunchHistory()
    {
        try
        {
            var path = Path.Combine(MinecraftPathHelper.BaseDir, "history.json");
            if (File.Exists(path))
            {
                var entries = JsonSerializer.Deserialize<List<LaunchHistoryEntry>>(File.ReadAllText(path));
                if (entries != null)
                    foreach (var e in entries) LaunchHistory.Add(e);
            }
        }
        catch { }
    }

    

    private void SelectSkin()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "cyr299",
            Filter = "cyr300"
        };

        if (dialog.ShowDialog() == true)
        {
            Log($"cyr301");
            var (valid, w, h) = _skins.ValidateSkin(dialog.FileName);
            Log($"cyr302");
            if (!valid)
            {
                SkinStatus = $"cyr303";
                MessageBox.Show($"cyr304",
                    "cyr305", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                
                var prepared = _skins.PrepareSkin(dialog.FileName);

                string username = string.IsNullOrWhiteSpace(Account.Username) ? "player" : Account.Username;
                var savedPath = _skins.SaveSkin(prepared, username);
                Log($"cyr306");

                Account.SkinPath = savedPath;
                SaveAccount();

                LoadSkinPreview(savedPath);
                SkinStatus = $"cyr307";
                Status = $"cyr308";
                ShowToast("cyr309");
            }
            catch (Exception ex)
            {
                Log($"cyr310");
                SkinStatus = "cyr311";
                MessageBox.Show("cyr312" + ex.Message, "cyr313", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RemoveSkin()
    {
        Account.SkinPath = "";
        SaveAccount();
        SkinHeadPreview = null;
        SkinFullPreview = null;
        SkinStatus = "cyr314";
    }

    public void LoadSkinPreview(string? skinPath)
    {
        if (string.IsNullOrEmpty(skinPath) || !File.Exists(skinPath))
        {
            SkinHeadPreview = null;
            SkinFullPreview = null;
            return;
        }
        SkinHeadPreview = _skins.GetHeadPreview(skinPath);
        SkinFullPreview = _skins.GetFullPreview(skinPath);
    }

    private string _capeStatus = "cyr315";
    public string CapeStatus { get => _capeStatus; set => SetProperty(ref _capeStatus, value); }

    public void SelectCape()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "cyr316",
            Filter = "cyr317"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var (valid, w, h) = _skins.ValidateSkin(dialog.FileName);
            if (!valid)
            {
                MessageBox.Show($"cyr318", "cyr319",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var saved = _skins.SaveCape(dialog.FileName, Account.Username);
            CapeStatus = $"cyr320";
            LoadCapePreview();
            Status = "cyr321";
            ShowToast("cyr322");
        }
        catch (Exception ex)
        {
            CapeStatus = "cyr323";
            Status = $"cyr324";
        }
    }

    public void RemoveCape()
    {
        _skins.RemoveCape(Account.Username);
        CapeStatus = "cyr325";
        CapePreview = null;
        Status = "cyr326";
        ShowToast("cyr327");
    }

    

    private static readonly string[] OptimizationSlugs = { "sodium", "lithium", "ferritecore" };

    private bool _optimizing;
    public bool Optimizing { get => _optimizing; set => SetProperty(ref _optimizing, value); }

    
    public bool OptimizationInstalled => CurrentProfile != null &&
        OptimizationSlugs.All(slug => Directory.GetFiles(_mods.GetModsDir(CurrentProfile.Id), "*.jar")
            .Any(f => Path.GetFileName(f).ToLower().Contains(slug)));

    public string OptimizationButtonText => OptimizationInstalled ? "cyr328" : "cyr329";

    
    public async Task ToggleOptimizationPackAsync()
    {
        if (CurrentProfile == null || Optimizing) return;
        Optimizing = true;

        if (OptimizationInstalled)
        {
            
            try
            {
                var modsDir = _mods.GetModsDir(CurrentProfile.Id);
                var failed = false;
                foreach (var file in Directory.GetFiles(modsDir, "*.jar"))
                {
                    var name = Path.GetFileName(file).ToLower();
                    if (!OptimizationSlugs.Any(s => name.Contains(s))) continue;
                    try { File.Delete(file); }
                    catch { failed = true; }
                }
                LoadMods();
                OnPropertyChanged(nameof(OptimizationInstalled));
                OnPropertyChanged(nameof(OptimizationButtonText));
                if (failed)
                {
                    Status = "cyr330";
                    ShowToast("cyr331");
                }
                else
                {
                    Status = "cyr332";
                    ShowToast("cyr333");
                }
            }
            catch (Exception ex) { Status = $"cyr334"; }
            finally { Optimizing = false; }
            return;
        }

        
        IsBusy = true;
        Status = "cyr335";
        try
        {
            var loader = CurrentProfile.ModLoader == "Vanilla" ? "fabric" : CurrentProfile.ModLoader.ToLower();
            var modsDir = _mods.GetModsDir(CurrentProfile.Id);
            foreach (var slug in OptimizationSlugs)
            {
                
                if (Directory.GetFiles(modsDir, "*.jar").Any(f => Path.GetFileName(f).ToLower().Contains(slug)))
                    continue;

                var results = await _mods.SearchModrinthAsync(slug, "", "", 5);
                var mod = results.FirstOrDefault(r => r.Slug == slug || r.Title.Contains(slug, StringComparison.OrdinalIgnoreCase));
                if (mod == null) { Status = $"cyr336"; continue; }
                var progress = new Progress<DownloadProgress>(p =>
                    Application.Current.Dispatcher.Invoke(() => { DlFile = p.FileName; ReportProgress(p.Percentage); Status = $"cyr337"; }));
                await _mods.DownloadModrinthModAsync(mod, CurrentProfile.VersionId, loader, CurrentProfile.Id, progress);
            }
            LoadMods();
            OnPropertyChanged(nameof(OptimizationInstalled));
            OnPropertyChanged(nameof(OptimizationButtonText));
            Status = "cyr338";
            ShowToast("cyr339");
        }
        catch (Exception ex) { Status = $"cyr340"; }
        finally { Optimizing = false; FinishDownload(); }
    }
}

public class NewsItem
{
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
}

public class ScreenshotItem : INotifyPropertyChanged
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime TakenAt { get; set; }

    private System.Windows.Media.ImageSource? _thumbnail;
    public System.Windows.Media.ImageSource? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class LauncherSettings
{
    public string Theme { get; set; } = "Red";
    public bool AutoLogin { get; set; } = true;
    public int FontSize { get; set; } = 13;
    public string OfflineUsername { get; set; } = "Player";
    public bool MultipleInstances { get; set; }
    public string PostLaunchAction { get; set; } = "keep";
    public bool Ipv4Only { get; set; } = true;
    public bool? LowEndMode { get; set; }
    public bool SoftwareRendering { get; set; }
    public string DiscordClientId { get; set; } = "";
    public string DiscordClientSecret { get; set; } = "";
}

public class LaunchHistoryEntry
{
    public string Version { get; set; } = "";
    public DateTime Time { get; set; }
}

public class VersionOption
{
    public string McVersion { get; set; }
    public string Type { get; set; } = "release";

    public VersionOption(string mcVersion, string type)
    {
        McVersion = mcVersion;
        Type = type;
    }

    public bool IsSnapshot => Type == "snapshot";
    public bool IsOld => Type is "old_alpha" or "old_beta";
    public string TypeLabel => Type switch
    {
        "snapshot" => "cyr341",
        "old_alpha" => "Alpha",
        "old_beta" => "Beta",
        _ => ""
    };

    public override string ToString() => McVersion;
}

public class ServerEntry : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public int Port { get; set; } = 25565;
    public DateTime LastPlayed { get; set; } = DateTime.MinValue;
    public DateTime LastPingTime { get; set; } = DateTime.MinValue;
    public string Description { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public System.Windows.Media.ImageSource? Icon { get; set; }

    private string _online = "—";
    public string Online { get => _online; set { _online = value; Notify(); Notify(nameof(IsOnline)); Notify(nameof(StatusBrush)); } }

    private string _ping = "—";
    public string Ping { get => _ping; set { _ping = value; Notify(); } }

    public bool IsOnline => _online != "—" && !_online.StartsWith("cyr342", StringComparison.OrdinalIgnoreCase);

    public System.Windows.Media.Brush StatusBrush => IsOnline
        ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2ECC71"))
        : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7A7A7A"));

    public string AddressLabel => Port == 25565 ? Address : $"{Address}:{Port}";

    private void Notify([System.Runtime.CompilerServices.CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class PlayerEntry { public string Name { get; set; } = ""; public string Role { get; set; } = ""; }

public class FriendEntry : INotifyPropertyChanged
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string PinnedName { get; set; } = "";
    public DateTime LastSeen { get; set; } = DateTime.MinValue;
    public DateTime LastOnline { get; set; } = DateTime.MinValue;

    [JsonIgnore]
    private string? _server;
    [JsonIgnore]
    public string? Server
    {
        get => _server;
        set { _server = value; Notify(nameof(StatusText)); Notify(nameof(CanJoin)); }
    }

    [JsonIgnore]
    private string? _status;
    [JsonIgnore]
    public string? Status
    {
        get => _status;
        set { _status = value; Notify(nameof(StatusText)); }
    }

    [JsonIgnore]
    private string? _inviteServer;
    [JsonIgnore]
    public string? InviteServer
    {
        get => _inviteServer;
        set { _inviteServer = value; Notify(nameof(HasInvite)); }
    }
    [JsonIgnore]
    public bool HasInvite => !string.IsNullOrEmpty(InviteServer);

    [JsonIgnore]
    private int _unread;
    [JsonIgnore]
    public int Unread
    {
        get => _unread;
        set { _unread = value; Notify(); Notify(nameof(UnreadBadge)); }
    }
    [JsonIgnore]
    public string UnreadBadge => Unread > 0 ? Unread.ToString() : "";
    [JsonIgnore]
    public bool HasUnread => Unread > 0;

    [JsonIgnore]
    private bool _isTyping;
    [JsonIgnore]
    public bool IsTyping
    {
        get => _isTyping;
        set { _isTyping = value; Notify(nameof(StatusText)); }
    }

    [JsonIgnore]
    public bool IsOnline => DateTime.UtcNow - LastSeen < TimeSpan.FromSeconds(35);
    [JsonIgnore]
    public bool CanJoin => IsOnline && !string.IsNullOrEmpty(Server) && Server != "singleplayer";

    [JsonIgnore]
    public string StatusText
    {
        get
        {
            if (_isTyping) return "cyr343";
            if (!IsOnline) return LastOnline == DateTime.MinValue ? "cyr344" : "cyr345" + HumanizeAgo(LastOnline);
            if (!string.IsNullOrEmpty(Status)) return Status;
            return string.IsNullOrEmpty(Server) ? "cyr346" : Server;
        }
    }

    [JsonIgnore]
    public string DisplayName => !string.IsNullOrEmpty(PinnedName) ? PinnedName : (!string.IsNullOrEmpty(Name) ? Name : Code);

    public static string HumanizeAgo(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return "cyr347";
        if (span.TotalMinutes < 60) return $"cyr348";
        if (span.TotalHours < 24) return $"cyr349";
        if (span.TotalDays < 7) return $"cyr350";
        return utc.ToLocalTime().ToString("dd.MM");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public void Touch()
    {
        Notify(nameof(IsOnline)); Notify(nameof(StatusText)); Notify(nameof(CanJoin));
    }
}

public class GroupMember : INotifyPropertyChanged
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastSeen { get; set; } = DateTime.MinValue;

    [JsonIgnore]
    public bool IsOnline => DateTime.UtcNow - LastSeen < TimeSpan.FromSeconds(40);
    [JsonIgnore]
    public string DisplayName => string.IsNullOrEmpty(Name) ? Code : Name;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public void Touch() => Notify(nameof(IsOnline));
}

public class GroupChat : INotifyPropertyChanged
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    [JsonIgnore]
    private int _unread;
    [JsonIgnore]
    public int Unread
    {
        get => _unread;
        set { _unread = value; Notify(); Notify(nameof(UnreadBadge)); Notify(nameof(HasUnread)); }
    }
    [JsonIgnore]
    public string UnreadBadge => Unread > 0 ? Unread.ToString() : "";
    [JsonIgnore]
    public bool HasUnread => Unread > 0;

    [JsonIgnore]
    private int _onlineCount;
    [JsonIgnore]
    public int OnlineCount
    {
        get => _onlineCount;
        set { _onlineCount = value; Notify(); Notify(nameof(OnlineLabel)); }
    }
    [JsonIgnore]
    public string OnlineLabel => OnlineCount > 0 ? $"cyr351" : "";

    public string DisplayName => string.IsNullOrEmpty(Name) ? Code : Name;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class ChatLine
{
    public string Sender { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
    public bool IsMine { get; set; }

    
    public string InviteServer { get; set; } = "";

    [JsonIgnore]
    public bool IsInvite => !string.IsNullOrEmpty(InviteServer);
    [JsonIgnore]
    public string TimeLabel => Time.ToString("HH:mm");
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _exec;
    private readonly Func<object?, bool>? _can;
    public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null) { _exec = exec; _can = can; }
    public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
    public void Execute(object? p) => _exec(p);
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
