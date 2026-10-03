using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace SteamFixVN;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(false, "Global\\SteamFixVN-App-v1");
        bool acquired;
        try { acquired = mutex.WaitOne(5000); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) { MessageBox.Show("Steam Fix VN đang chạy."); return; }
        try { Application.Run(new MainForm(args.FirstOrDefault())); }
        finally { mutex.ReleaseMutex(); }
    }
}

internal sealed class MainForm : Form
{
    readonly Button apply = new() { Text = "Apply — Sửa Steam", Width = 210, Height = 46 };
    readonly Button restore = new() { Text = "Khôi phục", Width = 125, Height = 46 };
    readonly Button check = new() { Text = "Kiểm tra", Width = 125, Height = 46 };
    readonly TextBox output = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.FromArgb(17, 25, 36), ForeColor = Color.FromArgb(200, 221, 238), Font = new Font("Consolas", 10) };
    readonly Label status = new() { Text = "Sẵn sàng", AutoSize = true, ForeColor = Color.LightSkyBlue, Margin = new Padding(0, 10, 0, 10) };
    readonly CheckBox useDpi = new() { Text = "Tự thử GoodbyeDPI nếu DNS vẫn lỗi — giữ tool mở khi DPI bật", Checked = true, AutoSize = true };
    readonly ComboBox dnsProvider = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 510 };
    readonly DpiRuntime dpi = new();
    bool busy;

    public MainForm(string? action)
    {
        Text = "Steam Fix VN • 1.2 • DNS + DPI";
        ClientSize = new Size(800, 670);
        MinimumSize = new Size(760, 630);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(25, 36, 50);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 8 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.Controls.Add(new Label { Text = "STEAM FIX VN", Font = new Font("Segoe UI", 24, FontStyle.Bold), AutoSize = true }, 0, 0);
        layout.Controls.Add(new Label { Text = "Apply thử DNS IPv4 trước, rồi hosts/DoH và DPI nếu vẫn chưa truy cập được.\nTự sao lưu DNS/hosts; Khôi phục trả DNS về tự động hoặc giá trị cũ.\nChặn IP hoặc lỗi khác có thể vẫn cần mạng khác/VPN; không bảo đảm mọi lỗi -7.", Dock = DockStyle.Fill }, 0, 1);
        dnsProvider.Items.AddRange(["Giữ DNS IPv4 hiện tại", "Cloudflare — 1.1.1.1 / 1.0.0.1", "Google — 8.8.8.8 / 8.8.4.4"]);
        dnsProvider.SelectedIndex = 2;
        var dnsRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        dnsRow.Controls.Add(new Label { Text = "DNS IPv4:", AutoSize = true, Margin = new Padding(0, 5, 12, 0) });
        dnsRow.Controls.Add(dnsProvider);
        layout.Controls.Add(dnsRow, 0, 2);
        layout.Controls.Add(useDpi, 0, 3);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        apply.BackColor = Color.FromArgb(91, 178, 230); apply.ForeColor = Color.Black;
        actions.Controls.AddRange([apply, restore, check]);
        layout.Controls.Add(actions, 0, 4);
        layout.Controls.Add(status, 0, 5);
        layout.Controls.Add(output, 0, 6);
        var folder = new LinkLabel { Text = "Mở thư mục sao lưu và nhật ký", AutoSize = true, LinkColor = Color.LightSkyBlue, Margin = new Padding(0, 12, 0, 0) };
        folder.LinkClicked += (_, _) => { if (Directory.Exists(FixEngine.DataPath)) Process.Start(new ProcessStartInfo("explorer.exe", FixEngine.DataPath) { UseShellExecute = true }); else Log("Chưa có sao lưu. Sao lưu được tạo khi Apply hoặc Khôi phục."); };
        layout.Controls.Add(folder, 0, 7);
        Controls.Add(layout);
        apply.Click += async (_, _) => await Run("Apply", Apply);
        restore.Click += async (_, _) => await Run("Khôi phục", Restore);
        check.Click += async (_, _) => await Run("Kiểm tra", Check);
        FormClosing += (_, e) => { if (busy) { e.Cancel = true; Log("Đợi quá trình hiện tại hoàn tất trước khi đóng."); } else dpi.Dispose(); };
        Log("DNS IPv4 áp dụng cho Wi-Fi/Ethernet đang có đường ra Internet; ảnh hưởng cả ứng dụng khác trên card đó. Giữ nguyên IP/gateway/IPv6.");
        Log("Apply/Khôi phục sẽ yêu cầu quyền quản trị nếu cần. Steam sẽ được thoát nhẹ và mở lại sau Apply; hãy lưu game trước.");
        Log("GoodbyeDPI 0.2.2 chính thức. Đóng tool sẽ dừng DPI; hosts giữ nguyên cho đến Khôi phục.");
        var timer = new System.Windows.Forms.Timer { Interval = 3000 };
        bool dpiWasRunning = false;
        timer.Tick += (_, _) =>
        {
            if (!busy && dpiWasRunning && !dpi.Running) { Log("Engine DPI đã dừng ngoài dự kiến. Bấm Apply để thử lại."); status.Text = "DPI đã dừng"; }
            dpiWasRunning = dpi.Running;
        };
        timer.Start();
        FormClosed += (_, _) => timer.Dispose();
        Shown += async (_, _) =>
        {
            if (action?.StartsWith("--apply:", StringComparison.Ordinal) == true)
            {
                var options = action.Split(':');
                if (options.Length == 3 && int.TryParse(options[1], out int provider) && provider is >= 0 and <= 2 && options[2] is "0" or "1")
                {
                    dnsProvider.SelectedIndex = provider;
                    useDpi.Checked = options[2] == "1";
                    await Run("Apply", Apply);
                }
            }
            if (action == "--apply-dns") { useDpi.Checked = false; await Run("Apply", Apply); }
            if (action == "--apply") await Run("Apply", Apply);
            if (action == "--restore") await Run("Khôi phục", Restore);
        };
    }

    void Log(string message) => output.AppendText($"[{DateTime.Now:HH:mm:ss}] {message.Replace("\n", Environment.NewLine)}{Environment.NewLine}");

    bool EnsureAdmin(string action)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return true;
        Log("Mở lại tool với quyền quản trị. Quá trình tự tiếp tục sau khi bạn chấp nhận UAC.");
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, action) { UseShellExecute = true, Verb = "runas" });
            // Release this instance before the elevated one acquires its mutex.
            Environment.Exit(0);
        }
        catch (System.ComponentModel.Win32Exception) { Log("Chưa được cấp quyền quản trị; chưa thay đổi hệ thống."); }
        return false;
    }

    async Task Run(string name, Func<Task> action)
    {
        busy = true;
        apply.Enabled = restore.Enabled = check.Enabled = false;
        useDpi.Enabled = false;
        dnsProvider.Enabled = false;
        status.Text = name + " đang chạy…";
        try { await action(); status.Text = "Hoàn tất — xem kết quả bên dưới"; }
        catch (Exception e) { Log("LỖI: " + e.Message); status.Text = "Chưa hoàn tất — xem nhật ký"; }
        finally
        {
            busy = false;
            apply.Enabled = restore.Enabled = check.Enabled = true;
            useDpi.Enabled = true;
            dnsProvider.Enabled = true;
            try
            {
                if (Directory.Exists(FixEngine.DataPath))
                    File.WriteAllText(Path.Combine(FixEngine.DataPath, "latest.log"), output.Text, Encoding.UTF8);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log("Không ghi được nhật ký: " + e.Message); }
        }
    }

    async Task Apply()
    {
        if (!EnsureAdmin($"--apply:{dnsProvider.SelectedIndex}:{(useDpi.Checked ? 1 : 0)}")) return;
        dpi.Stop();
        byte[] before = File.ReadAllBytes(FixEngine.HostsPath);
        if (before.Contains((byte)0)) throw new IOException("Hosts dùng UTF-16; chưa thay đổi DNS/hosts.");
        FixEngine.RemoveBlock(Encoding.Latin1.GetString(before)); // Validate before making network changes.
        byte[] expected = before;
        DnsSettings.DnsChange? dnsChange = null;
        void UpdateHosts(Func<string, string> transform)
        {
            byte[] after = Encoding.Latin1.GetBytes(transform(Encoding.Latin1.GetString(expected)));
            string backup = FixEngine.WriteHosts(FixEngine.HostsPath, current =>
            {
                if (!Encoding.Latin1.GetBytes(current).SequenceEqual(expected))
                    throw new IOException("Hosts có sửa đổi đồng thời; dừng Apply.");
                return Encoding.Latin1.GetString(after);
            }, Path.Combine(FixEngine.DataPath, "backups"));
            expected = after;
            if (backup.Length > 0) Log("Sao lưu hosts: " + backup);
        }
        try
        {
            if (dnsProvider.SelectedIndex != 0) dnsChange = await DnsSettings.Apply(dnsProvider.SelectedIndex, Log);
            UpdateHosts(FixEngine.RemoveBlock); // Remove our old pinned IPs so the DNS-only test is meaningful.
            await FixEngine.Flush(Log);
            Log("Bước 1: kiểm tra với DNS hiện tại, chưa bật DPI.");
            bool ok = await VerifyWindows();
            if (!ok)
            {
                Log("Bước 2: thử IP lấy từ DNS mã hóa và cập nhật vùng hosts riêng.");
                var addresses = await FixEngine.Prepare(Log, requireConnectivity: !useDpi.Checked);
                UpdateHosts(text => FixEngine.AddBlock(text, addresses));
                await FixEngine.Flush(Log);
                ok = await VerifyWindows();
            }
            if (!ok && useDpi.Checked)
            {
                Log("Bước 3: DNS/hosts chưa đủ; khởi tạo GoodbyeDPI.");
                string engineDirectory = EnginePackage.Install();
                for (int profile = 0; profile < DpiRuntime.Profiles.Length; profile++)
                {
                    await dpi.Start(engineDirectory, profile, Log);
                    if (await VerifyWindows() && dpi.Running) { ok = true; break; }
                    dpi.Stop();
                    Log("Cấu hình này chưa đạt; dừng trước khi thử cấu hình tiếp theo.");
                }
            }
            if (!ok) throw new IOException("Các cách DNS/hosts/DPI đã chọn chưa giúp truy cập Steam. Có thể chặn IP hoặc Steam đang lỗi; hãy thử mạng khác/VPN.");
        }
        catch
        {
            dpi.Stop();
            try
            {
                byte[] current = File.ReadAllBytes(FixEngine.HostsPath);
                if (!current.SequenceEqual(before) && current.SequenceEqual(expected))
                {
                    UpdateHosts(_ => Encoding.Latin1.GetString(before));
                    Log("Apply thất bại: đã trả hosts về cấu hình trước lần Apply này.");
                }
                else if (!current.SequenceEqual(before)) Log("Hosts có thay đổi khác; không tự ghi đè. Dùng Khôi phục để gỡ riêng vùng tool nếu cần.");
            }
            catch (Exception rollbackError) { Log("Chưa khôi phục tự động được: " + rollbackError.Message + ". Dùng nút Khôi phục."); }
            try { if (dnsChange != null) await dnsChange.Rollback(Log); }
            catch (Exception dnsError) { Log("Chưa trả DNS cũ được: " + dnsError.Message + ". Sao lưu được giữ để Khôi phục thử lại."); }
            try { await FixEngine.Flush(Log); }
            catch (Exception flushError) { Log(flushError.Message); }
            throw;
        }
        Log("Store / Community / Help truy cập HTTPS được. Đang mở lại Steam…");
        await RestartSteam();
        Log("Hoàn tất kiểm tra web. Hãy kiểm tra Store trong Steam; phép thử này không xác nhận đăng nhập hoặc tải game.");
        if (dpi.Running) Log("DPI đang bật cho các tên miền Steam. Giữ cửa sổ tool mở khi dùng Steam.");
        else Log("Không cần bật DPI trong lần kiểm tra này. DNS/hosts giữ đến khi bấm Khôi phục.");
    }

    async Task<bool> VerifyWindows()
    {
        Log("Kiểm tra HTTPS qua cấu hình Windows…");
        bool ok = true;
        foreach (string domain in FixEngine.Domains.Take(3))
        {
            bool reachable = await FixEngine.Probe(domain);
            Log($"{domain}: {(reachable ? "HTTPS OK" : "CHƯA TRUY CẬP ĐƯỢC")}");
            ok &= reachable;
        }
        return ok;
    }

    async Task Restore()
    {
        if (!EnsureAdmin("--restore")) return;
        dpi.Stop();
        Log("Đã dừng engine DPI của tool.");
        var errors = new List<string>();
        try { await DnsSettings.Restore(Log); }
        catch (Exception e) { errors.Add("DNS: " + e.Message); }
        try
        {
            string backup = FixEngine.WriteHosts(FixEngine.HostsPath, FixEngine.RemoveBlock, Path.Combine(FixEngine.DataPath, "backups"));
            Log(backup.Length == 0 ? "Không có cấu hình hosts của SteamFixVN để gỡ." : "Đã gỡ riêng vùng SteamFixVN; giữ nguyên cấu hình khác. Sao lưu: " + backup);
        }
        catch (Exception e) { errors.Add("Hosts: " + e.Message); }
        try { await FixEngine.Flush(Log); }
        catch (Exception e) { errors.Add(e.Message); }
        if (errors.Count > 0) throw new IOException(string.Join("\n", errors));
        Log("Khôi phục hoàn tất. Thoát và mở lại Steam nếu đang chạy.");
    }

    async Task Check()
    {
        Log("Kiểm tra chỉ đọc, không sửa hệ thống.");
        try
        {
            var adapter = await DnsSettings.Capture();
            Log($"Card mạng: {adapter.Name}; DNS IPv4 {(adapter.Automatic ? "tự động" : "thủ công")}: {string.Join(" / ", adapter.Servers)}");
            Log("DNS IPv6 (tool giữ nguyên): " + string.Join(" / ", adapter.IPv6Servers));
        }
        catch (Exception e) { Log("Không đọc được adapter: " + e.Message); }
        foreach (string domain in FixEngine.Domains.Take(3))
            Log($"Windows → {domain}: {(await FixEngine.Probe(domain) ? "HTTPS OK" : "Không truy cập được")}");
        await FixEngine.Prepare(Log);
        Log("Đường kết nối qua IP DNS mã hóa hoạt động. Có thể dùng Apply.");
    }

    async Task RestartSteam()
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
        string? path = key?.GetValue("InstallPath") as string;
        string? exe = path == null ? null : Path.Combine(path, "steam.exe");
        if (Process.GetProcessesByName("steam").Length > 0)
        {
            if (exe == null || !File.Exists(exe)) { Log("Không tìm được Steam.exe. Thoát và mở lại Steam thủ công."); return; }
            using var shutdown = Process.Start(new ProcessStartInfo(exe, "-shutdown") { UseShellExecute = false, CreateNoWindow = true });
            for (int i = 0; i < 20; i++)
            {
                var running = Process.GetProcessesByName("steam");
                bool alive = running.Length > 0;
                foreach (var process in running) process.Dispose();
                if (!alive) break;
                await Task.Delay(1000);
            }
            var remaining = Process.GetProcessesByName("steam");
            bool stillRunning = remaining.Length > 0;
            foreach (var process in remaining) process.Dispose();
            if (stillRunning) { Log("Steam chưa thoát. Hãy thoát Steam rồi mở lại; tool không ép đóng tiến trình."); return; }
        }
        Process.Start(new ProcessStartInfo("explorer.exe", "steam://open/store") { UseShellExecute = true });
    }
}
