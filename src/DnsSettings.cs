using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SteamFixVN;

public sealed record AdapterDns(Guid Id, int Index, string Name, bool Automatic, string[] Servers, string[] IPv6Servers);
public sealed record DnsJournal(AdapterDns Original, string[] Applied, bool Pending);

public static class DnsSettings
{
    public static readonly string[][] Providers = [[], ["1.1.1.1", "1.0.0.1"], ["8.8.8.8", "8.8.4.4"]];
    public static bool Matches(AdapterDns current, bool automatic, string[] addresses) =>
        current.Automatic == automatic && (automatic || current.Servers.SequenceEqual(addresses));

    public static bool MayRestore(AdapterDns current, DnsJournal journal) =>
        current.Id == journal.Original.Id &&
        (Matches(current, false, journal.Applied) ||
         (journal.Pending && (Matches(current, journal.Original.Automatic, journal.Original.Servers) ||
          (!current.Automatic && current.Servers.Length > 0 && current.Servers.SequenceEqual(journal.Applied.Take(current.Servers.Length))))));

    public static List<string[]> Commands(int index, bool automatic, string[] addresses)
    {
        if (index <= 0) throw new IOException("Interface index không hợp lệ.");
        foreach (string ip in addresses)
            if (!IPAddress.TryParse(ip, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
                throw new IOException("Danh sách DNS IPv4 không hợp lệ.");
        var commands = new List<string[]>();
        if (automatic) commands.Add(["interface", "ipv4", "set", "dnsservers", $"name={index}", "source=dhcp"]);
        else
        {
            commands.Add(["interface", "ipv4", "set", "dnsservers", $"name={index}", "source=static", $"address={addresses.FirstOrDefault() ?? "none"}", "validate=no"]);
            for (int i = 1; i < addresses.Length; i++)
                commands.Add(["interface", "ipv4", "add", "dnsservers", $"name={index}", $"address={addresses[i]}", $"index={i + 1}", "validate=no"]);
        }
        return commands;
    }

    const string CaptureScript = """
        $ErrorActionPreference='Stop'
        $ProgressPreference='SilentlyContinue'
        [Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false)
        if ($targetGuid) {
            $adapter = Get-NetAdapter -IncludeHidden | Where-Object { ([guid]$_.InterfaceGuid) -eq ([guid]$targetGuid) } | Select-Object -First 1
        } else {
            $routes = @(Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -PolicyStore ActiveStore)
            $candidates = @(foreach ($a in @(Get-NetAdapter -Physical | Where-Object Status -eq 'Up')) {
                $route = $routes | Where-Object InterfaceIndex -eq $a.InterfaceIndex | Sort-Object RouteMetric | Select-Object -First 1
                if ($route) {
                    $metric = (Get-NetIPInterface -InterfaceIndex $a.InterfaceIndex -AddressFamily IPv4).InterfaceMetric
                    [pscustomobject]@{ Adapter=$a; Cost=([int]$route.RouteMetric+[int]$metric) }
                }
            })
            $adapter = ($candidates | Sort-Object Cost | Select-Object -First 1).Adapter
        }
        if (!$adapter) { throw 'Khong tim thay card mang vat ly co default route IPv4, hoac adapter da bi go.' }
        $id = ([guid]$adapter.InterfaceGuid).ToString('D')
        $registryPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{' + $id.Trim('{}') + '}'
        $settings = Get-ItemProperty -LiteralPath $registryPath
        $manual = [string]$settings.NameServer
        $automatic = [string]::IsNullOrWhiteSpace($manual)
        $effective = @((Get-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -AddressFamily IPv4).ServerAddresses)
        $servers = if ($automatic) { $effective } else { @([regex]::Split($manual.Trim(), '[,\s]+') | Where-Object { $_ }) }
        $v6 = @((Get-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -AddressFamily IPv6).ServerAddresses)
        [pscustomobject]@{ Id=$id; Index=[int]$adapter.InterfaceIndex; Name=$adapter.Name; Automatic=$automatic; Servers=@($servers); IPv6Servers=$v6 } | ConvertTo-Json -Compress -Depth 4
        """;

    public static async Task<AdapterDns> Capture(Guid? id = null)
    {
        string script = $"$targetGuid='{id?.ToString() ?? ""}'\n" + CaptureScript;
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"));
        info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-OutputFormat"); info.ArgumentList.Add("Text");
        info.ArgumentList.Add("-EncodedCommand"); info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        string json = await Run(info);
        var snapshot = JsonSerializer.Deserialize<AdapterDns>(json) ?? throw new IOException("Không đọc được DNS adapter.");
        if (snapshot.Id == Guid.Empty || snapshot.Index <= 0 || snapshot.Servers == null || snapshot.IPv6Servers == null)
            throw new IOException("Cấu hình adapter không hợp lệ.");
        Commands(snapshot.Index, snapshot.Automatic, snapshot.Servers);
        return snapshot;
    }

    static async Task<string> Run(ProcessStartInfo info)
    {
        info.UseShellExecute = false; info.CreateNoWindow = true;
        info.RedirectStandardOutput = info.RedirectStandardError = true;
        info.StandardOutputEncoding = info.StandardErrorEncoding = Encoding.UTF8;
        using var process = Process.Start(info) ?? throw new IOException("Không chạy được lệnh DNS.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw new IOException("Lệnh DNS quá thời gian chờ."); }
        string output = await stdout;
        string error = await stderr;
        if (process.ExitCode != 0) throw new IOException("Lệnh DNS thất bại: " + output.Trim() + " " + error.Trim());
        return output.Trim();
    }

    static async Task Set(AdapterDns adapter, bool automatic, string[] addresses)
    {
        foreach (var args in Commands(adapter.Index, automatic, addresses))
        {
            var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe"));
            foreach (string arg in args) info.ArgumentList.Add(arg);
            await Run(info);
        }
        var after = await Capture(adapter.Id);
        if (!Matches(after, automatic, addresses)) throw new IOException("Windows chưa giữ đúng DNS vừa đặt.");
        if (!after.IPv6Servers.SequenceEqual(adapter.IPv6Servers))
            throw new IOException("DNS IPv6 đã thay đổi ngoài dự kiến; dừng kiểm tra để tránh báo thành công sai.");
    }

    static string JournalPath(Guid id) => Path.Combine(FixEngine.DataPath, $"dns-original-{id:N}.json");
    static void Save(string path, DnsJournal journal)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temp, JsonSerializer.Serialize(journal), Encoding.UTF8); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    static DnsJournal Load(string path)
    {
        var journal = JsonSerializer.Deserialize<DnsJournal>(File.ReadAllText(path)) ?? throw new IOException("Sao lưu DNS không hợp lệ.");
        if (journal.Original == null || journal.Applied == null || journal.Original.Servers == null || journal.Original.Id == Guid.Empty)
            throw new IOException("Sao lưu DNS thiếu thông tin.");
        Commands(journal.Original.Index, journal.Original.Automatic, journal.Original.Servers);
        Commands(journal.Original.Index, false, journal.Applied);
        if (!Providers.Skip(1).Any(p => p.SequenceEqual(journal.Applied))) throw new IOException("Sao lưu DNS không khớp preset của tool.");
        return journal;
    }

    public static async Task<DnsChange?> Apply(int provider, Action<string> log)
    {
        if (provider is < 1 or > 2) throw new IOException("Preset DNS không hợp lệ.");
        AdapterDns before = await Capture();
        if (Matches(before, false, Providers[provider]))
        {
            log($"DNS IPv4 trên {before.Name} đã đúng preset; giữ nguyên, không chạy lệnh đổi DNS.");
            return null;
        }
        EnginePackage.SecureDataDirectory();
        string path = JournalPath(before.Id);
        DnsJournal? previous = File.Exists(path) ? Load(path) : null;
        if (previous != null && !MayRestore(before, previous))
            throw new IOException("DNS đã được sửa ngoài tool. Không ghi đè; kiểm tra sao lưu DNS trong thư mục dữ liệu.");
        var journal = new DnsJournal(previous?.Original ?? before, Providers[provider], true);
        Save(path, journal); // Record recovery information before the first netsh mutation.
        var change = new DnsChange(before, previous, journal, path);
        try
        {
            log($"Đổi DNS IPv4 trên {before.Name}: {string.Join(" / ", journal.Applied)}. DNS cũ: {(before.Automatic ? "tự động (DHCP)" : string.Join(" / ", before.Servers))}");
            await Set(before, false, journal.Applied);
            Save(path, journal with { Pending = false });
            change.Committed();
            return change;
        }
        catch
        {
            try { await change.Rollback(log); }
            catch (Exception e) { log("Chưa trả DNS cũ được: " + e.Message + ". Dùng Khôi phục để thử lại."); }
            throw;
        }
    }

    public sealed class DnsChange(AdapterDns before, DnsJournal? previous, DnsJournal applied, string path)
    {
        DnsJournal currentApplied = applied;
        public void Committed() => currentApplied = currentApplied with { Pending = false };
        public async Task Rollback(Action<string> log)
        {
            var current = await Capture(before.Id);
            if (!MayRestore(current, currentApplied)) throw new IOException("DNS có thay đổi khác; không tự ghi đè.");
            await Set(current, before.Automatic, before.Servers);
            if (previous == null) File.Delete(path); else Save(path, previous);
            log("Đã trả DNS IPv4 về trước lần Apply này.");
        }
    }

    public static async Task Restore(Action<string> log)
    {
        if (!Directory.Exists(FixEngine.DataPath)) return;
        var errors = new List<string>();
        foreach (string path in Directory.GetFiles(FixEngine.DataPath, "dns-original-*.json"))
        {
            try
            {
                var journal = Load(path);
                var current = await Capture(journal.Original.Id); // GUID, not a potentially recycled interface index.
                if (!Matches(current, journal.Original.Automatic, journal.Original.Servers))
                {
                    if (!MayRestore(current, journal)) throw new IOException("DNS đã được sửa ngoài tool; giữ nguyên để tránh ghi đè.");
                    await Set(current, journal.Original.Automatic, journal.Original.Servers);
                }
                File.Delete(path);
                log($"Đã khôi phục DNS IPv4: {current.Name} → {(journal.Original.Automatic ? "tự động (DHCP)" : string.Join(" / ", journal.Original.Servers))}");
            }
            catch (Exception e) { errors.Add(Path.GetFileName(path) + ": " + e.Message); }
        }
        if (errors.Count > 0) throw new IOException(string.Join("\n", errors));
    }
}
