using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public class MainForm : Form
{
    TextBox txtInput = new();
    TextBox txtOutput = new();
    NumericUpDown numMinutes = new();
    Button btnInput = new();
    Button btnOutput = new();
    Button btnStart = new();
    TextBox logBox = new();

    Process? ffmpeg;

    public MainForm()
    {
        Text = "MP4 無劣化 自動分割ツール";
        Width = 720;
        Height = 520;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 150,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(10)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        txtInput.Dock = DockStyle.Fill;
        txtOutput.Dock = DockStyle.Fill;

        btnInput.Text = "選択";
        btnOutput.Text = "選択";
        btnStart.Text = "分割開始";

        numMinutes.Minimum = 1;
        numMinutes.Maximum = 240;
        numMinutes.Value = 30;
        numMinutes.Dock = DockStyle.Left;

        panel.Controls.Add(new Label { Text = "入力MP4", AutoSize = true }, 0, 0);
        panel.Controls.Add(txtInput, 1, 0);
        panel.Controls.Add(btnInput, 2, 0);

        panel.Controls.Add(new Label { Text = "出力フォルダ", AutoSize = true }, 0, 1);
        panel.Controls.Add(txtOutput, 1, 1);
        panel.Controls.Add(btnOutput, 2, 1);

        panel.Controls.Add(new Label { Text = "分割分数", AutoSize = true }, 0, 2);
        panel.Controls.Add(numMinutes, 1, 2);

        panel.Controls.Add(btnStart, 2, 3);

        logBox.Dock = DockStyle.Fill;
        logBox.Multiline = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.Font = new System.Drawing.Font("Consolas", 10);

        Controls.Add(logBox);
        Controls.Add(panel);

        btnInput.Click += (_, _) => SelectInput();
        btnOutput.Click += (_, _) => SelectOutput();
        btnStart.Click += async (_, _) => await StartSplit();
    }

    void SelectInput()
    {
        using var ofd = new OpenFileDialog();
        ofd.Filter = "MP4 files (*.mp4)|*.mp4|All files (*.*)|*.*";

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            txtInput.Text = ofd.FileName;

            txtOutput.Text = Path.Combine(
                Path.GetDirectoryName(ofd.FileName)!,
                Path.GetFileNameWithoutExtension(ofd.FileName) + "_split"
            );
        }
    }

    void SelectOutput()
    {
        using var fbd = new FolderBrowserDialog();
        if (fbd.ShowDialog() == DialogResult.OK)
            txtOutput.Text = fbd.SelectedPath;
    }

    async Task StartSplit()
    {
        string input = txtInput.Text.Trim();
        string outputDir = txtOutput.Text.Trim();

        if (!File.Exists(input))
        {
            MessageBox.Show("入力MP4が見つかりません。");
            return;
        }

        string ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");

        if (!File.Exists(ffmpegPath))
        {
            MessageBox.Show("ffmpeg.exe をこのソフトと同じフォルダに置いてください。");
            return;
        }

        Directory.CreateDirectory(outputDir);

        int seconds = (int)numMinutes.Value * 60;

        string baseName = Path.GetFileNameWithoutExtension(input);
        string outputPattern = Path.Combine(outputDir, baseName + "_part_%03d.mp4");

        logBox.Clear();
        Log("分割開始...");
        Log("※ 無劣化モード（-c copy）");
        Log("※ キーフレームの関係で分割位置は少しズレる可能性あり");

        btnStart.Enabled = false;

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments =
                $"-y -i \"{input}\" " +
                $"-map 0 -c copy " +
                $"-f segment -segment_time {seconds} " +
                $"-reset_timestamps 1 " +
                $"\"{outputPattern}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8
        };

        ffmpeg = new Process();
        ffmpeg.StartInfo = psi;

        ffmpeg.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Log(e.Data);
        };

        ffmpeg.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Log(e.Data);
        };

        try
        {
            ffmpeg.Start();
            ffmpeg.BeginOutputReadLine();
            ffmpeg.BeginErrorReadLine();

            await ffmpeg.WaitForExitAsync();

            if (ffmpeg.ExitCode == 0)
            {
                Log("");
                Log("完了！");
                MessageBox.Show("分割完了！");
            }
            else
            {
                Log("");
                Log("エラー終了");
                MessageBox.Show("分割失敗。ログ確認して。");
            }
        }
        catch (Exception ex)
        {
            Log("エラー: " + ex.Message);
            MessageBox.Show(ex.Message);
        }
        finally
        {
            btnStart.Enabled = true;
            ffmpeg?.Dispose();
            ffmpeg = null;
        }
    }

    void Log(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(text));
            return;
        }

        logBox.AppendText(text + Environment.NewLine);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (ffmpeg != null && !ffmpeg.HasExited)
        {
            try { ffmpeg.Kill(true); } catch { }
        }

        base.OnFormClosing(e);
    }
}