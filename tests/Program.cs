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
if (args.Contains("--community-read"))
{
    var ips = await FixEngine.Resolve("steamcommunity.com");
    var tls12 = await FixEngine.ProbeDetailed(new Uri("https://steamcommunity.com/"), protocols: System.Security.Authentication.SslProtocols.Tls12);
    Console.WriteLine($"Windows Community TLS 1.2: {tls12.Success} — {tls12.Detail}");
    foreach (var uri in FixEngine.WebChecks)
    {
        var windows = await FixEngine.ProbeDetailed(uri);
        Console.WriteLine($"Windows {uri}: {windows.Success} — {windows.Detail}");
        if (uri.Host != "steamcommunity.com") continue;
        foreach (string ip in ips)
        {
            var direct = await FixEngine.ProbeDetailed(uri, ip);
            Console.WriteLine($"DoH IPv4 {ip} {uri.AbsolutePath}: {direct.Success} — {direct.Detail}");
        }
    }
    Console.WriteLine("Read-only check; no DNS/hosts changes or DPI driver loading. /my/ checks anonymous login routing only.");
    return;
}
if (args.Contains("--payment-read"))
{
    foreach (var uri in FixEngine.WebChecks.Where(uri => uri.Host is "store.steampowered.com" or "checkout.steampowered.com"))
    {
        var windows = await FixEngine.ProbeDetailed(uri);
        Console.WriteLine($"Windows {uri}: {windows.Success} — {windows.Detail}");
        foreach (string ip in await FixEngine.Resolve(uri.Host))
        {
            var direct = await FixEngine.ProbeDetailed(uri, ip);
            Console.WriteLine($"DoH {ip} {uri}: {direct.Success} — {direct.Detail}");
        }
    }
    Console.WriteLine("Read-only anonymous check; no account, cart change, purchase, DNS/hosts edit or driver loading.");
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
async Task<(FixEngine.WebProbe Result, int Requests)> WebScenario(params (int Status, string? Location)[] replies)
{
    using var handler = new ScriptedWebHandler(replies);
    using var client = new HttpClient(handler);
    var result = await FixEngine.ProbeWithClient(client, new Uri("https://steamcommunity.com/my/"));
    return (result, handler.Requests.Count);
}
var unavailableProfile = await WebScenario((302, "/login/home/?goto=/my/"), (503, null));
Assert(!unavailableProfile.Result.Success && unavailableProfile.Requests == 2,
    "Profile redirect to an unavailable login page cannot report success");
var availableProfile = await WebScenario((302, "/login/home/?goto=/my/"), (200, null));
Assert(availableProfile.Result.Success && availableProfile.Requests == 2,
    "Anonymous profile check follows Steam login redirect to a successful final response");
var externalRedirect = await WebScenario((302, "https://example.com/blocked"));
Assert(!externalRedirect.Result.Success && externalRedirect.Requests == 1,
    "A redirect outside Steam fails without contacting the external site");
var insecureRedirect = await WebScenario((302, "http://steamcommunity.com/login/"));
Assert(!insecureRedirect.Result.Success && insecureRedirect.Requests == 1,
    "HTTPS downgrade is rejected without sending a plaintext request");
Assert(!(await WebScenario((302, null))).Result.Success, "Redirect without Location fails");
Assert(!(await WebScenario((304, null))).Result.Success, "HTTP 304 does not prove a usable web page");
Assert(!(await WebScenario((403, null))).Result.Success, "Community HTTP 403 is a failure, unlike a CDN root");
var redirectLoop = await WebScenario(Enumerable.Repeat((302, (string?)"/my/"), 6).ToArray());
Assert(!redirectLoop.Result.Success && redirectLoop.Requests == 6, "Redirect loops stop after a bounded number of requests");
var storeLoginRedirect = await WebScenario((302, "https://login.steampowered.com/"), (200, null));
Assert(storeLoginRedirect.Result.Success, "Cross-host HTTPS redirect inside the Steam allowlist is supported");
Assert(FixEngine.WebChecks.Any(uri => uri.Host == "checkout.steampowered.com" && uri.AbsolutePath == "/checkout/")
    && FixEngine.WebChecks.Any(uri => uri.Host == "store.steampowered.com" && uri.AbsolutePath == "/cart/"),
    "Checkout and cart are required web checks even when the Store root works");
Assert(FixEngine.WebChecks.Any(uri => uri.AbsoluteUri == "https://checkout.steampowered.com/checkout/?accountcart=1"),
    "Checkout verification includes the reported account-cart URL");
using (var checkoutHandler = new ScriptedWebHandler([(302, "/login/"), (503, null)]))
using (var checkoutClient = new HttpClient(checkoutHandler))
{
    var result = await FixEngine.ProbeWithClient(checkoutClient, new Uri("https://checkout.steampowered.com/checkout/"));
    Assert(!result.Success && checkoutHandler.Requests.Count == 2,
        "Checkout redirect to an unavailable login page is a failure, not a successful payment check");
}
using (var dnsHandler = new DoHHandler(false))
using (var dnsClient = new HttpClient(dnsHandler))
{
    var ips = await FixEngine.ResolveWithClient(dnsClient, "steamcommunity.com").WaitAsync(TimeSpan.FromSeconds(3));
    Assert(ips.SequenceEqual(new[] { "23.10.20.30", "23.10.20.31" }),
        "Parallel DoH combines both providers, removes duplicates and rejects private DNS answers");
}
using (var dnsHandler = new DoHHandler(true))
using (var dnsClient = new HttpClient(dnsHandler))
{
    var ips = await FixEngine.ResolveWithClient(dnsClient, "steamcommunity.com").WaitAsync(TimeSpan.FromSeconds(3));
    Assert(ips.Contains("23.10.20.31"), "A failed Cloudflare request does not discard a valid Google answer");
}
int attempts = 0, stops = 0, refreshes = 0;
bool active = false;
bool selected = await DpiRuntime.TryProfiles(
    profile => { if (active) throw new Exception("Overlapping engines"); active = true; attempts++; return Task.CompletedTask; },
    () => { active = false; stops++; },
    () => Task.FromResult(false),
    () => { refreshes++; return Task.FromResult(attempts == 2); }, _ => { });
Assert(selected && attempts == 2 && stops == 1 && refreshes == 2 && active,
    "Failed pinned IP is retried under DPI; failed engine stops before next profile and winning engine stays alive");
active = false; attempts = stops = refreshes = 0;
selected = await DpiRuntime.TryProfiles(
    _ => { attempts++; return Task.CompletedTask; }, () => stops++,
    () => Task.FromResult(true),
    () => { refreshes++; return Task.FromResult(false); }, _ => { });
Assert(selected && attempts == 1 && stops == 0 && refreshes == 0,
    "Successful initial HTTPS check skips address refresh and later profiles");
var triedProfiles = new List<int>();
stops = refreshes = 0;
selected = await DpiRuntime.TryProfiles(
    profile => { triedProfiles.Add(profile); return Task.CompletedTask; }, () => stops++,
    () => Task.FromResult(true),
    () => { refreshes++; return Task.FromResult(false); }, _ => { }, selectedProfile: 2);
Assert(selected && triedProfiles.SequenceEqual(new[] { 2 }) && stops == 0 && refreshes == 0,
    "Manual retry selects the requested DPI profile even when web verification succeeds");
triedProfiles.Clear(); stops = 0;
selected = await DpiRuntime.TryProfiles(
    profile => { triedProfiles.Add(profile); return Task.CompletedTask; }, () => stops++,
    () => Task.FromResult(false), () => Task.FromResult(false), _ => { }, selectedProfile: 2);
Assert(!selected && triedProfiles.SequenceEqual(new[] { 2 }) && stops == 1,
    "Failed manual profile stops without silently selecting another profile");
foreach (int invalidProfile in new[] { -1, DpiRuntime.Profiles.Length })
{
    attempts = 0;
    try
    {
        await DpiRuntime.TryProfiles(_ => { attempts++; return Task.CompletedTask; }, () => { },
            () => Task.FromResult(true), () => Task.FromResult(true), _ => { }, invalidProfile);
        throw new Exception("Expected invalid profile rejection");
    }
    catch (ArgumentOutOfRangeException) { Assert(attempts == 0, "Invalid manual profile rejected before starting an engine"); }
}
attempts = stops = 0;
selected = await DpiRuntime.TryProfiles(
    _ => { attempts++; return Task.CompletedTask; }, () => stops++,
    () => Task.FromResult(false), () => Task.FromResult(false), _ => { });
Assert(!selected && attempts == DpiRuntime.Profiles.Length && stops == attempts,
    "Exhausted strategy search is bounded and stops every failed engine");
stops = 0;
try
{
    await DpiRuntime.TryProfiles(_ => Task.CompletedTask, () => stops++,
        () => Task.FromResult(false), () => throw new IOException("Concurrent hosts edit"), _ => { });
    throw new Exception("Expected refresh error");
}
catch (IOException) { Assert(stops == 1, "Address refresh failure stops engine before propagating rollback error"); }
stops = 0;
try
{
    await DpiRuntime.TryProfiles(_ => throw new IOException("Driver failure"), () => stops++,
        () => Task.FromResult(true), () => Task.FromResult(true), _ => { });
    throw new Exception("Expected driver error");
}
catch (IOException) { Assert(stops == 1, "Driver initialization failure stops owned engine and aborts strategy search"); }
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
Assert(DpiRuntime.Profiles[0].Contains("--native-frag") && DpiRuntime.Profiles[0].Contains("--reverse-frag")
    && DpiRuntime.Profiles[0].Contains("--max-payload=4096"),
    "Automatic DPI starts with the profile confirmed to open Checkout on the reported FPT connection");
Assert(package.ContainsKey("goodbyedpi.exe") && package.ContainsKey("WinDivert.dll") && package.ContainsKey("WinDivert64.sys"), "Pinned engine archive contains all x64 dependencies");
Assert(package.Keys.Count(k => k.StartsWith("licenses/")) == 4, "Upstream licenses included");
for (int profile = 0; profile < DpiRuntime.Profiles.Length; profile++)
{
    var info = DpiRuntime.BuildStartInfo(@"C:\test folder\engine", profile);
    Assert(info.ArgumentList.Contains("--blacklist") && info.ArgumentList.Last().EndsWith("steam-domains.txt"), "Every DPI profile limited by Steam hostname list");
    Assert(!info.ArgumentList.Contains("-p") && !info.ArgumentList.Contains("-q") && !info.ArgumentList.Contains("--dns-addr")
        && !info.ArgumentList.Contains("--allow-no-sni") && !info.ArgumentList.Contains("--auto-ttl")
        && !info.ArgumentList.Contains("-5") && !info.ArgumentList.Contains("--set-ttl"), "No global reset/QUIC/DNS, unscoped SNI or route-specific TTL rules");
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

sealed class ScriptedWebHandler((int Status, string? Location)[] replies) : System.Net.Http.HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];
    protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var reply = replies[Requests.Count];
        Requests.Add(request.RequestUri!);
        var response = new System.Net.Http.HttpResponseMessage((System.Net.HttpStatusCode)reply.Status);
        if (reply.Location != null) response.Headers.Location = new Uri(reply.Location, UriKind.RelativeOrAbsolute);
        return Task.FromResult(response);
    }
}

sealed class DoHHandler(bool failCloudflare) : System.Net.Http.HttpMessageHandler
{
    readonly TaskCompletionSource bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int started;
    protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken token)
    {
        if (Interlocked.Increment(ref started) == 2) bothStarted.SetResult();
        await bothStarted.Task.WaitAsync(token);
        bool cloudflare = request.RequestUri!.Host == "1.1.1.1";
        if (cloudflare && failCloudflare) throw new System.Net.Http.HttpRequestException("Resolver unavailable");
        string json = cloudflare
            ? """{"Status":0,"Answer":[{"type":1,"data":"23.10.20.30"},{"type":1,"data":"192.168.1.1"}]}"""
            : """{"Status":0,"Answer":[{"type":1,"data":"23.10.20.30"},{"type":1,"data":"23.10.20.31"}]}""";
        return new(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) };
    }
}
