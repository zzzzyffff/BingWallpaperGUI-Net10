using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BingWallpaperWPF.Services;

public static class WallpaperService
{
    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    public static bool SetDesktopWallpaper(string imagePath)
    {
        if (!File.Exists(imagePath))
            return false;

        try
        {
            return SystemParametersInfoW(SPI_SETDESKWALLPAPER, 0, Path.GetFullPath(imagePath), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
        catch
        {
            return false;
        }
    }
}
