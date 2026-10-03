using System.Text;
using System.Diagnostics;
using System.Reflection;
using SteamFixVN;

ProcessStartInfo HelperInfo(string arg)
{
    var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
    info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    info.ArgumentList.Add(arg);
    return info;
}
if (args.Contains("--child-wait"))
{
    Console.WriteLine("READY");
    await Task.Delay(Timeout.Infinite);
    return;
}
if (args.Contains("--parent-crash"))
{
    using var owned = OwnedProcess.Start(HelperInfo("--child-wait"));
    await owned.Process.StandardOutput.ReadLineAsync();
    Console.WriteLine(owned.Process.Id);
    await Task.Delay(Timeout.Infinite);
    return;
}
if (args.Contains("--network"))
{
    var networkAddresses = await FixEngine.Prepare(Console.WriteLine);
    Console.WriteLine($"Read-only network check: {networkAddresses.Count} verified domains. Hosts untouched.");
    return;
}
if (args.Contains("--dns-read"))
{
    var adapter = await DnsSettings.Capture();
    var sameAdapter = await DnsSettings.Capture(adapter.Id);
    if (sameAdapter.Id != adapter.Id || !sameAdapter.Servers.SequenceEqual(adapter.Servers))
        throw new Exception("GUID lookup did not return the same adapter/DNS snapshot.");
    Console.WriteLine("Adapter lookup by GUID confirmed.");
    Console.WriteLine($"Adapter: {adapter.Name}; IPv4 DNS mode: {(adapter.Automatic ? "DHCP" : "Static")}; servers: {string.Join(", ", adapter.Servers)}");
    Console.WriteLine("IPv6 DNS (unchanged): " + string.Join(", ", adapter.IPv6Servers));
    foreach (string domain in FixEngine.Domains.Take(3))
        Console.WriteLine($"Current Windows HTTPS: {domain} = {await FixEngine.Probe(domain)}");
    Console.WriteLine("Read-only adapter/DNS/HTTPS check; no netsh mutation, hosts edit or driver loading.");
    return;
}
int count = 0;
void Assert(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); count++;
}
void Reject(Action action, string name)
{
    try { action(); }
    catch (IOException) { Assert(true, name); return; }
    throw new Exception("FAIL: " + name);
}
var addresses = new Dictionary<string, string> { ["store.steampowered.com"] = "23.10.20.30" };
foreach (string original in new[] { "", "# original", "# original\r\n", "# original\n127.0.0.1 localhost\n", "# café\r\n" })
{
    string changed = FixEngine.AddBlock(original, addresses);
    Assert(FixEngine.RemoveBlock(changed) == original, "Exact restore including line endings");
    Assert(FixEngine.AddBlock(changed, addresses) == changed, "Apply idempotent");
}
Reject(() => FixEngine.AddBlock("127.0.0.1 STORE.STEAMPOWERED.COM\n", addresses), "Existing mapping conflict");
Assert(FixEngine.AddBlock("# 127.0.0.1 store.steampowered.com\n", addresses).Contains("23.10.20.30"), "Comments are not conflicts");
var refreshed = new Dictionary<string, string> { ["store.steampowered.com"] = "23.10.20.31" };
Assert(!FixEngine.AddBlock(FixEngine.AddBlock("# original", addresses), refreshed).Contains("23.10.20.30"), "Reapply refreshes CDN address");
Reject(() => FixEngine.RemoveBlock(FixEngine.Begin + "broken"), "Incomplete block rejected");
Reject(() => FixEngine.RemoveBlock(FixEngine.Begin + FixEngine.End + FixEngine.Begin + FixEngine.End), "Duplicate blocks rejected");
Reject(() => FixEngine.AddBlock("", new Dictionary<string, string> { ["store.steampowered.com"] = "127.0.0.1" }), "Loopback DNS rejected");
Reject(() => FixEngine.AddBlock("", new Dictionary<string, string> { ["evil.test"] = "23.10.20.30" }), "Non-Steam domain rejected");
string dir = Path.Combine(Path.GetTempPath(), "SteamFixVN-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try
{
    string hosts = Path.Combine(dir, "hosts");
    byte[] original = Encoding.UTF8.GetBytes("# giữ cấu hình\r\n127.0.0.1 localhost\r\n");
    File.WriteAllBytes(hosts, original);
    string backup = FixEngine.WriteHosts(hosts, s => FixEngine.AddBlock(s, addresses), Path.Combine(dir, "backups"));
    Assert(File.ReadAllBytes(backup).SequenceEqual(original), "Byte-exact backup");
    Assert(FixEngine.WriteHosts(hosts, s => FixEngine.AddBlock(s, addresses), Path.Combine(dir, "backups")) == "", "Reapply does not create unnecessary backup");
    File.AppendAllText(hosts, "10.0.0.1 intranet.local\r\n", Encoding.UTF8);
    FixEngine.WriteHosts(hosts, FixEngine.RemoveBlock, Path.Combine(dir, "backups"));
    Assert(File.ReadAllBytes(hosts).SequenceEqual(original.Concat(Encoding.UTF8.GetBytes("10.0.0.1 intranet.local\r\n"))), "Restore preserves subsequent edits and original UTF-8 bytes");
    File.WriteAllBytes(hosts, original);
    Reject(() => FixEngine.WriteHosts(hosts, s => { File.WriteAllText(hosts, "# concurrent edit"); return FixEngine.AddBlock(s, addresses); }, dir), "Concurrent edit aborts replace");
    Assert(File.ReadAllText(hosts) == "# concurrent edit", "Concurrent edit preserved");
    Assert(Directory.GetFiles(dir, "*.steamfix-*").Length == 0, "Temporary file cleaned after abort");
    File.WriteAllBytes(hosts, Encoding.Unicode.GetBytes("# hosts"));
    Reject(() => FixEngine.WriteHosts(hosts, FixEngine.RemoveBlock, dir), "UTF-16 rejected without mutation");
}
finally { Directory.Delete(dir, recursive: true); }
var originalDns = new AdapterDns(Guid.NewGuid(), 12, "Wi-Fi", true, ["192.168.1.1"], ["fe80::1"]);
var changedDns = originalDns with { Automatic = false, Servers = DnsSettings.Providers[2] };
var journalDns = new DnsJournal(originalDns, DnsSettings.Providers[2], false);
Assert(DnsSettings.MayRestore(changedDns, journalDns), "Restore DNS owned by this tool");
Assert(!DnsSettings.MayRestore(changedDns with { Id = Guid.NewGuid() }, journalDns), "Recycled interface index does not identify original adapter");
Assert(!DnsSettings.MayRestore(changedDns with { Servers = ["9.9.9.9"] }, journalDns), "External manual DNS change is protected");
Assert(!DnsSettings.MayRestore(originalDns, journalDns), "Committed change does not overwrite externally restored DHCP");
Assert(DnsSettings.MayRestore(changedDns with { Servers = ["8.8.8.8"] }, journalDns with { Pending = true }), "Crash recovery accepts partially applied preset");
Assert(!DnsSettings.MayRestore(changedDns with { Servers = ["8.8.8.8"] }, journalDns), "Partial DNS not accepted after successful commit");
Assert(DnsSettings.MayRestore(originalDns, journalDns with { Pending = true }), "Crash before first command preserves original DHCP");
Assert(DnsSettings.Matches(originalDns with { Servers = ["192.168.2.1"] }, true, originalDns.Servers), "DHCP recovery resets mode instead of pinning old lease DNS");
var restoreDhcp = DnsSettings.Commands(12, true, originalDns.Servers);
Assert(restoreDhcp.Count == 1 && restoreDhcp[0].Contains("source=dhcp") && !restoreDhcp[0].Any(a => a.StartsWith("address=")), "DHCP restore has no stale static server");
var restoreStatic = DnsSettings.Commands(14, false, ["10.0.0.53", "10.0.0.54", "192.168.0.1"]);
Assert(restoreStatic.Count == 3 && restoreStatic[2].Contains("index=3"), "Original static DNS order and private addresses preserved");
Assert(restoreStatic.All(c => c[1] == "ipv4" && c.Contains("name=14") && !c.Contains("gateway")), "DNS commands cannot modify IPv6 or gateway");
Assert(DnsSettings.Commands(12, false, []).Single().Contains("address=none"), "Explicit empty static DNS can be restored");
Reject(() => DnsSettings.Commands(0, false, ["1.1.1.1"]), "Invalid adapter index rejected");
Reject(() => DnsSettings.Commands(12, false, ["1.1.1.1;whoami"]), "Command injection through DNS values rejected");
Reject(() => DnsSettings.Commands(12, false, ["2606:4700:4700::1111"]), "IPv6 address not accepted for IPv4 change");
var roundTripDns = System.Text.Json.JsonSerializer.Deserialize<DnsJournal>(System.Text.Json.JsonSerializer.Serialize(journalDns))!;
Assert(roundTripDns.Original.Automatic && roundTripDns.Original.Id == originalDns.Id && roundTripDns.Original.IPv6Servers.SequenceEqual(originalDns.IPv6Servers), "Recovery record preserves mode, GUID and IPv6 snapshot");
var package = EnginePackage.Read();
Assert(package.ContainsKey("goodbyedpi.exe") && package.ContainsKey("WinDivert.dll") && package.ContainsKey("WinDivert64.sys"), "Pinned engine archive contains all x64 dependencies");
Assert(package.Keys.Count(k => k.StartsWith("licenses/")) == 4, "Upstream licenses included");
for (int profile = 0; profile < DpiRuntime.Profiles.Length; profile++)
{
    var info = DpiRuntime.BuildStartInfo(@"C:\test folder\engine", profile);
    Assert(info.ArgumentList.Contains("--blacklist") && info.ArgumentList.Last().EndsWith("steam-domains.txt"), "Every DPI profile limited by Steam hostname list");
    Assert(!info.ArgumentList.Contains("-p") && !info.ArgumentList.Contains("-q") && !info.ArgumentList.Contains("--dns-addr"), "No global reset/QUIC/DNS rules");
    Assert(!info.UseShellExecute && info.CreateNoWindow && info.ArgumentList.Last().Contains("test folder"), "Safe argument handling for paths with spaces");
}
using (var owned = OwnedProcess.Start(HelperInfo("--child-wait")))
{
    Assert(await owned.Process.StandardOutput.ReadLineAsync() == "READY", "Job-owned helper starts");
    using var childMonitor = Process.GetProcessById(owned.Process.Id);
    owned.Dispose();
    Assert(childMonitor.WaitForExit(5000), "Stopping engine kills its own child");
}
using (var parent = Process.Start(HelperInfo("--parent-crash"))!)
{
    int childPid = int.Parse((await parent.StandardOutput.ReadLineAsync())!);
    using var childMonitor = Process.GetProcessById(childPid);
    parent.Kill();
    await parent.WaitForExitAsync();
    Assert(childMonitor.WaitForExit(5000), "UI crash cannot leave engine helper running");
}
Console.WriteLine($"{count} checks passed; system hosts untouched.");
