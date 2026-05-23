using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

public class UltraStarSimplificadoForm : Form
{
    private const string AppVersion = "v1.0";

    private readonly TextBox searchBox = new TextBox();
    private readonly FlowLayoutPanel songGrid = new FlowLayoutPanel();
    private readonly Panel infoPanel = new Panel();
    private readonly Panel previewPanel = new Panel();
    private readonly Label previewArtist = new Label();
    private readonly Label previewTitle = new Label();
    private readonly Label previewMeta = new Label();
    private readonly Label previewCreator = new Label();
    private readonly Label songCountLabel = new Label();
    private readonly Panel playerPanel = new Panel();
    private readonly Panel videoPanel = new Panel();
    private readonly KaraokeLineView lyricLine = new KaraokeLineView();
    private readonly NextLineView nextLine = new NextLineView();
    private readonly Panel lyricsOverlay = new Panel();
    private readonly Panel pitchOverlay = new Panel();
    private readonly Panel vocalOverlay = new Panel();
    private readonly Label titleLabel = new Label();
    private readonly Label hintLabel = new Label();
    private readonly TrackBar pitchBar = new TrackBar();
    private readonly TrackBar vocalBar = new TrackBar();
    private readonly Label versionLabel = new Label();
    private readonly Timer timer = new Timer();
    private readonly Timer previewFadeTimer = new Timer();
    private readonly List<Song> songs = new List<Song>();
    private readonly List<Song> visibleSongs = new List<Song>();
    private readonly List<Panel> songCards = new List<Panel>();
    private readonly List<Phrase> phrases = new List<Phrase>();

    private string songsRoot = "";
    private string mpvPath = "";
    private string pipeName = "";
    private string previewPipeName = "";
    private Process mpv;
    private Process previewMpv;
    private Song previewSong;
    private DateTime previewFadeStarted;
    private bool isFullscreen = false;
    private int currentPhraseIndex = -1;
    private int selectedSongIndex = -1;
    private Panel selectedCard;
    private Song currentSong;
    private bool usingInstrumental;

    public UltraStarSimplificadoForm()
    {
        Text = "UltraStar Simplificado " + AppVersion;
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1280;
        Height = 720;
        MinimumSize = new Size(980, 620);
        WindowState = FormWindowState.Normal;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(12, 12, 14);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 11);
        KeyPreview = true;
        ApplyBackground(this);

        titleLabel.Text = "";
        titleLabel.Font = new Font("Segoe UI", 34, FontStyle.Regular);
        titleLabel.Left = 24;
        titleLabel.Top = 22;
        titleLabel.Width = 760;
        titleLabel.Height = 70;
        hintLabel.Text = "";
        hintLabel.ForeColor = Color.FromArgb(180, 180, 185);
        hintLabel.Left = 28;
        hintLabel.Top = 24;
        hintLabel.Width = 1;
        hintLabel.Height = 1;

        songCountLabel.Left = ClientSize.Width - 230;
        songCountLabel.Top = 24;
        songCountLabel.Width = 200;
        songCountLabel.Height = 38;
        songCountLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        songCountLabel.Font = new Font("Segoe UI", 18, FontStyle.Bold);
        songCountLabel.ForeColor = Color.White;
        songCountLabel.BackColor = Color.FromArgb(105, 85, 0, 95);
        songCountLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(songCountLabel);

        infoPanel.Left = 18;
        infoPanel.Top = 78;
        infoPanel.Width = 330;
        infoPanel.Height = 500;
        infoPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        infoPanel.BackColor = Color.FromArgb(140, 105, 0, 120);
        Controls.Add(infoPanel);

        previewPanel.Left = 14;
        previewPanel.Top = 18;
        previewPanel.Width = 302;
        previewPanel.Height = 170;
        previewPanel.BackColor = Color.Black;
        previewPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        infoPanel.Controls.Add(previewPanel);

        previewArtist.Left = 18;
        previewArtist.Top = 204;
        previewArtist.Width = 292;
        previewArtist.Height = 48;
        previewArtist.Font = new Font("Segoe UI", 22, FontStyle.Bold);
        previewArtist.ForeColor = Color.White;
        previewArtist.BackColor = Color.Transparent;
        previewArtist.TextAlign = ContentAlignment.MiddleCenter;
        infoPanel.Controls.Add(previewArtist);

        previewTitle.Left = 18;
        previewTitle.Top = 286;
        previewTitle.Width = 292;
        previewTitle.Height = 46;
        previewTitle.Font = new Font("Segoe UI", 18, FontStyle.Regular);
        previewTitle.ForeColor = Color.White;
        previewTitle.BackColor = Color.Transparent;
        previewTitle.TextAlign = ContentAlignment.MiddleCenter;
        infoPanel.Controls.Add(previewTitle);

        previewMeta.Left = 18;
        previewMeta.Top = 374;
        previewMeta.Width = 292;
        previewMeta.Height = 34;
        previewMeta.Font = new Font("Segoe UI", 12, FontStyle.Regular);
        previewMeta.ForeColor = Color.White;
        previewMeta.BackColor = Color.Transparent;
        previewMeta.TextAlign = ContentAlignment.MiddleCenter;
        infoPanel.Controls.Add(previewMeta);

        previewCreator.Left = 18;
        previewCreator.Top = 438;
        previewCreator.Width = 292;
        previewCreator.Height = 34;
        previewCreator.Font = new Font("Segoe UI", 11, FontStyle.Regular);
        previewCreator.ForeColor = Color.White;
        previewCreator.BackColor = Color.Transparent;
        previewCreator.TextAlign = ContentAlignment.MiddleCenter;
        infoPanel.Controls.Add(previewCreator);

        searchBox.Left = 560;
        searchBox.Top = ClientSize.Height - 58;
        searchBox.Width = 540;
        searchBox.Height = 30;
        searchBox.Anchor = AnchorStyles.Bottom;
        searchBox.Font = new Font("Segoe UI", 13);
        searchBox.BackColor = Color.FromArgb(48, 10, 48);
        searchBox.ForeColor = Color.White;
        searchBox.TextChanged += delegate { RenderSongGrid(); };
        searchBox.KeyDown += SearchBoxKeyDown;
        Controls.Add(searchBox);

        songGrid.Left = 400;
        songGrid.Top = 78;
        songGrid.Width = ClientSize.Width - 420;
        songGrid.Height = ClientSize.Height - 156;
        songGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        songGrid.AutoScroll = true;
        songGrid.BackColor = Color.FromArgb(32, 12, 38);
        EnableDoubleBuffer(songGrid);
        Controls.Add(songGrid);

        playerPanel.Dock = DockStyle.Fill;
        playerPanel.BackColor = Color.FromArgb(46, 18, 78);
        ApplyBackground(playerPanel);
        playerPanel.Visible = false;
        Controls.Add(playerPanel);

        videoPanel.Dock = DockStyle.Fill;
        videoPanel.BackColor = Color.Black;
        playerPanel.Controls.Add(videoPanel);

        lyricsOverlay.Height = 150;
        lyricsOverlay.Dock = DockStyle.Bottom;
        lyricsOverlay.BackColor = Color.FromArgb(12, 12, 14);
        playerPanel.Controls.Add(lyricsOverlay);
        lyricsOverlay.BringToFront();

        lyricLine.Height = 86;
        lyricLine.Dock = DockStyle.Top;
        lyricsOverlay.Controls.Add(lyricLine);

        nextLine.Height = 54;
        nextLine.Dock = DockStyle.Bottom;
        nextLine.Font = new Font("Segoe UI", 22, FontStyle.Bold);
        nextLine.BackColor = Color.FromArgb(12, 12, 14);
        lyricsOverlay.Controls.Add(nextLine);

        SetupSliderOverlay(pitchOverlay, "Tono", pitchBar, -12, 12, 0);
        SetupSliderOverlay(vocalOverlay, "Karaoke", vocalBar, 0, 100, 0);
        playerPanel.Controls.Add(pitchOverlay);
        playerPanel.Controls.Add(vocalOverlay);
        pitchOverlay.BringToFront();
        vocalOverlay.BringToFront();
        LayoutSliderOverlays();
        playerPanel.Resize += delegate { LayoutSliderOverlays(); };
        pitchBar.ValueChanged += delegate { ApplyAudioFilters(); };
        vocalBar.ValueChanged += delegate { ApplyAudioFilters(); };

        versionLabel.Text      = AppVersion;
        versionLabel.Font      = new Font("Segoe UI", 9, FontStyle.Regular);
        versionLabel.ForeColor = Color.FromArgb(100, 100, 110);
        versionLabel.BackColor = Color.Transparent;
        versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        versionLabel.Width     = 60;
        versionLabel.Height    = 22;
        versionLabel.Left      = 18;
        versionLabel.Top       = ClientSize.Height - 26;
        versionLabel.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(versionLabel);

        timer.Interval = 80;
        timer.Tick += delegate { UpdateKaraoke(); };
        previewFadeTimer.Interval = 90;
        previewFadeTimer.Tick += delegate { FadePreviewVolume(); };
        FormClosing += delegate { StopMpv(); StopPreviewMpv(); };
        KeyDown += HandleKeys;
        Shown += delegate { LoadSongs(); };
    }

    private void LayoutVideoPanel()
    {
        videoPanel.Bounds = playerPanel.ClientRectangle;
    }

    private void LayoutSliderOverlays()
    {
        pitchOverlay.Left = 18;
        pitchOverlay.Top = 18;
        vocalOverlay.Left = Math.Max(18, playerPanel.ClientSize.Width - vocalOverlay.Width - 18);
        vocalOverlay.Top = 18;
    }

    private void EnableDoubleBuffer(Control control)
    {
        var property = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (property != null) property.SetValue(control, true, null);
    }

    private Label MakeLabel(string text, int left)
    {
        return new Label { Text = text, Left = left, Top = 11, Width = 72, Height = 24, ForeColor = Color.White, BackColor = Color.FromArgb(16, 16, 20) };
    }

    private void SetupBar(TrackBar bar, int min, int max, int value, int left, Control parent)
    {
        bar.Left = left;
        bar.Top = 3;
        bar.Width = 210;
        bar.Height = 42;
        bar.Minimum = min;
        bar.Maximum = max;
        bar.Value = value;
        bar.SmallChange = 1;
        bar.LargeChange = 2;
        bar.TickFrequency = min < 0 ? 1 : 10;
        parent.Controls.Add(bar);
    }

    private void SetupSliderOverlay(Panel panel, string label, TrackBar bar, int min, int max, int value)
    {
        panel.Width = 310;
        panel.Height = 48;
        panel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        panel.BackColor = Color.FromArgb(16, 16, 20);
        panel.Paint += PaintSliderOverlay;
        panel.Controls.Add(MakeLabel(label, 14));
        SetupBar(bar, min, max, value, 84, panel);
    }

    private void PaintSliderOverlay(object sender, PaintEventArgs e)
    {
        using (var pen = new Pen(Color.FromArgb(255, 0, 170), 2))
        {
            var panel = sender as Panel;
            if (panel == pitchOverlay) DrawZeroMarker(e.Graphics, pitchBar, -12, 12, pen);
            else if (panel == vocalOverlay) DrawZeroMarker(e.Graphics, vocalBar, 0, 100, pen);
        }
    }

    private void DrawZeroMarker(Graphics graphics, TrackBar bar, int min, int max, Pen pen)
    {
        var usableLeft = bar.Left + 16;
        var usableWidth = bar.Width - 32;
        var ratio = (0.0 - min) / (max - min);
        var x = usableLeft + (int)(usableWidth * ratio);
        graphics.DrawLine(pen, x, bar.Top + 4, x, bar.Top + bar.Height - 7);
    }

    private void LoadSongs()
    {
        mpvPath = FindTool("mpv.exe");
        songsRoot = FindSongsRoot();
        if (songsRoot.Length == 0)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Elige la carpeta donde estan tus canciones UltraStar";
                if (dialog.ShowDialog() != DialogResult.OK) return;
                songsRoot = dialog.SelectedPath;
            }
        }

        songs.Clear();
        songGrid.Controls.Clear();
        foreach (var txt in Directory.GetFiles(songsRoot, "*.txt", SearchOption.AllDirectories))
        {
            var song = ReadSongInfo(txt);
            if (song != null) songs.Add(song);
        }
        songs.Sort((a, b) => String.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
        RenderSongGrid();
        songCountLabel.Text = songs.Count + " canciones";
    }

    private void ApplyBackground(Control control)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fondo.jpg");
        if (!File.Exists(path)) path = Path.Combine(Environment.CurrentDirectory, "fondo.jpg");
        if (File.Exists(path))
        {
            control.BackgroundImage = Image.FromFile(path);
            control.BackgroundImageLayout = ImageLayout.Stretch;
        }
    }

    private string FindSongsRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "songs"),
            Path.Combine(Environment.CurrentDirectory, "songs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UltraStar Songs"),
            @"C:\PROYECTOS JORDI\CANCIONES ULTRASTAR",
            @"C:\Program Files (x86)\UltraStar WorldParty\songs"
        };
        foreach (var path in candidates)
        {
            if (Directory.Exists(path) && Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories).Length > 0) return path;
        }
        return "";
    }

    private string FindTool(string name)
    {
        var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
        if (File.Exists(local)) return local;
        var dist = Path.Combine(Environment.CurrentDirectory, "dist", name);
        if (File.Exists(dist)) return dist;
        return name;
    }

    private Song ReadSongInfo(string txtPath)
    {
        var song = new Song { TxtPath = txtPath, Folder = Path.GetDirectoryName(txtPath), Title = Path.GetFileNameWithoutExtension(txtPath) };
        foreach (var raw in ReadUltraStarLines(txtPath))
        {
            if (raw.StartsWith("#TITLE:")) song.Title = raw.Substring(7).Trim();
            else if (raw.StartsWith("#ARTIST:")) song.Artist = raw.Substring(8).Trim();
            else if (raw.StartsWith("#YEAR:")) song.Year = raw.Substring(6).Trim();
            else if (raw.StartsWith("#CREATOR:")) song.Creator = raw.Substring(9).Trim();
            else if (raw.StartsWith("#GENRE:")) song.Genre = raw.Substring(7).Trim();
            else if (raw.StartsWith("#COVER:")) song.Cover = Existing(song.Folder, raw.Substring(7).Trim());
            else if (raw.StartsWith("#VIDEO:")) song.Media = Existing(song.Folder, raw.Substring(7).Trim());
            else if (raw.StartsWith("#MP3:")) song.Audio = Existing(song.Folder, raw.Substring(5).Trim());
        }
        if (song.Cover.Length == 0) song.Cover = FirstFile(song.Folder, new[] { "cover*.jpg", "cover*.jpeg", "cover*.png", "*[CO].jpg", "*[CO].jpeg", "*.jpg", "*.jpeg", "*.png" });
        if (song.Media.Length == 0) song.Media = FirstFile(song.Folder, new[] { "*.mp4", "*.avi", "*.divx", "*.mkv", "*.webm", "*.mov", "*.flv", "*.m4v" });
        if (song.Audio.Length == 0) song.Audio = FirstFile(song.Folder, new[] { "*.mp3", "*.m4a", "*.ogg", "*.wav" });
        song.NormalizedAudio = FirstFile(song.Folder, new[] { "audio_normalizado.mp3" });
        song.Instrumental = FirstFile(song.Folder, new[] { "instrumental.mp3", "instrumental.wav", "karaoke.mp3", "no_vocals.mp3" });
        song.NormalizedInstrumental = FirstFile(song.Folder, new[] { "instrumental_normalizado.mp3" });
        if (song.NormalizedAudio.Length > 0) song.Audio = song.NormalizedAudio;
        if (song.NormalizedInstrumental.Length > 0) song.Instrumental = song.NormalizedInstrumental;
        if (song.Media.Length == 0) song.Media = song.Audio;
        return song.Media.Length == 0 ? null : song;
    }

    private string Existing(string folder, string file)
    {
        var path = Path.Combine(folder, file);
        return File.Exists(path) ? path : "";
    }

    private string FirstFile(string folder, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var files = Directory.GetFiles(folder, pattern);
            if (files.Length > 0) return files[0];
        }
        return "";
    }

    private void RenderSongGrid()
    {
        visibleSongs.Clear();
        songCards.Clear();
        selectedCard = null;
        SuspendDrawing(songGrid);
        songGrid.Controls.Clear();
        var query = Normalize(searchBox.Text);
        foreach (var song in songs)
        {
            if (query.Length == 0 || Normalize(song.DisplayName).Contains(query)) visibleSongs.Add(song);
        }
        foreach (var song in visibleSongs) AddSongCard(song);
        selectedSongIndex = visibleSongs.Count > 0 ? 0 : -1;
        ResumeDrawing(songGrid);
        UpdateSelectedCard();
    }

    private void SuspendDrawing(Control control)
    {
        NativeMethods.SendMessage(control.Handle, 11, IntPtr.Zero, IntPtr.Zero);
    }

    private void ResumeDrawing(Control control)
    {
        NativeMethods.SendMessage(control.Handle, 11, new IntPtr(1), IntPtr.Zero);
        control.Refresh();
    }

    private string Normalize(string value)
    {
        var normalized = (value ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private void AddSongCard(Song song)
    {
        var card = new Panel { Width = 190, Height = 250, Margin = new Padding(10), BackColor = Color.FromArgb(28, 28, 34), Cursor = Cursors.Hand, Tag = song };
        card.Paint += PaintSongCard;
        var cover = new PictureBox { Left = 12, Top = 12, Width = 166, Height = 166, BackColor = Color.FromArgb(45, 45, 52), SizeMode = PictureBoxSizeMode.Zoom };
        if (song.Cover.Length > 0) cover.Image = LoadImageCopy(song.Cover);
        var artistLabel = new Label { Text = song.Artist.Length > 0 ? song.Artist : song.Title, Left = 12, Top = 184, Width = 166, Height = 24, ForeColor = Color.White, BackColor = card.BackColor, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoEllipsis = true };
        var titleCardLabel = new Label { Text = song.Artist.Length > 0 ? song.Title : "", Left = 12, Top = 210, Width = 166, Height = 32, ForeColor = Color.White, BackColor = card.BackColor, TextAlign = ContentAlignment.TopCenter, Font = new Font("Segoe UI", 9, FontStyle.Regular), AutoEllipsis = true };
        card.Controls.Add(cover);
        card.Controls.Add(artistLabel);
        card.Controls.Add(titleCardLabel);
        card.Click += delegate { PlaySong(song); };
        cover.Click += delegate { PlaySong(song); };
        artistLabel.Click += delegate { PlaySong(song); };
        titleCardLabel.Click += delegate { PlaySong(song); };
        card.MouseEnter += delegate { SelectSongCard(card); };
        cover.MouseEnter += delegate { SelectSongCard(card); };
        artistLabel.MouseEnter += delegate { SelectSongCard(card); };
        titleCardLabel.MouseEnter += delegate { SelectSongCard(card); };
        songGrid.Controls.Add(card);
        songCards.Add(card);
    }

    private Image LoadImageCopy(string path)
    {
        try
        {
            using (var source = Image.FromFile(path))
            {
                return new Bitmap(source);
            }
        }
        catch
        {
            return null;
        }
    }

    private void SelectSongCard(Panel card)
    {
        var index = songCards.IndexOf(card);
        if (index < 0) return;
        selectedSongIndex = index;
        UpdateSelectedCard();
    }

    private void ShowSongPreview(Song song)
    {
        if (playerPanel.Visible) return;
        previewArtist.Text = song.Artist.Length > 0 ? song.Artist : song.Title;
        previewTitle.Text = song.Artist.Length > 0 ? song.Title : "";
        previewMeta.Text = (song.Year.Length > 0 ? song.Year : "") + (song.Genre.Length > 0 ? "   " + song.Genre : "");
        previewCreator.Text = song.Creator.Length > 0 ? song.Creator : "";
        StartPreviewMpv(song);
    }

    private void StartPreviewMpv(Song song)
    {
        if (playerPanel.Visible) return;
        if (mpvPath.Length == 0 || !File.Exists(mpvPath) || song.Media.Length == 0) return;
        if (previewSong == song && previewMpv != null && !previewMpv.HasExited) return;
        EnsurePreviewMpv();
        if (previewMpv == null || previewMpv.HasExited || String.IsNullOrEmpty(previewPipeName)) return;
        previewSong = song;
        SendMpvToPipe(previewPipeName, "{\"command\":[\"set_property\",\"volume\",10]}");
        SendMpvToPipe(previewPipeName, "{\"command\":[\"loadfile\",\"" + JsonEscape(song.Media) + "\",\"replace\"]}");
        if (song.Audio.Length > 0 && song.Audio != song.Media)
        {
            SendMpvToPipe(previewPipeName, "{\"command\":[\"audio-add\",\"" + JsonEscape(song.Audio) + "\",\"auto\"]}");
        }
        SendMpvToPipe(previewPipeName, "{\"command\":[\"seek\",30,\"absolute\"]}");
        previewFadeStarted = DateTime.Now;
        previewFadeTimer.Start();
    }

    private void EnsurePreviewMpv()
    {
        if (previewMpv != null && !previewMpv.HasExited && !String.IsNullOrEmpty(previewPipeName)) return;
        previewPanel.CreateControl();
        previewPanel.Update();
        previewPipeName = "ultrastar_preview_" + Process.GetCurrentProcess().Id + "_" + DateTime.Now.Ticks;
        previewMpv = new Process();
        previewMpv.StartInfo.FileName = mpvPath;
        previewMpv.StartInfo.Arguments = "--idle=yes --wid=" + previewPanel.Handle + " --input-ipc-server=\"\\\\.\\pipe\\" + previewPipeName + "\" --force-window=yes --no-terminal --vo=direct3d --hwdec=no --volume=10 --mute=no --loop-file=inf --ao=wasapi,dsound,win32";
        previewMpv.StartInfo.UseShellExecute = false;
        previewMpv.StartInfo.CreateNoWindow = true;
        previewMpv.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            previewMpv.Start();
            WaitForPipe(previewPipeName, 800);
        }
        catch
        {
            previewMpv = null;
            previewPipeName = "";
        }
    }

    private void FadePreviewVolume()
    {
        if (previewMpv == null || previewMpv.HasExited || String.IsNullOrEmpty(previewPipeName))
        {
            previewFadeTimer.Stop();
            return;
        }
        var elapsed = (DateTime.Now - previewFadeStarted).TotalMilliseconds;
        var progress = Math.Max(0, Math.Min(1, elapsed / 1200.0));
        var volume = 10 + (int)Math.Round(70 * progress);
        SendMpvToPipe(previewPipeName, "{\"command\":[\"set_property\",\"volume\"," + volume.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]}");
        if (progress >= 1) previewFadeTimer.Stop();
    }

    private void StopPreviewMpv()
    {
        previewFadeTimer.Stop();
        try { if (previewMpv != null && !previewMpv.HasExited) previewMpv.Kill(); }
        catch { }
        previewMpv = null;
        previewPipeName = "";
        previewSong = null;
    }

    private void PlaySong(Song song)
    {
        if (mpvPath.Length == 0 || !File.Exists(mpvPath))
        {
            MessageBox.Show("No encontre mpv.exe. Ponlo junto al ejecutable o en la carpeta dist.", "Falta mpv", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        StopPreviewMpv();
        StopMpv();
        currentSong = song;
        usingInstrumental = false;
        ParsePhrases(song.TxtPath);
        playerPanel.Visible = true;
        playerPanel.BringToFront();
        LayoutVideoPanel();
        LayoutSliderOverlays();
        videoPanel.SendToBack();
        lyricsOverlay.BringToFront();
        pitchOverlay.BringToFront();
        vocalOverlay.BringToFront();
        lyricLine.TextToShow = "";
        nextLine.TextToShow = song.DisplayName;
        nextLine.Invalidate();
        currentPhraseIndex = -1;
        pitchBar.Value = 0;
        vocalBar.Value = 0;
        videoPanel.CreateControl();
        videoPanel.Update();
        Application.DoEvents();

        pipeName = "ultrastar_simple_" + Process.GetCurrentProcess().Id + "_" + DateTime.Now.Ticks;
        mpv = new Process();
        mpv.StartInfo.FileName = mpvPath;
        var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mpv.log");
        var audioArg = song.Audio.Length > 0 && song.Audio != song.Media ? " --audio-file=\"" + song.Audio + "\"" : "";
        if (song.Instrumental.Length > 0) audioArg += " --audio-file=\"" + song.Instrumental + "\"";
        mpv.StartInfo.Arguments = "--wid=" + videoPanel.Handle + " --input-ipc-server=\"\\\\.\\pipe\\" + pipeName + "\" --force-window=yes --no-terminal --vo=direct3d --hwdec=no --volume=100 --mute=no --ao=wasapi,dsound,win32 --log-file=\"" + logFile + "\"" + audioArg + " \"" + song.Media + "\"";
        mpv.StartInfo.UseShellExecute = false;
        mpv.StartInfo.CreateNoWindow = true;
        mpv.StartInfo.WorkingDirectory = song.Folder;
        mpv.Start();
        timer.Start();
    }

    private void ShowSongGrid()
    {
        StopMpv();
        playerPanel.Visible = false;
        if (selectedSongIndex >= 0 && selectedSongIndex < visibleSongs.Count) ShowSongPreview(visibleSongs[selectedSongIndex]);
    }

    private void ParsePhrases(string txtPath)
    {
        phrases.Clear();
        var bpm = 300.0;
        var gap = 0;
        var current = new Phrase();
        foreach (var raw in ReadUltraStarLines(txtPath))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("#BPM:")) bpm = ParseDouble(line.Substring(5), 300.0);
            else if (line.StartsWith("#GAP:")) gap = (int)ParseDouble(line.Substring(5), 0);
            else if (line.StartsWith(":") || line.StartsWith("*") || line.StartsWith("F"))
            {
                var parts = line.Split(new[] { ' ' }, 5);
                if (parts.Length >= 5)
                {
                    int start, dur;
                    Int32.TryParse(parts[1], out start);
                    Int32.TryParse(parts[2], out dur);
                    if (current.Words.Count == 0) current.StartBeat = start;
                    current.EndBeat = Math.Max(current.EndBeat, start + dur);
                    current.Words.Add(parts[4]);
                }
            }
            else if (line.StartsWith("-")) AddPhrase(current, bpm, gap, ref current);
        }
        AddPhrase(current, bpm, gap, ref current);
    }

    private void AddPhrase(Phrase phrase, double bpm, int gap, ref Phrase current)
    {
        if (phrase.Words.Count == 0) return;
        phrase.Bpm = bpm;
        phrase.Gap = gap;
        phrases.Add(phrase);
        current = new Phrase();
    }

    private string[] ReadUltraStarLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        try
        {
            var strictUtf8 = new UTF8Encoding(false, true);
            return strictUtf8.GetString(bytes).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
        catch
        {
            return Encoding.GetEncoding(1252).GetString(bytes).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
    }

    private double ParseDouble(string value, double fallback)
    {
        double parsed;
        if (Double.TryParse(value.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out parsed)) return parsed;
        return fallback;
    }

    private void UpdateKaraoke()
    {
        var seconds = QueryMpvTime();
        if (seconds < 0) return;
        var index = -1;
        for (int i = 0; i < phrases.Count; i++)
        {
            if (seconds >= phrases[i].StartSeconds() - 0.05 && seconds <= phrases[i].EndSeconds() + 0.35) { index = i; break; }
        }
        if (index < 0)
        {
            lyricLine.TextToShow = "";
            lyricLine.Progress = 0;
            lyricLine.Invalidate();
            return;
        }
        var phrase = phrases[index];
        lyricLine.TextToShow = phrase.Text();
        lyricLine.Progress = phrase.Progress(seconds);
        lyricLine.Invalidate();
        if (currentPhraseIndex != index)
        {
            currentPhraseIndex = index;
            nextLine.TextToShow = index + 1 < phrases.Count ? phrases[index + 1].Text() : "";
            nextLine.Invalidate();
        }
    }

    private void ApplyAudioFilters()
    {
        if (String.IsNullOrEmpty(pipeName)) return;
        ApplyInstrumentalMode();
        var filters = new List<string>();
        if (pitchBar.Value != 0)
        {
            var scale = Math.Pow(2.0, pitchBar.Value / 12.0).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
            filters.Add("rubberband=pitch-scale=" + scale + ":transients=crisp:detector=compound:phase=laminar:window=long:smoothing=on:formant=preserved");
        }
        if (vocalBar.Value > 0)
        {
            var amount = (vocalBar.Value / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            filters.Add("lavfi=[pan=stereo|c0=c0-" + amount + "*c1|c1=c1-" + amount + "*c0]");
        }
        if (filters.Count == 0) SendMpv("{\"command\":[\"set_property\",\"af\",[]]}");
        else SendMpv("{\"command\":[\"set_property\",\"af\",\"" + JsonEscape(String.Join(",", filters.ToArray())) + "\"]}");
    }

    private void ApplyInstrumentalMode()
    {
        if (currentSong == null || currentSong.Instrumental.Length == 0) return;
        var shouldUseInstrumental = vocalBar.Value >= 50;
        if (shouldUseInstrumental == usingInstrumental) return;
        usingInstrumental = shouldUseInstrumental;
        var audioId = shouldUseInstrumental ? 2 : 1;
        SendMpv("{\"command\":[\"set_property\",\"aid\"," + audioId + "]}");
    }

    private string JsonEscape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private double QueryMpvTime()
    {
        try
        {
            using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
            {
                pipe.Connect(45);
                var bytes = Encoding.UTF8.GetBytes("{\"command\":[\"get_property\",\"time-pos\"],\"request_id\":1}\n");
                pipe.Write(bytes, 0, bytes.Length);
                pipe.Flush();
                var buffer = new byte[4096];
                var read = pipe.Read(buffer, 0, buffer.Length);
                var match = Regex.Match(Encoding.UTF8.GetString(buffer, 0, read), "\"data\"\\s*:\\s*([0-9\\.]+)");
                if (match.Success) return ParseDouble(match.Groups[1].Value, -1);
            }
        }
        catch { }
        return -1;
    }

    private void SendMpv(string json)
    {
        SendMpvToPipe(pipeName, json);
    }

    private void SendMpvToPipe(string targetPipeName, string json)
    {
        try
        {
            using (var pipe = new NamedPipeClientStream(".", targetPipeName, PipeDirection.Out))
            {
                pipe.Connect(180);
                var bytes = Encoding.UTF8.GetBytes(json + "\n");
                pipe.Write(bytes, 0, bytes.Length);
            }
        }
        catch { }
    }

    private void WaitForPipe(string targetPipeName, int timeoutMs)
    {
        var start = Environment.TickCount;
        while (Environment.TickCount - start < timeoutMs)
        {
            try
            {
                using (var pipe = new NamedPipeClientStream(".", targetPipeName, PipeDirection.Out))
                {
                    pipe.Connect(60);
                    return;
                }
            }
            catch
            {
                System.Threading.Thread.Sleep(30);
            }
        }
    }

    private void StopMpv()
    {
        timer.Stop();
        try { if (mpv != null && !mpv.HasExited) mpv.Kill(); } catch { }
        mpv = null;
        pipeName = "";
    }

    private void HandleKeys(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            if (playerPanel.Visible) ShowSongGrid();
            else Close();
        }
        else if (e.KeyCode == Keys.F11) ToggleFullscreen();
        else if (e.KeyCode == Keys.Space && playerPanel.Visible) SendMpv("{\"command\":[\"cycle\",\"pause\"]}");
        else if (!playerPanel.Visible) HandleSongGridKeys(e);
    }

    private void SearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (playerPanel.Visible)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        HandleSongGridKeys(e);
    }

    private void HandleSongGridKeys(KeyEventArgs e)
    {
        if (playerPanel.Visible) return;
        if (visibleSongs.Count == 0) return;
        var columns = Math.Max(1, songGrid.ClientSize.Width / 210);
        if (e.KeyCode == Keys.Right) selectedSongIndex = Math.Min(visibleSongs.Count - 1, selectedSongIndex + 1);
        else if (e.KeyCode == Keys.Left) selectedSongIndex = Math.Max(0, selectedSongIndex - 1);
        else if (e.KeyCode == Keys.Down) selectedSongIndex = Math.Min(visibleSongs.Count - 1, selectedSongIndex + columns);
        else if (e.KeyCode == Keys.Up) selectedSongIndex = Math.Max(0, selectedSongIndex - columns);
        else if (e.KeyCode == Keys.Enter) PlaySong(visibleSongs[Math.Max(0, selectedSongIndex)]);
        else return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        UpdateSelectedCard();
    }

    private void UpdateSelectedCard()
    {
        var next = selectedSongIndex >= 0 && selectedSongIndex < songCards.Count ? songCards[selectedSongIndex] : null;
        if (selectedCard == next) return;
        if (selectedCard != null) StyleSongCard(selectedCard, false);
        if (next != null) StyleSongCard(next, true);
        selectedCard = next;
        if (next != null) songGrid.ScrollControlIntoView(next);
        if (selectedSongIndex >= 0 && selectedSongIndex < visibleSongs.Count) ShowSongPreview(visibleSongs[selectedSongIndex]);
    }

    private void StyleSongCard(Panel card, bool selected)
    {
        card.SuspendLayout();
        card.Width = 190;
        card.Height = 250;
        card.BackColor = Color.FromArgb(28, 28, 34);
        foreach (Control child in card.Controls)
        {
            if (child is PictureBox)
            {
                child.Left = 12;
                child.Top = 12;
                child.Width = 166;
                child.Height = 166;
            }
            else if (child is Label)
            {
                child.Left = 12;
                child.Top = child.Font.Bold ? 184 : 210;
                child.Width = 166;
                child.BackColor = card.BackColor;
            }
        }
        card.ResumeLayout();
        card.Invalidate();
    }

    private void PaintSongCard(object sender, PaintEventArgs e)
    {
        var card = sender as Panel;
        if (card == null || card != selectedCard) return;
        using (var pen = new Pen(Color.FromArgb(255, 0, 170), 4))
        {
            e.Graphics.DrawRectangle(pen, 2, 2, card.Width - 5, card.Height - 5);
        }
    }

    private void ToggleFullscreen()
    {
        isFullscreen = !isFullscreen;
        FormBorderStyle = isFullscreen ? FormBorderStyle.None : FormBorderStyle.Sizable;
        WindowState = isFullscreen ? FormWindowState.Maximized : FormWindowState.Normal;
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new UltraStarSimplificadoForm());
    }

    private class Song
    {
        public string TxtPath = "";
        public string Folder = "";
        public string Title = "";
        public string Artist = "";
        public string Year = "";
        public string Creator = "";
        public string Genre = "";
        public string Cover = "";
        public string Media = "";
        public string Audio = "";
        public string NormalizedAudio = "";
        public string Instrumental = "";
        public string NormalizedInstrumental = "";
        public string DisplayName { get { return (Artist.Length > 0 ? Artist + " - " : "") + Title; } }
    }

    private class Phrase
    {
        public int StartBeat;
        public int EndBeat;
        public double Bpm = 300;
        public int Gap;
        public readonly List<string> Words = new List<string>();
        public string Text() { return Regex.Replace(String.Join("", Words.ToArray()), "\\s+", " ").Trim(); }
        public double StartSeconds() { return Gap / 1000.0 + StartBeat * 15.0 / Bpm; }
        public double EndSeconds() { return Gap / 1000.0 + EndBeat * 15.0 / Bpm; }
        public double Progress(double seconds) { return Math.Max(0, Math.Min(1, (seconds - StartSeconds()) / Math.Max(0.1, EndSeconds() - StartSeconds()))); }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}

public class KaraokeLineView : Control
{
    public string TextToShow = "";
    public double Progress;

    public KaraokeLineView()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(12, 12, 14);
        Font = new Font("Segoe UI", 30, FontStyle.Bold);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        e.Graphics.Clear(BackColor);
        var text = TextToShow ?? "";
        if (text.Length == 0) return;
        var rect = ClientRectangle;
        rect.Inflate(-20, -8);
        using (var format = new StringFormat(StringFormat.GenericTypographic))
        {
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.Trimming = StringTrimming.EllipsisCharacter;
            using (var path = new GraphicsPath())
            using (var outline = new Pen(Color.Black, 7) { LineJoin = LineJoin.Round })
            using (var baseBrush = new SolidBrush(Color.White))
            {
                path.AddString(text, Font.FontFamily, (int)Font.Style, e.Graphics.DpiY * Font.Size / 72f, rect, format);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(outline, path);
                e.Graphics.FillPath(baseBrush, path);
            }
            var size = e.Graphics.MeasureString(text, Font, rect.Size, format);
            var left = rect.Left + (rect.Width - size.Width) / 2f;
            var width = Math.Max(0, Math.Min(size.Width, size.Width * (float)Progress));
            var oldClip = e.Graphics.Clip;
            e.Graphics.SetClip(new RectangleF(left, rect.Top, width, rect.Height));
            using (var path = new GraphicsPath())
            using (var outline = new Pen(Color.Black, 7) { LineJoin = LineJoin.Round })
            using (var sungBrush = new SolidBrush(Color.FromArgb(255, 0, 170)))
            {
                path.AddString(text, Font.FontFamily, (int)Font.Style, e.Graphics.DpiY * Font.Size / 72f, rect, format);
                e.Graphics.DrawPath(outline, path);
                e.Graphics.FillPath(sungBrush, path);
            }
            e.Graphics.Clip = oldClip;
        }
    }
}

public class NextLineView : Control
{
    public string TextToShow = "";

    public NextLineView()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(46, 18, 78);
        ForeColor = Color.FromArgb(220, 220, 225);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        e.Graphics.Clear(BackColor);
        var rect = ClientRectangle;
        rect.Inflate(-16, -4);
        using (var format = new StringFormat())
        using (var brush = new SolidBrush(ForeColor))
        {
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.Trimming = StringTrimming.EllipsisCharacter;
            e.Graphics.DrawString(TextToShow ?? "", Font, brush, rect, format);
        }
    }
}
