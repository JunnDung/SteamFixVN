using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SteamFixVN;

public static class EnginePackage
{
    public const string PackageSha256 = "426F7BA28B3138DA0D5F5BCC6B54DCF58D20E6D325EB507ED4B452DF22CE8AE8";
    public static Dictionary<string, byte[]> Read()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SteamFixVN.Engine.zip")
            ?? throw new IOException("Thiếu engine GoodbyeDPI trong EXE.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        if (Convert.ToHexString(SHA256.HashData(memory.ToArray())) != PackageSha256)
            throw new IOException("Gói engine không đúng SHA-256.");
        memory.Position = 0;
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read);
        var result = new Dictionary<string, byte[]>();
        foreach (var entry in zip.Entries)
        {
            if (entry.Name.Length == 0) continue;
            string name = entry.FullName.Replace('\\', '/');
            if (Path.IsPathRooted(name) || name.Split('/').Any(p => p is "." or ".."))
                throw new IOException("Đường dẫn engine không hợp lệ.");
            using var contents = entry.Open();
            using var bytes = new MemoryStream();
            contents.CopyTo(bytes);
            result.Add(name, bytes.ToArray());
        }
        return result;
    }

    public static void SecureDataDirectory()
    {
        // Executables loaded with administrator privileges must not be writable by ordinary users.
        string root = FixEngine.DataPath;
        if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Thư mục dữ liệu là liên kết; từ chối nạp engine.");
        Directory.CreateDirectory(root);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        foreach (var sid in new[] { WellKnownSidType.BuiltinAdministratorsSid, WellKnownSidType.LocalSystemSid })
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(root).SetAccessControl(security);
    }

    public static string Install()
    {
        SecureDataDirectory();
        string root = FixEngine.DataPath;
        var security = new DirectoryInfo(root).GetAccessControl();
        string directory = Path.Combine(root, "engine-0.2.2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        new DirectoryInfo(directory).SetAccessControl(security);
        foreach (var entry in Read())
        {
            string destination = Path.Combine(directory, entry.Key.Replace('/', Path.DirectorySeparatorChar));
            string parent = Path.GetDirectoryName(destination)!;
            if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Thư mục engine chứa liên kết không hợp lệ.");
            Directory.CreateDirectory(parent);
            string temp = destination + "." + Guid.NewGuid().ToString("N");
            try { File.WriteAllBytes(temp, entry.Value); File.Move(temp, destination, overwrite: true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        string domains = Path.Combine(directory, "steam-domains.txt");
        string domainTemp = domains + "." + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(domainTemp, "steampowered.com\nsteamcommunity.com\nsteamstatic.com\n", Encoding.ASCII); File.Move(domainTemp, domains, true); }
        finally { if (File.Exists(domainTemp)) File.Delete(domainTemp); }
        return directory;
    }
}

// Closing this handle also ends the child if the UI exits unexpectedly.
public sealed class OwnedProcess : IDisposable
{
    readonly SafeFileHandle job;
    bool disposed;
    public Process Process { get; }
    OwnedProcess(SafeFileHandle job, Process process) { this.job = job; Process = process; }
    public static OwnedProcess Start(ProcessStartInfo info)
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        if (job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        Process? process = null;
        try
        {
            var limits = new ExtendedLimits();
            limits.Basic.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf<ExtendedLimits>()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            process = Process.Start(info) ?? throw new IOException("Không chạy được engine.");
            if (!AssignProcessToJobObject(job, process.Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return new OwnedProcess(job, process);
        }
        catch
        {
            if (process != null) { if (!process.HasExited) process.Kill(); process.Dispose(); }
            job.Dispose(); throw;
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        job.Dispose();
        if (!Process.HasExited) Process.WaitForExit(5000);
        Process.Dispose();
    }
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint LimitFlags;
        public UIntPtr MinWorkingSet, MaxWorkingSet;
        public uint ActiveProcesses;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref ExtendedLimits limits, int length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
}

public sealed class DpiRuntime : IDisposable
{
    public static readonly string[][] Profiles =
    [
        ["-f", "2", "-e", "2", "--native-frag", "--max-payload=1200"],
        ["-6"]
    ];
    OwnedProcess? child;
    Task<string>? stdout, stderr;
    public bool Running => child != null && !child.Process.HasExited;
    public static ProcessStartInfo BuildStartInfo(string directory, int profile)
    {
        var info = new ProcessStartInfo(Path.Combine(directory, "goodbyedpi.exe"))
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in Profiles[profile]) info.ArgumentList.Add(arg);
        info.ArgumentList.Add("--blacklist");
        info.ArgumentList.Add(Path.Combine(directory, "steam-domains.txt"));
        return info;
    }
    public async Task Start(string directory, int profile, Action<string> log)
    {
        Stop();
        child = OwnedProcess.Start(BuildStartInfo(directory, profile));
        stdout = Drain(child.Process.StandardOutput);
        stderr = Drain(child.Process.StandardError);
        log($"GoodbyeDPI: thử cấu hình {profile + 1}; đang chờ driver khởi tạo…");
        // Upstream sleeps 20 seconds before exiting on driver errors, and buffers stdout in pipes.
        for (int i = 0; i < 22; i++)
        {
            await Task.Delay(1000);
            if (!Running)
            {
                string details = await stdout + "\n" + await stderr;
                Stop();
                throw new IOException("GoodbyeDPI không nạp được driver/filter.\n" + details.Trim());
            }
        }
        log("Engine vẫn chạy; kiểm tra HTTPS để đánh giá cấu hình.");
    }
    public void Stop()
    {
        child?.Dispose(); child = null;
        stdout = stderr = null;
    }
    public void Dispose() => Stop();
    static async Task<string> Drain(StreamReader reader)
    {
        var tail = new StringBuilder();
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                tail.AppendLine(line);
                if (tail.Length > 8192) tail.Remove(0, tail.Length - 8192);
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException) { }
        return tail.ToString();
    }
}
