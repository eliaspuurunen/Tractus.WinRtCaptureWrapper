using SharpDX;
using System;
using System.Linq;

namespace Tractus.WinRtCaptureWrapper;

/// <summary>
/// Base class for consumers of frames during a capture session.
/// </summary>
public abstract class FrameHandler : IDisposable
{
    private bool isDisposed;

    public abstract void SendFrame(
        DataRectangle rect,
        int width,
        int height);

    protected void Dispose(bool disposing)
    {
        if (!this.isDisposed)
        {
            if (disposing)
            {
                this.OnDisposing();
            }

            this.isDisposed = true;
        }
    }

    protected virtual void OnDisposing()
    {

    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
