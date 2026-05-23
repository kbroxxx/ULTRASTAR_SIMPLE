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

    private readonly TextBox         searchBox        = new TextBox();
    private          VirtualSongGrid vGrid;
    private readonly Panel           infoPanel        = new Panel();
    private readonly Panel           previewPanel     = new Panel();
    private readonly PictureBox      coverPreview     = new PictureBox();
    private readonly Label           previewArtist    = new Label();
    private readonly Label           previewTitle     = new Label();
    private readonly Label           previewMeta      = new Label();
    private readonly Label           previewCreator   = new Label();
    private readonly Label           songCountLabel   = new Label();
    private readonly Panel           playerPanel      = new Panel();
    private readonly Panel           videoPanel       = new Panel();
    private readonly KaraokeLineView lyricLine        = new KaraokeLineView();
    private readonly NextLineView    nextLine         = new NextLineView();
    private readonly Panel           lyricsOverlay    = new Panel();
    private readonly Panel           pitchOverlay     = new Panel();
    private readonly Panel           vocalOverlay     = new Panel();
    private readonly Label           versionLabel     = new Label();
    private readonly TrackBar        pitchBar         = new TrackBar();
    private readonly TrackBar        vocalBar         = new TrackBar();
    private readonly Button          favFilterBtn     = new Button();
    private readonly Button          rescanBtn        = new Button();
    private readonly Button          sizePlusBtn      = new Button();
    private readonly Button          sizeMinusBtn     = new Button();
    private readonly Panel           loadingPanel     = new Panel();
    private readonly Label           loadingTitle     = new Label();
    private readonly Label           loadingStatus    = new Label();
    private readonly Timer           timer            = new Timer();
    private readonly Timer           previewFadeTimer = new Timer();
    private readonly Timer           previewDebounceTimer = new Timer();

    private Song   pendingPreviewSong;
    private string songsRoot       = "";
    private string mpvPath         = "";
    private string pipeName    = "";
    private Process  mpv;
    private Process  previewMpv;
    private bool  isFullscreen       = false;
    private int   currentPhraseIndex = -1;
    private Song  currentSong;
    private bool  usingInstrumental;
    private bool  showFavOnly = false;

    private readonly List<Song>      songs        = new List<Song>();
    private readonly List<Song>      visibleSongs = new List<Song>();
    private readonly List<Phrase>    phrases      = new List<Phrase>();
    private readonly HashSet<string> favorites    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // ── Constructor ───────────────────────────────────────────────────────────

    public UltraStarSimplificadoForm()
    {
        Text            = "UltraStar Simplificado " + AppVersion;
        StartPosition   = FormStartPosition.CenterScreen;
        Width           = 1280;
        Height          = 720;
        MinimumSize     = new Size(800, 560);
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor       = Color.FromArgb(12, 12, 14);
        ForeColor       = Color.White;
        Font            = new Font("Segoe UI", 11);
        KeyPreview      = true;
        ApplyBackground(this);

        // Info panel (left sidebar)
        infoPanel.Left      = 18;
        infoPanel.Top       = 72;
        infoPanel.Width     = 330;
        infoPanel.Height    = 510;
        infoPanel.Anchor    = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        infoPanel.BackColor = Color.FromArgb(140, 105, 0, 120);
        Controls.Add(infoPanel);

        previewPanel.Left      = 14;
        previewPanel.Top       = 18;
        previewPanel.Width     = 302;
        previewPanel.Height    = 170;
        previewPanel.BackColor = Color.Black;
        previewPanel.Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        infoPanel.Controls.Add(previewPanel);

        // Cover art shown as fallback while video hasn't loaded yet
        coverPreview.Dock      = DockStyle.Fill;
        coverPreview.SizeMode  = PictureBoxSizeMode.Zoom;
        coverPreview.BackColor = Color.Black;
        previewPanel.Controls.Add(coverPreview);

        previewArtist.Left      = 18;  previewArtist.Top    = 204;
        previewArtist.Width     = 292; previewArtist.Height = 48;
        previewArtist.Font      = new Font("Segoe UI", 22, FontStyle.Bold);
        previewArtist.ForeColor = Color.White;
        previewArtist.BackColor = Color.Transparent;
        previewArtist.TextAlign = ContentAlignment.MiddleCenter;
        infoPanel.Controls.Add(previewArtist);

        previewTitle.Left      = 18;  previewTitle.Top    = 252;
        previewTitle.Width     = 292; previewTitle.Height = 46;
        previewTitle.Font      = new Font("Segoe UI", 18);
        previewTitle.ForeColor = Color.White;
        previewTitle.BackColor = Color.Transparent;
        previewTitle.TextAlign = ContentAlignment.MiddleCenter;
        infoPanel.Controls.Add(previewTitle);

        previewMeta.Left      = 18;  previewMeta.Top    = 340;
        previewMeta.Width     = 292; previewMeta.Height = 34;
        previewMeta.Font      = new Font("Segoe UI", 12);
        previewMeta.ForeColor = Color.White;
        previewMeta.BackColor = Color.Transparent;
        previewMeta.TextAlign = ContentAlignment.MiddleCenter;
        infoPanel.Controls.Add(previewMeta);

        previewCreator.Left      = 18;  previewCreator.Top    = 374;
        previewCreator.Width     = 292; previewCreator.Height = 34;
        previewCreator.Font      = new Font("Segoe UI", 11);
        previewCreator.ForeColor = Color.White;
        previewCreator.BackColor = Color.Transparent;
        previewCreator.TextAlign = ContentAlignment.MiddleCenter;
        infoPanel.Controls.Add(previewCreator);

        // Song count
        songCountLabel.Left      = ClientSize.Width - 230;
        songCountLabel.Top       = 18;
        songCountLabel.Width     = 200;
        songCountLabel.Height    = 38;
        songCountLabel.Anchor    = AnchorStyles.Top | AnchorStyles.Right;
        songCountLabel.Font      = new Font("Segoe UI", 18, FontStyle.Bold);
        songCountLabel.ForeColor = Color.White;
        songCountLabel.BackColor = Color.FromArgb(105, 85, 0, 95);
        songCountLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(songCountLabel);

        // Buttons row
        SetupBtn(favFilterBtn,  "★ Favs",      360, 18, 100, delegate { showFavOnly = !showFavOnly; favFilterBtn.BackColor = showFavOnly ? Color.FromArgb(180,0,120) : Color.FromArgb(60,30,70); FilterSongs(); });
        SetupBtn(rescanBtn,     "↺ Actualizar", 468, 18, 110, delegate { DeleteCache(); LoadSongs(); });
        SetupBtn(sizePlusBtn,   "+",            586, 18,  36, delegate { ChangeCardSize(+20); });
        SetupBtn(sizeMinusBtn,  "−",            626, 18,  36, delegate { ChangeCardSize(-20); });
        Controls.Add(favFilterBtn);
        Controls.Add(rescanBtn);
        Controls.Add(sizePlusBtn);
        Controls.Add(sizeMinusBtn);

        // Search box
        searchBox.Left      = 360;
        searchBox.Top       = ClientSize.Height - 52;
        searchBox.Width     = ClientSize.Width - 380;
        searchBox.Height    = 30;
        searchBox.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        searchBox.Font      = new Font("Segoe UI", 13);
        searchBox.BackColor = Color.FromArgb(48, 10, 48);
        searchBox.ForeColor = Color.White;
        searchBox.TextChanged += delegate { FilterSongs(); };
        searchBox.KeyDown     += SearchBoxKeyDown;
        Controls.Add(searchBox);

        // Virtual song grid
        vGrid = new VirtualSongGrid(IsFavorite);
        vGrid.Left   = 360;
        vGrid.Top    = 72;
        vGrid.Width  = ClientSize.Width - 378;
        vGrid.Height = ClientSize.Height - 136;
        vGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        vGrid.SetCoverSize(LoadCardSize());
        vGrid.SongHovered     += s => ShowSongPreview(FindSong(s.TxtPath));
        vGrid.SongActivated   += s => PlaySong(FindSong(s.TxtPath));
        vGrid.FavoriteToggled += s => { ToggleFavorite(s.TxtPath); vGrid.Invalidate(); };
        Controls.Add(vGrid);

        // Player panel
        playerPanel.Dock      = DockStyle.Fill;
        playerPanel.BackColor = Color.FromArgb(46, 18, 78);
        ApplyBackground(playerPanel);
        playerPanel.Visible   = false;
        Controls.Add(playerPanel);

        videoPanel.Dock      = DockStyle.Fill;
        videoPanel.BackColor = Color.Black;
        playerPanel.Controls.Add(videoPanel);

        lyricsOverlay.Height    = 150;
        lyricsOverlay.Dock      = DockStyle.Bottom;
        lyricsOverlay.BackColor = Color.FromArgb(12, 12, 14);
        playerPanel.Controls.Add(lyricsOverlay);
        lyricsOverlay.BringToFront();

        lyricLine.Height = 86;
        lyricLine.Dock   = DockStyle.Top;
        lyricsOverlay.Controls.Add(lyricLine);

        nextLine.Height    = 54;
        nextLine.Dock      = DockStyle.Bottom;
        nextLine.Font      = new Font("Segoe UI", 22, FontStyle.Bold);
        nextLine.BackColor = Color.FromArgb(12, 12, 14);
        lyricsOverlay.Controls.Add(nextLine);

        SetupSliderOverlay(pitchOverlay, "Tono",    pitchBar, -12, 12,  0);
        SetupSliderOverlay(vocalOverlay, "Karaoke", vocalBar,   0, 100, 0);
        playerPanel.Controls.Add(pitchOverlay);
        playerPanel.Controls.Add(vocalOverlay);
        pitchOverlay.BringToFront();
        vocalOverlay.BringToFront();
        LayoutSliderOverlays();
        playerPanel.Resize        += delegate { LayoutSliderOverlays(); };
        pitchBar.ValueChanged     += delegate { ApplyAudioFilters(); };
        vocalBar.ValueChanged     += delegate { ApplyAudioFilters(); };

        // Loading panel
        loadingPanel.Dock      = DockStyle.Fill;
        loadingPanel.BackColor = Color.FromArgb(12, 12, 14);
        loadingPanel.Visible   = false;
        Controls.Add(loadingPanel);

        loadingTitle.Text      = "UltraStar Simplificado";
        loadingTitle.Font      = new Font("Segoe UI", 28, FontStyle.Bold);
        loadingTitle.ForeColor = Color.FromArgb(255, 0, 170);
        loadingTitle.BackColor = Color.FromArgb(12, 12, 14);
        loadingTitle.TextAlign = ContentAlignment.MiddleCenter;
        loadingTitle.Dock      = DockStyle.Fill;
        loadingPanel.Controls.Add(loadingTitle);

        loadingStatus.Text      = "Cargando canciones...";
        loadingStatus.Font      = new Font("Segoe UI", 14);
        loadingStatus.ForeColor = Color.FromArgb(200, 200, 210);
        loadingStatus.BackColor = Color.FromArgb(12, 12, 14);
        loadingStatus.TextAlign = ContentAlignment.BottomCenter;
        loadingStatus.Dock      = DockStyle.Bottom;
        loadingStatus.Height    = 60;
        loadingPanel.Controls.Add(loadingStatus);

        // Version label
        versionLabel.Text      = AppVersion;
        versionLabel.Font      = new Font("Segoe UI", 9);
        versionLabel.ForeColor = Color.FromArgb(100, 100, 110);
        versionLabel.BackColor = Color.Transparent;
        versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        versionLabel.Width     = 60;
        versionLabel.Height    = 22;
        versionLabel.Left      = 18;
        versionLabel.Top       = ClientSize.Height - 26;
        versionLabel.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(versionLabel);

        // Timers
        timer.Interval = 50;
        timer.Tick    += delegate { UpdateKaraoke(); };

        previewFadeTimer.Interval = 90;
        previewFadeTimer.Tick    += delegate { FadePreviewVolume(); };

        previewDebounceTimer.Interval = 250;
        previewDebounceTimer.Tick    += delegate { previewDebounceTimer.Stop(); StartPreviewMpv(pendingPreviewSong); };

        FormClosing += OnFormClosing;
        KeyDown     += HandleKeys;
        Shown       += delegate { LoadFavorites(); LoadSongs(); };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupBtn(Button btn, string text, int left, int top, int width, Action onClick)
    {
        btn.Text      = text;
        btn.Left      = left; btn.Top    = top;
        btn.Width     = width; btn.Height = 34;
        btn.FlatStyle = FlatStyle.Flat;
        btn.Font      = new Font("Segoe UI", 10);
        btn.ForeColor = Color.White;
        btn.BackColor = Color.FromArgb(60, 30, 70);
        btn.FlatAppearance.BorderColor = Color.FromArgb(160, 80, 180);
        btn.Anchor    = AnchorStyles.Top | AnchorStyles.Left;
        btn.Click    += delegate { onClick(); };
    }

    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !playerPanel.Visible)
        {
            if (MessageBox.Show("¿Salir de UltraStar Simplificado?", "Salir",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            { e.Cancel = true; return; }
        }
        StopMpv(); StopPreviewMpv();
    }

    private void ApplyBackground(Control control)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fondo.jpg");
        if (!File.Exists(path)) path = Path.Combine(Environment.CurrentDirectory, "fondo.jpg");
        if (File.Exists(path))
        {
            control.BackgroundImage       = Image.FromFile(path);
            control.BackgroundImageLayout = ImageLayout.Stretch;
        }
    }

    private Label MakeLabel(string text, int left)
    {
        return new Label { Text = text, Left = left, Top = 11, Width = 72, Height = 24,
            ForeColor = Color.White, BackColor = Color.FromArgb(16, 16, 20) };
    }

    private void SetupBar(TrackBar bar, int min, int max, int value, int left, Control parent)
    {
        bar.Left = left; bar.Top = 3; bar.Width = 210; bar.Height = 42;
        bar.Minimum = min; bar.Maximum = max; bar.Value = value;
        bar.SmallChange = 1; bar.LargeChange = 2;
        bar.TickFrequency = min < 0 ? 1 : 10;
        parent.Controls.Add(bar);
    }

    private void SetupSliderOverlay(Panel panel, string label, TrackBar bar, int min, int max, int value)
    {
        panel.Width = 310; panel.Height = 48;
        panel.Anchor    = AnchorStyles.Top | AnchorStyles.Left;
        panel.BackColor = Color.FromArgb(16, 16, 20);
        panel.Paint    += PaintSliderOverlay;
        panel.Controls.Add(MakeLabel(label, 14));
        SetupBar(bar, min, max, value, 84, panel);
    }

    private void PaintSliderOverlay(object sender, PaintEventArgs e)
    {
        using (var pen = new Pen(Color.FromArgb(255, 0, 170), 2))
        {
            var panel = sender as Panel;
            if      (panel == pitchOverlay) DrawZeroMarker(e.Graphics, pitchBar, -12, 12,  pen);
            else if (panel == vocalOverlay) DrawZeroMarker(e.Graphics, vocalBar,   0, 100, pen);
        }
    }

    private void DrawZeroMarker(Graphics g, TrackBar bar, int min, int max, Pen pen)
    {
        var x = bar.Left + 16 + (int)((bar.Width - 32) * (0.0 - min) / (max - min));
        g.DrawLine(pen, x, bar.Top + 4, x, bar.Top + bar.Height - 7);
    }

    private void LayoutSliderOverlays()
    {
        pitchOverlay.Left = 18; pitchOverlay.Top = 18;
        vocalOverlay.Left = Math.Max(18, playerPanel.ClientSize.Width - vocalOverlay.Width - 18);
        vocalOverlay.Top  = 18;
    }

    private void LayoutVideoPanel() { videoPanel.Bounds = playerPanel.ClientRectangle; }

    private string FindTool(string name)
    {
        var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
        if (File.Exists(local)) return local;
        var dist = Path.Combine(Environment.CurrentDirectory, "dist", name);
        if (File.Exists(dist)) return dist;
        return name;
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
            if (Directory.Exists(path) && Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories).Length > 0)
                return path;
        return "";
    }

    private string Normalize(string value)
    {
        var norm = (value ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb   = new StringBuilder();
        foreach (var ch in norm)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private string JsonEscape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
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

    // ── Paths ─────────────────────────────────────────────────────────────────

    private string CachePath    { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "songs_cache.txt"); } }
    private string ThumbDir     { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cover_cache"); } }
    private string FavsPath     { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorites.txt"); } }
    private string CardSizePath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "card_size.txt"); } }

    private void DeleteCache() { try { if (File.Exists(CachePath)) File.Delete(CachePath); } catch { } }

    // ── Song loading ──────────────────────────────────────────────────────────

    private void LoadSongs()
    {
        mpvPath   = FindTool("mpv.exe");
        songsRoot = FindSongsRoot();
        if (songsRoot.Length == 0)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Elige la carpeta donde están tus canciones UltraStar";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                songsRoot = dlg.SelectedPath;
            }
        }

        loadingPanel.BringToFront();
        loadingPanel.Visible = true;
        loadingStatus.Text   = "Escaneando canciones...";
        Application.DoEvents();

        var cached = LoadFromCache();
        if (cached != null) { songs.Clear(); songs.AddRange(cached); }
        else ScanSongs();

        loadingPanel.Visible = false;
        FilterSongs();
        songCountLabel.Text = songs.Count + " canciones";
    }

    private List<Song> LoadFromCache()
    {
        if (!File.Exists(CachePath)) return null;
        try
        {
            var list = new List<Song>();
            foreach (var line in File.ReadAllLines(CachePath, Encoding.UTF8))
            {
                var p = line.Split('\t');
                if (p.Length < 8) continue;
                var s = new Song
                {
                    TxtPath = p[0], Folder  = Path.GetDirectoryName(p[0]),
                    Title   = p[1], Artist  = p[2], Year    = p[3],
                    Genre   = p[4], Creator = p[5], Cover   = p[6], Media = p[7],
                    Audio        = p.Length > 8 ? p[8] : "",
                    Instrumental = p.Length > 9 ? p[9] : ""
                };
                if (File.Exists(s.TxtPath) && s.Media.Length > 0) list.Add(s);
            }
            return list.Count > 0 ? list : null;
        }
        catch { return null; }
    }

    private void ScanSongs()
    {
        songs.Clear();
        var txts  = Directory.GetFiles(songsRoot, "*.txt", SearchOption.AllDirectories);
        var total = txts.Length;
        for (int i = 0; i < total; i++)
        {
            if (i % 50 == 0) { loadingStatus.Text = String.Format("Escaneando {0}/{1}...", i, total); Application.DoEvents(); }
            var song = ReadSongInfo(txts[i]);
            if (song != null) songs.Add(song);
        }
        songs.Sort((a, b) => String.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
        SaveCache();
        System.Threading.ThreadPool.QueueUserWorkItem(delegate { foreach (var s in songs) EnsureThumbExists(s); });
    }

    private void SaveCache()
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var s in songs)
                sb.AppendFormat("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\r\n",
                    s.TxtPath, s.Title, s.Artist, s.Year, s.Genre, s.Creator, s.Cover, s.Media, s.Audio, s.Instrumental);
            File.WriteAllText(CachePath, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }

    private Song ReadSongInfo(string txtPath)
    {
        var song = new Song { TxtPath = txtPath, Folder = Path.GetDirectoryName(txtPath), Title = Path.GetFileNameWithoutExtension(txtPath) };
        foreach (var raw in ReadUltraStarLines(txtPath))
        {
            if      (raw.StartsWith("#TITLE:"))   song.Title   = raw.Substring(7).Trim();
            else if (raw.StartsWith("#ARTIST:"))  song.Artist  = raw.Substring(8).Trim();
            else if (raw.StartsWith("#YEAR:"))    song.Year    = raw.Substring(6).Trim();
            else if (raw.StartsWith("#CREATOR:")) song.Creator = raw.Substring(9).Trim();
            else if (raw.StartsWith("#GENRE:"))   song.Genre   = raw.Substring(7).Trim();
            else if (raw.StartsWith("#COVER:"))   song.Cover   = Existing(song.Folder, raw.Substring(7).Trim());
            else if (raw.StartsWith("#VIDEO:"))   song.Media   = Existing(song.Folder, raw.Substring(7).Trim());
            else if (raw.StartsWith("#MP3:"))     song.Audio   = Existing(song.Folder, raw.Substring(5).Trim());
        }
        if (song.Cover.Length == 0) song.Cover = FirstFile(song.Folder, new[] { "cover*.jpg", "cover*.jpeg", "cover*.png", "*[CO].jpg", "*.jpg", "*.jpeg", "*.png" });
        if (song.Media.Length == 0) song.Media = FirstFile(song.Folder, new[] { "*.mp4", "*.avi", "*.divx", "*.mkv", "*.webm", "*.mov", "*.flv", "*.m4v" });
        if (song.Audio.Length == 0) song.Audio = FirstFile(song.Folder, new[] { "*.mp3", "*.m4a", "*.ogg", "*.wav" });
        song.NormalizedAudio        = FirstFile(song.Folder, new[] { "audio_normalizado.mp3" });
        song.Instrumental           = FirstFile(song.Folder, new[] { "instrumental.mp3", "instrumental.wav", "karaoke.mp3", "no_vocals.mp3" });
        song.NormalizedInstrumental = FirstFile(song.Folder, new[] { "instrumental_normalizado.mp3" });
        if (song.NormalizedAudio.Length        > 0) song.Audio        = song.NormalizedAudio;
        if (song.NormalizedInstrumental.Length > 0) song.Instrumental = song.NormalizedInstrumental;
        if (song.Media.Length == 0) song.Media = song.Audio;
        return song.Media.Length == 0 ? null : song;
    }

    private string GetThumbPath(Song song)
    {
        return Path.Combine(ThumbDir, (uint)song.TxtPath.ToLowerInvariant().GetHashCode() + ".jpg");
    }

    private void EnsureThumbExists(Song song)
    {
        var thumbPath = GetThumbPath(song);
        if (File.Exists(thumbPath) || song.Cover.Length == 0) return;
        try
        {
            using (var original = Image.FromFile(song.Cover))
            using (var thumb    = new Bitmap(155, 155))
            {
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(original, 0, 0, 155, 155);
                }
                Directory.CreateDirectory(ThumbDir);
                var tmp = thumbPath + ".tmp";
                thumb.Save(tmp, System.Drawing.Imaging.ImageFormat.Jpeg);
                if (File.Exists(thumbPath)) File.Delete(thumbPath);
                File.Move(tmp, thumbPath);
            }
        }
        catch { }
    }

    // ── Favorites ─────────────────────────────────────────────────────────────

    private void LoadFavorites()
    {
        favorites.Clear();
        if (!File.Exists(FavsPath)) return;
        try { foreach (var line in File.ReadAllLines(FavsPath, Encoding.UTF8)) if (line.Trim().Length > 0) favorites.Add(line.Trim()); }
        catch { }
    }

    private void SaveFavorites()
    {
        try { File.WriteAllLines(FavsPath, new List<string>(favorites).ToArray(), Encoding.UTF8); }
        catch { }
    }

    private bool IsFavorite(string txtPath) { return favorites.Contains(txtPath); }

    private void ToggleFavorite(string txtPath)
    {
        if (favorites.Contains(txtPath)) favorites.Remove(txtPath); else favorites.Add(txtPath);
        SaveFavorites();
    }

    // ── Card size ─────────────────────────────────────────────────────────────

    private int  LoadCardSize() { try { var s = int.Parse(File.ReadAllText(CardSizePath).Trim()); return Math.Max(70, Math.Min(260, s)); } catch { return 135; } }
    private void SaveCardSize(int size) { try { File.WriteAllText(CardSizePath, size.ToString()); } catch { } }
    private void ChangeCardSize(int delta) { vGrid.SetCoverSize(vGrid.CoverSize + delta); SaveCardSize(vGrid.CoverSize); }

    // ── Grid ──────────────────────────────────────────────────────────────────

    private Song FindSong(string txtPath)
    {
        foreach (var s in songs) if (String.Equals(s.TxtPath, txtPath, StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    private void FilterSongs()
    {
        visibleSongs.Clear();
        var query = Normalize(searchBox.Text);
        foreach (var song in songs)
        {
            if (showFavOnly && !favorites.Contains(song.TxtPath)) continue;
            if (query.Length > 0 && !Normalize(song.DisplayName).Contains(query)) continue;
            visibleSongs.Add(song);
        }
        var vSongs = new List<VGridSong>();
        foreach (var s in visibleSongs)
            vSongs.Add(new VGridSong { TxtPath = s.TxtPath, Artist = s.Artist, Title = s.Title, ThumbPath = GetThumbPath(s) });
        vGrid.SetSongs(vSongs);
        songCountLabel.Text = visibleSongs.Count + " canciones";
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    private void ShowSongPreview(Song song)
    {
        if (playerPanel.Visible || song == null) return;
        previewArtist.Text  = song.Artist.Length > 0 ? song.Artist : song.Title;
        previewTitle.Text   = song.Artist.Length > 0 ? song.Title  : "";
        previewMeta.Text    = (song.Year.Length > 0 ? song.Year : "") + (song.Genre.Length > 0 ? "   " + song.Genre : "");
        previewCreator.Text = song.Creator.Length > 0 ? song.Creator : "";

        // Show cover art immediately as placeholder while video loads
        coverPreview.Visible = true;
        if (song.Cover.Length > 0 && File.Exists(song.Cover))
        {
            try
            {
                var old = coverPreview.Image;
                coverPreview.Image = Image.FromFile(song.Cover);
                if (old != null) old.Dispose();
            }
            catch { coverPreview.Image = null; }
        }
        else coverPreview.Image = null;

        pendingPreviewSong = song;
        previewDebounceTimer.Stop();
        previewDebounceTimer.Start();
    }

    private void StartPreviewMpv(Song song)
    {
        if (playerPanel.Visible || song == null) return;
        if (mpvPath.Length == 0 || !File.Exists(mpvPath) || song.Media.Length == 0) return;

        // Kill any running preview instance first
        try { if (previewMpv != null && !previewMpv.HasExited) previewMpv.Kill(); } catch { }
        previewMpv = null;

        // Ensure the panel HWND is fully created and rendered before passing to mpv
        previewPanel.CreateControl();
        previewPanel.Update();
        Application.DoEvents();   // flush pending messages so the window is real

        // Hide cover art — mpv paints directly onto previewPanel via --wid
        coverPreview.Visible = false;

        var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mpv_preview.log");

        previewMpv = new Process();
        previewMpv.StartInfo.FileName  = mpvPath;
        // Mirror main-player argument style exactly — only differ in handle, file, volume, start
        previewMpv.StartInfo.Arguments =
            "--wid=" + previewPanel.Handle +
            " --force-window=yes --no-terminal --vo=direct3d --hwdec=no" +
            " --volume=35 --mute=no --loop-file=inf --ao=wasapi,dsound,win32" +
            " --start=40" +
            " --log-file=\"" + logFile + "\"" +
            " \"" + song.Media + "\"";
        previewMpv.StartInfo.UseShellExecute  = false;
        previewMpv.StartInfo.CreateNoWindow   = true;
        previewMpv.StartInfo.WorkingDirectory = song.Folder;
        try { previewMpv.Start(); }
        catch { previewMpv = null; coverPreview.Visible = true; }
    }

    private void FadePreviewVolume()
    {
        // No-op: preview now starts mpv directly with the file (no IPC needed)
        previewFadeTimer.Stop();
    }

    private void StopPreviewMpv()
    {
        previewFadeTimer.Stop(); previewDebounceTimer.Stop();
        try { if (previewMpv != null && !previewMpv.HasExited) previewMpv.Kill(); } catch { }
        previewMpv = null; pendingPreviewSong = null;
        coverPreview.Visible = true; // restore cover art placeholder
    }

    // ── Playback ──────────────────────────────────────────────────────────────

    private void PlaySong(Song song)
    {
        if (song == null) return;
        if (mpvPath.Length == 0 || !File.Exists(mpvPath))
        { MessageBox.Show("No encontré mpv.exe. Ponlo junto al ejecutable.", "Falta mpv", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        StopPreviewMpv(); StopMpv();
        currentSong       = song;
        usingInstrumental = false;
        ParsePhrases(song.TxtPath);
        playerPanel.Visible = true;
        playerPanel.BringToFront();
        LayoutVideoPanel(); LayoutSliderOverlays();
        videoPanel.SendToBack();
        lyricsOverlay.BringToFront(); pitchOverlay.BringToFront(); vocalOverlay.BringToFront();
        lyricLine.TextToShow   = "";
        lyricLine.BallProgress = -1;
        nextLine.TextToShow    = song.DisplayName;
        nextLine.Invalidate();
        currentPhraseIndex = -1;
        pitchBar.Value = 0; vocalBar.Value = 0;
        videoPanel.CreateControl(); videoPanel.Update();
        Application.DoEvents();

        pipeName = "ultrastar_simple_" + Process.GetCurrentProcess().Id + "_" + DateTime.Now.Ticks;
        mpv = new Process();
        mpv.StartInfo.FileName = mpvPath;
        var logFile  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mpv.log");
        var audioArg = song.Audio.Length > 0 && song.Audio != song.Media ? " --audio-file=\"" + song.Audio + "\"" : "";
        if (song.Instrumental.Length > 0) audioArg += " --audio-file=\"" + song.Instrumental + "\"";
        mpv.StartInfo.Arguments       = "--wid=" + videoPanel.Handle +
            " --input-ipc-server=\"\\\\.\\pipe\\" + pipeName +
            "\" --force-window=yes --no-terminal --vo=direct3d --hwdec=no --volume=100 --mute=no" +
            " --ao=wasapi,dsound,win32 --log-file=\"" + logFile + "\"" + audioArg + " \"" + song.Media + "\"";
        mpv.StartInfo.UseShellExecute  = false;
        mpv.StartInfo.CreateNoWindow   = true;
        mpv.StartInfo.WorkingDirectory = song.Folder;
        mpv.Start();
        timer.Start();
    }

    private void ShowSongGrid() { StopMpv(); playerPanel.Visible = false; }

    private void StopMpv()
    {
        timer.Stop();
        try { if (mpv != null && !mpv.HasExited) mpv.Kill(); } catch { }
        mpv = null; pipeName = "";
    }

    // ── Karaoke ───────────────────────────────────────────────────────────────

    private void ParsePhrases(string txtPath)
    {
        phrases.Clear();
        var bpm = 300.0; var gap = 0; var current = new Phrase();
        foreach (var raw in ReadUltraStarLines(txtPath))
        {
            var line = raw.TrimEnd();
            if      (line.StartsWith("#BPM:")) bpm = ParseDouble(line.Substring(5), 300.0);
            else if (line.StartsWith("#GAP:")) gap = (int)ParseDouble(line.Substring(5), 0);
            else if (line.StartsWith(":") || line.StartsWith("*") || line.StartsWith("F"))
            {
                var parts = line.Split(new[] { ' ' }, 5);
                if (parts.Length >= 5)
                {
                    int start, dur;
                    Int32.TryParse(parts[1], out start); Int32.TryParse(parts[2], out dur);
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
        phrase.Bpm = bpm; phrase.Gap = gap;
        phrases.Add(phrase);
        current = new Phrase();
    }

    private void UpdateKaraoke()
    {
        try { if (mpv != null && mpv.HasExited) { ShowSongGrid(); return; } } catch { }
        var seconds = QueryMpvTime();
        if (seconds < 0) return;

        var index = -1;
        for (int i = 0; i < phrases.Count; i++)
            if (seconds >= phrases[i].StartSeconds() - 0.05 && seconds <= phrases[i].EndSeconds() + 0.35) { index = i; break; }

        if (index >= 0)
        {
            var phrase = phrases[index];
            lyricLine.TextToShow   = phrase.Text();
            lyricLine.Progress     = phrase.Progress(seconds);
            lyricLine.BallProgress = -1;
            lyricLine.Invalidate();
            if (currentPhraseIndex != index)
            {
                currentPhraseIndex  = index;
                nextLine.TextToShow = index + 1 < phrases.Count ? phrases[index + 1].Text() : "";
                nextLine.Invalidate();
            }
        }
        else
        {
            var nextIndex = -1;
            for (int i = 0; i < phrases.Count; i++)
                if (phrases[i].StartSeconds() > seconds) { nextIndex = i; break; }

            if (nextIndex >= 0)
            {
                if (currentPhraseIndex != nextIndex)
                {
                    currentPhraseIndex  = nextIndex;
                    lyricLine.TextToShow = phrases[nextIndex].Text();
                    lyricLine.Progress   = 0;
                    nextLine.TextToShow  = nextIndex + 1 < phrases.Count ? phrases[nextIndex + 1].Text() : "";
                    nextLine.Invalidate();
                }
                var gapStart    = nextIndex > 0 ? phrases[nextIndex - 1].EndSeconds() : 0.0;
                var gapEnd      = phrases[nextIndex].StartSeconds();
                var gapDuration = gapEnd - gapStart;
                lyricLine.BallProgress = gapDuration >= 0.3
                    ? Math.Max(0.0, Math.Min(1.0, (seconds - gapStart) / gapDuration))
                    : -1;
                lyricLine.Invalidate();
            }
            else
            {
                lyricLine.TextToShow   = "";
                lyricLine.Progress     = 0;
                lyricLine.BallProgress = -1;
                lyricLine.Invalidate();
            }
        }
    }

    private void ApplyAudioFilters()
    {
        if (String.IsNullOrEmpty(pipeName)) return;
        ApplyInstrumentalMode();
        var filters = new List<string>();
        if (pitchBar.Value != 0)
        {
            var scale = Math.Pow(2.0, pitchBar.Value / 12.0).ToString("0.0000", CultureInfo.InvariantCulture);
            filters.Add("rubberband=pitch-scale=" + scale + ":transients=crisp:detector=compound:phase=laminar:window=long:smoothing=on:formant=preserved");
        }
        if (vocalBar.Value > 0)
        {
            var amount = (vocalBar.Value / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
            filters.Add("lavfi=[pan=stereo|c0=c0-" + amount + "*c1|c1=c1-" + amount + "*c0]");
        }
        if (filters.Count == 0) SendMpv("{\"command\":[\"set_property\",\"af\",[]]}");
        else                    SendMpv("{\"command\":[\"set_property\",\"af\",\"" + JsonEscape(String.Join(",", filters.ToArray())) + "\"]}");
    }

    private void ApplyInstrumentalMode()
    {
        if (currentSong == null || currentSong.Instrumental.Length == 0) return;
        var shouldUse = vocalBar.Value >= 50;
        if (shouldUse == usingInstrumental) return;
        usingInstrumental = shouldUse;
        SendMpv("{\"command\":[\"set_property\",\"aid\"," + (shouldUse ? 2 : 1) + "]}");
    }

    // ── mpv IPC ───────────────────────────────────────────────────────────────

    private double QueryMpvTime()
    {
        try
        {
            using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
            {
                pipe.Connect(45);
                var bytes = Encoding.UTF8.GetBytes("{\"command\":[\"get_property\",\"time-pos\"],\"request_id\":1}\n");
                pipe.Write(bytes, 0, bytes.Length); pipe.Flush();
                var buf  = new byte[4096];
                var read = pipe.Read(buf, 0, buf.Length);
                var m    = Regex.Match(Encoding.UTF8.GetString(buf, 0, read), "\"data\"\\s*:\\s*([0-9\\.]+)");
                if (m.Success) return ParseDouble(m.Groups[1].Value, -1);
            }
        }
        catch { }
        return -1;
    }

    private void SendMpv(string json) { SendMpvToPipe(pipeName, json); }

    private void SendMpvToPipe(string targetPipe, string json)
    {
        try
        {
            using (var pipe = new NamedPipeClientStream(".", targetPipe, PipeDirection.Out))
            {
                pipe.Connect(180);
                var bytes = Encoding.UTF8.GetBytes(json + "\n");
                pipe.Write(bytes, 0, bytes.Length);
            }
        }
        catch { }
    }

    private void WaitForPipe(string targetPipe, int timeoutMs)
    {
        var start = Environment.TickCount;
        while (Environment.TickCount - start < timeoutMs)
        {
            try { using (var p = new NamedPipeClientStream(".", targetPipe, PipeDirection.Out)) { p.Connect(60); return; } }
            catch { System.Threading.Thread.Sleep(30); }
        }
    }

    // ── Keys ──────────────────────────────────────────────────────────────────

    private void HandleKeys(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            if (playerPanel.Visible) ShowSongGrid();
            else if (MessageBox.Show("¿Salir de UltraStar Simplificado?", "Salir",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Close();
            e.Handled = true; return;
        }
        if (e.KeyCode == Keys.F11)    { ToggleFullscreen(); e.Handled = true; return; }
        if (e.KeyCode == Keys.Space && playerPanel.Visible) { SendMpv("{\"command\":[\"cycle\",\"pause\"]}"); e.Handled = true; return; }
        if (!playerPanel.Visible) vGrid.HandleKey(e.KeyCode);
    }

    private void SearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (playerPanel.Visible) { e.Handled = true; e.SuppressKeyPress = true; return; }
        if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down ||
            e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter)
        { vGrid.HandleKey(e.KeyCode); e.Handled = true; e.SuppressKeyPress = true; }
    }

    private void ToggleFullscreen()
    {
        isFullscreen    = !isFullscreen;
        FormBorderStyle = isFullscreen ? FormBorderStyle.None : FormBorderStyle.Sizable;
        WindowState     = isFullscreen ? FormWindowState.Maximized : FormWindowState.Normal;
    }

    // ── Parsing helpers ───────────────────────────────────────────────────────

    private string[] ReadUltraStarLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        try { return new UTF8Encoding(false, true).GetString(bytes).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None); }
        catch { return Encoding.GetEncoding(1252).GetString(bytes).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None); }
    }

    private double ParseDouble(string value, double fallback)
    {
        double v;
        return Double.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : fallback;
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new UltraStarSimplificadoForm());
    }

    // ── Data classes ──────────────────────────────────────────────────────────

    private class Song
    {
        public string TxtPath = ""; public string Folder = ""; public string Title = ""; public string Artist = "";
        public string Year = ""; public string Creator = ""; public string Genre = "";
        public string Cover = ""; public string Media = ""; public string Audio = "";
        public string NormalizedAudio = ""; public string Instrumental = ""; public string NormalizedInstrumental = "";
        public string DisplayName { get { return (Artist.Length > 0 ? Artist + " - " : "") + Title; } }
    }

    private class Phrase
    {
        public int StartBeat; public int EndBeat; public double Bpm = 300; public int Gap;
        public readonly List<string> Words = new List<string>();
        public string Text() { return Regex.Replace(String.Join("", Words.ToArray()), "\\s+", " ").Trim(); }
        public double StartSeconds() { return Gap / 1000.0 + StartBeat * 15.0 / Bpm; }
        public double EndSeconds()   { return Gap / 1000.0 + EndBeat   * 15.0 / Bpm; }
        public double Progress(double s) { return Math.Max(0, Math.Min(1, (s - StartSeconds()) / Math.Max(0.1, EndSeconds() - StartSeconds()))); }
    }
}

// ── VGridSong ─────────────────────────────────────────────────────────────────

public class VGridSong
{
    public string TxtPath = ""; public string Artist = ""; public string Title = ""; public string ThumbPath = "";
}

// ── VirtualSongGrid ───────────────────────────────────────────────────────────

public class VirtualSongGrid : Panel
{
    private int _coverS = 135;
    private int CardW { get { return _coverS + 20; } }
    private int CardH { get { return _coverS + 75; } }
    private const int GAP      = 7;
    private const int MAX_CACHE = 400;
    private int CellW { get { return CardW + GAP * 2; } }
    private int CellH { get { return CardH + GAP * 2; } }

    private List<VGridSong> _songs = new List<VGridSong>();
    private int _selectedIndex = -1;

    private readonly LinkedList<string>                         _lruOrder = new LinkedList<string>();
    private readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new Dictionary<string, LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Image>                  _imgCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string>                            _pending  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly VScrollBar        _vbar;
    private readonly Func<string, bool> _isFavFn;

    private Font        _artistFont, _titleFont, _starFont, _placeholderFont;
    private readonly SolidBrush  _bgBrush, _bgSelBrush, _coverBgBrush, _whiteBrush, _grayBrush, _goldBrush, _dimStarBrush;
    private readonly StringFormat _centerFmt, _labelFmt;
    private float _rainbowPhase = 0f;
    private readonly System.Windows.Forms.Timer _animTimer;

    public event Action<VGridSong> SongActivated;
    public event Action<VGridSong> SongHovered;
    public event Action<VGridSong> FavoriteToggled;

    public VirtualSongGrid(Func<string, bool> isFavFn)
    {
        DoubleBuffered = true;
        BackColor      = Color.FromArgb(32, 12, 38);
        _isFavFn       = isFavFn;
        UpdateFonts();

        _bgBrush      = new SolidBrush(Color.FromArgb(28, 28, 34));
        _bgSelBrush   = new SolidBrush(Color.FromArgb(55, 20, 70));
        _coverBgBrush = new SolidBrush(Color.FromArgb(45, 45, 52));
        _whiteBrush   = new SolidBrush(Color.White);
        _grayBrush    = new SolidBrush(Color.Gray);
        _goldBrush    = new SolidBrush(Color.Gold);
        _dimStarBrush = new SolidBrush(Color.FromArgb(100, 100, 110));

        _centerFmt           = new StringFormat();
        _centerFmt.Alignment = StringAlignment.Center;
        _centerFmt.LineAlignment = StringAlignment.Center;

        _labelFmt              = new StringFormat();
        _labelFmt.Alignment    = StringAlignment.Center;
        _labelFmt.LineAlignment = StringAlignment.Center;
        _labelFmt.Trimming     = StringTrimming.EllipsisCharacter;
        _labelFmt.FormatFlags  = StringFormatFlags.LineLimit;

        _animTimer          = new System.Windows.Forms.Timer();
        _animTimer.Interval = 40;
        _animTimer.Tick    += delegate { _rainbowPhase = (_rainbowPhase + 0.018f) % 1.0f; if (_selectedIndex >= 0) Invalidate(); };
        _animTimer.Start();

        _vbar          = new VScrollBar();
        _vbar.Dock     = DockStyle.Right;
        _vbar.TabStop  = false;
        _vbar.Scroll  += delegate { Invalidate(); };
        Controls.Add(_vbar);

        Resize           += delegate { UpdateScrollBar(); Invalidate(); };
        MouseClick       += OnMouseClick;
        MouseDoubleClick += OnMouseDoubleClick;
        MouseWheel       += OnMouseWheelHandler;
    }

    private void UpdateFonts()
    {
        if (_artistFont      != null) _artistFont.Dispose();
        if (_titleFont       != null) _titleFont.Dispose();
        if (_starFont        != null) _starFont.Dispose();
        if (_placeholderFont != null) _placeholderFont.Dispose();
        var scale        = _coverS / 135f;
        _artistFont      = new Font("Segoe UI", Math.Max(6f, Math.Min(13f, 8f  * scale)), FontStyle.Bold);
        _titleFont       = new Font("Segoe UI", Math.Max(6f, Math.Min(12f, 8f  * scale)));
        _starFont        = new Font("Segoe UI", Math.Max(10f, Math.Min(24f, 17f * scale)));
        _placeholderFont = new Font("Segoe UI", Math.Max(10f, Math.Min(32f, 20f * scale)));
    }

    public int  CoverSize { get { return _coverS; } }

    public void SetCoverSize(int size) { _coverS = Math.Max(70, Math.Min(260, size)); UpdateFonts(); UpdateScrollBar(); Invalidate(); }

    public void SetSongs(List<VGridSong> songs)
    {
        _songs         = songs ?? new List<VGridSong>();
        _selectedIndex = _songs.Count > 0 ? 0 : -1;
        UpdateScrollBar(); Invalidate();
    }

    private int Columns     { get { return Math.Max(1, (Width - (_vbar.Visible ? _vbar.Width : 0)) / CellW); } }
    private int GridOffsetX
    {
        get
        {
            var usable = Width - (_vbar.Visible ? _vbar.Width : 0);
            return Math.Max(0, (usable - Math.Max(1, usable / CellW) * CellW) / 2);
        }
    }

    private void UpdateScrollBar()
    {
        var cols  = Columns;
        var total = ((_songs.Count + cols - 1) / cols) * CellH;
        if (total <= Height) { _vbar.Visible = false; _vbar.Value = 0; }
        else
        {
            _vbar.Visible     = true;
            _vbar.Minimum     = 0;
            _vbar.Maximum     = total;
            _vbar.LargeChange = Height;
            _vbar.SmallChange = CellH / 2;
            var maxVal        = Math.Max(0, _vbar.Maximum - _vbar.LargeChange);
            if (_vbar.Value > maxVal) _vbar.Value = maxVal;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (_songs.Count == 0) return;

        var cols    = Columns;
        var offsetX = GridOffsetX;
        var scrollY = _vbar.Visible ? _vbar.Value : 0;
        var first   = (scrollY / CellH) * cols;
        var last    = Math.Min(_songs.Count, ((scrollY + Height + CellH - 1) / CellH + 1) * cols);
        var toLoad  = new List<VGridSong>();

        for (int i = first; i < last; i++)
        {
            DrawCard(g, i, offsetX + (i % cols) * CellW + GAP, (i / cols) * CellH + GAP - scrollY);
            var song = _songs[i];
            if (song.ThumbPath.Length > 0 && !_imgCache.ContainsKey(song.TxtPath) && !_pending.Contains(song.TxtPath))
            { _pending.Add(song.TxtPath); toLoad.Add(song); }
        }

        foreach (var s in toLoad)
        {
            var cap = s;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Image img = null;
                if (File.Exists(cap.ThumbPath))
                    try { using (var ms = new MemoryStream(File.ReadAllBytes(cap.ThumbPath))) img = Image.FromStream(ms); } catch { }
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke((Action)delegate { _pending.Remove(cap.TxtPath); if (img != null) AddToCache(cap.TxtPath, img); Invalidate(); });
                else if (img != null) img.Dispose();
            });
        }
    }

    private void DrawCard(Graphics g, int index, int x, int y)
    {
        var song     = _songs[index];
        var selected = index == _selectedIndex;

        g.FillRectangle(selected ? _bgSelBrush : _bgBrush, x, y, CardW, CardH);
        if (selected) DrawRainbowBorder(g, x, y);

        Image img;
        if (_imgCache.TryGetValue(song.TxtPath, out img) && img != null)
            g.DrawImage(img, x + 10, y + 10, _coverS, _coverS);
        else
        {
            g.FillRectangle(_coverBgBrush, x + 10, y + 10, _coverS, _coverS);
            g.DrawString("♪", _placeholderFont, _grayBrush, new RectangleF(x + 10, y + 10, _coverS, _coverS), _centerFmt);
        }

        var isFav = _isFavFn != null && _isFavFn(song.TxtPath);
        g.DrawString(isFav ? "★" : "☆", _starFont, isFav ? _goldBrush : _dimStarBrush,
            new RectangleF(x + CardW - 32, y + 1, 30, 30), _centerFmt);

        // Row 1: ARTIST — always uppercase+bold, even if empty
        // Row 2: SONG TITLE — always here, never substituted by path
        var artistText = song.Artist.ToUpperInvariant();
        var titleText  = song.Title;
        if (artistText.Length > 0)
        {
            g.DrawString(artistText, _artistFont, _whiteBrush, new RectangleF(x + 8, y + _coverS + 10, CardW - 16, 28), _labelFmt);
            g.DrawString(titleText,  _titleFont,  _whiteBrush, new RectangleF(x + 8, y + _coverS + 38, CardW - 16, 30), _labelFmt);
        }
        else
        {
            // No artist: center title vertically in the text area
            g.DrawString(titleText, _artistFont, _whiteBrush, new RectangleF(x + 8, y + _coverS + 18, CardW - 16, 50), _labelFmt);
        }
    }

    private void DrawRainbowBorder(Graphics g, int x, int y)
    {
        const int N = 96; const float THICK = 4f; const float GLOW = 10f;
        var r = new Rectangle(x + 1, y + 1, CardW - 3, CardH - 3);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < N; i++)
        {
            float t0  = (float)i / N;
            var   col = HsvToRgb((_rainbowPhase + t0) % 1.0f, 1f, 1f);
            var   p0  = PerimeterPoint(r, t0);
            var   p1  = PerimeterPoint(r, (float)(i + 1) / N);
            using (var pen = new Pen(Color.FromArgb(60, col.R, col.G, col.B), GLOW))
            { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; g.DrawLine(pen, p0, p1); }
            using (var pen = new Pen(col, THICK))
            { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; g.DrawLine(pen, p0, p1); }
        }
        g.SmoothingMode = SmoothingMode.None;
    }

    private static PointF PerimeterPoint(Rectangle r, float t)
    {
        float w = r.Width, h = r.Height, d = t * (2f * (w + h));
        if (d <= w)  return new PointF(r.Left + d, r.Top);   d -= w;
        if (d <= h)  return new PointF(r.Right,    r.Top + d); d -= h;
        if (d <= w)  return new PointF(r.Right - d, r.Bottom); d -= w;
        return new PointF(r.Left, r.Bottom - d);
    }

    private static Color HsvToRgb(float h, float s, float v)
    {
        int hi = (int)(h * 6f) % 6; float f = h * 6f - (int)(h * 6f);
        float p = v*(1f-s), q = v*(1f-f*s), tt = v*(1f-(1f-f)*s);
        float cr, cg, cb;
        switch (hi) {
            case 0: cr=v; cg=tt; cb=p; break; case 1: cr=q; cg=v; cb=p; break;
            case 2: cr=p; cg=v; cb=tt;break; case 3: cr=p; cg=q; cb=v; break;
            case 4: cr=tt;cg=p; cb=v; break; default:cr=v; cg=p; cb=q; break;
        }
        return Color.FromArgb((int)(cr*255f),(int)(cg*255f),(int)(cb*255f));
    }

    private int HitTest(int mx, int my)
    {
        var cols = Columns; var ox = GridOffsetX; var sy = _vbar.Visible ? _vbar.Value : 0;
        var col  = (mx - ox) / CellW;
        if (col < 0 || col >= cols) return -1;
        var row = (my + sy) / CellH;
        var idx = row * cols + col;
        if (idx < 0 || idx >= _songs.Count) return -1;
        var cx = ox + col * CellW + GAP; var cy = row * CellH + GAP - sy;
        if (mx < cx || mx > cx + CardW || my < cy || my > cy + CardH) return -1;
        return idx;
    }

    private bool IsStarHit(int mx, int my, int idx)
    {
        if (idx < 0 || idx >= _songs.Count) return false;
        var cols = Columns; var ox = GridOffsetX; var sy = _vbar.Visible ? _vbar.Value : 0;
        var x = ox + (idx % cols) * CellW + GAP;
        var y = (idx / cols) * CellH + GAP - sy;
        return mx >= x + CardW - 32 && mx <= x + CardW - 2 && my >= y + 1 && my <= y + 31;
    }

    private void OnMouseClick(object sender, MouseEventArgs e)
    {
        Focus(); var idx = HitTest(e.X, e.Y); if (idx < 0) return;
        if (IsStarHit(e.X, e.Y, idx)) { if (FavoriteToggled != null) FavoriteToggled(_songs[idx]); Invalidate(); return; }
        _selectedIndex = idx; Invalidate();
        if (SongHovered != null) SongHovered(_songs[idx]);
    }

    private void OnMouseDoubleClick(object sender, MouseEventArgs e)
    {
        var idx = HitTest(e.X, e.Y);
        if (idx < 0 || IsStarHit(e.X, e.Y, idx)) return;
        _selectedIndex = idx; Invalidate();
        if (SongActivated != null) SongActivated(_songs[idx]);
    }

    private void OnMouseWheelHandler(object sender, MouseEventArgs e)
    {
        if (!_vbar.Visible) return;
        var maxVal = Math.Max(0, _vbar.Maximum - _vbar.LargeChange);
        _vbar.Value = Math.Max(0, Math.Min(maxVal, _vbar.Value + (-e.Delta / 120 * (CellH / 2))));
        Invalidate();
    }

    public bool HandleKey(Keys key)
    {
        if (_songs.Count == 0) return false;
        var cols = Columns; var old = _selectedIndex < 0 ? 0 : _selectedIndex; int next = old;
        if      (key == Keys.Right) next = Math.Min(_songs.Count - 1, old + 1);
        else if (key == Keys.Left)  next = Math.Max(0, old - 1);
        else if (key == Keys.Down)  next = Math.Min(_songs.Count - 1, old + cols);
        else if (key == Keys.Up)    next = Math.Max(0, old - cols);
        else if (key == Keys.Enter) { if (_selectedIndex >= 0 && SongActivated != null) SongActivated(_songs[_selectedIndex]); return true; }
        else return false;
        _selectedIndex = next; Invalidate(); ScrollToSelected();
        if (SongHovered != null) SongHovered(_songs[_selectedIndex]);
        return true;
    }

    private void ScrollToSelected()
    {
        if (_selectedIndex < 0 || !_vbar.Visible) return;
        var sy  = _vbar.Value; var row = _selectedIndex / Columns;
        var top = row * CellH; var bot = top + CellH;
        var max = Math.Max(0, _vbar.Maximum - _vbar.LargeChange);
        if      (top < sy)           _vbar.Value = Math.Max(0,   Math.Min(max, top - GAP));
        else if (bot > sy + Height)  _vbar.Value = Math.Max(0,   Math.Min(max, bot - Height + GAP));
    }

    protected override bool IsInputKey(Keys keyData)
    {
        switch (keyData & ~Keys.Modifiers)
        { case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true; }
        return base.IsInputKey(keyData);
    }

    private void AddToCache(string key, Image img)
    {
        LinkedListNode<string> existing;
        if (_lruNodes.TryGetValue(key, out existing))
        { _lruOrder.Remove(existing); _lruNodes.Remove(key); Image old; if (_imgCache.TryGetValue(key, out old) && old != null) old.Dispose(); _imgCache.Remove(key); }
        while (_lruOrder.Count >= MAX_CACHE)
        {
            var last = _lruOrder.Last.Value; _lruOrder.RemoveLast(); _lruNodes.Remove(last);
            Image old; if (_imgCache.TryGetValue(last, out old)) { _imgCache.Remove(last); if (old != null) old.Dispose(); }
        }
        _imgCache[key] = img; _lruNodes[key] = _lruOrder.AddFirst(key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var img in _imgCache.Values) if (img != null) img.Dispose();
            _imgCache.Clear();
            _artistFont.Dispose(); _titleFont.Dispose(); _starFont.Dispose(); _placeholderFont.Dispose();
            _bgBrush.Dispose(); _bgSelBrush.Dispose(); _coverBgBrush.Dispose();
            _whiteBrush.Dispose(); _grayBrush.Dispose(); _goldBrush.Dispose(); _dimStarBrush.Dispose();
            _animTimer.Dispose(); _centerFmt.Dispose(); _labelFmt.Dispose();
        }
        base.Dispose(disposing);
    }
}

// ── KaraokeLineView ───────────────────────────────────────────────────────────

public class KaraokeLineView : Control
{
    public string TextToShow   = "";
    public double Progress;
    public double BallProgress = -1;

    public KaraokeLineView()
    {
        DoubleBuffered = true;
        BackColor      = Color.FromArgb(12, 12, 14);
        Font           = new Font("Segoe UI", 30, FontStyle.Bold);
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
            format.Alignment     = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.Trimming      = StringTrimming.EllipsisCharacter;

            // White base text with black outline
            using (var path = new GraphicsPath())
            using (var outline = new Pen(Color.Black, 7) { LineJoin = LineJoin.Round })
            using (var baseBrush = new SolidBrush(Color.White))
            {
                path.AddString(text, Font.FontFamily, (int)Font.Style, e.Graphics.DpiY * Font.Size / 72f, rect, format);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(outline, path); e.Graphics.FillPath(baseBrush, path);
            }

            // Pink progress overlay
            var size    = e.Graphics.MeasureString(text, Font, rect.Size, format);
            var left    = rect.Left + (rect.Width - size.Width) / 2f;
            var width   = Math.Max(0, Math.Min(size.Width, size.Width * (float)Progress));
            var oldClip = e.Graphics.Clip;
            e.Graphics.SetClip(new RectangleF(left, rect.Top, width, rect.Height));
            using (var path = new GraphicsPath())
            using (var outline = new Pen(Color.Black, 7) { LineJoin = LineJoin.Round })
            using (var sungBrush = new SolidBrush(Color.FromArgb(255, 0, 170)))
            {
                path.AddString(text, Font.FontFamily, (int)Font.Style, e.Graphics.DpiY * Font.Size / 72f, rect, format);
                e.Graphics.DrawPath(outline, path); e.Graphics.FillPath(sungBrush, path);
            }
            e.Graphics.Clip = oldClip;

            // Timing ball: slides from left toward text start — no bounce
            if (BallProgress >= 0)
            {
                var bp      = (float)Math.Max(0, Math.Min(1.0, BallProgress));
                var bStartX = rect.Left + 10f;
                var bEndX   = Math.Max(bStartX + 4f, left - 20f);
                var bx      = bStartX + (bEndX - bStartX) * bp;
                var by      = rect.Bottom - 16f;
                const float R = 10f;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var glow  = new SolidBrush(Color.FromArgb(60,  255, 0, 170)))
                    e.Graphics.FillEllipse(glow,  bx - R * 1.8f, by - R * 1.8f, R * 3.6f, R * 3.6f);
                using (var fill  = new SolidBrush(Color.FromArgb(255, 255, 0, 170)))
                    e.Graphics.FillEllipse(fill,  bx - R,        by - R,        R * 2f,   R * 2f);
                using (var shine = new SolidBrush(Color.FromArgb(130, 255, 255, 255)))
                    e.Graphics.FillEllipse(shine, bx - R * 0.45f, by - R * 0.7f, R * 0.65f, R * 0.55f);
            }
        }
    }
}

// ── NextLineView ──────────────────────────────────────────────────────────────

public class NextLineView : Control
{
    public string TextToShow = "";

    public NextLineView()
    {
        DoubleBuffered = true;
        BackColor      = Color.FromArgb(12, 12, 14);
        ForeColor      = Color.FromArgb(220, 220, 225);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        e.Graphics.Clear(BackColor);
        var rect = ClientRectangle;
        rect.Inflate(-16, -4);
        using (var format = new StringFormat())
        using (var brush  = new SolidBrush(ForeColor))
        {
            format.Alignment     = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.Trimming      = StringTrimming.EllipsisCharacter;
            e.Graphics.DrawString(TextToShow ?? "", Font, brush, rect, format);
        }
    }
}
