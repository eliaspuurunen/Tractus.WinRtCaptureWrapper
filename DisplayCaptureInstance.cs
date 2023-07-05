using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Tractus.WinRtCaptureWrapper.Models;
using Tractus.WinRtCaptureWrapper.Win32Api;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace Tractus.WinRtCaptureWrapper;

public class DisplayCaptureInstance
{
    public bool IsRunning { get; set; }

    private GraphicsCaptureItem? item;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private SizeInt32 lastSize;

    private IDirect3DDevice? device;
    private Device? d3dDevice;
    private SharpDX.DXGI.SwapChain1? swapChain;
    private SharpDX.DXGI.Factory2? dxgiFactory;

    private WindowInfo? window;
    private ScreenInfo? screenInfo;

    private readonly object threadLock = new();

    private FrameHandler? frameHandler;

    public void SetFrameHandler(FrameHandler handler)
    {
        if (this.IsRunning)
        {
            throw new InvalidOperationException("Cannot change frame handler when running.");
        }
        this.frameHandler = handler;
    }

    public void Stop()
    {
        if (!this.IsRunning)
        {
            return;
        }

        this.CaptureSessionStopped?.Invoke();

        lock (this.threadLock)
        {
            this.framePool.FrameArrived -= this.OnFrameArrived;
            this.session.Dispose();
            this.framePool.Dispose();
            this.swapChain.Dispose();
            this.d3dDevice.Dispose();
            this.device.Dispose();

            if(this.frameHandler is not null)
            {
                this.frameHandler.Dispose();
                this.frameHandler = null;
            }

            this.IsRunning = false;
        }
    }

    public void Start(WindowInfo window)
    {
        if (this.IsRunning)
        {
            return;
        }

        this.window = window;
        this.Start();
    }

    public void Start(ScreenInfo screen)
    {
        if (this.IsRunning)
        {
            return;
        }

        this.screenInfo = screen;
        this.Start();
    }

    public event Action CaptureSessionStopped;

    // Inspired by code found on NewTek NDI forums:
    // https://forums.newtek.com/threads/wpf-rendertargetbitmap-is-too-slow-any-alternatives.160559/
    // and
    // https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/issues/41

    private void Start()
    {
        if (this.window is null && this.screenInfo is null)
        {
            throw new ArgumentException("Either window or screenInfo must be set.");
        }

        this.device = Direct3D11Helper.CreateDevice();
        this.d3dDevice = Direct3D11Helper.CreateSharpDXDevice(this.device);

        if (this.window is not null)
        {
            this.item = GraphicsCaptureItem.TryCreateFromWindowId(new Windows.UI.WindowId((ulong)this.window.Handle));
        }
        else if (this.screenInfo is not null)
        {
            this.item = GraphicsCaptureItem.TryCreateFromDisplayId(this.screenInfo.Handle);
        }
        else
        {
            throw new NotImplementedException("How did we get here?");
        }

        if(this.item is null)
        {
            this.device.Dispose();
            this.d3dDevice.Dispose();
            this.device = null;
            this.d3dDevice = null;
            throw new InvalidOperationException("Target for capture is invalid.");
        }

        this.item.Closed += this.OnCaptureItemClosed;

        this.dxgiFactory = new SharpDX.DXGI.Factory2();
        var description = new SharpDX.DXGI.SwapChainDescription1()
        {
            Width = item.Size.Width,
            Height = item.Size.Height,
            Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = new SharpDX.DXGI.SampleDescription()
            {
                Count = 1,
                Quality = 0
            },
            Usage = SharpDX.DXGI.Usage.RenderTargetOutput,
            BufferCount = 2,
            Scaling = SharpDX.DXGI.Scaling.Stretch,
            SwapEffect = SharpDX.DXGI.SwapEffect.FlipSequential,
            AlphaMode = SharpDX.DXGI.AlphaMode.Premultiplied,
            Flags = SharpDX.DXGI.SwapChainFlags.None
        };

        this.swapChain = new SharpDX.DXGI.SwapChain1(dxgiFactory, d3dDevice, ref description);

        this.framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            this.device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            item.Size);

        session = framePool.CreateCaptureSession(item);
        session.IsBorderRequired = false;
        session.IsCursorCaptureEnabled = false;
        lastSize = item.Size;

        this.framePool.FrameArrived += this.OnFrameArrived;

        this.session.StartCapture();

        this.IsRunning = true;
    }

    private void OnCaptureItemClosed(GraphicsCaptureItem sender, object args)
    {
        if(this.item is not null)
        {
            this.item.Closed -= this.OnCaptureItemClosed;
        }

        this.Stop();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        lock (this.threadLock)
        {
            if (!this.IsRunning)
            {
                return;
            }

            var newSize = false;

            using (var frame = sender.TryGetNextFrame())
            {
                if (frame is null)
                {
                    System.Diagnostics.Debug.WriteLine("Got a NULL frame. Bail.");
                    return;
                }

                if (frame.ContentSize.Width != lastSize.Width ||
                    frame.ContentSize.Height != lastSize.Height)
                {
                    // DO NOT RELY ON ITEM.SIZE
                    // It is not guaranteed to be updated when the window is resized.

                    // The only reliable message from item.Size is if it's 0x0

                    System.Diagnostics.Debug.WriteLine(
                        $"Frame size differs from last frame received\r\nOld: {lastSize.Width} x {lastSize.Height}\r\nNew: {frame.ContentSize.Width} x {frame.ContentSize.Height}\r\nItem Size: {this.item.Size.Width} x {this.item.Size.Height}");

                    // The thing we have been capturing has changed size.
                    // We need to resize the swap chain first, then blit the pixels.
                    // After we do that, retire the frame and then recreate the frame pool.
                    newSize = true;
                    lastSize = frame.ContentSize;
                    swapChain.ResizeBuffers(
                        2,
                        lastSize.Width,
                        lastSize.Height,
                        SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                        SharpDX.DXGI.SwapChainFlags.None);
                }

                using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
                using (var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface))
                {
                    d3dDevice.ImmediateContext.CopyResource(bitmap, backBuffer);

                    var copy = new Texture2D(this.d3dDevice, new Texture2DDescription
                    {
                        Width = lastSize.Width,
                        Height = lastSize.Height,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = bitmap.Description.Format,
                        Usage = ResourceUsage.Staging,
                        SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                        BindFlags = BindFlags.None,
                        CpuAccessFlags = CpuAccessFlags.Read,
                        OptionFlags = ResourceOptionFlags.None
                    });

                    d3dDevice.ImmediateContext.CopyResource(bitmap, copy);

                    var dataBox = d3dDevice.ImmediateContext.MapSubresource(copy, 0, 0, MapMode.Read, MapFlags.None,
                       out DataStream stream);

                    var rect = new DataRectangle
                    {
                        DataPointer = stream.DataPointer,
                        Pitch = dataBox.RowPitch
                    };

                    if (!newSize)
                    {
                        this.frameHandler?.SendFrame(rect, lastSize.Width, lastSize.Height);
                    }

                    d3dDevice.ImmediateContext.UnmapSubresource(copy, 0);
                    copy.Dispose();
                }
            }

            swapChain.Present(0, SharpDX.DXGI.PresentFlags.None);

            if (newSize)
            {
                framePool.Recreate(
                    device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    lastSize);
            }
        }
    }
}
