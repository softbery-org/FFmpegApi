// Version: 0.0.0.1
using System;
using System.Drawing;
using System.IO;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

using Device = SharpDX.Direct3D11.Device;

namespace FFmpegApi;
public class D3D11Context : IDisposable
{
    public Device Device;
    public DeviceContext Context;
    public Texture2D VideoTexture;
    public ShaderResourceView SRV;

    public D3D11Context()
    {
        Device = new Device(
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport);

        Context = Device.ImmediateContext;
    }

    public Texture2D CreateVideoTexture(int width, int height)
    {
        VideoTexture?.Dispose();

        VideoTexture = new Texture2D(Device, new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            Usage = ResourceUsage.Default,
            SampleDescription = new SampleDescription(1, 0)
        });

        SRV = new ShaderResourceView(Device, VideoTexture);
        return VideoTexture;
    }

    public void Dispose()
    {
        SRV?.Dispose();
        VideoTexture?.Dispose();
        Context?.Dispose();
        Device?.Dispose();
    }
}
