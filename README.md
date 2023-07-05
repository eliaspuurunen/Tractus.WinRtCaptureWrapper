# Tractus.WinRtCaptureWrapper

A simple Windows 10+ Capture API Wrapper for .NET 7 projects.

## Architecture

The static class `WinRtGraphicsCaptureHelper` provides a wrapper to get the available valid windows
that can be captured using the Windows 10 Screen Capture API. It will also provide a list of screens.

Get valid window targets: `WinRtGraphicsCaptureHelper.GetAvailableWindowsAsync()`

Get valid screens: `WinRtGraphicsCaptureHelper.GetAvailableScreensAsync()`

If permission to capture the screen programmatically is denied, these methods will return null.

If the permission check fails, check `WinRtGraphicsCaptureHelper.LastPermissionCheckResult` for details.

### Starting capture

To start capture, you will need to use the `CreateCaptureForX` methods in `WinRtGraphicsCaptureHelper`. These
methods will create a `DisplayCaptureInstance` object which takes care of setting up everything needed
to capture displays/windows.

When starting capture, you'll need to pass in a `FrameHandler`. The frame handler is passed a frame when
it arrives from the capture session. This frame is a SharpDX `DataRectangle`. Pointer to the graphics data,
the pitch, width, and height are passed to a the `FrameHandler` via the `SendFrame` method.

This data is available for CPU access.

You do not need to dispose of any pointers yourself - the `DisplayCaptureInstance` will take care of that
for you. If you wish to do anything else with the video data, you will need to make a copy of it.

The frame format is 32-bit BGRA, 8 bits per channel.

You can also pass an `IntPtr` of a window (window handle) to attempt capture.

If these methods fail to start a capture session, an exception will be thrown.

### Stopping capture

At the end of your capture session, call the `Stop()` method on your `DisplayCaptureInstance`.

It is also possible for a capture session to be closed because the captured window is closed. Handle the
capture instance's `CaptureSessionStopped` event. When the session is closed because of window close,
the capture instance `Stop()` method will be called automatically.

### Example Code

```
public ObservableCollection<WindowInfo> Windows { get; } = new ObservableCollection<WindowInfo>();
public ObservableCollection<ScreenInfo> Screens { get; } = new ObservableCollection<ScreenInfo>();

// Omitting declarations for the selected screen & selected window code...

public async Task Refresh()
{
    this.SelectedScreen = null;
    this.SelectedWindow = null;

    this.Windows.Clear();
    this.Screens.Clear();

    var securityCheck = await WinRtGraphicsCaptureHelper.RequestPermissionsAsync();
    if (!securityCheck)
    {
        MessageBox.Show("Could not get capture permissions.");
        return;
    }

    var screens = await WinRtGraphicsCaptureHelper.GetAvailableScreensAsync();
    foreach(var item in screens)
    {
        this.Screens.Add(item);
    }

    var windows = await WinRtGraphicsCaptureHelper.GetAvailableWindowsAsync();
    foreach (var item in windows)
    {
        this.Windows.Add(item);
    }
}

private DisplayCaptureInstance? capture;

public void ToggleCapture()
{
    if(this.capture is not null)
    {
        this.capture.Stop();
        return;
    }

    var frameHandler = new NdiFrameHandler()
    {
        NdiSenderName = this.NdiSenderName
    };

    if (this.windowCapture && this.selectedWindow is not null)
    {
        this.capture = WinRtGraphicsCaptureHelper.CreateCaptureForWindow(this.selectedWindow, frameHandler);
    }
    else if(this.screenCapture && this.selectedScreen is not null)
    {
        this.capture = WinRtGraphicsCaptureHelper.CreateCaptureForScreen(this.selectedScreen, frameHandler);
    }
    else
    {
        this.capture = null;
        MessageBox.Show("No source selected. Not started.");
    }

    if(this.capture != null)
    {
        this.capture.CaptureSessionStopped += this.OnCaptureSessionStopped;
    }
}

private void OnCaptureSessionStopped()
{
    if(this.capture != null)
    {
        this.capture.CaptureSessionStopped -= this.OnCaptureSessionStopped;
    }
    this.capture = null;
}

```

#### Example NDI Frame Handler

NOTE: This code requires the NDI SDK available from https://ndi.video/download-ndi-sdk/

```
using NewTek;
using NewTek.NDI;
using SharpDX;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Tractus.WinRtCaptureWrapper;

namespace WpfRtTestbed;

public class NdiFrameHandler : FrameHandler
{
    IntPtr sendInstancePtr = IntPtr.Zero;

    public string NdiSenderName { get; set; }

    protected override void OnDisposing()
    {
        if (this.sendInstancePtr != IntPtr.Zero)
        {
            NDIlib.send_destroy(this.sendInstancePtr);
            this.sendInstancePtr = IntPtr.Zero;
        }
    }

    public override void SendFrame(DataRectangle rect, int width, int height)
    {
        int xres = width; // NdiWidth;
        int yres = height; //NdiHeight;

        int frNum = 30;
        int frDen = 1;

        if (sendInstancePtr == IntPtr.Zero)
        {
            var ptr = new NDIlib.send_create_t
            {
                clock_audio = false,
                clock_video = true,
                p_groups = IntPtr.Zero,
                p_ndi_name = UTF.StringToUtf8(this.NdiSenderName)
            };
            sendInstancePtr = NDIlib.send_create(ref ptr);
        }

        var stride = rect.Pitch; // (xres * 32/*BGRA bpp*/ + 7) / 8;
        var requiredFrameSize = yres * stride;
        var aspectRatio = xres / (float)yres;

        // We are going to create a progressive frame at 60Hz.
        NDIlib.video_frame_v2_t videoFrame = new NDIlib.video_frame_v2_t()
        {
            // Resolution
            xres = xres,
            yres = yres,
            // Use BGRA video
            FourCC = NDIlib.FourCC_type_e.FourCC_type_BGRA,
            // The frame-eate
            frame_rate_N = frNum,
            frame_rate_D = frDen,
            // The aspect ratio
            picture_aspect_ratio = aspectRatio,
            // This is a progressive frame
            frame_format_type = NDIlib.frame_format_type_e.frame_format_type_progressive,
            // Timecode.
            timecode = NDIlib.send_timecode_synthesize,
            // The video memory used for this frame
            p_data = rect.DataPointer,
            // The line to line stride of this image
            line_stride_in_bytes = stride,
            // no metadata
            p_metadata = IntPtr.Zero,
            // only valid on received frames
            timestamp = 0,
        };

        NDIlib.send_send_video_v2(this.sendInstancePtr, ref videoFrame);
        //// add it to the output queue
        //AddFrame(videoFrame);    }
    }
}

```

## Implementation Notes

Ensure the following is added to your project to indicate that your WPF/Forms app will
run on Windows 10 20348 or higher.

```
<TargetFramework>net7.0-windows10.0.22000.0</TargetFramework>
<SupportedOSPlatformVersion>10.0.20348.0</SupportedOSPlatformVersion>
```

## Inspiration/Sources

- WPF RenderTargetBitmap post on NDI Forums: https://forums.newtek.com/threads/wpf-rendertargetbitmap-is-too-slow-any-alternatives.160559/
- Windows.UI.Composition samples: https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/issues/41