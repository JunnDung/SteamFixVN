using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SteamFixVN;

public static class FixEngine
{
    public static readonly string[] Domains = ["store.steampowered.com", "steamcommunity.com", "help.steampowered.com", "login.steampowered.com", "checkout.steampowered.com", "store.akamai.steamstatic.com", "community.akamai.steamstatic.com", "shared.akamai.steamstatic.com", "avatars.akamai.steamstatic.com"];
    public static readonly Uri[] WebChecks = [
        new("https://store.steampowered.com/"),
        new("https://steamcommunity.com/"),
        new("https://steamcommunity.com/discussions/"),
        new("https://steamcommunity.com/my/"),
        new("https://help.steampowered.com/")
    ];
    public sealed record WebProbe(bool Success, string Detail);
    public const string Begin = "\r\n# BEGIN SteamFixVN v1\r\n";
    public const string End = "# END SteamFixVN v1\r\n";
    public static string HostsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
    public static string DataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SteamFixVN");

    public static string RemoveBlock(string text)
    {
        int start = text.IndexOf(Begin, StringComparison.Ordinal);
        if (start < 0)
        {
            if (text.Contains("# BEGIN SteamFixVN") || text.Contains("# END SteamFixVN"))
                throw new IOException("Vùng SteamFixVN đã bị sửa thủ công. Kiểm tra file hosts trước khi tiếp tục.");
            return text;
        }
        int end = text.IndexOf(End, start + Begin.Length, StringComparison.Ordinal);
        if (end < 0) throw new IOException("Vùng SteamFixVN thiếu dấu kết thúc; chưa thay đổi hosts.");
        string clean = text.Remove(start, end + End.Length - start);
        if (clean.Contains("# BEGIN SteamFixVN") || clean.Contains("# END SteamFixVN"))
            throw new IOException("Có nhiều vùng SteamFixVN hoặc dấu mốc không hợp lệ.");
        return clean;
    }

    public static string AddBlock(string text, IReadOnlyDictionary<string, string> addresses)
    {
        string clean = RemoveBlock(text);
        foreach (string line in clean.Split('\n'))
        {
            var parts = line.Split('#')[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Skip(1).Any(p => addresses.ContainsKey(p.ToLowerInvariant())))
                throw new IOException("Hosts đã có cấu hình Steam từ tool khác. Gỡ cấu hình đó trước để tránh xung đột.");
        }
        foreach (var pair in addresses)
            if (!Domains.Contains(pair.Key) || !IPAddress.TryParse(pair.Value, out var ip) || !IsPublicV4(ip))
                throw new IOException("Địa chỉ Steam không hợp lệ.");
        return clean + Begin + "# IP lấy qua HTTPS; Apply lại nếu IP CDN thay đổi.\r\n" +
            string.Join("", addresses.Select(p => $"{p.Value} {p.Key}\r\n")) + End;
    }

    public static bool IsPublicV4(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] != 0 && b[0] != 10 && b[0] != 127 && b[0] < 224 &&
            !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] >= 16 && b[1] <= 31) &&
            !(b[0] == 192 && b[1] == 168) && !(b[0] == 100 && b[1] >= 64 && b[1] <= 127);
    }

    public static async Task<List<string>> Resolve(string domain)
    {
        var candidates = new List<string>();
        foreach (string endpoint in new[] { "https://1.1.1.1/dns-query", "https://dns.google/resolve" })
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                client.DefaultRequestHeaders.Accept.ParseAdd("application/dns-json");
                using var response = await client.GetAsync($"{endpoint}?name={Uri.EscapeDataString(domain)}&type=A");
                response.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (doc.RootElement.GetProperty("Status").GetInt32() != 0) continue;
                if (!doc.RootElement.TryGetProperty("Answer", out var answer)) continue;
                var ips = answer.EnumerateArray().Where(a => a.GetProperty("type").GetInt32() == 1)
                    .Select(a => a.GetProperty("data").GetString()!)
                    .Where(s => IPAddress.TryParse(s, out var ip) && IsPublicV4(ip)).Distinct().Take(3).ToList();
                foreach (string ip in ips)
                    if (!candidates.Contains(ip)) candidates.Add(ip);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { }
        }
        if (candidates.Count > 0) return candidates;
        throw new IOException($"Không lấy được DNS mã hóa cho {domain}. Kiểm tra Internet hoặc thử mạng khác.");
    }

    // Connect to the resolved IP, keeping the real hostname for TLS/SNI validation.
    public static async Task<bool> Probe(string domain, string? address = null, bool cdnRoot = false)
    {
        var checks = WebChecks.Where(uri => uri.Host == domain).ToArray();
        if (checks.Length == 0) checks = [new Uri($"https://{domain}/")];
        foreach (var uri in checks)
            if (!(await ProbeDetailed(uri, address, cdnRoot)).Success) return false;
        return true;
    }

    public static async Task<WebProbe> ProbeDetailed(Uri uri, string? address = null, bool cdnRoot = false)
    {
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false };
        if (address != null)
            handler.ConnectCallback = async (context, token) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    // A redirect to another Steam hostname must use that hostname's own DNS/IP.
                    if (context.DnsEndPoint.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase))
                        await socket.ConnectAsync(IPAddress.Parse(address), context.DnsEndPoint.Port, token);
                    else
                        await socket.ConnectAsync(context.DnsEndPoint, token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        return await ProbeWithClient(client, uri, cdnRoot);
    }

    public static async Task<WebProbe> ProbeWithClient(HttpClient client, Uri uri, bool cdnRoot = false)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            for (int redirects = 0; redirects <= 5; redirects++)
            {
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                int status = (int)response.StatusCode;
                if (status is 301 or 302 or 303 or 307 or 308)
                {
                    var location = response.Headers.Location;
                    if (location == null) return new(false, $"HTTP {status}: thiếu địa chỉ chuyển hướng");
                    var next = new Uri(uri, location);
                    if (next.Scheme != Uri.UriSchemeHttps || !next.IsDefaultPort || next.UserInfo.Length != 0 || !Domains.Contains(next.Host))
                        return new(false, $"HTTP {status}: chuyển hướng ngoài HTTPS Steam");
                    uri = next;
                    continue;
                }
                // CDN roots can return 403/404; web pages must finish with a successful status.
                bool ok = status is >= 200 and < 300 || (cdnRoot && status is 403 or 404);
                return new(ok, $"HTTP {status} — {uri.Host}{uri.AbsolutePath}");
            }
            return new(false, "Chuyển hướng quá 5 lần");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return new(false, e is TaskCanceledException ? "Hết thời gian kết nối" : e.GetBaseException().Message);
        }
    }

    public static async Task<Dictionary<string, string>> Prepare(Action<string> log, bool requireConnectivity = true)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string domain in Domains)
        {
            log($"DNS mã hóa: {domain}");
            try
            {
                var ips = await Resolve(domain);
                string? working = null;
                foreach (string ip in ips)
                    if (await Probe(domain, ip, domain.EndsWith(".steamstatic.com", StringComparison.Ordinal))) { working = ip; break; }
                bool verified = working != null;
                if (working == null && !requireConnectivity)
                {
                    working = ips[0];
                    log("  Chưa kết nối HTTPS; sẽ kiểm tra lại sau khi bật DPI.");
                }
                if (working == null) throw new IOException($"HTTPS chưa truy cập được: {domain}");
                result.Add(domain, working);
                log($"  {(verified ? "HTTPS OK" : "IP DoH, HTTPS chưa đạt")} → {working}");
            }
            catch (IOException e)
            {
                if (Domains.Take(3).Contains(domain))
                    throw new IOException(e.Message + "\nChưa áp dụng. Có thể mạng chặn IP/SNI hoặc Steam đang lỗi; cần VPN/tunnel hoặc mạng khác nếu chặn sâu hơn DNS.", e);
                log("  Bỏ qua mục phụ: " + e.Message);
            }
        }
        return result;
    }

    public static string WriteHosts(string path, Func<string, string> transform, string backupDirectory)
    {
        byte[] before = File.ReadAllBytes(path);
        if (before.Contains((byte)0)) throw new IOException("Hosts có mã hóa UTF-16 không hỗ trợ; chưa sửa file.");
        byte[] after = Encoding.Latin1.GetBytes(transform(Encoding.Latin1.GetString(before)));
        if (before.SequenceEqual(after)) return "";
        Directory.CreateDirectory(backupDirectory);
        string backup = Path.Combine(backupDirectory, $"hosts-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bak");
        File.WriteAllBytes(backup, before);
        string temp = path + ".steamfix-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temp, after);
            if (!File.ReadAllBytes(path).SequenceEqual(before))
                throw new IOException("Hosts vừa bị chương trình khác sửa; dừng để bảo vệ cấu hình.");
            File.Replace(temp, path, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return backup;
    }

    public static async Task Flush(Action<string> log)
    {
        using var process = Process.Start(new ProcessStartInfo("ipconfig.exe", "/flushdns") { CreateNoWindow = true, UseShellExecute = false });
        if (process == null) throw new IOException("Không chạy được ipconfig.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new IOException("Hosts đã cập nhật nhưng Windows chưa xóa được DNS cache. Khởi động lại máy.");
        log("Đã xóa DNS cache.");
    }
}
