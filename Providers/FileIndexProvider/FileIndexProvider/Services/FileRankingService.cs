using FileIndexProvider.Models;

namespace FileIndexProvider.Services;

public sealed class FileRankingService
{
    private static readonly HashSet<string> BoostedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".lnk", ".pdf", ".docx", ".sln"
    };

    public IReadOnlyList<FileIndexRecord> Rank(string query, IReadOnlyList<FileIndexRecord> candidates, int maxResults)
    {
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return candidates.Take(maxResults).ToList();
        }

        var trimmedQuery = query.Trim();
        var scored = new List<(FileIndexRecord Record, int Score)>(candidates.Count);

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var record in candidates)
        {
            var score = CalculateScore(trimmedQuery, record, nowUnix);
            scored.Add((record, score));
        }

        // Hierarchical Ancestor Match Penalty:
        // If a candidate directory is a descendant of another matched candidate directory,
        // penalize the descendant heavily (-350 pts) so the higher-level parent directory wins cleanly.
        var matchedDirs = scored
            .Where(x => x.Record.IsDirectory && x.Score > 0)
            .Select(x => x.Record)
            .ToList();

        if (matchedDirs.Count > 1)
        {
            for (int i = 0; i < scored.Count; i++)
            {
                var current = scored[i];
                if (!current.Record.IsDirectory) continue;

                var currentPath = current.Record.FullPath;
                var hasMatchedAncestor = false;

                foreach (var parent in matchedDirs)
                {
                    if (ReferenceEquals(parent, current.Record)) continue;

                    var parentPathWithSlash = parent.FullPath.EndsWith('\\')
                        ? parent.FullPath
                        : parent.FullPath + "\\";

                    if (currentPath.StartsWith(parentPathWithSlash, StringComparison.OrdinalIgnoreCase))
                    {
                        hasMatchedAncestor = true;
                        break;
                    }
                }

                if (hasMatchedAncestor)
                {
                    scored[i] = (current.Record, current.Score - 350);
                }
            }
        }

        return scored
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Record.FileName.Length)
            .Take(maxResults)
            .Select(x => x.Record)
            .ToList();
    }

    public int CalculateScore(string query, FileIndexRecord record, long nowUnix)
    {
        int score = 0;
        var fileName = record.FileName;

        // For files, extract name without extension. For directories, dots are part of the name.
        string nameWithoutExt;
        if (!record.IsDirectory)
        {
            var dotIndex = fileName.LastIndexOf('.');
            nameWithoutExt = dotIndex > 0 ? fileName[..dotIndex] : fileName;
        }
        else
        {
            nameWithoutExt = fileName;
        }

        // Exact match
        var isExact = false;
        if (string.Equals(query, fileName, StringComparison.OrdinalIgnoreCase))
        {
            score += 700;
            isExact = true;
        }
        else if (!record.IsDirectory && string.Equals(query, nameWithoutExt, StringComparison.OrdinalIgnoreCase))
        {
            score += 650;
            isExact = true;
        }

        // Prefix match vs Word boundary vs Substring
        // (Do NOT stack prefix and word boundary to prevent compound names from outscoring exact matches)
        if (!isExact)
        {
            if (fileName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 400;
            }
            else if (HasWordBoundaryMatch(fileName, query))
            {
                score += 300;
            }
            else
            {
                var pos = fileName.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (pos >= 0)
                {
                    score += Math.Max(0, 150 - (2 * pos));
                }
            }
        }

        // Excess length penalty for directories:
        // e.g. searching 'windowssearch' (13) vs 'WindowsSearch.Benchmark.Cli' (27) -> penalize (27 - 13) * 5 = -70 pts
        if (record.IsDirectory && fileName.Length > query.Length)
        {
            score -= (fileName.Length - query.Length) * 5;
        }

        // Frecency bonus
        if (record.AccessCount > 0)
        {
            score += Math.Min(250, record.AccessCount * 30);
        }

        // Recency bonus (files modified within the last 30 days)
        if (record.LastWriteTimeUtc > 0)
        {
            var ageDays = Math.Max(0, (nowUnix - record.LastWriteTimeUtc) / 86400.0);
            if (ageDays < 30.0)
            {
                var recencyBonus = 150.0 * (1.0 - (ageDays / 30.0));
                score += (int)recencyBonus;
            }
        }

        // Depth penalty (prefer shallower paths, heavier penalty for directories)
        var depth = CountPathSeparators(record.DirectoryPath);
        if (record.IsDirectory)
        {
            score -= depth * 10;
        }
        else
        {
            score -= Math.Min(50, depth * 3);
        }

        // Boost common executables and project files
        if (!string.IsNullOrEmpty(record.Extension) && BoostedExtensions.Contains(record.Extension))
        {
            score += 30;
        }

        // Base boost for directories
        if (record.IsDirectory)
        {
            score += 35;
        }

        return score;
    }

    private static bool HasWordBoundaryMatch(string fileName, string query)
    {
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(query))
        {
            return false;
        }

        // Check delimiters _, -, ., space
        for (int i = 0; i < fileName.Length - 1; i++)
        {
            char c = fileName[i];
            if (c == '_' || c == '-' || c == '.' || c == ' ')
            {
                if (fileName.AsSpan(i + 1).StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            // CamelCase transition: lowercase followed by uppercase
            else if (char.IsLower(c) && char.IsUpper(fileName[i + 1]))
            {
                if (fileName.AsSpan(i + 1).StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountPathSeparators(string path)
    {
        if (string.IsNullOrEmpty(path)) return 0;
        int count = 0;
        foreach (char c in path)
        {
            if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
            {
                count++;
            }
        }
        return count;
    }
}
