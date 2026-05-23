using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class NormalizarCancionesForm : Form
{
    private readonly TextBox songsBox = new TextBox();
    private readonly TextBox logBox = new TextBox();
    private Process activeProcess;

    public NormalizarCancionesForm()
    {
        Text = "Normalizar Canciones";
        Width = 900;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 245, 248);

        Controls.Add(new Label { Text = "Carpeta songs", Left = 18, Top = 20, Width = 110, Height = 24 });
        songsBox.Left = 132;
        songsBox.Top = 16;
        songsBox.Width = 610;
        songsBox.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "songs");
        Controls.Add(songsBox);

        var browseButton = new Button { Text = "...", Left = 752, Top = 14, Width = 42, Height = 30 };
        browseButton.Click += delegate { PickSongsFolder(); };
        Controls.Add(browseButton);

        var processButton = new Button { Text = "Normalizar canciones", Left = 18, Top = 58, Width = 180, Height = 34 };
        processButton.Click += delegate { ProcessSongs(); };
        Controls.Add(processButton);

        var stopButton = new Button { Text = "Detener", Left = 210, Top = 58, Width = 90, Height = 34 };
        stopButton.Click += delegate { StopActiveProcess(); };
        Controls.Add(stopButton);

        logBox.Left = 18;
        logBox.Top = 108;
        logBox.Width = 846;
        logBox.Height = 452;
        logBox.Multiline = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.ReadOnly = true;
        logBox.BackColor = Color.White;
        logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(logBox);
    }

    private void PickSongsFolder()
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.SelectedPath = songsBox.Text;
            if (dialog.ShowDialog() == DialogResult.OK) songsBox.Text = dialog.SelectedPath;
        }
    }

    private void ProcessSongs()
    {
        var script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "normalizar_canciones.py");
        if (!File.Exists(script))
        {
            MessageBox.Show("No encontre normalizar_canciones.py junto a esta app.", "Falta script", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Run("python", "\"" + script + "\" \"" + songsBox.Text + "\"");
    }

    private void Run(string file, string args)
    {
        StopActiveProcess();
        logBox.Clear();
        activeProcess = new Process();
        activeProcess.StartInfo.FileName = file;
        activeProcess.StartInfo.Arguments = args;
        activeProcess.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
        activeProcess.StartInfo.UseShellExecute = false;
        activeProcess.StartInfo.RedirectStandardOutput = true;
        activeProcess.StartInfo.RedirectStandardError = true;
        activeProcess.StartInfo.CreateNoWindow = true;
        activeProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { AppendLog(e.Data); };
        activeProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { AppendLog(e.Data); };
        activeProcess.EnableRaisingEvents = true;
        activeProcess.Exited += delegate { AppendLog("Terminado."); };
        activeProcess.Start();
        activeProcess.BeginOutputReadLine();
        activeProcess.BeginErrorReadLine();
    }

    private void AppendLog(string text)
    {
        if (text == null) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), text);
            return;
        }
        logBox.AppendText(text + Environment.NewLine);
    }

    private void StopActiveProcess()
    {
        try
        {
            if (activeProcess != null && !activeProcess.HasExited) activeProcess.Kill();
        }
        catch { }
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new NormalizarCancionesForm());
    }
}
