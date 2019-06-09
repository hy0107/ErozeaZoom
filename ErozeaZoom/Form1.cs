using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Timer = System.Threading.Timer;

// DROP: Config Crypto
// TODO: Auto Launch
// TODO: Single Instance Limit
// DONE: Version Check
// TODO: UI Simplify

namespace ErozeaZoom
{
    public partial class Form1 : Form
    {
        private static readonly Lazy<Settings> LazySettings = new Lazy<Settings>(() => Settings.Load());
        private Timer _timer;

        public Form1()
        {
            InitializeComponent();
        }

        private static Settings Settings
        {
            get { return LazySettings.Value; }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _autoApplyCheckbox.Checked = Settings.AutoApply;
            _autoApplyCheckbox.CheckedChanged += AutoApplyCheckChanged;

            _zoomUpDown.Value = (decimal) Settings.DesiredZoom;
            _zoomUpDown.ValueChanged += NumberChanged;
            _fovUpDown.Value = (decimal) Settings.DesiredFov;
            _fovUpDown.ValueChanged += NumberChanged;

            _updateOffsetsTextbox.Text = Settings.OffsetUpdateLocation;

            _timer = new Timer(TimerCallback, null, TimeSpan.FromMilliseconds(100), Timeout.InfiniteTimeSpan);
            label2.Text = "当前配置版本：" + Settings.LastUpdate;
            //var t = Memory.VersionVerify(Settings, Memory.GetPids().ToArray()[0]);
        }

        private void NumberChanged(object sender, EventArgs e)
        {
            Settings.DesiredZoom = (float) _zoomUpDown.Value;
            Settings.DesiredFov = (float) _fovUpDown.Value;
            Settings.Save();
            ApplyChanges();
        }

        private void TimerCallback(object state)
        {
            try
            {
                Invoke(
                    () =>
                    {
                        var activePids = Memory.GetPids()
                            .ToArray();
                        var knownPids = GetCurrentPids();
                        foreach (var pid in activePids.Except(knownPids))
                        {
                            _processList.Items.Add(pid);
                        }

                        for (var i = _processList.Items.Count - 1; i >= 0; i--)
                        {
                            var pid = (int) _processList.Items[i];
                            if (!activePids.Contains(pid))
                            {
                                _processList.Items.RemoveAt(i);
                            }
                        }

                        if (_processList.Items.Count > 0 && _processList.SelectedItem == null)
                        {
                            _processList.SelectedIndex = 0;
                        }

                        if (Settings.AutoApply)
                        {
                            var newPids = activePids.Except(knownPids)
                                .ToArray();
                            if (newPids.Any())
                            {
                                ApplyChanges(newPids);
                            }
                        }
                    });
            }
            catch
            {
                /* something went wrong on the background thread, should find a way to log this..*/
            }
            finally
            {
                _timer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
            }
        }

        private IReadOnlyCollection<int> GetCurrentPids()
        {
            return _processList.Items.Cast<int>().ToArray();
        }

        private void ApplyChanges(IEnumerable<int> pids = null)
        {
            foreach (var pid in (pids ?? GetCurrentPids()))
            {
                Memory.Kernel(Settings, pid);
            }
        }

        private void AutoApplyCheckChanged(object sender, EventArgs e)
        {
            Settings.AutoApply = !Settings.AutoApply;
            Settings.Save();
            if (Settings.AutoApply)
            {
                ApplyChanges();
            }
        }

        private void Invoke(Action action)
        {
            Delegate d = action;
            Invoke(d);
        }

        private void _gotoProcessButton_Click(object sender, EventArgs e)
        {
            if (_processList.SelectedItem == null)
            {
                return;
            }

            var selectedPid = (int) _processList.SelectedItem;
            using (var process = Process.GetProcessById(selectedPid))
            {
                var handle = process.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    SetForegroundWindow(handle);
                }
            }
        }

        [DllImport("USER32.DLL")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void _zoomDefaultButton_Click(object sender, EventArgs e)
        {
            _zoomUpDown.Value = 20m;
        }

        private void _fovDefaultButton_Click(object sender, EventArgs e)
        {
            _fovUpDown.Value = .78m;
        }

        private void _updateOffsetsButton_Click(object sender, EventArgs e)
        {
            _updateOffsetsButton.Enabled = false;
            Settings.OffsetUpdateLocation = _updateOffsetsTextbox.Text;

            Cursor = Cursors.WaitCursor;

            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    try
                    {
                        var offsets = Settings.Load(Settings.OffsetUpdateLocation + "?_=" + DateTime.Now.Ticks);

                        if (string.Equals(Settings.LastUpdate, offsets.LastUpdate))
                        {
                            MessageBox.Show("目前已经是最新版本");
                            return;
                        }

                        Settings.DX11_StructureAddress = offsets.DX11_StructureAddress;
                        Settings.DX11_ZoomCurrent = offsets.DX11_ZoomCurrent;
                        Settings.DX11_ZoomMax = offsets.DX11_ZoomMax;
                        Settings.DX11_FovCurrent = offsets.DX11_FovCurrent;
                        Settings.DX11_FovMax = offsets.DX11_FovMax;
                        Settings.DX9_StructureAddress = offsets.DX9_StructureAddress;
                        Settings.DX9_ZoomCurrent = offsets.DX9_ZoomCurrent;
                        Settings.DX9_ZoomMax = offsets.DX9_ZoomMax;
                        Settings.DX9_FovCurrent = offsets.DX9_FovCurrent;
                        Settings.DX9_FovMax = offsets.DX9_FovMax;
                        Settings.LastUpdate = offsets.LastUpdate;
                        Settings.Version = offsets.Version;
                        Settings.Save();

                        if (Settings.AutoApply)
                        {
                            Invoke(() => ApplyChanges());
                        }
                        MessageBox.Show("成功升级到: " + Settings.LastUpdate);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("更新进程发生错误: " + ex);
                    }
                    finally
                    {
                        Invoke(() =>
                               {
                                   Cursor = Cursors.Default;
                                   _updateOffsetsButton.Enabled = true;
                               });
                    }
                });
        }

        private void _updateLocationDefault_Click(object sender, EventArgs e)
        {
            _updateOffsetsTextbox.Text = @"https://raw.githubusercontent.com/LimiQS/Erozea-Zoom/master/Offsets.xml";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Well, for fools
            ApplyChanges();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "notepad.exe";
            info.Arguments = generateAbout();
            //info.WorkingDirectory = "c:\\";
            Process.Start(info);
        }

        private string generateAbout()
        {
            string content;
            #region AboutContent
            content = @"Erozea Zoom
	
版本1.0

作者：
    八云缘
    邮箱 - i@kage.moe

使用建议：
    拍照使用。
    建议不要于副本内使用。
    建议不要于直播录播中使用。
    请勿于非标定版本的游戏使用。

作者承诺：
    该软件仅修改了游戏显示相关的参数。
    不涉及任何人物具体属性，战斗数值
    及其他可能影响游戏平衡的数值。
    不涉及任何形式的自动化操作和播报提示。
    不涉及任何形式的脚本编写。

兼容性：
    兼容ReShader 3 [实测版本3.0.8.183]
    兼容ACT [实测版本NGA整合版For 4.05]
    兼容GeForce Experience [实测版本3.11.0.73]
    -理论-兼容所有模型修改类美化插件
    已于野外、玩家房屋和部分副本内测试

安全警示：
    本程序在代码上使用了和脚本/外挂相近的技术。
    尽管修改的相关数据不会被提交到服务器亦无涉
    人物数值，故不存在被服务器检测为欺诈的可能，
    亦不存在破坏游戏平衡及其他玩家体验的可能。
    但依旧有被客户端反篡改技术识别的风险。

免责声明：
    使用本软件产品风险由用户自行承担，在适用法律
    允许的最大范围内，对因使用或不能使用本软件
    所产生的损害及风险，包括但不限于直接或间接的
    个人损害、商业赢利的丧失、贸易中断、商业信息
    的丢失或任何其它经济损失，作者不承担任何责任。";

            #endregion

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ErozeaZoom");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var file = Path.Combine(path, "About.txt");
            if (!File.Exists(file))
            {
                File.WriteAllText(file, content);
            }
            return file;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = checkBox1.Checked;
        }
    }
}