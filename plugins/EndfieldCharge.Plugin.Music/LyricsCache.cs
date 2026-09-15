using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// LRCLIB 歌词磁盘缓存（<c>&lt;插件数据目录&gt;\lyrics</c>）的体积管理：统计 / 按 LRU 淘汰 / 清空。
/// <para>
/// <b>安全边界</b>：这个目录与用户的**手工本地歌词**共用（<see cref="LocalLrcProvider"/>
/// 在同一目录里按「艺术家 - 曲名.lrc」找文件），所以只有文件名匹配
/// <c>^[0-9A-F]{16}\.lrc$</c>（大小写不敏感，即 <c>SHA256(query.Key)</c> 前 16 位十六进制）的
/// 缓存文件才会被统计、淘汰或删除 —— 手写的 .lrc 一律不碰。
/// </para>
/// <para>
/// 「最久未用」= <see cref="File.GetLastWriteTimeUtc"/> 最小：命中缓存时
/// <see cref="LrclibProvider"/> 会回写该时间戳，所以淘汰顺序反映**最后使用**而不是首次写入。
/// </para>
/// </summary>
public static class LyricsCache
{
    /// <summary>缓存文件名（与 <c>LrclibProvider.CachePath</c> 的命名一致）。</summary>
    private static readonly Regex CacheName = new(
        "^[0-9A-F]{16}\\.lrc$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>上限下限（MB）：再小就没有缓存的意义。</summary>
    public const int MinimumLimitMegabytes = 5;

    /// <summary>上限上限（MB）：再大就该考虑清理而不是扩容。</summary>
    public const int MaximumLimitMegabytes = 200;

    /// <summary>默认上限（MB）。</summary>
    public const int DefaultLimitMegabytes = 20;

    /// <summary>默认上限（字节）：<see cref="LrclibProvider"/> 的构造参数默认值。</summary>
    public const long DefaultLimitBytes = DefaultLimitMegabytes * 1024L * 1024L;

    /// <summary>文件名是否是缓存文件（用户手工放的 .lrc 不匹配）。</summary>
    public static bool IsCacheFile(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && CacheName.IsMatch(fileName);

    /// <summary>把 MB 上限夹到 5–200（设置里的「已用值」一律经过这里）。</summary>
    public static int ClampLimitMegabytes(int megabytes) =>
        Math.Clamp(megabytes, MinimumLimitMegabytes, MaximumLimitMegabytes);

    /// <summary>把 MB 上限换算成字节（先夹到 5–200 再换算）。</summary>
    public static long ToBytes(int megabytes) => ClampLimitMegabytes(megabytes) * 1024L * 1024L;

    /// <summary>
    /// 统计缓存占用：**只计缓存文件**（用户歌词不计入上限）。目录不存在 / 不可读时返回 <c>(0, 0)</c>，不抛异常。
    /// </summary>
    public static (int Files, long Bytes) Measure(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return (0, 0);

        int files = 0;
        long bytes = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.lrc"))
            {
                if (!IsCacheFile(Path.GetFileName(path)))
                    continue;

                try
                {
                    bytes += new FileInfo(path).Length;
                    files++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 读取途中被其它进程拿走 / 权限变化：这一轮不算它，下次统计自然会修正
                    Logger.Warn($"Music: 歌词缓存条目统计失败（{path}）—— {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn($"Music: 歌词缓存统计失败 —— {ex.Message}");
        }

        return (files, bytes);
    }

    /// <summary>
    /// 按 LRU 淘汰：<see cref="File.GetLastWriteTimeUtc"/> 升序（最久未用在前）逐个删除，
    /// 直到缓存总量 ≤ <paramref name="limitBytes"/>。
    /// <para>
    /// 返回（删除的文件数, 释放的字节数）；目录不存在或已在上限内时返回 <c>(0, 0)</c>，不抛异常。
    /// 只删缓存文件：用户手工放的 .lrc 永远不参与统计与淘汰。
    /// </para>
    /// </summary>
    public static (int Deleted, long FreedBytes) Prune(string directory, long limitBytes)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return (0, 0);

        var entries = new List<(string Path, DateTime UsedUtc, long Bytes)>();
        long total = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.lrc"))
            {
                if (!IsCacheFile(Path.GetFileName(path)))
                    continue;

                try
                {
                    var info = new FileInfo(path);
                    entries.Add((path, info.LastWriteTimeUtc, info.Length));
                    total += info.Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.Warn($"Music: 歌词缓存条目读取失败（{path}）—— {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn($"Music: 歌词缓存淘汰失败 —— {ex.Message}");
            return (0, 0);
        }

        if (limitBytes < 0)
            limitBytes = 0;

        if (total <= limitBytes)
            return (0, 0);

        // 同刻写入的文件按路径兜底排序：淘汰结果可复现，不依赖枚举顺序
        entries.Sort(static (a, b) =>
        {
            int byTime = a.UsedUtc.CompareTo(b.UsedUtc);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.Path, b.Path);
        });

        return Delete(entries, ref total, limitBytes);
    }

    /// <summary>
    /// 清空缓存：只删缓存文件（用户歌词原样保留）。返回（删除的文件数, 释放的字节数）。
    /// </summary>
    public static (int Deleted, long FreedBytes) Clear(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return (0, 0);

        var entries = new List<(string Path, DateTime UsedUtc, long Bytes)>();
        long total = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.lrc"))
            {
                if (!IsCacheFile(Path.GetFileName(path)))
                    continue;

                try
                {
                    var info = new FileInfo(path);
                    entries.Add((path, info.LastWriteTimeUtc, info.Length));
                    total += info.Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.Warn($"Music: 歌词缓存条目读取失败（{path}）—— {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn($"Music: 歌词缓存清空失败 —— {ex.Message}");
            return (0, 0);
        }

        return Delete(entries, ref total, limitBytes: 0);
    }

    /// <summary>
    /// 逐个删除条目，直到总量 ≤ <paramref name="limitBytes"/>（0 = 全删）。
    /// 单个文件删不掉只记日志并继续，返回实际删掉的数量与字节数。
    /// </summary>
    private static (int Deleted, long FreedBytes) Delete(
        List<(string Path, DateTime UsedUtc, long Bytes)> entries, ref long total, long limitBytes)
    {
        int deleted = 0;
        long freed = 0;
        foreach (var entry in entries)
        {
            if (total <= limitBytes)
                break;

            try
            {
                File.Delete(entry.Path);
                total -= entry.Bytes;
                deleted++;
                freed += entry.Bytes;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn($"Music: 歌词缓存删除失败（{entry.Path}）—— {ex.Message}");
            }
        }

        return (deleted, freed);
    }

    /// <summary>把字节数格式化成人读的大小（<c>312 KB</c> / <c>4.2 MB</c>）。</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";

        double kb = bytes / 1024d;
        if (kb < 1024)
            return kb.ToString(kb >= 100 ? "F0" : "F1", CultureInfo.InvariantCulture) + " KB";

        double mb = kb / 1024d;
        return mb.ToString(mb >= 100 ? "F0" : "F1", CultureInfo.InvariantCulture) + " MB";
    }
}
