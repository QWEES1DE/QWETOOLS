using System.Drawing;
using System.Reflection;

namespace RobRunPotato;

internal sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(13, 12, 15);
    private static readonly Color Panel = Color.FromArgb(21, 19, 24);
    private static readonly Color Surface = Color.FromArgb(31, 29, 34);
    private static readonly Color Fg = Color.FromArgb(243, 240, 242);
    private static readonly Color Dim = Color.FromArgb(153, 147, 153);
    private static readonly Color Accent = Color.FromArgb(241, 43, 65);
    private static readonly Color Success = Color.FromArgb(171, 220, 71);

    private readonly ComboBox _exeBox = new();
    private readonly Button _browseBtn = new();
    private readonly Button _findBtn = new();
    private readonly PresetSlider _presetBar = new();
    private readonly Label _presetName = new();
    private readonly Label _presetDesc = new();
    private readonly Label _presetStep = new();
    private readonly CheckBox _customLodCheck = new();
    private readonly TrackBar _lodBar = new();
    private readonly Label _lodValue = new();
    private readonly TextBox _log = new();
    private readonly ListView _preview = new();
    private readonly Label _logHeading = new();
    private readonly Button _applyBtn = new();
    private readonly Button _readBtn = new();
    private readonly Button _resetBtn = new();
    private readonly Button _diagBtn = new();
    private readonly Button _wipeBtn = new();
    private readonly Label _status = new();
    private readonly Label _driverBadge = new();

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobRunPotato", "last_exe.txt");

    public MainForm()
    {
        Text = "QWETOOLS — ROB RUN Potato Slider";
        BackColor = Bg;
        ForeColor = Fg;
        Font = new Font("Segoe UI", 9.5f);
        MinimumSize = new Size(480, 360);
        ClientSize = new Size(1020, 930);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("QWETOOLS.Icon");
        if (iconStream != null) Icon = new Icon(iconStream);
        BuildUi();
        Load += (_, _) => { FitToWorkingArea(); OnLoaded(); };
    }

    // Fit the initial window to the usable desktop (excluding the taskbar).
    private void FitToWorkingArea()
    {
        var area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }

    // ------------------------------------------------------------------ UI
    private void BuildUi()
    {
        var scroll = new System.Windows.Forms.Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg };
        Controls.Add(scroll);
        var root = new TableLayoutPanel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left, MinimumSize = new Size(740, 860), Padding = new Padding(28, 22, 28, 16),
            ColumnCount = 1, RowCount = 6, BackColor = Bg
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 290));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        scroll.Controls.Add(root);

        // A fill-docked child is clipped to the viewport instead of establishing
        // a scrollable extent. Size the undocked content to at least its minimum
        // and let AutoScroll expose anything that does not fit.
        void SizeContent()
        {
            int width = scroll.ClientSize.Width;
            int height = scroll.ClientSize.Height;
            if (root.MinimumSize.Height > height)
                width -= SystemInformation.VerticalScrollBarWidth;
            if (root.MinimumSize.Width > width)
                height -= SystemInformation.HorizontalScrollBarHeight;
            root.Size = new Size(Math.Max(root.MinimumSize.Width, width), Math.Max(root.MinimumSize.Height, height));
        }
        scroll.ClientSizeChanged += (_, _) => SizeContent();
        scroll.HandleCreated += (_, _) => SizeContent();
        SizeContent();

        // Logo / status
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, BackColor = Bg };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        var avatar = new PictureBox { Dock = DockStyle.Top, SizeMode = PictureBoxSizeMode.Zoom, Height = 64, Width = 64, Margin = new Padding(0, 0, 12, 0), BackColor = Surface };
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("QWETOOLS.Avatar"))
            if (stream != null) { using var image = Image.FromStream(stream); avatar.Image = new Bitmap(image); }
        header.Controls.Add(avatar, 0, 0);
        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = Bg };
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        var brandName = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty, BackColor = Bg };
        brandName.Controls.Add(new Label { Text = "QWE", Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = true, ForeColor = Fg, Margin = Padding.Empty });
        brandName.Controls.Add(new Label { Text = "TOOLS", Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = true, ForeColor = Accent, Margin = Padding.Empty });
        brand.Controls.Add(brandName, 0, 0);
        brand.Controls.Add(MakeLabel("ROB RUN · POTATO SLIDER", 9, FontStyle.Bold, Fg), 0, 1);
        brand.Controls.Add(MakeLabel("Настройка профиля NVIDIA. В игру ничего не внедряется.", 8.5f, FontStyle.Regular, Dim), 0, 2);
        header.Controls.Add(brand, 1, 0);
        _driverBadge.Text = "●  ПРОВЕРКА NVIDIA"; _driverBadge.Dock = DockStyle.Fill;
        _driverBadge.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold); _driverBadge.ForeColor = Dim;
        _driverBadge.TextAlign = ContentAlignment.TopRight;
        header.Controls.Add(_driverBadge, 2, 0);
        root.Controls.Add(header, 0, 0);

        // Game executable
        var game = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = Bg };
        game.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); game.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        game.Controls.Add(MakeLabel("ФАЙЛ ИГРЫ (.EXE)", 8, FontStyle.Bold, Dim), 0, 0);
        var gameInput = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, BackColor = Bg };
        gameInput.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gameInput.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        gameInput.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        _exeBox.Dock = DockStyle.Fill; _exeBox.Margin = new Padding(0, 5, 9, 0);
        _exeBox.DropDownStyle = ComboBoxStyle.DropDown; _exeBox.BackColor = Surface; _exeBox.ForeColor = Fg; _exeBox.FlatStyle = FlatStyle.Flat;
        _exeBox.AccessibleName = "Файл игры (.exe)";
        StyleButton(_browseBtn, "Обзор…", false); StyleButton(_findBtn, "Найти", false);
        _findBtn.Click += (_, _) => AutoFind(showMessage: true); _browseBtn.Click += (_, _) => Browse();
        gameInput.Controls.Add(_exeBox, 0, 0); gameInput.Controls.Add(_browseBtn, 1, 0); gameInput.Controls.Add(_findBtn, 2, 0);
        game.Controls.Add(gameInput, 0, 1); root.Controls.Add(game, 0, 1);

        // Preset and optional LOD
        var preset = Card(6); preset.Margin = new Padding(0, 0, 0, 12);
        preset.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        preset.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        preset.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        preset.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        preset.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        preset.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var presetTop = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Panel };
        presetTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); presetTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _presetStep.Dock = DockStyle.Fill; _presetStep.Font = new Font("Segoe UI", 15, FontStyle.Bold); _presetStep.ForeColor = Accent;
        _presetName.Dock = DockStyle.Fill; _presetName.Font = new Font("Segoe UI", 16, FontStyle.Bold); _presetName.ForeColor = Fg;
        presetTop.Controls.Add(_presetStep, 0, 0); presetTop.Controls.Add(_presetName, 1, 0); preset.Controls.Add(presetTop, 0, 0);
        _presetDesc.Dock = DockStyle.Fill; _presetDesc.ForeColor = Dim; _presetDesc.Font = new Font("Segoe UI", 9);
        preset.Controls.Add(_presetDesc, 0, 1);
        _presetBar.Dock = DockStyle.Fill; _presetBar.BackColor = Panel; _presetBar.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        _presetBar.ValueChanged += (_, _) => UpdatePreview(); preset.Controls.Add(_presetBar, 0, 2);
        var lodTitle = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Surface };
        lodTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); lodTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        _customLodCheck.Text = "Свой LOD Bias вместо значения из пресета"; _customLodCheck.Dock = DockStyle.Fill;
        _customLodCheck.ForeColor = Fg; _customLodCheck.Padding = new Padding(10, 0, 0, 0);
        _customLodCheck.CheckedChanged += (_, _) => { _lodBar.Enabled = _customLodCheck.Checked; UpdatePreview(); };
        _lodValue.Dock = DockStyle.Fill; _lodValue.Font = new Font("Consolas", 10, FontStyle.Bold);
        _lodValue.ForeColor = Success; _lodValue.TextAlign = ContentAlignment.MiddleRight; _lodValue.Padding = new Padding(0, 0, 10, 0);
        lodTitle.Controls.Add(_customLodCheck, 0, 0); lodTitle.Controls.Add(_lodValue, 1, 0); preset.Controls.Add(lodTitle, 0, 3);
        _lodBar.Dock = DockStyle.Fill; _lodBar.Minimum = -24; _lodBar.Maximum = 120;
        _lodBar.SmallChange = 1; _lodBar.LargeChange = 4; _lodBar.TickFrequency = 8;
        _lodBar.Value = 24; _lodBar.Enabled = false; _lodBar.BackColor = Panel;
        _lodBar.AccessibleName = "Ручное значение LOD Bias";
        _lodBar.ValueChanged += (_, _) => UpdatePreview(); preset.Controls.Add(_lodBar, 0, 4);
        preset.Controls.Add(MakeLabel("LOD влияет на текстуры, но не на дальность прорисовки. DX12 и Vulkan могут игнорировать настройки.", 8.5f, FontStyle.Regular, Dim), 0, 5);
        root.Controls.Add(preset, 0, 2);

        // Preview values and operation output
        var details = Card(2); details.Margin = new Padding(0, 0, 0, 8);
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var detailsHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Panel };
        detailsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); detailsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        _logHeading.Dock = DockStyle.Fill; _logHeading.Font = new Font("Segoe UI", 9, FontStyle.Bold); _logHeading.ForeColor = Dim;
        _logHeading.TextAlign = ContentAlignment.MiddleLeft;
        detailsHeader.Controls.Add(_logHeading, 0, 0);
        var previewBtn = new Button(); StyleButton(previewBtn, "Показать план", false); previewBtn.Click += (_, _) => UpdatePreview();
        detailsHeader.Controls.Add(previewBtn, 1, 0); details.Controls.Add(detailsHeader, 0, 0);
        var output = new System.Windows.Forms.Panel { Dock = DockStyle.Fill, BackColor = Bg, Margin = Padding.Empty };
        _preview.Dock = DockStyle.Fill; _preview.View = View.Details; _preview.FullRowSelect = true; _preview.HeaderStyle = ColumnHeaderStyle.None;
        _preview.BackColor = Bg; _preview.ForeColor = Fg; _preview.BorderStyle = BorderStyle.None;
        _preview.Columns.Add("Настройка", 350); _preview.Columns.Add("Значение", 260);
        _preview.Resize += (_, _) => { if (_preview.Columns.Count == 2) { _preview.Columns[0].Width = Math.Max(150, _preview.ClientSize.Width * 58 / 100); _preview.Columns[1].Width = Math.Max(100, _preview.ClientSize.Width - _preview.Columns[0].Width - 5); } };
        _log.Dock = DockStyle.Fill; _log.Multiline = true; _log.ReadOnly = true; _log.ScrollBars = ScrollBars.Vertical; _log.WordWrap = true;
        _log.BackColor = Bg; _log.ForeColor = Fg; _log.BorderStyle = BorderStyle.None; _log.Font = new Font("Consolas", 9);
        output.Controls.Add(_log); output.Controls.Add(_preview); details.Controls.Add(output, 0, 1); root.Controls.Add(details, 0, 3);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 7, 0, 7), Margin = Padding.Empty, BackColor = Bg };
        for (int i = 0; i < 5; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        StyleButton(_applyBtn, "Применить", true); StyleButton(_readBtn, "Текущие", false);
        StyleButton(_diagBtn, "Диагностика", false); StyleButton(_resetBtn, "Сбросить всё", false);
        StyleButton(_wipeBtn, "Удалить профили", false);
        actions.Controls.Add(_applyBtn, 0, 0); actions.Controls.Add(_readBtn, 1, 0); actions.Controls.Add(_diagBtn, 2, 0); actions.Controls.Add(_resetBtn, 3, 0); actions.Controls.Add(_wipeBtn, 4, 0);
        _applyBtn.Click += (_, _) => Apply(Presets.All[_presetBar.Value], CustomLod());
        _readBtn.Click += (_, _) => ReadCurrent(); _diagBtn.Click += (_, _) => RunDiagnostics();
        _resetBtn.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Сбросить настройки драйвера для выбранной игры?\n\nВсе профили, в которые входит этот exe (включая созданные в Панели управления NVIDIA), будут удалены, после чего будет создан чистый профиль со стандартными значениями.", "Сброс настроек", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _customLodCheck.Checked = false; _presetBar.Value = 0; Apply(Presets.All[0], null);
        };
        _wipeBtn.Click += (_, _) => WipeProfiles();
        root.Controls.Add(actions, 0, 4);
        _status.Dock = DockStyle.Fill; _status.ForeColor = Dim; _status.Text = "Проверка драйвера NVIDIA…";
        root.Controls.Add(_status, 0, 5); AcceptButton = _applyBtn;
    }

    private static TableLayoutPanel Card(int rows)
    {
        var card = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rows,
            BackColor = Panel, Padding = new Padding(20, 14, 20, 12), Margin = Padding.Empty };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(59, 53, 59));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };
        return card;
    }

    private static Label MakeLabel(string text, float size, FontStyle style, Color color) => new()
    {
        Text = text, Dock = DockStyle.Fill, Margin = Padding.Empty,
        Font = new Font("Segoe UI", size, style), ForeColor = color
    };

    private static void StyleButton(Button b, string text, bool primary)
    {
        b.Text = text; b.Dock = DockStyle.Fill; b.Margin = new Padding(0, 3, 7, 3);
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.FlatAppearance.BorderColor = Color.FromArgb(68, 62, 68);
        b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(255, 64, 81) : Color.FromArgb(45, 41, 47);
        b.BackColor = primary ? Accent : Surface; b.ForeColor = Fg;
        b.Font = new Font("Segoe UI", 9, FontStyle.Bold); b.Cursor = Cursors.Hand;
    }

    private void ShowLog()
    {
        _preview.Visible = false;
        _log.Visible = true;
        _log.Clear();
        _log.BringToFront();
    }

    // ------------------------------------------------------------------ Логика
    private void OnLoaded()
    {
        try
        {
            NvApi.Initialize();
            _driverBadge.Text = "●  NVIDIA ДОСТУПЕН"; _driverBadge.ForeColor = Success;
            SetStatus("Драйвер NVIDIA найден. Выберите игру и пресет.", Success);
        }
        catch (Exception ex)
        {
            _driverBadge.Text = "●  NVIDIA НЕДОСТУПЕН"; _driverBadge.ForeColor = Color.IndianRed;
            SetStatus("NVAPI недоступен: " + ex.Message, Color.IndianRed);
            _applyBtn.Enabled = _readBtn.Enabled = _resetBtn.Enabled = false;
        }

        try
        {
            if (File.Exists(ConfigPath))
                _exeBox.Text = File.ReadAllText(ConfigPath).Trim();
        }
        catch (IOException) { /* сохранённый путь необязателен */ }
        catch (UnauthorizedAccessException) { /* сохранённый путь необязателен */ }
        if (string.IsNullOrWhiteSpace(_exeBox.Text))
            AutoFind(showMessage: false);

        _presetBar.Value = Presets.All.Length - 2; // по умолчанию «Potato»
        UpdatePreview();
    }

    private double? CustomLod() => _customLodCheck.Checked ? _lodBar.Value / 8.0 : null;

    private string? ExeName()
    {
        string t = _exeBox.Text.Trim().Trim('"');
        if (t.Length == 0) return null;
        string name = Path.GetFileName(t);
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : null;
    }

    private void UpdatePreview()
    {
        var preset = Presets.All[_presetBar.Value];
        _presetName.Text = preset.Name;
        _presetStep.Text = $"{_presetBar.Value + 1} / {Presets.All.Length}";
        _logHeading.Text = "БУДЕТ УСТАНОВЛЕНО";
        _presetDesc.Text = preset.Description;
        double? shownLod = CustomLod() ?? preset.Lod;
        _lodValue.Text = shownLod.HasValue ? NvSetting.FormatLod((int)Math.Round(shownLod.Value * 8)) : "СБРОС";

        var values = DriverProfile.BuildValues(preset, CustomLod());
        _preview.BeginUpdate();
        _preview.Items.Clear();
        if (values.Count == 0)
            _preview.Items.Add(new ListViewItem(new[] { "Настройки программы", "Сброс" }));
        else
        {
            foreach (var (id, value) in values)
                _preview.Items.Add(new ListViewItem(new[] { NvSetting.Name(id), NvSetting.Describe(id, value) }));
            _preview.Items.Add(new ListViewItem(new[] { "Остальные настройки программы", "Сброс" }));
        }
        _preview.EndUpdate();
        _log.Visible = false;
        _preview.Visible = true;
        _preview.BringToFront();
    }

    private void Browse()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Выберите exe-файл ROB RUN (не лаунчер!)",
            Filter = "Программы (*.exe)|*.exe",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _exeBox.Text = dlg.FileName;
            SaveExe();
        }
    }

    private void AutoFind(bool showMessage)
    {
        var found = new List<string>();
        var diag = Diagnostics.Run();
        found.AddRange(diag.RunningGameExes);
        if (diag.RealExePath != null) found.Add(diag.RealExePath);
        found.AddRange(GameLocator.FindCandidates());
        found = found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _exeBox.Items.Clear();
        foreach (var f in found) _exeBox.Items.Add(f);

        if (found.Count > 0)
        {
            _exeBox.Text = found[0];
            SaveExe();
            SetStatus($"Найдено: {Path.GetFileName(found[0])}" + (found.Count > 1 ? $" (+ ещё {found.Count - 1} в списке)" : ""), Success);
        }
        else if (showMessage)
        {
            MessageBox.Show(this,
                "Не удалось найти игру автоматически.\n\n" +
                "Подсказка: запустите ROB RUN и нажмите «Найти» ещё раз, тогда exe определится по процессу.\n" +
                "Или укажите файл вручную через «Обзор…» (Steam → ROB RUN → Управление → Просмотреть локальные файлы).",
                "Поиск игры", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SaveExe()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, _exeBox.Text.Trim());
        }
        catch { /* не критично */ }
    }

    /// <summary>Удаляет все профили драйвера, в которые входит выбранный exe (кнопка «Удалить профили»).</summary>
    private void WipeProfiles()
    {
        string? exe = ExeName();
        if (exe == null)
        {
            MessageBox.Show(this, "Сначала укажите exe-файл игры.", "Нет файла игры", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show(this,
                $"Удалить все профили драйвера, в которые входит «{exe}»?\n\n" +
                "Это уберёт и профили, созданные/изменённые в Панели управления NVIDIA, чтобы ни одна их настройка не была главнее. " +
                "Профили других игр не тронем. Пресет применён не будет — после очистки нажмите «Применить».",
                "Очистка профилей", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        SaveExe();
        ShowLog();
        _logHeading.Text = "Результат очистки";
        try
        {
            Cursor = Cursors.WaitCursor;
            var log = DriverProfile.Cleanup(exe);
            _log.Lines = log.ToArray();
            SetStatus("Профили для выбранной игры удалены. Нажмите «Применить», чтобы создать чистый профиль.", Success);
        }
        catch (Exception ex)
        {
            _log.AppendText(Environment.NewLine + "ОШИБКА: " + ex.Message);
            SetStatus("Ошибка: " + ex.Message, Color.IndianRed);
        }
        finally { Cursor = Cursors.Default; }
    }

    private void Apply(Preset preset, double? customLod)
    {
        string? exe = ExeName();
        if (exe == null)
        {
            MessageBox.Show(this, "Сначала укажите exe-файл игры.", "Нет файла игры", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        string lower = exe.ToLowerInvariant();
        if (lower.StartsWith("start_protected") || lower.StartsWith("unitycrashhandler") || lower.Contains("launcher") || lower.StartsWith("easyanticheat"))
        {
            if (MessageBox.Show(this,
                    $"«{exe}» — это лаунчер/служебный файл, а не сама игра. Драйвер применяет профиль к процессу, " +
                    "который рисует картинку, поэтому настройки не подействуют.\n\nНажмите «Диагностика» при запущенной игре, " +
                    "чтобы найти правильный exe.\n\nВсё равно применить?",
                    "Похоже, выбран не тот exe", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
        }
        SaveExe();
        ShowLog();
        _logHeading.Text = "РЕЗУЛЬТАТ ПРИМЕНЕНИЯ";
        try
        {
            Cursor = Cursors.WaitCursor;
            var log = DriverProfile.Apply(exe, preset, customLod);
            _logHeading.Text = "Результат применения";
            _log.Lines = log.ToArray();
            SetStatus($"Готово: «{preset.Name}» для {exe}. Перезапустите игру.", Success);
        }
        catch (Exception ex)
        {
            _log.AppendText(Environment.NewLine + "ОШИБКА: " + ex.Message);
            SetStatus("Ошибка: " + ex.Message, Color.IndianRed);
        }
        finally { Cursor = Cursors.Default; }
    }

    private void ReadCurrent()
    {
        string? exe = ExeName();
        if (exe == null)
        {
            MessageBox.Show(this, "Сначала укажите exe-файл игры.", "Нет файла игры", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ShowLog();
        try
        {
            _logHeading.Text = "Текущие настройки";
            var cur = DriverProfile.ReadCurrent(exe);
            if (cur == null)
            {
                _log.Lines = new[] { $"Для {exe} профиля в драйвере пока нет: работают стандартные настройки." };
                return;
            }
            var lines = new List<string> { $"Сейчас в профиле «{DriverProfile.LastProfileName}» ({exe}):" };
            if (cur.Count == 0) lines.Add("  (ничего из наших настроек не изменено)");
            foreach (var (id, v) in cur)
                lines.Add($"{NvSetting.Name(id)}: {NvSetting.Describe(id, v)}");
            _log.Lines = lines.ToArray();
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка: " + ex.Message, Color.IndianRed);
        }
    }

    private void RunDiagnostics()
    {
        ShowLog();
        Cursor = Cursors.WaitCursor;
        Diagnostics.Result r;
        try { r = Diagnostics.Run(); }
        catch (Exception ex)
        {
            _logHeading.Text = "Результат диагностики";
            _log.Text = "Не удалось выполнить диагностику: " + ex.Message;
            SetStatus("Ошибка диагностики: " + ex.Message, Color.IndianRed);
            return;
        }
        finally { Cursor = Cursors.Default; }

        string? selected = ExeName();
        var lines = Diagnostics.Report(r, selected);

        // Покажем, к какому профилю привязан выбранный exe
        if (selected != null)
        {
            try
            {
                var cur = DriverProfile.ReadCurrent(selected);
                lines.Add("");
                lines.Add(cur == null
                    ? $"[4] {selected} не привязан ни к одному профилю драйвера."
                    : $"[4] {selected} → профиль «{DriverProfile.LastProfileName}», наших настроек задано: {cur.Count}.");
            }
            catch (Exception ex) { lines.Add("[4] Ошибка чтения профиля: " + ex.Message); }
        }
        _logHeading.Text = "Результат диагностики";
        _log.Lines = lines.ToArray();
        _log.SelectionStart = 0; _log.ScrollToCaret();

        string? real = r.RunningGameExes.FirstOrDefault() ?? r.RealExePath;
        if (real != null && !Path.GetFileName(real).Equals(selected, StringComparison.OrdinalIgnoreCase))
        {
            if (MessageBox.Show(this,
                    $"Игра на самом деле запускается как:\n{real}\n\nЗаменить exe и применить выбранный уровень заново?",
                    "Найден правильный exe", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _exeBox.Text = real;
                SaveExe();
                Apply(Presets.All[_presetBar.Value], CustomLod());
            }
        }
        else if (r.IsDx12 || r.IsVulkan)
        {
            SetStatus("Игра работает на DX12/Vulkan — LOD Bias через драйвер там не работает. См. лог.", Color.Orange);
        }
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.ForeColor = color;
    }
}
