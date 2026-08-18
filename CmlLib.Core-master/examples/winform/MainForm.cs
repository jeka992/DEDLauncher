using CmlLib.Core;
using CmlLib.Core.Auth;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using CmlLib.Core.VersionMetadata;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Rules;
using CmlLib.Core.Auth.Microsoft;

namespace CmlLibWinFormSample
{
    public partial class MainForm : Form
    {
        private readonly HttpClient _httpClient = new();

        private Panel panelSidebar;
        private Label lblLogo;
        private Label lblSubtitle;
        private Button btnNavPlay;
        private Button btnNavMods;
        private Button btnNavServers;
        private Button btnNavP2P;
        private Button btnNavSettings;
        private Button btnNavConsole;
        private TabControl tabControlMain;
        private TabPage tabPagePlay;
        private TabPage tabPageMods;
        private TabPage tabPageServers;
        private TabPage tabPageP2P;
        private TabPage tabPageConsole;
        private TabPage tabPageSettings;
        private Panel bannerPanel;
        private Panel configPanel;
        private Label lblNickname;
        private Label lblVersion;
        private Label lblRam;
        private Label lblRamValue;
        private TrackBar trackBarRam;
        private ComboBox cbBuildVersion;
        private TextBox txtNicknameNew;
        private Button btnPlayLaunch;
        private ProgressBar pbLaunchProgress;
        private ListBox listBoxMods;
        private Button btnModsOpenFolder;
        private Button btnModsInstallOpt;
        private Button btnModrinth;
        private Button btnCurseForge;
        private Button btnAddModToBuild;
        private ListBox listBoxAvailableMods;
        private ListBox listBoxBuildMods;
        private Label lblAvailableMods;
        private Label lblBuildMods;
        private FlowLayoutPanel flpServerCards;
        private readonly Dictionary<string, List<string>> buildModLists = new();
        private readonly List<(string DisplayName, string MinecraftVersion)> buildOptions = new()
        {
            ("1.20.1 Forge Optimizer v4.2", "1.20.1"),
            ("1.20.1 Fabric Performance Pro", "1.20.1"),
            ("1.20.1 Vanilla сборка", "1.20.1"),
            ("1.20.4 Forge Ultimate", "1.20.4"),
            ("1.20.4 Fabric Lite", "1.20.4"),
            ("1.19.4 Vanilla", "1.19.4"),
            ("1.18.2 Forge Tech Pack", "1.18.2"),
            ("1.16.5 Legacy Mods", "1.16.5"),
            ("1.12.2 Модпак", "1.12.2"),
            ("1.8.9 PvP Classic", "1.8.9")
        };
        private RichTextBox rtbP2PChat;
        private TextBox txtChatInput;
        private TextBox txtPeerAddress;
        private TextBox txtPeerPort;
        private ComboBox cbStatus;
        private ListBox lbPeers;
        private Button btnSendChat;
        private Button btnAddPeer;
        private RichTextBox rtbConsole;

        public MainForm()
        {
            InitializeComponent();
            SetupCustomUi();
        }

        private void SetupCustomUi()
        {
            BackColor = ColorTranslator.FromHtml("#121212");
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            ForeColor = Color.White;
            Text = "DED Launcher // Леби-Мод Версия";
            ClientSize = new Size(1180, 620);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            groupBox1.Visible = false;
            groupBox2.Visible = false;
            groupBox3.Visible = false;
            groupBox4.Visible = false;
            btnGithub.Visible = false;
            btnWiki.Visible = false;
            btnChangelog.Visible = false;
            btnOptions.Visible = false;
            label12.Visible = false;
            lbLibraryVersion.Visible = false;
            Pb_Progress.Visible = false;
            Lv_Status.Visible = false;
            lbTime.Visible = false;

            panelSidebar = new Panel
            {
                BackColor = ColorTranslator.FromHtml("#181818"),
                Location = new Point(10, 10),
                Size = new Size(200, 600)
            };
            Controls.Add(panelSidebar);

            lblLogo = new Label
            {
                Text = "DED LAUNCHER",
                ForeColor = ColorTranslator.FromHtml("#ff4d4d"),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(16, 16),
                AutoSize = true
            };
            panelSidebar.Controls.Add(lblLogo);

            lblSubtitle = new Label
            {
                Text = "Леби-Мод Версия",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic, GraphicsUnit.Point),
                Location = new Point(16, 44),
                AutoSize = true
            };
            panelSidebar.Controls.Add(lblSubtitle);

            btnNavPlay = CreateSidebarButton("  ИГРАТЬ", new Point(0, 88));
            btnNavMods = CreateSidebarButton("  МОДЫ", new Point(0, 138));
            btnNavServers = CreateSidebarButton("  СЕРВЕРЫ", new Point(0, 188));
            btnNavP2P = CreateSidebarButton("  P2P СЕТЬ", new Point(0, 238));
            btnNavSettings = CreateSidebarButton("  НАСТРОЙКИ", new Point(0, 288));
            btnNavConsole = CreateSidebarButton("  КОНСОЛЬ", new Point(0, 338));

            panelSidebar.Controls.AddRange(new Control[]
            {
                btnNavPlay,
                btnNavMods,
                btnNavServers,
                btnNavP2P,
                btnNavSettings,
                btnNavConsole
            });

            tabControlMain = new TabControl
            {
                Location = new Point(220, 10),
                Size = new Size(940, 600),
                Appearance = TabAppearance.FlatButtons,
                ItemSize = new Size(0, 1),
                SizeMode = TabSizeMode.Fixed,
                TabStop = false
            };
            Controls.Add(tabControlMain);

            tabPagePlay = new TabPage();
            tabPageMods = new TabPage();
            tabPageServers = new TabPage();
            tabPageP2P = new TabPage();
            tabPageSettings = new TabPage();
            tabPageConsole = new TabPage();

            tabControlMain.TabPages.AddRange(new[] { tabPagePlay, tabPageMods, tabPageServers, tabPageP2P, tabPageSettings, tabPageConsole });

            BuildPlayTab();
            BuildModsTab();
            BuildServersTab();
            BuildP2PTab();
            BuildConsoleTab();
            BuildSettingsTab();

            SetActiveNavButton(btnNavPlay);
            tabControlMain.SelectedTab = tabPagePlay;
        }

        private Button CreateSidebarButton(string text, Point location)
        {
            var button = new Button
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                ForeColor = ColorTranslator.FromHtml("#ffffff"),
                BackColor = ColorTranslator.FromHtml("#181818"),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
                Size = new Size(200, 40),
                Location = location,
                FlatAppearance = { BorderSize = 0 }
            };
            button.Click += SidebarButton_Click;
            return button;
        }

        private void SetActiveNavButton(Button active)
        {
            foreach (Control control in panelSidebar.Controls)
            {
                if (control is Button btn)
                {
                    btn.BackColor = btn == active ? ColorTranslator.FromHtml("#b30000") : ColorTranslator.FromHtml("#181818");
                    btn.ForeColor = btn == active ? Color.White : ColorTranslator.FromHtml("#ffffff");
                }
            }
        }

        private void SidebarButton_Click(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                SetActiveNavButton(btn);
                switch (btn.Text.Trim())
                {
                    case "ИГРАТЬ": tabControlMain.SelectedTab = tabPagePlay; break;
                    case "МОДЫ": tabControlMain.SelectedTab = tabPageMods; break;
                    case "СЕРВЕРЫ": tabControlMain.SelectedTab = tabPageServers; break;
                    case "P2P СЕТЬ": tabControlMain.SelectedTab = tabPageP2P; break;
                    case "НАСТРОЙКИ": tabControlMain.SelectedTab = tabPageSettings; break;
                    case "КОНСОЛЬ": tabControlMain.SelectedTab = tabPageConsole; break;
                }
            }
        }

        private void BuildPlayTab()
        {
            tabPagePlay.BackColor = ColorTranslator.FromHtml("#121212");

            bannerPanel = new Panel
            {
                BackColor = ColorTranslator.FromHtml("#1c1c1c"),
                Size = new Size(900, 120),
                Location = new Point(20, 20)
            };
            tabPagePlay.Controls.Add(bannerPanel);

            var lblBannerTitle = new Label
            {
                Text = "ДОБРО ПОЖАЛОВАТЬ В MINECRAFT!",
                ForeColor = ColorTranslator.FromHtml("#ff4d4d"),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(20, 20),
                AutoSize = true
            };
            bannerPanel.Controls.Add(lblBannerTitle);

            var lblBannerDesc = new Label
            {
                Text = "Запусти свою сборку с оптимизированными модами и настройками в фирменном стиле DED Launcher.",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(20, 58),
                Size = new Size(860, 45)
            };
            bannerPanel.Controls.Add(lblBannerDesc);

            configPanel = new Panel
            {
                BackColor = ColorTranslator.FromHtml("#181818"),
                Size = new Size(900, 320),
                Location = new Point(20, 150)
            };
            tabPagePlay.Controls.Add(configPanel);

            var lblNicknameHeader = new Label
            {
                Text = "ИГРОВОЙ НИКНЕЙМ",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(20, 24),
                AutoSize = true
            };
            configPanel.Controls.Add(lblNicknameHeader);

            txtNicknameNew = new TextBox
            {
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(20, 52),
                Size = new Size(380, 30),
                Text = "DED_Player"
            };
            configPanel.Controls.Add(txtNicknameNew);

            var lblVersionHeader = new Label
            {
                Text = "ВЕРСИЯ СБОРКИ",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(20, 100),
                AutoSize = true
            };
            configPanel.Controls.Add(lblVersionHeader);

            cbBuildVersion = new ComboBox
            {
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(20, 128),
                Size = new Size(380, 30)
            };
            cbBuildVersion.Items.AddRange(buildOptions.Select(o => o.DisplayName).ToArray());
            cbBuildVersion.SelectedIndex = 0;
            cbBuildVersion.SelectedIndexChanged += CbBuildVersion_SelectedIndexChanged;
            configPanel.Controls.Add(cbBuildVersion);

            lblRam = new Label
            {
                Text = "ВЫДЕЛЕНИЕ ОПЕРАТИВНОЙ ПАМЯТИ",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(440, 24),
                AutoSize = true
            };
            configPanel.Controls.Add(lblRam);

            lblRamValue = new Label
            {
                Text = "4 ГБ ОЗУ (Рекомендуется)",
                ForeColor = ColorTranslator.FromHtml("#ffffff"),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(440, 52),
                AutoSize = true
            };
            configPanel.Controls.Add(lblRamValue);

            trackBarRam = new TrackBar
            {
                BackColor = ColorTranslator.FromHtml("#181818"),
                Location = new Point(440, 85),
                Size = new Size(420, 45),
                Minimum = 2,
                Maximum = 16,
                Value = 4,
                TickFrequency = 2,
                LargeChange = 2,
                SmallChange = 1
            };
            trackBarRam.ValueChanged += TrackBarRam_ValueChanged;
            configPanel.Controls.Add(trackBarRam);

            btnPlayLaunch = new Button
            {
                Text = "ИГРАТЬ",
                BackColor = ColorTranslator.FromHtml("#b30000"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point),
                Size = new Size(420, 60),
                Location = new Point(440, 150)
            };
            btnPlayLaunch.FlatAppearance.BorderSize = 0;
            btnPlayLaunch.Click += BtnPlayLaunch_Click;
            configPanel.Controls.Add(btnPlayLaunch);

            pbLaunchProgress = new ProgressBar
            {
                Location = new Point(440, 230),
                Size = new Size(420, 16),
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = ColorTranslator.FromHtml("#ff4d4d")
            };
            configPanel.Controls.Add(pbLaunchProgress);
        }

        private void BuildModsTab()
        {
            tabPageMods.BackColor = ColorTranslator.FromHtml("#121212");
            var lblModsHeader = new Label
            {
                Text = "УПРАВЛЕНИЕ МОДИФИКАЦИЯМИ",
                ForeColor = ColorTranslator.FromHtml("#ff4d4d"),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(20, 20),
                AutoSize = true
            };
            tabPageMods.Controls.Add(lblModsHeader);

            var lblModsDesc = new Label
            {
                Text = "Здесь находятся установленные моды. Управляй активными сборками и оптимизацией.",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(20, 52),
                Size = new Size(880, 30)
            };
            tabPageMods.Controls.Add(lblModsDesc);

            listBoxMods = new ListBox
            {
                BackColor = ColorTranslator.FromHtml("#1c1c1c"),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Location = new Point(20, 90),
                Size = new Size(320, 320)
            };
            listBoxMods.Items.AddRange(new object[]
            {
                "[АКТИВЕН] OptiFine_1.20.1.jar",
                "[АКТИВЕН] PerformanceTweaks.jar",
                "[АКТИВЕН] BetterFPS.jar"
            });
            tabPageMods.Controls.Add(listBoxMods);

            btnModsOpenFolder = new Button
            {
                Text = "ОТКРЫТЬ ПАПКУ МОДОВ",
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(20, 425),
                Size = new Size(200, 40)
            };
            btnModsOpenFolder.FlatAppearance.BorderSize = 0;
            tabPageMods.Controls.Add(btnModsOpenFolder);

            btnModsInstallOpt = new Button
            {
                Text = "УСТАНОВИТЬ ОПТИМИЗАЦИЮ",
                BackColor = ColorTranslator.FromHtml("#b30000"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(230, 425),
                Size = new Size(200, 40)
            };
            btnModsInstallOpt.FlatAppearance.BorderSize = 0;
            tabPageMods.Controls.Add(btnModsInstallOpt);

            lblAvailableMods = new Label
            {
                Text = "ИСТОЧНИК МОДОВ",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(360, 90),
                AutoSize = true
            };
            tabPageMods.Controls.Add(lblAvailableMods);

            btnModrinth = new Button
            {
                Text = "Modrinth",
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(360, 120),
                Size = new Size(120, 34)
            };
            btnModrinth.FlatAppearance.BorderSize = 0;
            btnModrinth.Click += BtnModrinth_Click;
            tabPageMods.Controls.Add(btnModrinth);

            btnCurseForge = new Button
            {
                Text = "CurseForge",
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(490, 120),
                Size = new Size(120, 34)
            };
            btnCurseForge.FlatAppearance.BorderSize = 0;
            btnCurseForge.Click += BtnCurseForge_Click;
            tabPageMods.Controls.Add(btnCurseForge);

            listBoxAvailableMods = new ListBox
            {
                BackColor = ColorTranslator.FromHtml("#1c1c1c"),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Location = new Point(360, 170),
                Size = new Size(250, 260)
            };
            tabPageMods.Controls.Add(listBoxAvailableMods);

            btnAddModToBuild = new Button
            {
                Text = "ДОБАВИТЬ В СБОРКУ",
                BackColor = ColorTranslator.FromHtml("#b30000"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(360, 440),
                Size = new Size(250, 40)
            };
            btnAddModToBuild.FlatAppearance.BorderSize = 0;
            btnAddModToBuild.Click += BtnAddModToBuild_Click;
            tabPageMods.Controls.Add(btnAddModToBuild);

            lblBuildMods = new Label
            {
                Text = "МОДЫ В ТЕКУЩЕЙ СБОРКЕ",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(640, 90),
                AutoSize = true
            };
            tabPageMods.Controls.Add(lblBuildMods);

            listBoxBuildMods = new ListBox
            {
                BackColor = ColorTranslator.FromHtml("#1c1c1c"),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Location = new Point(640, 120),
                Size = new Size(260, 320)
            };
            tabPageMods.Controls.Add(listBoxBuildMods);

            LoadModSource(ModSource.Modrinth);
            UpdateBuildModList();
        }

        private enum ModSource
        {
            Modrinth,
            CurseForge
        }

        private ModSource currentModSource = ModSource.Modrinth;

        private async void BtnModrinth_Click(object? sender, EventArgs e)
        {
            await LoadModSource(ModSource.Modrinth);
        }

        private async void BtnCurseForge_Click(object? sender, EventArgs e)
        {
            await LoadModSource(ModSource.CurseForge);
        }

        private void BtnAddModToBuild_Click(object? sender, EventArgs e)
        {
            if (cbBuildVersion.SelectedItem == null)
            {
                MessageBox.Show("Выберите сборку для добавления мода.");
                return;
            }
            if (listBoxAvailableMods.SelectedItem == null)
            {
                MessageBox.Show("Выберите мод из списка доступных модов.");
                return;
            }

            var buildName = cbBuildVersion.SelectedItem.ToString()!;
            var modName = listBoxAvailableMods.SelectedItem.ToString()!;

            if (!buildModLists.TryGetValue(buildName, out var mods))
            {
                mods = new List<string>();
                buildModLists[buildName] = mods;
            }

            if (!mods.Contains(modName))
            {
                mods.Add(modName);
                UpdateBuildModList();
            }
        }

        private void CbBuildVersion_SelectedIndexChanged(object? sender, EventArgs e)
        {
            SetBuildVersionSelection();
            UpdateBuildModList();
        }

        private void SetBuildVersionSelection()
        {
            if (cbBuildVersion.SelectedIndex < 0)
                return;

            var buildVersion = buildOptions[cbBuildVersion.SelectedIndex].MinecraftVersion;
            for (int i = 0; i < cbVersion.Items.Count; i++)
            {
                if (cbVersion.Items[i].ToString() == buildVersion)
                {
                    cbVersion.SelectedIndex = i;
                    return;
                }
            }
        }

        private async Task LoadModSource(ModSource source)
        {
            currentModSource = source;
            listBoxAvailableMods.Items.Clear();

            if (source == ModSource.Modrinth)
            {
                btnModrinth.BackColor = ColorTranslator.FromHtml("#b30000");
                btnCurseForge.BackColor = ColorTranslator.FromHtml("#282828");
                var mods = await GetSampleModrinthModsAsync();
                listBoxAvailableMods.Items.AddRange(mods.ToArray());
            }
            else
            {
                btnModrinth.BackColor = ColorTranslator.FromHtml("#282828");
                btnCurseForge.BackColor = ColorTranslator.FromHtml("#b30000");
                var mods = GetSampleCurseForgeMods();
                listBoxAvailableMods.Items.AddRange(mods.ToArray());
            }
        }

        private async Task<List<string>> GetSampleModrinthModsAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync("https://api.modrinth.com/v2/tag/featured");
                using var json = JsonDocument.Parse(response);
                if (json.RootElement.TryGetProperty("hits", out var hits))
                {
                    return hits.EnumerateArray()
                        .Select(item => item.GetProperty("title").GetString() ?? item.GetProperty("slug").GetString() ?? "Modrinth Mod")
                        .Take(12)
                        .ToList();
                }
            }
            catch
            {
                // ignore if network fails
            }

            return new List<string>
            {
                "Sodium",
                "Lithium",
                "Phosphor",
                "Fabric API",
                "Indium",
                "Trinkets",
                "WTHIT",
                "Immersive Portals",
                "Roughly Enough Items",
                "Xaero's Minimap",
                "Create",
                "Alex's Mobs"
            };
        }

        private List<string> GetSampleCurseForgeMods()
        {
            return new List<string>
            {
                "OptiFine",
                "JourneyMap",
                "Biomes O' Plenty",
                "Tinkers' Construct",
                "MineColonies",
                "Just Enough Items",
                "Waystones",
                "Thermal Expansion",
                "Applied Energistics 2",
                "Immersive Engineering",
                "Mekanism",
                "Twilight Forest"
            };
        }

        private void UpdateBuildModList()
        {
            listBoxBuildMods.Items.Clear();
            if (cbBuildVersion.SelectedItem == null)
                return;

            var buildName = cbBuildVersion.SelectedItem.ToString()!;
            if (!buildModLists.ContainsKey(buildName))
            {
                buildModLists[buildName] = new List<string>();
            }

            listBoxBuildMods.Items.AddRange(buildModLists[buildName].ToArray());
        }

        private void BuildServersTab()
        {
            tabPageServers.BackColor = ColorTranslator.FromHtml("#121212");
            var lblServersHeader = new Label
            {
                Text = "СЕРВЕРЫ",
                ForeColor = ColorTranslator.FromHtml("#ff4d4d"),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(20, 20),
                AutoSize = true
            };
            tabPageServers.Controls.Add(lblServersHeader);

            flpServerCards = new FlowLayoutPanel
            {
                Location = new Point(20, 60),
                Size = new Size(900, 500),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            tabPageServers.Controls.Add(flpServerCards);
            flpServerCards.Controls.Add(CreateServerCard("DED Survival", "play.dedserver.ru", "1.20.1", "312/1000"));
            flpServerCards.Controls.Add(CreateServerCard("SpeedZone", "pvp.ded.zone", "1.20.1", "58/300"));
            flpServerCards.Controls.Add(CreateServerCard("Лоби-Мир", "lobby.ded.local", "1.20.1", "122/500"));
        }

        private Panel CreateServerCard(string title, string ip, string version, string online)
        {
            var card = new Panel
            {
                BackColor = ColorTranslator.FromHtml("#1c1c1c"),
                Size = new Size(880, 110),
                Margin = new Padding(0, 0, 0, 16)
            };
            var lblTitle = new Label
            {
                Text = title,
                ForeColor = ColorTranslator.FromHtml("#ff4d4d"),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(16, 12),
                AutoSize = true
            };
            card.Controls.Add(lblTitle);

            var lblIp = new Label
            {
                Text = $"IP: {ip}",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(16, 40),
                AutoSize = true
            };
            card.Controls.Add(lblIp);

            var lblVersion = new Label
            {
                Text = $"Версия: {version}",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(16, 60),
                AutoSize = true
            };
            card.Controls.Add(lblVersion);

            var lblOnline = new Label
            {
                Text = online,
                ForeColor = ColorTranslator.FromHtml("#78dc78"),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(16, 82),
                AutoSize = true
            };
            card.Controls.Add(lblOnline);

            var btnConnect = new Button
            {
                Text = "ПОДКЛЮЧИТЬСЯ",
                BackColor = ColorTranslator.FromHtml("#b30000"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Size = new Size(150, 34),
                Location = new Point(700, 38)
            };
            btnConnect.FlatAppearance.BorderSize = 0;
            card.Controls.Add(btnConnect);
            return card;
        }

        private void BuildP2PTab()
        {
            tabPageP2P.BackColor = ColorTranslator.FromHtml("#121212");
            rtbP2PChat = new RichTextBox
            {
                BackColor = ColorTranslator.FromHtml("#0a0a0a"),
                ForeColor = ColorTranslator.FromHtml("#78dc78"),
                Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(20, 20),
                Size = new Size(560, 480),
                ReadOnly = true,
                Text = "[12:00:00] P2P чат готов к работе...\n"
            };
            tabPageP2P.Controls.Add(rtbP2PChat);

            txtChatInput = new TextBox
            {
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(20, 520),
                Size = new Size(420, 30)
            };
            tabPageP2P.Controls.Add(txtChatInput);

            btnSendChat = new Button
            {
                Text = "ОТПРАВИТЬ",
                BackColor = ColorTranslator.FromHtml("#b30000"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(450, 520),
                Size = new Size(130, 30)
            };
            btnSendChat.FlatAppearance.BorderSize = 0;
            btnSendChat.Click += BtnSendChat_Click;
            tabPageP2P.Controls.Add(btnSendChat);

            var panelPeer = new Panel
            {
                BackColor = ColorTranslator.FromHtml("#1c1c1c"),
                Location = new Point(600, 20),
                Size = new Size(320, 530)
            };
            tabPageP2P.Controls.Add(panelPeer);

            var lblIp = new Label
            {
                Text = "IP АДРЕС ПИРА",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(16, 16),
                AutoSize = true
            };
            panelPeer.Controls.Add(lblIp);

            txtPeerAddress = new TextBox
            {
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(16, 40),
                Size = new Size(288, 28),
                Text = "127.0.0.1"
            };
            panelPeer.Controls.Add(txtPeerAddress);

            var lblPort = new Label
            {
                Text = "ПОРТ",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(16, 78),
                AutoSize = true
            };
            panelPeer.Controls.Add(lblPort);

            txtPeerPort = new TextBox
            {
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(16, 102),
                Size = new Size(288, 28),
                Text = "25555"
            };
            panelPeer.Controls.Add(txtPeerPort);

            var lblStatus = new Label
            {
                Text = "МОЙ СТАТУС",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(16, 140),
                AutoSize = true
            };
            panelPeer.Controls.Add(lblStatus);

            cbStatus = new ComboBox
            {
                BackColor = ColorTranslator.FromHtml("#282828"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(16, 164),
                Size = new Size(288, 28)
            };
            cbStatus.Items.AddRange(new object[] { "В сети", "Ищу пати", "AFK", "Играю в Minecraft" });
            cbStatus.SelectedIndex = 0;
            panelPeer.Controls.Add(cbStatus);

            lbPeers = new ListBox
            {
                BackColor = ColorTranslator.FromHtml("#0f0f0f"),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(16, 210),
                Size = new Size(288, 260)
            };
            lbPeers.Items.AddRange(new object[] { "DED_Friend1", "Peer_Alpha", "Игрок_33" });
            panelPeer.Controls.Add(lbPeers);

            btnAddPeer = new Button
            {
                Text = "ДОБАВИТЬ ПИРА",
                BackColor = ColorTranslator.FromHtml("#b30000"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(16, 482),
                Size = new Size(288, 32)
            };
            btnAddPeer.FlatAppearance.BorderSize = 0;
            btnAddPeer.Click += BtnAddPeer_Click;
            panelPeer.Controls.Add(btnAddPeer);
        }

        private void BuildConsoleTab()
        {
            tabPageConsole.BackColor = ColorTranslator.FromHtml("#121212");
            rtbConsole = new RichTextBox
            {
                BackColor = ColorTranslator.FromHtml("#0a0a0a"),
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(20, 20),
                Size = new Size(900, 540),
                ReadOnly = true,
                Text = "[12:00:00] Консоль лаунчера готова."
            };
            tabPageConsole.Controls.Add(rtbConsole);
        }

        private void BuildSettingsTab()
        {
            tabPageSettings.BackColor = ColorTranslator.FromHtml("#121212");
            var lblSettingsHeader = new Label
            {
                Text = "НАСТРОЙКИ",
                ForeColor = ColorTranslator.FromHtml("#ff4d4d"),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(20, 20),
                AutoSize = true
            };
            tabPageSettings.Controls.Add(lblSettingsHeader);

            var lblSettingsDesc = new Label
            {
                Text = "Настройки будут доступны в следующих версиях DED Launcher.",
                ForeColor = ColorTranslator.FromHtml("#a0a0a0"),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(20, 52),
                Size = new Size(880, 30)
            };
            tabPageSettings.Controls.Add(lblSettingsDesc);
        }

        private void TrackBarRam_ValueChanged(object sender, EventArgs e)
        {
            lblRamValue.Text = $"{trackBarRam.Value} ГБ ОЗУ (Рекомендуется)";
        }

        private void BtnPlayLaunch_Click(object sender, EventArgs e)
        {
            txtUsername.Text = txtNicknameNew.Text;
            if (cbBuildVersion.SelectedIndex >= 0)
            {
                var buildVersion = buildOptions[cbBuildVersion.SelectedIndex].MinecraftVersion;
                for (int i = 0; i < cbVersion.Items.Count; i++)
                {
                    if (cbVersion.Items[i].ToString() == buildVersion)
                    {
                        cbVersion.SelectedIndex = i;
                        break;
                    }
                }
            }

            txtXmx.Text = (trackBarRam.Value * 1024).ToString();
            txtXms.Text = Math.Max(1024, trackBarRam.Value * 512).ToString();
            Btn_Launch_Click(sender, e);
        }

        private void BtnSendChat_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtChatInput.Text))
                return;
            rtbP2PChat.AppendText($"[{DateTime.Now:HH:mm:ss}] Я: {txtChatInput.Text}\n");
            txtChatInput.Clear();
        }

        private void BtnAddPeer_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtPeerAddress.Text))
            {
                lbPeers.Items.Add(txtPeerAddress.Text);
            }
        }

        private void UpdateBuildVersionOptions()
        {
            if (cbBuildVersion == null)
                return;
            cbBuildVersion.Items.Clear();
            foreach (var item in cbVersion.Items)
            {
                cbBuildVersion.Items.Add(item);
            }
            if (cbBuildVersion.Items.Count > 0)
                cbBuildVersion.SelectedIndex = 0;
        }

        CancellationTokenSource? cancellationToken;
        MinecraftLauncher? launcher;
        private bool isInstalling = false;

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            lbLibraryVersion.Text = "CmlLib.Core " + Util.GetLibraryVersion();
            txtExtraJVMArguments.Text = string.Join(' ', MLaunchOption.DefaultExtraJvmArguments.SelectMany(arg => arg.Values));

            var defaultSession = MSession.CreateOfflineSession("cmltester123");
            txtUsername.Text = defaultSession.Username;
            txtUUID.Text = defaultSession.UUID;
            txtAccessToken.Text = defaultSession.AccessToken;
            txtXUID.Text = defaultSession.Xuid;

            // Initialize launcher
            await initializeLauncher(new MinecraftPath());
        }

        private async Task initializeLauncher(MinecraftPath path)
        {
            txtPath.Text = path.BasePath;

            var parameters = MinecraftLauncherParameters.CreateDefault(path, _httpClient);
            launcher = new MinecraftLauncher(parameters);
            await refreshVersions();
        }

        private async void btnRefreshVersion_Click(object sender, EventArgs e)
        {
            await refreshVersions();
        }

        private async Task refreshVersions(string? showVersion = null)
        {
            if (launcher == null)
            {
                MessageBox.Show("Initialize the launcher first");
                return;
            }

            cbVersion.Items.Clear();
            var versions = await launcher.GetAllVersionsAsync();

            bool showVersionExist = false;
            foreach (var item in versions)
            {
                if (item.Name == showVersion)
                    showVersionExist = true;
                cbVersion.Items.Add(item.Name);
            }

            if (showVersion == null || !showVersionExist)
                btnSetLastVersion_Click(null, null);
            else
                cbVersion.Text = showVersion;
        }

        private void btnSetLastVersion_Click(object? sender, EventArgs? e)
        {
            cbVersion.Text = launcher?.Versions?.LatestReleaseName;
        }

        private void btnSortFilter_Click(object sender, EventArgs e)
        {
            if (launcher == null)
            {
                MessageBox.Show("Initialize the launcher first");
                return;
            }
            var form = new VersionSortOptionForm(launcher, new MVersionSortOption());
            form.ShowDialog();
        }

        // Start Game
        private async void Btn_Launch_Click(object sender, EventArgs e)
        {
            if (launcher == null)
            {
                MessageBox.Show("Initialize the launcher first");
                return;
            }

            if (isInstalling)
            {
                MessageBox.Show("Уже выполняется установка/запуск. Подождите, пожалуйста.");
                return;
            }

            if (string.IsNullOrWhiteSpace(cbVersion.Text))
            {
                MessageBox.Show("Select Version");
                return;
            }

            isInstalling = true;
            setUIEnabled(false);

            try
            {
                var launchOption = new MLaunchOption
                {
                    Session = CreateLaunchSession(),
                    IsDemo = cbDemo.Checked,
                    FullScreen = cbFullscreen.Checked,
                    JvmArgumentOverrides = new[] { MArgument.FromCommandLine(txtJVMArgumentOverrides.Text) },
                    ExtraJvmArguments = new[] { MArgument.FromCommandLine(txtExtraJVMArguments.Text) },
                    ExtraGameArguments = new[] { MArgument.FromCommandLine(txtExtraGameArguments.Text) },
                    MaximumRamMb = ResolveMaximumRam(),
                    MinimumRamMb = ResolveMinimumRam()
                };

                if (!cbJavaUseDefault.Checked && !string.IsNullOrWhiteSpace(txtJava.Text))
                    launchOption.JavaPath = FindJavaPath(txtJava.Text.Trim());

                if (!string.IsNullOrWhiteSpace(txtClientId.Text))
                    launchOption.ClientId = txtClientId.Text;

                if (!string.IsNullOrWhiteSpace(txtVersionType.Text))
                    launchOption.VersionType = txtVersionType.Text;

                if (!string.IsNullOrWhiteSpace(txtGLauncherName.Text))
                    launchOption.GameLauncherName = txtGLauncherName.Text;

                if (!string.IsNullOrWhiteSpace(txtGLauncherVersion.Text))
                    launchOption.GameLauncherVersion = txtGLauncherVersion.Text;

                if (!string.IsNullOrWhiteSpace(txtDockName.Text))
                    launchOption.DockName = txtDockName.Text;

                if (!string.IsNullOrWhiteSpace(txtDockIcon.Text))
                    launchOption.DockIcon = txtDockIcon.Text;

                if (!string.IsNullOrWhiteSpace(txtQuickPlayPath.Text))
                    launchOption.QuickPlayPath = txtQuickPlayPath.Text;

                if (!string.IsNullOrWhiteSpace(txtQuickPlaySingleplay.Text))
                    launchOption.QuickPlaySingleplayer = txtQuickPlaySingleplay.Text;

                if (!string.IsNullOrWhiteSpace(txtQuickPlayReamls.Text))
                    launchOption.QuickPlayRealms = txtQuickPlayReamls.Text;

                if (!string.IsNullOrWhiteSpace(txtServerIP.Text))
                    launchOption.ServerIp = txtServerIP.Text;

                if (!string.IsNullOrWhiteSpace(txtServerPort.Text) && int.TryParse(txtServerPort.Text, out var serverPort))
                    launchOption.ServerPort = serverPort;

                if (!string.IsNullOrWhiteSpace(txtScreenWidth.Text) && int.TryParse(txtScreenWidth.Text, out var screenWidth))
                    launchOption.ScreenWidth = screenWidth;

                if (!string.IsNullOrWhiteSpace(txtScreenHeight.Text) && int.TryParse(txtScreenHeight.Text, out var screenHeight))
                    launchOption.ScreenHeight = screenHeight;

                if (!string.IsNullOrWhiteSpace(txtFeatures.Text))
                {
                    launchOption.Features = txtFeatures.Text
                        .Split(',')
                        .Select(f => f.Trim())
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .ToList();
                }

                var version = cbVersion.Text;
                if (cbBuildVersion.SelectedIndex >= 0)
                {
                    version = buildOptions[cbBuildVersion.SelectedIndex].MinecraftVersion;
                    SetBuildVersionSelection();
                }
                else if (!string.IsNullOrEmpty(cbBuildVersion.Text))
                {
                    var buildMatch = buildOptions.FirstOrDefault(o => o.DisplayName == cbBuildVersion.Text);
                    if (buildMatch != default)
                    {
                        version = buildMatch.MinecraftVersion;
                        SetBuildVersionSelection();
                    }
                }

                if (launcher.Versions == null || !launcher.Versions.TryGetVersionMetadata(version, out _))
                {
                    MessageBox.Show($"Версия '{version}' не найдена. Выберите версию из списка.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(launchOption.JavaPath))
                    launchOption.JavaPath = ResolveJavaFromEnvironment();

                if (string.IsNullOrWhiteSpace(launchOption.JavaPath))
                {
                    MessageBox.Show("Java не найдена. Установите Java или укажите путь к javaw.exe вручную.");
                    return;
                }

                cancellationToken = new CancellationTokenSource();

                var fileProgress = new SyncProgress<InstallerProgressChangedEventArgs>(Launcher_FileChanged);
                var byteProgress = new SyncProgress<ByteProgress>(Launcher_ProgressChanged);
                var stopwatch = new Stopwatch();

                var process = await Task.Run(async () =>
                {
                    stopwatch.Start();
                    var result = await launcher.InstallAndBuildProcessAsync(
                        version,
                        launchOption,
                        fileProgress,
                        byteProgress,
                        cancellationToken.Token);
                    stopwatch.Stop();
                    return result;
                }); // Create Arguments and Process

                lbTime.Text = stopwatch.Elapsed.ToString();
                StartProcess(process); // Start Process with debug options

                var gameLog = new GameLog(process);
                gameLog.Show();
            }
            catch (FormatException fex) // int.Parse exception
            {
                MessageBox.Show("Failed to create MLaunchOption\n\n" + fex);
            }
            catch (Win32Exception wex) // java exception
            {
                MessageBox.Show(wex + "\n\nIt seems your java setting has problem");
            }
            catch (Exception ex) // all exception
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                setUIEnabled(true);
                isInstalling = false;
            }
        }

        private MSession CreateLaunchSession()
        {
            if (!string.IsNullOrWhiteSpace(txtAccessToken.Text) &&
                !string.IsNullOrWhiteSpace(txtUUID.Text) &&
                !string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                return new MSession
                {
                    Username = txtUsername.Text.Trim(),
                    AccessToken = txtAccessToken.Text.Trim(),
                    UUID = txtUUID.Text.Trim(),
                    Xuid = txtXUID.Text.Trim()
                };
            }

            var userName = string.IsNullOrWhiteSpace(txtUsername.Text)
                ? "Player"
                : txtUsername.Text.Trim();

            return MSession.GetOfflineSession(userName);
        }

        private int ResolveMaximumRam()
        {
            if (int.TryParse(txtXmx.Text, out var xmx) && xmx > 0)
                return Math.Clamp(xmx, 1024, 16384);

            return Math.Max(1024, trackBarRam.Value * 1024);
        }

        private int ResolveMinimumRam()
        {
            if (int.TryParse(txtXms.Text, out var xms) && xms > 0)
                return Math.Clamp(xms, 512, ResolveMaximumRam());

            return Math.Min(ResolveMaximumRam(), Math.Max(1024, trackBarRam.Value * 512));
        }

        private string? FindJavaPath(string? manualJavaPath)
        {
            if (!string.IsNullOrWhiteSpace(manualJavaPath))
            {
                var candidate = manualJavaPath.Trim();
                if (File.Exists(candidate))
                    return candidate;

                if (Directory.Exists(candidate))
                {
                    foreach (var fileName in new[] { "javaw.exe", "java.exe" })
                    {
                        var candidatePath = Path.Combine(candidate, "bin", fileName);
                        if (File.Exists(candidatePath))
                            return candidatePath;
                    }
                }
            }

            return ResolveJavaFromEnvironment();
        }

        private string? ResolveJavaFromEnvironment()
        {
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                foreach (var fileName in new[] { "javaw.exe", "java.exe" })
                {
                    var candidate = Path.Combine(javaHome, "bin", fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathEnv))
                return null;

            foreach (var part in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                foreach (var fileName in new[] { "javaw.exe", "java.exe" })
                {
                    var candidate = Path.Combine(part.Trim('"'), fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return null;
        }

        ByteProgress byteProgress;
        private void Launcher_ProgressChanged(ByteProgress e)
        {
            byteProgress = e;
        }

        InstallerProgressChangedEventArgs? fileProgress;
        private void Launcher_FileChanged(InstallerProgressChangedEventArgs e)
        {
            if (e.EventType == InstallerEventType.Done)
                fileProgress = e;
        }

        private void eventTimer_Tick(object sender, EventArgs e)
        {
            var bytePercentage = (int)(byteProgress.ProgressedBytes / (double)byteProgress.TotalBytes * 100);
            if (bytePercentage >= 0 && bytePercentage <= 100)
            {
                Pb_Progress.Value = bytePercentage;
                Pb_Progress.Maximum = 100;
            }

            if (fileProgress != null)
                Lv_Status.Text = $"[{fileProgress.ProgressedTasks}/{fileProgress.TotalTasks}] {fileProgress.Name}";
        }

        private void cbJavaUseDefault_CheckedChanged(object sender, EventArgs e)
        {
            if (cbJavaUseDefault.Checked)
            {
                txtJava.ReadOnly = true;
            }
            else
            {
                txtJava.ReadOnly = false;
            }
        }

        private async void btnChangePath_Click(object sender, EventArgs e)
        {
            if (launcher == null)
            {
                MessageBox.Show("Initialize the launcher first");
                return;
            }
            var form = new PathForm(launcher.MinecraftPath);
            form.ShowDialog();
            await initializeLauncher(form.MinecraftPath);
        }

        private void btnAutoRamSet_Click(object sender, EventArgs e)
        {
            var computerMemory = Util.GetMemoryMb();
            if (computerMemory == null)
            {
                MessageBox.Show("Failed to get computer memory");
                return;
            }

            var max = computerMemory / 2;
            if (max < 1024)
                max = 1024;
            else if (max > 8192)
                max = 8192;

            var min = max / 10;

            txtXmx.Text = max.ToString();
            txtXms.Text = min.ToString();
        }

        private void setUIEnabled(bool value)
        {
            groupBox1.Enabled = value;
            groupBox2.Enabled = value;
            groupBox3.Enabled = value;
            btnLaunch.Enabled = value;
            btnPlayLaunch.Enabled = value;
            btnCancel.Enabled = !value;
        }

        private void StartProcess(Process process)
        {
            File.WriteAllText("launcher.txt", process.StartInfo.Arguments);

            // process options to display game log

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
            process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            process.EnableRaisingEvents = true;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }

        private void btnChangelog_Click(object sender, EventArgs e)
        {
            // Game Changelog
            var f = new ChangeLog();
            f.Show();
        }

        private void btnOptions_Click(object sender, EventArgs e)
        {
            if (launcher == null)
            {
                MessageBox.Show("Initialize the launcher first");
                return;
            }
            var path = Path.Combine(launcher.MinecraftPath.BasePath, "options.txt");
            var f = new GameOptions(path);
            f.Show();
        }

        private void btnGithub_Click(object sender, EventArgs e)
        {
            Util.OpenUrl("https://github.com/AlphaBs/CmlLib.Core");
        }

        private void btnWiki_Click(object sender, EventArgs e)
        {
            Util.OpenUrl("https://github.com/AlphaBs/CmlLib.Core/wiki/");
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            cancellationToken?.Cancel();
        }

        JELoginHandler loginHandler = JELoginHandlerBuilder.BuildDefault();

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            setUIEnabled(false);
            try
            {
                var session = await loginHandler.Authenticate();
                txtAccessToken.Text = session.AccessToken;
                txtUsername.Text = session.Username;
                txtUUID.Text = session.UUID;
                txtXUID.Text = session.Xuid;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            setUIEnabled(true);
        }

        private async void btnLogout_Click(object sender, EventArgs e)
        {
            setUIEnabled(false);
            try
            {
                await loginHandler.SignoutWithBrowser();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            setUIEnabled(true);
        }
    }
}
