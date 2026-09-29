using System.Reflection;
using System.Text.Json;

namespace GamepadMouse;

/// <summary>
/// 检查更新：查询 GitHub Releases 的最新发布版本（releases/latest API），
/// 与当前程序集版本比较。纯查询，不做下载/安装，由界面引导用户前往 Release 页。
/// </summary>
internal static class UpdateChecker
{
    public const string RepoUrl = "https://github.com/StoneBeast/gamepad-mouse";
    public const string ReleasesPageUrl = RepoUrl + "/releases";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/StoneBeast/gamepad-mouse/releases/latest";

    /// <summary>一次检查的结果。</summary>
    /// <param name="LatestVersion">最新版本显示文本（语义化版本号或原始 tag）。</param>
    /// <param name="Url">该版本的 Release 页面地址。</param>
    /// <param name="HasUpdate">最新版本是否比当前运行版本新。</param>
    public sealed record Result(string LatestVersion, string Url, bool HasUpdate);

    /// <summary>当前程序集版本（csproj 的 &lt;Version&gt;，如 "1.3.1"）。</summary>
    public static string CurrentVersion()
    {
        Version v = Assembly.GetExecutingAssembly().GetName().Version!;
        return $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
    }

    public static async Task<Result> CheckLatestReleaseAsync(CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub API 要求带 User-Agent，否则 403
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"GamepadMouse/{CurrentVersion()}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        string json = await http.GetStringAsync(LatestReleaseApiUrl, ct);
        using var doc = JsonDocument.Parse(json);
        string tag = doc.RootElement.GetProperty("tag_name").GetString()?.Trim() ?? "";
        string url = doc.RootElement.TryGetProperty("html_url", out var u)
            ? u.GetString() ?? ReleasesPageUrl
            : ReleasesPageUrl;

        // tag 形如 v1.3.1；解析失败（非语义化 tag）时不误报有更新
        Version? latest = Version.TryParse(tag.TrimStart('v', 'V'), out var v) ? v : null;
        Version current = Assembly.GetExecutingAssembly().GetName().Version!;
        bool hasUpdate = latest != null && latest > current;

        return new Result(latest?.ToString(3) ?? tag, url, hasUpdate);
    }
}
