using System;
using System.Linq;
using Tractus.WinRtCaptureWrapper;
using Tractus.WinRtCaptureWrapper.Models;
using Tractus.WinRtCaptureWrapper.Win32Api;
using Windows.Graphics.Capture;
using Windows.Graphics.Display;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace Tractus.WinRtCaptureWrapper;

public static class WinRtGraphicsCaptureHelper
{
    public static AppCapabilityAccessStatus LastPermissionCheckResult { get; private set; }

    public static async Task<bool> RequestPermissionsAsync()
    {
        var result = await GraphicsCaptureAccess.RequestAccessAsync(
            GraphicsCaptureAccessKind.Borderless | GraphicsCaptureAccessKind.Programmatic);

        LastPermissionCheckResult = result;
        var toReturn = result == AppCapabilityAccessStatus.Allowed;

        return toReturn;
    }

    public static async Task<List<ScreenInfo>> GetAvailableScreensAsync()
    {
        var toReturn = new List<ScreenInfo>();

        if(!await RequestPermissionsAsync())
        {
            return toReturn;
        }

        var screens = DisplayServices.FindAll();

        foreach (var screen in screens)
        {
            var toCreate = GraphicsCaptureItem.TryCreateFromDisplayId(screen);

            if (toCreate is null)
            {
                continue;
            }

            var toAdd = new ScreenInfo
            {
                Handle = screen,
                Title = $"{toCreate.DisplayName} -- {toCreate.Size.Width} x {toCreate.Size.Height}"
            };

            toReturn.Add(toAdd);
        }

        return toReturn.OrderBy(x => x.Title).ToList(); 
    }

    public static async Task<List<WindowInfo>> GetAvailableWindowsAsync()
    {
        var toReturn = new List<WindowInfo>();

        if(!await RequestPermissionsAsync())
        {
            return toReturn;
        }

        var allOpenWindows = OpenWindowListHelper.GetWindowCaptureCandidates();

        foreach (var window in allOpenWindows.OrderBy(x => x.Title))
        {
            toReturn.Add(window);
        }

        return toReturn;
    }

    public static DisplayCaptureInstance CreateCaptureForHandle(
        IntPtr handle,
        FrameHandler frameHandler)
    {
        var windowInfo = new WindowInfo
        {
            Handle = handle
        };

        return CreateCaptureForWindow(windowInfo, frameHandler); 
    }

    public static DisplayCaptureInstance CreateCaptureForWindow(
        WindowInfo window,
        FrameHandler frameHandler)
    {
        var capture = new DisplayCaptureInstance();

        try
        {
            capture.SetFrameHandler(frameHandler);

            capture.Start(window);
        }
        catch 
        {
            capture.Stop();

            throw;
        }

        return capture;
    }

    public static DisplayCaptureInstance CreateCaptureForScreen(
        ScreenInfo screen,
        FrameHandler frameHandler)
    {
        var capture = new DisplayCaptureInstance();

        try
        {
            capture.SetFrameHandler(frameHandler);

            capture.Start(screen);
        }
        catch
        {
            capture.Stop();

            throw;
        }

        return capture;
    }
}
