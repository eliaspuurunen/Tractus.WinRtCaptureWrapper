using System;
using System.Linq;
using System.Runtime.InteropServices;
using Tractus.WinRtCaptureWrapper.Models;

namespace Tractus.WinRtCaptureWrapper.Win32Api;

// Thank you based chatGPT for coding this up.
public class OpenWindowListHelper
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private static bool EnumWindow(IntPtr hWnd, IntPtr lParam)
    {
        if (IsWindowVisible(hWnd))
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0)
                return true;

            var stringBuilder = new System.Text.StringBuilder(length + 1);
            GetWindowText(hWnd, stringBuilder, stringBuilder.Capacity);

            string windowTitle = stringBuilder.ToString();
            var windowInfo = new WindowInfo { Handle = hWnd, Title = windowTitle };
            windowList.Add(windowInfo);
        }
        return true;
    }

    /// <summary>
    /// Gets an array of any windows which are visible to the user and are valid targets for display capture.
    /// </summary>
    /// <returns>An array containing any windows which are valid for display capture.</returns>
    public static WindowInfo[] GetWindowCaptureCandidates()
    {
        windowList.Clear();
        EnumWindows(EnumWindow, IntPtr.Zero);
        return windowList.ToArray();
    }

    private static int GetWindowTextLength(IntPtr hWnd)
    {
        return SendMessage(hWnd, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_GETTEXTLENGTH = 0x000E;

    private static readonly List<WindowInfo> windowList = new List<WindowInfo>();
}
