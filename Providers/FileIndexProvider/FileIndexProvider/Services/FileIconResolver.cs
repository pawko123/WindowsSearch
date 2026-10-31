using System.IO.Abstractions;

namespace FileIndexProvider.Services;

public sealed class FileIconResolver
{
    private readonly IFileSystem _fileSystem;
    private readonly string _iconsDirectory;
    private static readonly Dictionary<string, string> ExtensionIconMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Code
        [".cs"] = "csharp.png",
        [".py"] = "python.png",
        [".pyw"] = "python.png",
        [".ts"] = "typescript.png",
        [".tsx"] = "typescript.png",
        [".js"] = "javascript.png",
        [".jsx"] = "javascript.png",
        [".mjs"] = "javascript.png",
        [".cpp"] = "cpp.png",
        [".c"] = "cpp.png",
        [".h"] = "cpp.png",
        [".hpp"] = "cpp.png",
        [".rs"] = "rust.png",
        [".go"] = "go.png",
        [".java"] = "java.png",
        [".jar"] = "java.png",
        [".html"] = "html.png",
        [".htm"] = "html.png",
        [".css"] = "css.png",
        [".scss"] = "css.png",
        [".json"] = "json.png",
        [".xml"] = "xml.png",
        [".xaml"] = "xml.png",
        [".csproj"] = "xml.png",
        [".props"] = "xml.png",
        [".targets"] = "xml.png",
        [".sln"] = "xml.png",
        [".slnx"] = "xml.png",
        [".yaml"] = "yaml.png",
        [".yml"] = "yaml.png",
        [".sql"] = "sql.png",
        [".sh"] = "terminal.png",
        [".bash"] = "terminal.png",
        [".ps1"] = "terminal.png",
        [".bat"] = "terminal.png",
        [".cmd"] = "terminal.png",
        [".md"] = "markdown.png",

        // Documents
        [".txt"] = "doc.png",
        [".doc"] = "doc.png",
        [".docx"] = "doc.png",
        [".rtf"] = "doc.png",
        [".odt"] = "doc.png",
        [".pdf"] = "pdf.png",

        // Spreadsheets & Presentations
        [".xls"] = "sheet.png",
        [".xlsx"] = "sheet.png",
        [".csv"] = "sheet.png",
        [".ppt"] = "presentation.png",
        [".pptx"] = "presentation.png",

        // Archives
        [".zip"] = "archive.png",
        [".rar"] = "archive.png",
        [".7z"] = "archive.png",
        [".tar"] = "archive.png",
        [".gz"] = "archive.png",
        [".bz2"] = "archive.png",

        // Media
        [".mp3"] = "audio.png",
        [".wav"] = "audio.png",
        [".flac"] = "audio.png",
        [".ogg"] = "audio.png",
        [".m4a"] = "audio.png",
        [".aac"] = "audio.png",
        [".mp4"] = "video.png",
        [".mkv"] = "video.png",
        [".avi"] = "video.png",
        [".mov"] = "video.png",
        [".wmv"] = "video.png",
        [".webm"] = "video.png",

        // Images
        [".png"] = "image.png",
        [".jpg"] = "image.png",
        [".jpeg"] = "image.png",
        [".gif"] = "image.png",
        [".bmp"] = "image.png",
        [".webp"] = "image.png",
        [".svg"] = "image.png",
        [".ico"] = "image.png"
    };

    public FileIconResolver(IFileSystem? fileSystem = null, string? iconsDirectory = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _iconsDirectory = iconsDirectory ?? _fileSystem.Path.Combine(AppContext.BaseDirectory, "Icons");
    }

    public string GetCategoryIconPath()
    {
        var categoryIconPath = _fileSystem.Path.Combine(_iconsDirectory, "category.png");
        return _fileSystem.File.Exists(categoryIconPath) ? categoryIconPath : "Icons/category.png";
    }

    public string ResolveDirectoryIcon()
    {
        var folderIconPath = _fileSystem.Path.Combine(_iconsDirectory, "folder.png");
        return _fileSystem.File.Exists(folderIconPath) ? folderIconPath : "Icons/folder.png";
    }

    public string ResolveIcon(string fullPath, string extension, bool isDirectory = false)
    {
        if (isDirectory)
        {
            return ResolveDirectoryIcon();
        }
        // For executables and shortcuts, return file path so Hub extracts live shell icon
        if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            if (_fileSystem.File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var iconFileName = ExtensionIconMap.TryGetValue(extension, out var mapped)
            ? mapped
            : "file.png";

        var fullIconPath = _fileSystem.Path.Combine(_iconsDirectory, iconFileName);
        return _fileSystem.File.Exists(fullIconPath) ? fullIconPath : iconFileName;
    }
}
