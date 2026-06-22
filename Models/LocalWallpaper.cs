using System.IO;

namespace BingWallpaperWPF.Models;

public class LocalWallpaper
{
    public FileInfo File { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Copyright { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? MetadataPath { get; set; }
}
