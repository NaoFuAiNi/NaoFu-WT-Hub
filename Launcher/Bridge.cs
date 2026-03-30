using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.Web.WebView2.WinForms;

namespace NaoFu.WT.Font.Launcher;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class Bridge
{
    private readonly WebView2 _webView;
    private readonly Action<bool>? _onTitleBarTheme;
    private const string ToolVersion = "2.1.3";
    private static readonly string ExeName = "NaoFu WT Customize Font " + ToolVersion + ".exe";
    /// <summary>项目名最大长度（与 C 端一致）</summary>
    private const int ProjectNameMax = 64;
    /// <summary>作者名最大长度（与 C 端一致）</summary>
    private const int AuthorNameMax = 128;
    /// <summary>路径非法字符</summary>
    private static readonly char[] InvalidPathChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
    private const string ConfigFileName = "nf_config.json";
    private const int MaxFontHistory = 10;

    /// <summary>nf_config.json 对应结构</summary>
    private class NfConfig
    {
        public string? GamePath { get; set; }
        public string? LastFontPath { get; set; }
        public List<string>? LastFontPaths { get; set; }
        public string? Theme { get; set; }
    }

    private static string GetConfigPath() => Path.Combine(GetAppRoot(), ConfigFileName);

    private static NfConfig LoadConfig()
    {
        var path = GetConfigPath();
        if (!File.Exists(path)) return new NfConfig();
        try
        {
            var json = File.ReadAllText(path);
            var c = JsonSerializer.Deserialize<NfConfig>(json);
            return c ?? new NfConfig();
        }
        catch { return new NfConfig(); }
    }

    private static void SaveConfig(NfConfig c)
    {
        try
        {
            var path = GetConfigPath();
            var json = JsonSerializer.Serialize(c, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    /// <summary>已保存主题，true=暗色</summary>
    public static bool GetSavedTheme()
    {
        var theme = LoadConfig().Theme?.Trim();
        return string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>保存主题到 config</summary>
    public static void SaveTheme(bool isDark)
    {
        try
        {
            var c = LoadConfig();
            c.Theme = isDark ? "dark" : "light";
            SaveConfig(c);
        }
        catch { }
    }
    private const int MachineIdSize = 8;
    private static readonly byte[] BuiltinMagic = Encoding.ASCII.GetBytes("NAOFUBIN");
    private const string BuiltinFontsDir = "font/builtin";
    /// <summary>builtin 子目录名</summary>
    private const string BuiltinSubDir = "系统自带";
    /// <summary>导入字体子目录名</summary>
    private const string ImportedSubDir = "导入";
    private static readonly string[] BuiltinFontNames = { "爱点风雅黑", "奶酪体", "也子工厂" };
    private static readonly string BuiltinFontsSourceDir = "";

    public Bridge(WebView2 webView, Action<bool>? onTitleBarTheme = null)
    {
        _webView = webView;
        _onTitleBarTheme = onTitleBarTheme;
    }

    /// <summary>应用根目录，exe 在 bin/app 时取父目录</summary>
    public static string GetAppRoot()
    {
        var startup = Application.StartupPath ?? "";
        var name = Path.GetFileName(startup.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("app", StringComparison.OrdinalIgnoreCase))
            return Directory.GetParent(startup)?.FullName ?? startup;
        return startup;
    }

    /// <summary>设置标题栏主题 light/dark</summary>
    public void SetTitleBarTheme(string theme)
    {
        _onTitleBarTheme?.Invoke(string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>查找 C 程序与工作目录。工作目录必须包含 font\source\fonts.vromfs.bin，否则 C 程序会报无法打开。</summary>
    private (string? exePath, string? workDir) FindWorkDir()
    {
        string baseDir = Application.StartupPath ?? "";
        var dirsToTry = new[] { baseDir,
            Path.GetFullPath(Path.Combine(baseDir, "..")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..")) };
        foreach (var dir in dirsToTry)
        {
            var candidate = Path.Combine(dir, ExeName);
            if (File.Exists(candidate))
            {
                var exeDir = Path.GetDirectoryName(candidate) ?? dir;
                var exeDirName = Path.GetFileName(exeDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var workDir = (exeDirName.Equals("bin", StringComparison.OrdinalIgnoreCase) || exeDirName.Equals("tools", StringComparison.OrdinalIgnoreCase) || exeDirName.Equals("app", StringComparison.OrdinalIgnoreCase))
                    ? (Directory.GetParent(exeDir)?.FullName ?? exeDir)
                    : exeDir;
                workDir = ResolveWorkDirWithFontSource(workDir);
                return (candidate, workDir);
            }
            var candidateInTools = Path.Combine(dir, "tools", ExeName);
            if (File.Exists(candidateInTools))
            {
                var workDir = ResolveWorkDirWithFontSource(dir);
                return (candidateInTools, workDir);
            }
        }
        return (null, null);
    }

    /// <summary>若 workDir 下没有 font\source\fonts.vromfs.bin，则向上查找直到找到包含该文件的目录（避免从 Launcher 输出目录运行时工作目录错误）。</summary>
    private static string ResolveWorkDirWithFontSource(string workDir)
    {
        var required = Path.Combine(workDir, "font", "source", "fonts.vromfs.bin");
        if (File.Exists(required))
            return workDir;
        var dir = Directory.GetParent(workDir);
        while (dir != null)
        {
            var p = Path.Combine(dir.FullName, "font", "source", "fonts.vromfs.bin");
            if (File.Exists(p))
                return dir.FullName;
            dir = dir.Parent;
        }
        return workDir;
    }

    /// <summary>执行字体替换，mode 1/2/3，slotsJson 如 "[1,2,5]"</summary>
    public void RunFontReplace(string projectName, string authorName, int mode, string slotsJson)
    {
        var author = string.IsNullOrWhiteSpace(authorName) ? "这个用户很神秘" : authorName.Trim();
        _ = RunFontReplaceAsync(projectName, author, mode, slotsJson);
    }

    /// <summary>选字体对话框，复制到 font/custom，返回 JSON</summary>
    public string SelectFontAndCopy()
    {
        if (_webView.InvokeRequired)
            return (string)_webView.Invoke(new Func<string>(SelectFontAndCopy))!;
        return SelectFontAndCopyCore();
    }

    private string SelectFontAndCopyCore()
    {
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir))
            return JsonSerializer.Serialize(new { ok = false, message = "未找到 C 程序所在目录。" });

        string? initialDir = null;
        try
        {
            var lastPath = LoadConfig().LastFontPath?.Trim();
            if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
                initialDir = Path.GetDirectoryName(lastPath);
        }
        catch { }

        using var dlg = new OpenFileDialog
        {
            Title = "选择字体文件",
            Filter = "字体文件 (*.ttf;*.otf)|*.ttf;*.otf|所有文件 (*.*)|*.*",
            FilterIndex = 0
        };
        if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
            dlg.InitialDirectory = initialDir;
        if (dlg.ShowDialog() != DialogResult.OK)
            return JsonSerializer.Serialize(new { ok = false, message = "用户取消" });
        var selectedPath = dlg.FileName;
        if (string.IsNullOrEmpty(selectedPath))
            return JsonSerializer.Serialize(new { ok = false, message = "用户取消" });

        return CopyFontFromPath(selectedPath);
    }

    /// <summary>从路径复制字体到 font/custom，返回 JSON</summary>
    public string CopyFontFromPath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return JsonSerializer.Serialize(new { ok = false, message = "文件不存在。" });
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (ext != ".ttf" && ext != ".otf")
            return JsonSerializer.Serialize(new { ok = false, message = "仅支持 .ttf 或 .otf 字体文件。" });
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir))
            return JsonSerializer.Serialize(new { ok = false, message = "未找到 C 程序所在目录。" });
        try
        {
            var customFontDir = Path.Combine(workDir, "font", "custom");
            Directory.CreateDirectory(customFontDir);
            var destPath = Path.Combine(customFontDir, "MyFonts.ttf");
            File.Copy(sourcePath, destPath, overwrite: true);
            var c = LoadConfig();
            c.LastFontPath = sourcePath;
            AppendToFontHistory(c, sourcePath);
            SaveConfig(c);
            return JsonSerializer.Serialize(new { ok = true, message = "已复制到 font/custom/MyFonts.ttf", sourcePath = sourcePath });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, message = "复制失败: " + ex.Message });
        }
    }

    /// <summary>上次选择的字体路径</summary>
    public string GetLastFontPath()
    {
        try
        {
            var s = LoadConfig().LastFontPath?.Trim() ?? "";
            return !string.IsNullOrEmpty(s) && File.Exists(s) ? s : "";
        }
        catch { return ""; }
    }

    /// <summary>build 下含 fonts.vromfs.bin 的子目录名，JSON 数组</summary>
    public string GetBuildFolderNames()
    {
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir))
            return "[]";
        var buildDir = Path.Combine(workDir, "build");
        if (!Directory.Exists(buildDir))
            return "[]";
        var list = new List<string>();
        try
        {
            foreach (var dir in Directory.GetDirectories(buildDir))
            {
                var binPath = Path.Combine(dir, "fonts.vromfs.bin");
                if (File.Exists(binPath))
                    list.Add(Path.GetFileName(dir));
            }
        }
        catch { }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(list);
    }

    /// <summary>本机 machine_id 十六进制串（8 字节）</summary>
    public string GetMachineId()
    {
        var id = GetMachineIdBytes();
        if (id == null || id.Length == 0) return "";
        var sb = new StringBuilder(id.Length * 2);
        foreach (var b in id) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    private static byte[]? GetMachineIdBytes()
    {
        try
        {
            var list = new List<byte[]>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var pa = ni.GetPhysicalAddress();
                if (pa == null || pa.GetAddressBytes().Length < 6) continue;
                var mac = pa.GetAddressBytes();
                var six = new byte[6];
                Array.Copy(mac, 0, six, 0, Math.Min(6, mac.Length));
                list.Add(six);
            }
            if (list.Count == 0) return new byte[MachineIdSize];
            list.Sort((a, b) =>
            {
                for (int i = 0; i < 6; i++)
                {
                    if (a[i] != b[i]) return a[i].CompareTo(b[i]);
                }
                return 0;
            });
            var result = new byte[MachineIdSize];
            Array.Clear(result, 0, result.Length);
            Array.Copy(list[0], 0, result, 0, 6);
            return result;
        }
        catch { }
        return new byte[MachineIdSize];
    }

    /// <summary>首块网卡 MAC+2 零，兼容旧 C 的 machine_id</summary>
    private static byte[]? GetFirstMachineIdBytes()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var pa = ni.GetPhysicalAddress();
                if (pa == null || pa.GetAddressBytes().Length < 6) continue;
                var mac = pa.GetAddressBytes();
                var result = new byte[MachineIdSize];
                Array.Clear(result, 0, result.Length);
                Array.Copy(mac, 0, result, 0, Math.Min(6, mac.Length));
                return result;
            }
        }
        catch { }
        return new byte[MachineIdSize];
    }

    /// <summary>本机网卡 MAC+2 零去重，用于 self 识别</summary>
    private static List<byte[]> GetAllMachineIdCandidates()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<byte[]>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var pa = ni.GetPhysicalAddress();
                if (pa == null || pa.GetAddressBytes().Length < 6) continue;
                var mac = pa.GetAddressBytes();
                bool allZero = true;
                for (int i = 0; i < 6 && i < mac.Length; i++) { if (mac[i] != 0) { allZero = false; break; } }
                if (allZero) continue;
                var eight = new byte[MachineIdSize];
                Array.Clear(eight, 0, MachineIdSize);
                Array.Copy(mac, 0, eight, 0, Math.Min(6, mac.Length));
                var key = Convert.ToHexString(eight);
                if (seen.Add(key)) list.Add(eight);
            }
        }
        catch { }
        return list;
    }

    private static uint Crc32(byte[] data)
    {
        uint[] table = new uint[256];
        for (int n = 0; n < 256; n++)
        {
            uint c = (uint)n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320U ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        uint crc = 0xFFFFFFFFU;
        foreach (byte b in data)
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFU;
    }

    /// <summary>解析 DATA.NaoFu，返回 { projectName, author, type }</summary>
    public string ParseDataNaoFu(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return JsonSerializer.Serialize(new { projectName = "", author = "", type = "imported" });
        try
        {
            var raw = File.ReadAllBytes(filePath);
            if (raw.Length < 5 + 1 + 4 + 1 + 4) return JsonSerializer.Serialize(new { projectName = "", author = "", type = "imported" });
            if (raw[0] != 'N' || raw[1] != 'a' || raw[2] != 'o' || raw[3] != 'F' || raw[4] != 'u')
                return JsonSerializer.Serialize(new { projectName = "", author = "", type = "imported" });
            int ver = raw[5];
            int off = 5 + 1 + 4; // magic + ver + timestamp
            if (off >= raw.Length) return JsonSerializer.Serialize(new { projectName = "", author = "", type = "imported" });
            int toolLen = raw[off++];
            if (off + toolLen > raw.Length) return JsonSerializer.Serialize(new { projectName = "", author = "", type = "imported" });
            off += toolLen;
            if (off >= raw.Length) return JsonSerializer.Serialize(new { projectName = "", author = "", type = "imported" });
            int projLen = raw[off++];
            if (off + projLen > raw.Length) return JsonSerializer.Serialize(new { projectName = "", author = "", type = "imported" });
            string projectName = Encoding.UTF8.GetString(raw, off, projLen);
            off += projLen;
            if (off >= raw.Length) return JsonSerializer.Serialize(new { projectName, author = "", type = "imported" });
            int authorLen = raw[off++];
            if (off + authorLen > raw.Length) return JsonSerializer.Serialize(new { projectName, author = "", type = "imported" });
            string author = Encoding.UTF8.GetString(raw, off, authorLen);
            off += authorLen;
            string type = "imported";
            if (ver >= 2 && off + MachineIdSize + 4 <= raw.Length)
            {
                var mid = new byte[MachineIdSize];
                Array.Copy(raw, off, mid, 0, MachineIdSize);
                off += MachineIdSize;
                var payload = new byte[off];
                Array.Copy(raw, 0, payload, 0, off);
                uint computed = Crc32(payload);
                uint stored = (uint)(raw[off] | (raw[off + 1] << 8) | (raw[off + 2] << 16) | (raw[off + 3] << 24));
                if (computed == stored)
                {
                    if (mid.SequenceEqual(BuiltinMagic)) type = "builtin";
                    else if (mid.All(b => b == 0)) type = "imported";
                    else
                    {
                        foreach (var candidate in GetAllMachineIdCandidates())
                        {
                            if (candidate != null && candidate.Length == MachineIdSize && mid.SequenceEqual(candidate))
                            {
                                type = "self";
                                break;
                            }
                        }
                    }
                }
            }
            return JsonSerializer.Serialize(new { projectName, author, type });
        }
        catch { return JsonSerializer.Serialize(new { projectName = "", author = "", type = "imported" }); }
    }

    /// <summary>确保 builtin 目录存在并复制三个内置 bin</summary>
    private void EnsureBuiltinFonts(string workDir)
    {
        if (string.IsNullOrEmpty(workDir)) return;
        var builtinDir = Path.Combine(workDir, BuiltinFontsDir);
        var systemBuiltinDir = Path.Combine(builtinDir, BuiltinSubDir);
        var importedDir = Path.Combine(builtinDir, ImportedSubDir);
        try
        {
            Directory.CreateDirectory(builtinDir);
            Directory.CreateDirectory(systemBuiltinDir);
            Directory.CreateDirectory(importedDir);
            foreach (var name in BuiltinFontNames)
            {
                var destDir = Path.Combine(systemBuiltinDir, name);
                var destBin = Path.Combine(destDir, "fonts.vromfs.bin");
                var dataPath = Path.Combine(destDir, "DATA.NaoFu");
                Directory.CreateDirectory(destDir);
                if (!File.Exists(destBin) && Directory.Exists(BuiltinFontsSourceDir))
                {
                    var srcBin = Path.Combine(BuiltinFontsSourceDir, name + ".bin");
                    if (File.Exists(srcBin))
                    {
                        try { File.Copy(srcBin, destBin, false); } catch { }
                    }
                }
                if (!File.Exists(dataPath))
                    WriteDataNaoFu(destDir, name, "NaoFu", BuiltinMagic);
            }
        }
        catch { }
    }

    /// <summary>列举字体列表，type=builtin|imported|self，返回 JSON 数组</summary>
    public string GetBuildFolderInfo()
    {
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir)) return "[]";
        EnsureBuiltinFonts(workDir);
        var list = new List<object>();
        try
        {
            var baseDirs = new[] {
                (Path.Combine(workDir, BuiltinFontsDir, BuiltinSubDir), "builtin"),
                (Path.Combine(workDir, BuiltinFontsDir, ImportedSubDir), "imported"),
                (Path.Combine(workDir, "build"), "self")
            };
            foreach (var (baseDir, type) in baseDirs)
            {
                if (!Directory.Exists(baseDir)) continue;
                var dirs = Directory.GetDirectories(baseDir).OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var dir in dirs)
                {
                    var binPath = Path.Combine(dir, "fonts.vromfs.bin");
                    if (!File.Exists(binPath)) continue;
                    var name = Path.GetFileName(dir) ?? "";
                    var dataPath = Path.Combine(dir, "DATA.NaoFu");
                    string projectName = "", author = "";
                    if (File.Exists(dataPath))
                    {
                        var json = ParseDataNaoFu(dataPath);
                        var obj = JsonSerializer.Deserialize<JsonElement>(json);
                        projectName = obj.TryGetProperty("projectName", out var p) ? p.GetString() ?? "" : "";
                        author = obj.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "";
                    }
                    list.Add(new { name, projectName, author, type });
                }
            }
            list = list.OrderBy(x => (string)(JsonSerializer.SerializeToElement(x).GetProperty("name").GetString() ?? ""), StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch { }
        return JsonSerializer.Serialize(list);
    }

    /// <summary>导入单个 .bin 到 font/builtin/导入，需字体名，返回 { ok, message }</summary>
    public string ImportFontFromBin(string binPath, string fontName)
    {
        if (string.IsNullOrWhiteSpace(binPath) || !File.Exists(binPath))
            return JsonSerializer.Serialize(new { ok = false, message = "文件不存在。" });
        var ext = Path.GetExtension(binPath).ToLowerInvariant();
        if (ext != ".bin")
            return JsonSerializer.Serialize(new { ok = false, message = "请选择 .bin 文件。" });
        var name = (fontName ?? "").Trim();
        if (!ValidateFolderName(name, ProjectNameMax, out var errFontName))
            return JsonSerializer.Serialize(new { ok = false, message = errFontName });
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir))
            return JsonSerializer.Serialize(new { ok = false, message = "未找到工作目录。" });
        var importedDir = Path.Combine(workDir, BuiltinFontsDir, ImportedSubDir);
        var destDir = Path.Combine(importedDir, name);
        try
        {
            Directory.CreateDirectory(importedDir);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
            Directory.CreateDirectory(destDir);
            var destBin = Path.Combine(destDir, "fonts.vromfs.bin");
            File.Copy(binPath, destBin, true);
            WriteDataNaoFu(destDir, name, "这个人很神秘", new byte[MachineIdSize]);
            return JsonSerializer.Serialize(new { ok = true, message = "已导入到 font/builtin/导入/" + name + "。" });
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { ok = false, message = ex.Message }); }
    }

    /// <summary>导入含 bin+DATA.NaoFu 的文件夹到 font/builtin/导入</summary>
    public string ImportFontFromFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return JsonSerializer.Serialize(new { ok = false, message = "文件夹不存在。" });
        var binPath = Path.Combine(folderPath, "fonts.vromfs.bin");
        var dataPath = Path.Combine(folderPath, "DATA.NaoFu");
        if (!File.Exists(binPath))
            return JsonSerializer.Serialize(new { ok = false, message = "该文件夹内缺少 fonts.vromfs.bin。" });
        if (!File.Exists(dataPath))
            return JsonSerializer.Serialize(new { ok = false, message = "该文件夹内缺少 DATA.NaoFu。" });
        var name = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "ImportedFont";
        if (string.IsNullOrEmpty(name)) name = "ImportedFont";
        if (!ValidateFolderName(name, ProjectNameMax, out var errFolderName))
            return JsonSerializer.Serialize(new { ok = false, message = errFolderName });
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir))
            return JsonSerializer.Serialize(new { ok = false, message = "未找到工作目录。" });
        var importedDir = Path.Combine(workDir, BuiltinFontsDir, ImportedSubDir);
        var destDir = Path.Combine(importedDir, name);
        var fullDest = Path.GetFullPath(destDir);
        var fullImported = Path.GetFullPath(importedDir);
        if (!fullDest.StartsWith(fullImported, StringComparison.OrdinalIgnoreCase))
            return JsonSerializer.Serialize(new { ok = false, message = "目标路径非法。" });
        try
        {
            Directory.CreateDirectory(importedDir);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
            CopyDirectory(folderPath, destDir);
            return JsonSerializer.Serialize(new { ok = true, message = "已导入到 font/builtin/导入/" + name + "。" });
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { ok = false, message = ex.Message }); }
    }

    /// <summary>选 .bin 文件对话框，返回 { ok, path? }</summary>
    public string ShowOpenBinDialog()
    {
        if (_webView.InvokeRequired)
            return (string)_webView.Invoke(new Func<string>(ShowOpenBinDialog))!;
        using var dlg = new OpenFileDialog
        {
            Title = "选择 .bin 文件",
            Filter = "Vromfs 字体包 (*.bin)|*.bin|所有文件 (*.*)|*.*",
            FilterIndex = 0
        };
        return dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(dlg.FileName)
            ? JsonSerializer.Serialize(new { ok = true, path = dlg.FileName })
            : JsonSerializer.Serialize(new { ok = false });
    }

    /// <summary>选文件夹对话框，返回 { ok, path? }</summary>
    public string ShowOpenFolderDialog()
    {
        if (_webView.InvokeRequired)
            return (string)_webView.Invoke(new Func<string>(ShowOpenFolderDialog))!;
        using var dlg = new FolderBrowserDialog
        {
            Description = "选择包含三件套（fonts.vromfs.bin、.ttf、DATA.NaoFu）的文件夹"
        };
        return dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedPath)
            ? JsonSerializer.Serialize(new { ok = true, path = dlg.SelectedPath })
            : JsonSerializer.Serialize(new { ok = false });
    }

    /// <summary>嗅探游戏安装路径，返回 { ok, path?, message }</summary>
    public string SearchWarThunderPath()
    {
        var candidates = new List<string>();
        try
        {
            var uninstallPaths = new[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var subPath in uninstallPaths)
                {
                    try
                    {
                        using var uninstall = root.OpenSubKey(subPath);
                        if (uninstall == null) continue;
                        foreach (var name in uninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using var key = uninstall.OpenSubKey(name);
                            if (key?.GetValue("DisplayName") is string dn && (dn.Contains("War Thunder", StringComparison.OrdinalIgnoreCase) || dn.Contains("战争雷霆")))
                            {
                                if (key.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
                                    candidates.Add(loc.Trim().Trim('"'));
                            }
                        }
                        catch { }
                    }
                    }
                    catch { }
                }
            }
            var drives = Environment.GetLogicalDrives();
            var common = new List<string>();
            foreach (var d in drives)
            {
                common.Add(Path.Combine(d, "Program Files (x86)", "Steam", "steamapps", "common", "War Thunder"));
                common.Add(Path.Combine(d, "Program Files", "Steam", "steamapps", "common", "War Thunder"));
                common.Add(Path.Combine(d, "Steam", "steamapps", "common", "War Thunder"));
                common.Add(Path.Combine(d, "SteamLibrary", "steamapps", "common", "War Thunder"));
                common.Add(Path.Combine(d, "Program Files (x86)", "War Thunder"));
                common.Add(Path.Combine(d, "Program Files", "War Thunder"));
                common.Add(Path.Combine(d, "Games", "War Thunder"));
                common.Add(Path.Combine(d, "Epic Games", "War Thunder"));
            }
            foreach (var p in common)
            {
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) candidates.Add(p);
            }
            foreach (var steam in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam") })
            {
                if (string.IsNullOrEmpty(steam) || !Directory.Exists(steam)) continue;
                var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    try
                    {
                        var text = File.ReadAllText(vdf);
                        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*""path""\s+""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (m.Success)
                            {
                                var lib = m.Groups[1].Value.Replace(@"\\", @"\").Trim();
                                var wt = Path.Combine(lib, "steamapps", "common", "War Thunder");
                                if (Directory.Exists(wt)) candidates.Add(wt);
                            }
                        }
                    }
                    catch { }
                }
            }
            foreach (var p in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var uiDir = Path.Combine(p, "ui");
                if (Directory.Exists(uiDir))
                    return JsonSerializer.Serialize(new { ok = true, path = p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), message = "已找到游戏路径。" });
            }
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { ok = false, message = ex.Message }); }
        return JsonSerializer.Serialize(new { ok = false, message = "未找到战争雷霆安装路径，请手动输入游戏根目录。" });
    }

    /// <summary>已保存的游戏根目录</summary>
    public string GetSavedGamePath()
    {
        try
        {
            var s = LoadConfig().GamePath?.Trim().Trim('"') ?? "";
            return string.IsNullOrEmpty(s) ? "" : s;
        }
        catch { return ""; }
    }

    /// <summary>保存游戏根目录到 config</summary>
    public void SetSavedGamePath(string gamePath)
    {
        try
        {
            var c = LoadConfig();
            c.GamePath = string.IsNullOrWhiteSpace(gamePath) ? "" : (gamePath ?? "").Trim().Trim('"');
            SaveConfig(c);
        }
        catch { }
    }

    /// <summary>将字体 bin 复制到游戏 ui 并替换，原文件备份 .bak</summary>
    public string ImportFontToGame(string baseType, string fontName, string gameRootPath)
    {
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir))
            return JsonSerializer.Serialize(new { ok = false, message = "未找到工作目录。" });
        var fontDir = "";
        if (baseType == "builtin") fontDir = Path.Combine(workDir, BuiltinFontsDir, BuiltinSubDir, fontName ?? "");
        else if (baseType == "imported") fontDir = Path.Combine(workDir, BuiltinFontsDir, ImportedSubDir, fontName ?? "");
        else if (baseType == "self") fontDir = Path.Combine(workDir, "build", fontName ?? "");
        else return JsonSerializer.Serialize(new { ok = false, message = "无效的字体类型。" });
        var srcBin = Path.Combine(fontDir, "fonts.vromfs.bin");
        if (!File.Exists(srcBin))
            return JsonSerializer.Serialize(new { ok = false, message = "字体文件夹内缺少 fonts.vromfs.bin。" });
        var root = (gameRootPath ?? "").Trim().Trim('"');
        if (string.IsNullOrEmpty(root)) root = GetSavedGamePath();
        if (string.IsNullOrEmpty(root))
            return JsonSerializer.Serialize(new { ok = false, message = "请先输入或搜索游戏根目录。" });
        var uiDir = Path.Combine(root, "ui");
        var destBin = Path.Combine(uiDir, "fonts.vromfs.bin");
        if (!Directory.Exists(uiDir))
            return JsonSerializer.Serialize(new { ok = false, message = "游戏根目录下未找到 ui 文件夹，请确认路径是否正确。" });
        try
        {
            if (File.Exists(destBin))
            {
                var bak = destBin + ".bak";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(destBin, bak);
            }
            File.Copy(srcBin, destBin, true);
            SetSavedGamePath(root);
            return JsonSerializer.Serialize(new { ok = true, message = "已导入到游戏 UI 文件夹，可启动战雷。" });
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { ok = false, message = ex.Message }); }
    }

    /// <summary>用 font/source 下的默认 fonts.vromfs.bin 覆盖游戏 ui，恢复游戏默认字体</summary>
    public string RestoreDefaultFontToGame(string gameRootPath)
    {
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir))
            return JsonSerializer.Serialize(new { ok = false, message = "未找到工作目录。" });
        var srcBin = Path.Combine(workDir, "font", "source", "fonts.vromfs.bin");
        if (!File.Exists(srcBin))
            return JsonSerializer.Serialize(new { ok = false, message = "未找到默认字体文件 font/source/fonts.vromfs.bin。" });
        var root = (gameRootPath ?? "").Trim().Trim('"');
        if (string.IsNullOrEmpty(root)) root = GetSavedGamePath();
        if (string.IsNullOrEmpty(root))
            return JsonSerializer.Serialize(new { ok = false, message = "请先输入或搜索游戏根目录。" });
        var uiDir = Path.Combine(root, "ui");
        var destBin = Path.Combine(uiDir, "fonts.vromfs.bin");
        if (!Directory.Exists(uiDir))
            return JsonSerializer.Serialize(new { ok = false, message = "游戏根目录下未找到 ui 文件夹，请确认路径是否正确。" });
        try
        {
            if (File.Exists(destBin))
            {
                var bak = destBin + ".bak";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(destBin, bak);
            }
            File.Copy(srcBin, destBin, true);
            SetSavedGamePath(root);
            return JsonSerializer.Serialize(new { ok = true, message = "已恢复游戏默认字体。" });
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { ok = false, message = ex.Message }); }
    }

    /// <summary>打开 build 下某项目文件夹；projectName 为空时打开 build 根目录</summary>
    public string OpenBuildFolder(string projectName)
    {
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir))
            return JsonSerializer.Serialize(new { ok = false, message = "未找到工作目录。" });
        var name = (projectName ?? "").Trim();
        string dir;
        if (string.IsNullOrEmpty(name))
        {
            dir = Path.Combine(workDir, "build");
            if (!Directory.Exists(dir))
                return JsonSerializer.Serialize(new { ok = false, message = "build 目录不存在或为空。" });
        }
        else
        {
            foreach (char c in name)
                if (@"\/:*?""<>|".Contains(c))
                    return JsonSerializer.Serialize(new { ok = false, message = "项目名不能包含路径字符。" });
            dir = Path.Combine(workDir, "build", name);
            if (!Directory.Exists(dir))
                return JsonSerializer.Serialize(new { ok = false, message = "该生成目录不存在。" });
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { dir },
                UseShellExecute = true
            });
            return JsonSerializer.Serialize(new { ok = true, message = "已打开目录。" });
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { ok = false, message = ex.Message }); }
    }

    /// <summary>删除导入字体或自制字体，baseType=imported|self，返回 { ok, message }</summary>
    public string DeleteFont(string baseType, string fontName)
    {
        if (string.IsNullOrWhiteSpace(baseType) || (baseType != "imported" && baseType != "self"))
            return JsonSerializer.Serialize(new { ok = false, message = "只能删除导入字体或自制字体。" });
        var name = (fontName ?? "").Trim();
        if (!ValidateFolderName(name, ProjectNameMax, out var err))
            return JsonSerializer.Serialize(new { ok = false, message = err });
        var (_, workDir) = FindWorkDir();
        if (string.IsNullOrEmpty(workDir))
            return JsonSerializer.Serialize(new { ok = false, message = "未找到工作目录。" });
        string dir = baseType == "imported"
            ? Path.Combine(workDir, BuiltinFontsDir, ImportedSubDir, name)
            : Path.Combine(workDir, "build", name);
        if (!Directory.Exists(dir))
            return JsonSerializer.Serialize(new { ok = false, message = "该字体目录不存在。" });
        try
        {
            Directory.Delete(dir, true);
            return JsonSerializer.Serialize(new { ok = true, message = "已删除。" });
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { ok = false, message = ex.Message }); }
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src))
            CopyDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
    }

    private static void WriteDataNaoFu(string dir, string projectName, string author, byte[] machineId)
    {
        var magic = Encoding.ASCII.GetBytes("NaoFu");
        var list = new List<byte>();
        list.AddRange(magic);
        list.Add(2); // version 2, 与 nf_version.h DATA_NAOFU_VER 一致
        uint ts = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        list.Add((byte)(ts & 0xFF));
        list.Add((byte)((ts >> 8) & 0xFF));
        list.Add((byte)((ts >> 16) & 0xFF));
        list.Add((byte)((ts >> 24) & 0xFF));
        var tv = Encoding.UTF8.GetBytes(ToolVersion);
        list.Add((byte)Math.Min(255, tv.Length));
        list.AddRange(tv);
        var pn = Encoding.UTF8.GetBytes(projectName ?? "");
        list.Add((byte)Math.Min(255, pn.Length));
        list.AddRange(pn);
        var au = Encoding.UTF8.GetBytes(author ?? "");
        list.Add((byte)Math.Min(255, au.Length));
        list.AddRange(au);
        list.AddRange(machineId?.Length == MachineIdSize ? machineId : new byte[MachineIdSize]);
        var payload = list.ToArray();
        uint crc = Crc32(payload);
        list.Add((byte)(crc & 0xFF));
        list.Add((byte)((crc >> 8) & 0xFF));
        list.Add((byte)((crc >> 16) & 0xFF));
        list.Add((byte)((crc >> 24) & 0xFF));
        File.WriteAllBytes(Path.Combine(dir, "DATA.NaoFu"), list.ToArray());
    }

    /// <summary>最近使用的字体路径列表，最多 10 条</summary>
    public string GetLastFontPaths()
    {
        try
        {
            var list = LoadConfig().LastFontPaths ?? new List<string>();
            var valid = list.Where(s => !string.IsNullOrWhiteSpace(s) && File.Exists(s!)).Distinct().Take(MaxFontHistory).ToList();
            if (valid.Count == 0)
            {
                var single = GetLastFontPath();
                return string.IsNullOrEmpty(single) ? "[]" : JsonSerializer.Serialize(new[] { single });
            }
            return JsonSerializer.Serialize(valid);
        }
        catch { return "[]"; }
    }

    private static void AppendToFontHistory(NfConfig c, string newPath)
    {
        var list = c.LastFontPaths ?? new List<string>();
        list.RemoveAll(s => string.Equals(s, newPath, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, newPath);
        c.LastFontPaths = list.Take(MaxFontHistory).ToList();
    }

    private async Task RunFontReplaceAsync(string projectName, string authorName, int mode, string slotsJson)
    {
        var (exePath, workDir) = FindWorkDir();
        if (exePath == null || workDir == null)
        {
            await NotifyDone(false, "未找到 " + ExeName + "，请将启动器与 C 程序放在同一目录。");
            return;
        }
        var exeDir = Path.GetDirectoryName(exePath);
        var subsetToolPath = !string.IsNullOrEmpty(exeDir) ? Path.Combine(exeDir, "nf_subset_tool.exe") : "";
        if (string.IsNullOrEmpty(subsetToolPath) || !File.Exists(subsetToolPath))
        {
            await NotifyDone(false, "未找到 nf_subset_tool.exe（应与 " + ExeName + " 在同一目录）。请先完整编译：debug 下需有 nf_subset_tool.exe，再重新编译 Launcher。");
            return;
        }

        var pn = (projectName ?? "").Trim();
        if (!ValidateProjectName(pn, ProjectNameMax, out var errProject))
        {
            await NotifyDone(false, errProject!);
            return;
        }
        var au = (authorName ?? "").Trim();
        if (string.IsNullOrEmpty(au)) au = "这个用户很神秘";
        else if (!ValidateAuthorName(au, AuthorNameMax, out var errAuthor))
        {
            await NotifyDone(false, errAuthor!);
            return;
        }

        var pnArg = pn;
        if (pn.Contains(' ') || pn.Contains('"'))
            pnArg = "\"" + pn.Replace("\"", "\\\"") + "\"";
        var auArg = au;
        if (au.Contains(' ') || au.Contains('"'))
            auArg = "\"" + au.Replace("\"", "\\\"") + "\"";
        var args = new List<string> { "--project", pnArg, "--author", auArg, "--mode", mode.ToString() };
        if (mode == 2 || mode == 3)
        {
            try
            {
                var slots = JsonSerializer.Deserialize<int[]>(slotsJson ?? "[]") ?? Array.Empty<int>();
                if (mode == 2 && slots.Length > 0)
                    args.AddRange(new[] { "--slots", string.Join(",", slots) });
                else if (mode == 3 && slots.Length > 0)
                    args.AddRange(new[] { "--slot", slots[0].ToString() });
            }
            catch
            {
                await NotifyDone(false, "slots 格式错误，应为 JSON 数组如 [1,2,5]。");
                return;
            }
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Join(" ", args),
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        try
        {
            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
            {
                await NotifyDone(false, "无法启动进程。");
                return;
            }
            /* 向 stdin 写入 y\n，避免 C 端「是否瘦身/是否继续」等 scanf 一直阻塞 */
            try
            {
                await process.StandardInput.WriteAsync("y\ny\ny\ny\ny\n");
                process.StandardInput.Close();
            }
            catch { }

            var lastLines = new List<string>(8);
            var lastErrLines = new List<string>(8);
            var outTask = ReadLinesAsync(process.StandardOutput, line =>
            {
                lastLines.Add(line);
                if (lastLines.Count > 8) lastLines.RemoveAt(0);
                _ = _webView.CoreWebView2?.ExecuteScriptAsync(
                    "window.onNfProgress && window.onNfProgress(" + ToJsString(line) + ");");
            });
            var errTask = ReadLinesAsync(process.StandardError, line =>
            {
                lastErrLines.Add(line);
                if (lastErrLines.Count > 8) lastErrLines.RemoveAt(0);
            });
            await process.WaitForExitAsync();
            await Task.WhenAll(outTask, errTask);

            var success = process.ExitCode == 0;
            string msg;
            if (success)
                msg = lastLines.Count > 0 ? lastLines[lastLines.Count - 1] : "全部完成。";
            else
            {
                var tailOut = lastLines.Count > 0 ? string.Join(" ", lastLines.TakeLast(5)) : "";
                var tailErr = lastErrLines.Count > 0 ? string.Join(" ", lastErrLines.TakeLast(5)) : "";
                var tail = string.IsNullOrEmpty(tailErr) ? tailOut : (string.IsNullOrEmpty(tailOut) ? tailErr : tailOut + " " + tailErr);
                msg = string.IsNullOrEmpty(tail) ? ("退出码 " + process.ExitCode) : (tail + " · 退出码 " + process.ExitCode);
                /* 瘦身失败时优先显示 C 端写入的日志（Python/fonttools 真实报错），先查 exe 所在目录再查 workDir */
                string? logContent = null;
                string? logPathFound = null;
                foreach (var dir in new[] { exeDir, workDir })
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    var logPath = Path.Combine(dir, "nf_subset_tool_log.txt");
                    if (!File.Exists(logPath)) continue;
                    logPathFound = logPath;
                    try
                    {
                        logContent = File.ReadAllText(logPath, System.Text.Encoding.UTF8).Trim();
                        if (logContent.Length > 0) break;
                    }
                    catch { }
                }
                if (!string.IsNullOrEmpty(logContent))
                    msg = logContent + "\n" + msg;
                else if (!string.IsNullOrEmpty(logPathFound))
                    msg = msg + "\n详细错误请查看: " + logPathFound;
                else if (!string.IsNullOrEmpty(exeDir))
                    msg = msg + "\n详细错误（若有）: " + Path.Combine(exeDir, "nf_subset_tool_log.txt");
            }
            await NotifyDone(success, msg);
        }
        catch (Exception ex)
        {
            await NotifyDone(false, ex.Message);
        }
    }

    private static async Task ReadLinesAsync(StreamReader reader, Action<string> onLine)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
            onLine(line);
    }

    private async Task NotifyDone(bool success, string message)
    {
        try
        {
            await (_webView.CoreWebView2?.ExecuteScriptAsync(
                "window.onNfDone && window.onNfDone(" + (success ? "true" : "false") + "," + ToJsString(message) + ");") ?? Task.CompletedTask);
        }
        catch { }
    }

    private static string ToJsString(string? s)
    {
        if (s == null) return "null";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\"";
    }

    /// <summary>校验项目名（与 C 端一致）</summary>
    private static bool ValidateProjectName(string? name, int maxLen, out string? error)
    {
        error = null;
        var s = (name ?? "").Trim();
        if (string.IsNullOrEmpty(s)) { error = "项目名不能为空。"; return false; }
        if (s.Length > maxLen) { error = "项目名过长（最多 " + maxLen + " 个字符）。"; return false; }
        foreach (var c in InvalidPathChars)
            if (s.Contains(c)) { error = "项目名不能包含 \\ / : * ? \" < > | 。"; return false; }
        return true;
    }

    /// <summary>校验作者名</summary>
    private static bool ValidateAuthorName(string? name, int maxLen, out string? error)
    {
        error = null;
        var s = (name ?? "").Trim();
        if (s.Length > maxLen) { error = "作者名过长（最多 " + maxLen + " 个字符）。"; return false; }
        foreach (var c in InvalidPathChars)
            if (s.Contains(c)) { error = "作者名不能包含 \\ / : * ? \" < > | 。"; return false; }
        return true;
    }

    /// <summary>校验字体/文件夹名（导入用）</summary>
    private static bool ValidateFolderName(string? name, int maxLen, out string? error)
    {
        error = null;
        var s = (name ?? "").Trim();
        if (string.IsNullOrEmpty(s)) { error = "字体名称不能为空。"; return false; }
        if (s.Length > maxLen) { error = "字体名称过长（最多 " + maxLen + " 个字符）。"; return false; }
        foreach (var c in InvalidPathChars)
            if (s.Contains(c)) { error = "字体名称不能包含 \\ / : * ? \" < > | 。"; return false; }
        return true;
    }
}
