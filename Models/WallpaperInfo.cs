using System.Text.Json.Serialization;

namespace BingWallpaperWPF.Models;

public class WallpaperInfo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("urlbase")]
    public string UrlBase { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("copyright")]
    public string Copyright { get; set; } = string.Empty;

    [JsonPropertyName("copyrightlink")]
    public string CopyrightLink { get; set; } = string.Empty;

    [JsonPropertyName("startdate")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("enddate")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("hsh")]
    public string Hash { get; set; } = string.Empty;
}
