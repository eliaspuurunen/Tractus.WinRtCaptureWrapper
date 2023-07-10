using System;
using System.Linq;
using Windows.Graphics;

namespace Tractus.WinRtCaptureWrapper.Models;

public class ScreenInfo
{
    public IntPtr Hwnd { get; set; }
    public DisplayId Handle { get; set; }
    public string Title { get; set; }
}
