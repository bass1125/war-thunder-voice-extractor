using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WTExtractor
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    class MainForm : Form
    {
        ComboBox cmbLang;
        Label lblSrc;
        TextBox txtOut;
        CheckBox chkRename;
        Button btnStart;
        Label lblStatus;
        ProgressBar prg;
        ListBox lstFiles;
        bool extracting = false;
        string gameDir = null;
        string manualDir = null;
        List<string> outFiles = new List<string>();

        readonly Color cBg = Color.FromArgb(16, 16, 16);
        readonly Color cCtrl = Color.FromArgb(26, 26, 26);
        readonly Color cText = Color.FromArgb(240, 240, 240);
        readonly Color cBtn = Color.FromArgb(52, 52, 54);
        readonly Color cBtnHov = Color.FromArgb(80, 80, 85);
        readonly Color cBorder = Color.FromArgb(70, 70, 75);
        readonly Color cOk = Color.FromArgb(140, 210, 140);
        readonly Color cWarn = Color.FromArgb(235, 205, 130);
        readonly Color cErr = Color.FromArgb(235, 140, 140);

        readonly string[] candidates = {
            @"D:\Steam\steamapps\common\War Thunder\sound",
            @"C:\Program Files (x86)\Steam\steamapps\common\War Thunder\sound",
            @"C:\Program Files\Steam\steamapps\common\War Thunder\sound",
            @"D:\SteamLibrary\steamapps\common\War Thunder\sound",
            @"E:\SteamLibrary\steamapps\common\War Thunder\sound",
            @"F:\SteamLibrary\steamapps\common\War Thunder\sound",
            @"G:\SteamLibrary\steamapps\common\War Thunder\sound"
        };

        readonly string[] priority = { "de", "ru", "en_us", "en", "jp", "zh", "fr", "it", "sv", "he" };

        readonly Dictionary<string, string> countryNames = new Dictionary<string, string> {
            { "de", "德国" }, { "ru", "苏联/俄罗斯" }, { "en", "英语(英国)" }, { "en_us", "英语(美国)" },
            { "en_au", "英语(澳洲)" }, { "en_za", "英语(南非)" }, { "uk", "英语(英国)" }, { "us", "英语(美国)" }, { "jp", "日本" }, { "zh", "中国" },
            { "fr", "法国" }, { "it", "意大利" }, { "sv", "瑞典" }, { "he", "以色列" },
            { "pl", "波兰" }, { "fi", "芬兰" }, { "hu", "匈牙利" }, { "cz", "捷克" }, { "ko", "韩国" },
            { "tr", "土耳其" }, { "sp", "西班牙" }, { "pt", "葡萄牙" }, { "nl", "荷兰" }, { "nw", "挪威" },
            { "gl", "希腊" }, { "lt", "立陶宛" }, { "sr", "塞尔维亚" }, { "ar", "阿拉伯" }, { "hi", "印度" },
            { "vi", "越南" }, { "th", "泰国" }
        };

        public MainForm()
        {
            Text = "战争雷霆 · 语音包提取器 V1.0      开发者：i不是庸医  抖音：22451437057";
            Size = new Size(640, 570);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = cBg;
            Font = new Font("Microsoft YaHei UI", 9.5f);

            string icoPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "icon.ico");
            if (System.IO.File.Exists(icoPath))
            {
                try { Icon = new Icon(icoPath); } catch { }
            }

            BuildControls();
            DetectGameDir();
            RefreshLangs();
        }

        Label MakeLabel(string text, int x, int y, int w, Color c)
        {
            var l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.Size = new Size(w, 22);
            l.ForeColor = c;
            l.BackColor = cBg;
            l.Font = Font;
            return l;
        }

        Button MakeBtn(string text, int x, int y, int w, int h)
        {
            var b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.BackColor = cBtn;
            b.ForeColor = cText;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.MouseOverBackColor = cBtnHov;
            b.FlatAppearance.BorderColor = cBorder;
            b.Font = Font;
            return b;
        }

        void BuildControls()
        {
            Controls.Add(MakeLabel("国家:", 20, 22, 70, cText));
            cmbLang = new ComboBox();
            cmbLang.Location = new Point(110, 18);
            cmbLang.Size = new Size(230, 26);
            cmbLang.DropDownStyle = ComboBoxStyle.DropDown;
            cmbLang.BackColor = cCtrl;
            cmbLang.ForeColor = cText;
            cmbLang.FlatStyle = FlatStyle.Flat;
            Controls.Add(cmbLang);

            lblSrc = MakeLabel("", 355, 22, 260, cOk);
            Controls.Add(lblSrc);

            var btnDir = MakeBtn("手动选文件夹…", 110, 52, 140, 28);
            btnDir.Click += (s, e) =>
            {
                var dlg = new FolderBrowserDialog();
                dlg.Description = "选择游戏 sound 文件夹（含 .bank 语音包）";
                if (gameDir != null) dlg.SelectedPath = gameDir;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    manualDir = dlg.SelectedPath;
                    lblSrc.Text = "手动: " + manualDir;
                    lblSrc.ForeColor = cWarn;
                    RefreshLangs();
                }
            };
            Controls.Add(btnDir);

            Controls.Add(MakeLabel("输出目录:", 20, 95, 70, cText));
            txtOut = new TextBox();
            txtOut.Location = new Point(110, 92);
            txtOut.Size = new Size(310, 24);
            txtOut.Text = @"D:\HermesData\wt_voices_de";
            txtOut.BackColor = cCtrl;
            txtOut.ForeColor = cText;
            txtOut.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(txtOut);

            var btnOut = MakeBtn("…", 430, 92, 40, 24);
            btnOut.Click += (s, e) =>
            {
                var dlg = new FolderBrowserDialog();
                dlg.Description = "选择输出目录";
                if (Directory.Exists(txtOut.Text)) dlg.SelectedPath = txtOut.Text;
                if (dlg.ShowDialog() == DialogResult.OK) txtOut.Text = dlg.SelectedPath;
            };
            Controls.Add(btnOut);

            chkRename = new CheckBox();
            chkRename.Text = "翻译成中文文件名（车长_开火_v1.ogg）";
            chkRename.Location = new Point(110, 128);
            chkRename.AutoSize = true;
            chkRename.Checked = true;
            chkRename.ForeColor = cText;
            chkRename.BackColor = cBg;
            Controls.Add(chkRename);

            btnStart = MakeBtn("开始提取", 110, 162, 140, 36);
            btnStart.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            btnStart.Click += StartExtract;
            Controls.Add(btnStart);

            lblStatus = MakeLabel("就绪", 110, 208, 460, cText);
            Controls.Add(lblStatus);

            prg = new ProgressBar();
            prg.Location = new Point(110, 234);
            prg.Size = new Size(480, 18);
            prg.Minimum = 0;
            prg.Maximum = 100;
            Controls.Add(prg);

            Controls.Add(MakeLabel("提取结果:", 20, 268, 70, cText));
            lstFiles = new ListBox();
            lstFiles.Location = new Point(110, 265);
            lstFiles.Size = new Size(480, 185);
            lstFiles.HorizontalScrollbar = true;
            lstFiles.BackColor = cCtrl;
            lstFiles.ForeColor = cText;
            lstFiles.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(lstFiles);

            var btnOpen = MakeBtn("打开输出文件夹", 110, 462, 140, 28);
            btnOpen.Click += (s, e) =>
            {
                if (Directory.Exists(txtOut.Text.Trim())) Process.Start("explorer.exe", txtOut.Text.Trim());
                else MessageBox.Show("输出目录不存在", "提示");
            };
            Controls.Add(btnOpen);

            var btnPlay = MakeBtn("▶ 播放选中", 260, 462, 120, 28);
            btnPlay.Click += (s, e) =>
            {
                if (lstFiles.SelectedItem != null)
                {
                    string f = Path.Combine(txtOut.Text.Trim(), lstFiles.SelectedItem.ToString());
                    if (File.Exists(f)) Process.Start(f);
                }
                else MessageBox.Show("先在列表里选一个文件", "提示");
            };
            Controls.Add(btnPlay);
            lstFiles.DoubleClick += (s, e) => btnPlay.PerformClick();
        }

        void DetectGameDir()
        {
            foreach (var d in candidates)
            {
                if (Directory.Exists(d)) { gameDir = d; break; }
            }
            if (gameDir != null)
            {
                lblSrc.Text = "自动: " + gameDir;
            }
            else
            {
                lblSrc.Text = "未检测到游戏目录，请手动选文件夹";
                lblSrc.ForeColor = cErr;
            }
        }

        List<string> GetCrewBanks(string dir)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return list;
            try
            {
                foreach (var f in Directory.GetFiles(dir, "_crew_dialogs_ground_*.assets.bank"))
                {
                    list.Add(Path.GetFileName(f).Replace("_crew_dialogs_ground_", "").Replace(".assets.bank", ""));
                }
            }
            catch { }
            return list;
        }

        string CountryLabel(string code)
        {
            string name;
            if (code.StartsWith("sm_"))
            {
                string baseCode = code.Substring(3);
                if (countryNames.TryGetValue(baseCode, out name)) return name + "·第二套语音(" + code + ")";
                return code;
            }
            if (countryNames.TryGetValue(code, out name)) return name + " (" + code + ")";
            return code;
        }

        void RefreshLangs()
        {
            var codes = manualDir != null ? GetCrewBanks(manualDir) : GetCrewBanks(gameDir);
            cmbLang.Items.Clear();
            if (codes.Count == 0)
            {
                cmbLang.Text = "未检测到语音包";
                cmbLang.Enabled = false;
                return;
            }
            foreach (var p in priority) if (codes.Contains(p)) cmbLang.Items.Add(CountryLabel(p));
            foreach (var c in codes.Where(x => !priority.Contains(x)).OrderBy(x => x)) cmbLang.Items.Add(CountryLabel(c));
            cmbLang.SelectedIndex = 0;
            cmbLang.Enabled = true;
        }

        string GetSelectedCode()
        {
            if (cmbLang.SelectedItem == null) return "";
            var txt = cmbLang.SelectedItem.ToString();
            var m = Regex.Match(txt, @"\((\w+)\)$");
            if (m.Success) return m.Groups[1].Value;
            return txt;
        }

        void StartExtract(object sender, EventArgs e)
        {
            if (extracting) return;
            var code = GetSelectedCode();
            if (string.IsNullOrEmpty(code)) { MessageBox.Show("请先选择国家", "提示"); return; }
            var outDir = txtOut.Text.Trim();
            if (string.IsNullOrEmpty(outDir)) { MessageBox.Show("请填写输出目录", "提示"); return; }
            var srcDir = manualDir != null ? manualDir : gameDir;
            if (string.IsNullOrEmpty(srcDir)) { MessageBox.Show("未找到语音包目录，请点\"手动选文件夹\"", "提示"); return; }
            var bankPath = Path.Combine(srcDir, "_crew_dialogs_ground_" + code + ".assets.bank");
            if (!File.Exists(bankPath)) { MessageBox.Show("找不到语音包: " + bankPath, "错误"); return; }

            extracting = true;
            btnStart.Enabled = false;
            lstFiles.Items.Clear();
            outFiles.Clear();
            prg.Value = 0;
            lblStatus.Text = "正在解码…";

            string baseDir = Path.GetDirectoryName(Application.ExecutablePath);
            string cliPath = Path.Combine(baseDir, "gui", "extract_cli.mjs");
            string rename = chkRename.Checked ? "1" : "0";

            string nodePath = Path.Combine(baseDir, "node.exe");

            var psi = new ProcessStartInfo();
            psi.FileName = File.Exists(nodePath) ? nodePath : "node";
            psi.Arguments = "\"" + cliPath + "\" \"" + bankPath + "\" \"" + outDir + "\" " + rename;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            var proc = new Process();
            proc.StartInfo = psi;
            try { proc.Start(); }
            catch (Exception ex)
            {
                MessageBox.Show("启动 node 失败: " + ex.Message, "错误");
                extracting = false; btnStart.Enabled = true; return;
            }

            proc.OutputDataReceived += (s, ev) =>
            {
                if (string.IsNullOrEmpty(ev.Data)) return;
                try
                {
                    var data = ev.Data;
                    if (data.Contains("\"total\""))
                    {
                        int done = SafeInt(data, "done");
                        int total = SafeInt(data, "total");
                        string current = SafeStr(data, "current");
                        BeginInvoke((Action)(() =>
                        {
                            if (IsDisposed) return;
                            prg.Maximum = total;
                            prg.Value = Math.Min(done, total);
                            lblStatus.Text = "正在解码: " + current + "（" + done + "/" + total + "）";
                        }));
                    }
                    if (data.Contains("\"finished\""))
                    {
                        int ok = SafeInt(data, "ok");
                        int fail = SafeInt(data, "fail");
                        BeginInvoke((Action)(() =>
                        {
                            if (IsDisposed) return;
                            extracting = false;
                            btnStart.Enabled = true;
                            lblStatus.Text = "完成！成功 " + ok + " 条，失败 " + fail + " 条";
                            try
                            {
                                var names = Directory.GetFiles(txtOut.Text.Trim(), "*.ogg").Select(Path.GetFileName).OrderBy(x => x).ToList();
                                lstFiles.Items.Clear();
                                foreach (var n in names) lstFiles.Items.Add(n);
                                if (names.Count == 0) lblStatus.Text = "完成，但输出目录里没找到 ogg 文件";
                            }
                            catch { }
                        }));
                    }
                    if (data.Contains("\"error\""))
                    {
                        string err = SafeStr(data, "error");
                        BeginInvoke((Action)(() =>
                        {
                            if (IsDisposed) return;
                            extracting = false;
                            btnStart.Enabled = true;
                            lblStatus.Text = "出错: " + err;
                        }));
                    }
                }
                catch { }
            };
            proc.BeginOutputReadLine();
        }

        static int SafeInt(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\":(\\d+)");
            int v;
            if (m.Success && int.TryParse(m.Groups[1].Value, out v)) return v;
            return 0;
        }

        static string SafeStr(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\":\"([^\"]*)\"");
            if (m.Success) return m.Groups[1].Value;
            return "";
        }
    }
}