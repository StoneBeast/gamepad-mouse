using System.Diagnostics;
using System.Drawing.Drawing2D;
using GamepadMouse.Ui;

namespace GamepadMouse;

/// <summary>
/// 关于窗口：程序图标与版本、项目主页链接、开源许可，以及 GitHub Release 检查更新。
/// 几何按当前 DPI 直接构造（同「录制组合键」弹窗的做法），不依赖 AutoScaleMode。
/// </summary>
internal class AboutForm : Form
{
    private readonly ModernButton _btnCheck;
    private readonly ModernButton _btnDownload;
    private readonly Label _checkStatus;
    private string _latestUrl = UpdateChecker.ReleasesPageUrl;
    private bool _checking;

    public AboutForm()
    {
        Text = "关于 GamepadMouse";
        Font = UiTheme.FontUi;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UiTheme.WindowBg;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        HandleCreated += (_, _) => UiTheme.ApplyTitleBarTheme(this);

        // 几何按 96 DPI 设计，构造期一次性换算到当前 DPI
        int s(int v) => (int)Math.Round(v * DeviceDpi / 96f);
        ClientSize = new Size(s(420), s(356));

        var header = new UiPanel { Location = new Point(0, 0), Size = new Size(s(420), s(64)), BackColor = UiTheme.WindowBg };
        int hs(int v) => (int)Math.Round(v * header.DeviceDpi / 96f);
        header.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawIcon(AppIcon.Enabled, new Rectangle(hs(20), hs(14), hs(44), hs(44)));
            TextRenderer.DrawText(g, "GamepadMouse", UiTheme.FontTitle, new Point(hs(76), hs(14)), UiTheme.TextPrimary);
            TextRenderer.DrawText(g, $"版本 {UpdateChecker.CurrentVersion()}", UiTheme.FontTitleSub,
                new Point(hs(78), hs(43)), UiTheme.TextSecondary);
        };
        Controls.Add(header);

        // ---- 关于卡片：简介 / 项目主页 / 许可 ----
        var cardAbout = new Card("关于", new Point(s(12), s(72)), new Size(s(396), s(144)));
        var lblDesc = new Label
        {
            Text = "把手柄（XInput）映射为鼠标：摇杆控制光标、按键模拟点击与滚轮，组合键一键开关映射，全部可自定义。",
            Location = new Point(s(20), s(44)),
            Size = new Size(s(356), s(36)),
            ForeColor = UiTheme.TextSecondary,
            BackColor = UiTheme.CardBg,
            Font = UiTheme.FontUi,
        };
        var lblSite = new Label
        {
            Text = "项目主页：",
            Location = new Point(s(20), s(86)),
            Size = new Size(s(70), s(22)),
            ForeColor = UiTheme.TextPrimary,
            BackColor = UiTheme.CardBg,
            Font = UiTheme.FontUi,
        };
        var linkRepo = new LinkLabel
        {
            Text = "github.com/StoneBeast/gamepad-mouse",
            Location = new Point(s(90), s(86)),
            Size = new Size(s(286), s(22)),
            BackColor = UiTheme.CardBg,
            LinkBehavior = LinkBehavior.HoverUnderline,
            LinkColor = UiTheme.Accent,
            ActiveLinkColor = UiTheme.AccentHover,
        };
        linkRepo.Links.Add(0, linkRepo.Text.Length, UpdateChecker.RepoUrl);
        linkRepo.LinkClicked += (_, _) => OpenUrl(UpdateChecker.RepoUrl);
        var lblLicense = new Label
        {
            Text = "开源许可：MIT",
            Location = new Point(s(20), s(112)),
            Size = new Size(s(356), s(22)),
            ForeColor = UiTheme.TextSecondary,
            BackColor = UiTheme.CardBg,
            Font = UiTheme.FontUi,
        };
        cardAbout.Controls.AddRange([lblDesc, lblSite, linkRepo, lblLicense]);
        Controls.Add(cardAbout);

        // ---- 检查更新卡片 ----
        var cardUpdate = new Card("检查更新", new Point(s(12), s(226)), new Size(s(396), s(84)));
        _btnCheck = new ModernButton("检查更新", new Point(s(20), s(42)), new Size(s(92), s(28)), ModernButton.Style.Ghost)
        {
            BackColor = UiTheme.CardBg, // 圆角透出卡片底色
        };
        _btnCheck.Click += (_, _) => _ = CheckForUpdatesAsync();
        _checkStatus = new Label
        {
            Text = "当前版本 v" + UpdateChecker.CurrentVersion(),
            Location = new Point(s(124), s(48)),
            Size = new Size(s(160), s(22)),
            ForeColor = UiTheme.TextSecondary,
            BackColor = UiTheme.CardBg,
            Font = UiTheme.FontUi,
            AutoEllipsis = true,
        };
        _btnDownload = new ModernButton("前往下载", new Point(s(300), s(42)), new Size(s(76), s(28)), ModernButton.Style.Primary)
        {
            BackColor = UiTheme.CardBg,
            Visible = false,
        };
        _btnDownload.Click += (_, _) => OpenUrl(_latestUrl);
        cardUpdate.Controls.AddRange([_btnCheck, _checkStatus, _btnDownload]);
        Controls.Add(cardUpdate);

        var btnClose = new ModernButton("关闭", new Point(s(312), s(318)), new Size(s(96), s(30)), ModernButton.Style.Primary);
        btnClose.BackColor = UiTheme.WindowBg; // 圆角透出窗口底色
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_checking) return;
        _checking = true;
        _btnCheck.Enabled = false;
        _btnDownload.Visible = false;
        SetStatus("正在检查更新…", UiTheme.TextSecondary);
        try
        {
            var r = await UpdateChecker.CheckLatestReleaseAsync();
            _latestUrl = r.Url;
            if (r.HasUpdate)
            {
                SetStatus($"发现新版本 v{r.LatestVersion}", UiTheme.Accent);
                _btnDownload.Visible = true;
            }
            else
            {
                SetStatus("当前已是最新版本", UiTheme.Success);
            }
        }
        catch (Exception ex)
        {
            Log.Error("检查更新失败", ex);
            SetStatus("检查更新失败：请检查网络", UiTheme.TextSecondary);
        }
        finally
        {
            _checking = false;
            _btnCheck.Enabled = true;
        }
    }

    private void SetStatus(string text, Color color)
    {
        _checkStatus.Text = text;
        _checkStatus.ForeColor = color;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("打开网页失败：" + url, ex);
        }
    }
}
