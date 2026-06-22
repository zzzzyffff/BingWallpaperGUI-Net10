using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BingWallpaperWPF.Models;

public class BingApiResponse
{
    [JsonPropertyName("images")]
    public List<WallpaperInfo> Images { get; set; } = new();
}
