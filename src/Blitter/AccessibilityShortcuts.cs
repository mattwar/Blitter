using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Blitter;

/// <summary>
/// Windows-only helper that temporarily disables the accessibility
/// shortcut hotkeys (Sticky Keys via Shift×5, Filter Keys via
/// right-Shift hold, Toggle Keys via Num Lock hold) so they don't
/// hijack input during gameplay. Save the original state once via
/// <see cref="Disable"/> and restore it via <see cref="Restore"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowsAccessibility
{
    // Win32 SystemParametersInfo actions.
    private const uint SPI_GETSTICKYKEYS = 0x003A;
    private const uint SPI_SETSTICKYKEYS = 0x003B;
    private const uint SPI_GETFILTERKEYS = 0x0032;
    private const uint SPI_SETFILTERKEYS = 0x0033;
    private const uint SPI_GETTOGGLEKEYS = 0x0034;
    private const uint SPI_SETTOGGLEKEYS = 0x0035;

    // STICKYKEYS / FILTERKEYS / TOGGLEKEYS flags. Only the ones we
    // touch are named; see MSDN for the full set.
    private const uint SKF_STICKYKEYSON   = 0x00000001;
    private const uint SKF_HOTKEYACTIVE   = 0x00000004;
    private const uint SKF_CONFIRMHOTKEY  = 0x00000008;

    private const uint FKF_FILTERKEYSON   = 0x00000001;
    private const uint FKF_HOTKEYACTIVE   = 0x00000004;
    private const uint FKF_CONFIRMHOTKEY  = 0x00000008;

    private const uint TKF_TOGGLEKEYSON   = 0x00000001;
    private const uint TKF_HOTKEYACTIVE   = 0x00000004;
    private const uint TKF_CONFIRMHOTKEY  = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    private struct STICKYKEYS { public uint cbSize; public uint dwFlags; }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILTERKEYS
    {
        public uint cbSize;
        public uint dwFlags;
        public uint iWaitMSec;
        public uint iDelayMSec;
        public uint iRepeatMSec;
        public uint iBounceMSec;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOGGLEKEYS { public uint cbSize; public uint dwFlags; }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoSticky(uint uiAction, uint uiParam, ref STICKYKEYS pvParam, uint fWinIni);

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoFilter(uint uiAction, uint uiParam, ref FILTERKEYS pvParam, uint fWinIni);

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoToggle(uint uiAction, uint uiParam, ref TOGGLEKEYS pvParam, uint fWinIni);

    // Saved baseline so Restore can put things back exactly as they were.
    private static STICKYKEYS _savedSticky;
    private static FILTERKEYS _savedFilter;
    private static TOGGLEKEYS _savedToggle;
    private static bool _saved;

    /// <summary>
    /// Captures the current accessibility shortcut state and clears
    /// the hotkey-activation flags so Shift×5 / right-Shift-hold /
    /// Num-Lock-hold no longer pop the system dialog. If a user
    /// already has Sticky/Filter/Toggle Keys turned ON we leave it
    /// alone — only the hotkey trigger is suppressed.
    /// </summary>
    public static void Disable()
    {
        if (_saved) return;

        _savedSticky = new STICKYKEYS { cbSize = (uint)Marshal.SizeOf<STICKYKEYS>() };
        SystemParametersInfoSticky(SPI_GETSTICKYKEYS, _savedSticky.cbSize, ref _savedSticky, 0);

        _savedFilter = new FILTERKEYS { cbSize = (uint)Marshal.SizeOf<FILTERKEYS>() };
        SystemParametersInfoFilter(SPI_GETFILTERKEYS, _savedFilter.cbSize, ref _savedFilter, 0);

        _savedToggle = new TOGGLEKEYS { cbSize = (uint)Marshal.SizeOf<TOGGLEKEYS>() };
        SystemParametersInfoToggle(SPI_GETTOGGLEKEYS, _savedToggle.cbSize, ref _savedToggle, 0);

        _saved = true;

        if ((_savedSticky.dwFlags & SKF_STICKYKEYSON) == 0)
        {
            var s = _savedSticky;
            s.dwFlags &= ~(SKF_HOTKEYACTIVE | SKF_CONFIRMHOTKEY);
            SystemParametersInfoSticky(SPI_SETSTICKYKEYS, s.cbSize, ref s, 0);
        }

        if ((_savedFilter.dwFlags & FKF_FILTERKEYSON) == 0)
        {
            var f = _savedFilter;
            f.dwFlags &= ~(FKF_HOTKEYACTIVE | FKF_CONFIRMHOTKEY);
            SystemParametersInfoFilter(SPI_SETFILTERKEYS, f.cbSize, ref f, 0);
        }

        if ((_savedToggle.dwFlags & TKF_TOGGLEKEYSON) == 0)
        {
            var t = _savedToggle;
            t.dwFlags &= ~(TKF_HOTKEYACTIVE | TKF_CONFIRMHOTKEY);
            SystemParametersInfoToggle(SPI_SETTOGGLEKEYS, t.cbSize, ref t, 0);
        }
    }

    /// <summary>
    /// Restores the accessibility-shortcut state captured by the most
    /// recent <see cref="Disable"/> call. No-op if Disable was never
    /// called or has already been restored.
    /// </summary>
    public static void Restore()
    {
        if (!_saved) return;
        SystemParametersInfoSticky(SPI_SETSTICKYKEYS, _savedSticky.cbSize, ref _savedSticky, 0);
        SystemParametersInfoFilter(SPI_SETFILTERKEYS, _savedFilter.cbSize, ref _savedFilter, 0);
        SystemParametersInfoToggle(SPI_SETTOGGLEKEYS, _savedToggle.cbSize, ref _savedToggle, 0);
        _saved = false;
    }
}
