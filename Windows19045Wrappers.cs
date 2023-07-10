using System;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;

namespace Tractus.WinRtCaptureWrapper;

// https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/blob/master/dotnet/WPF/ScreenCapture/Composition.WindowsRuntimeHelpers/CaptureHelper.cs
[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComVisible(true)]
public interface IGraphicsCaptureItemInterop
{
    IntPtr CreateForWindow(
        [In] IntPtr window,
        [In] ref Guid iid);

    IntPtr CreateForMonitor(
        [In] IntPtr monitor,
        [In] ref Guid iid);
}

/// <summary>
/// Provides capture wrappers for the Windows 10 display capture API.
/// </summary>
public static class Windows10_19041_CaptureComWrappers
{
    static readonly Guid GraphicsCaptureItemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    /*
     * Based on code from 
     * https://github.com/microsoft/CsWinRT/blob/master/docs/interop.md
     * and
     * https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/blob/master/dotnet/WPF/ScreenCapture/Composition.WindowsRuntimeHelpers/CaptureHelper.cs
     * 
     * .NET 5+ does not support WinRT marshalling native anymore. So we use the guidance provided
     * in CsWinRT.
     * 
     * 
     */
    public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
    {
        var factory = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var interop = (IGraphicsCaptureItemInterop)factory;
        var temp = typeof(GraphicsCaptureItem);

        var itemPointer = interop.CreateForWindow(hwnd, GraphicsCaptureItemGuid);

        var item = GraphicsCaptureItem.FromAbi(itemPointer); 
        Marshal.Release(itemPointer);
        return item;
    }

    public static GraphicsCaptureItem CreateItemForDisplay(IntPtr hwnd)
    {
        var factory = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var interop = (IGraphicsCaptureItemInterop)factory;
        var temp = typeof(GraphicsCaptureItem);

        var itemPointer = interop.CreateForMonitor(hwnd, GraphicsCaptureItemGuid);

        var item = GraphicsCaptureItem.FromAbi(itemPointer);
        Marshal.Release(itemPointer);
        return item;
    }
}
