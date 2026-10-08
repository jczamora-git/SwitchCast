using SwitchCast.Services.Capture;
using Xunit;

namespace SwitchCast.Tests.Services;

public class Win32DiagnosticCaptureServiceTests
{
    [Fact]
    public async Task CaptureWindowDiagnosticAsync_ZeroHwnd_ReturnsNull()
    {
        var service = new Win32DiagnosticCaptureService();
        var result = await service.CaptureWindowDiagnosticAsync(nint.Zero);

        Assert.Null(result);
    }

    [Fact]
    public async Task CaptureWindowDiagnosticAsync_InvalidHwnd_ReturnsNull()
    {
        var service = new Win32DiagnosticCaptureService();
        // Arbitrary invalid window handle
        var result = await service.CaptureWindowDiagnosticAsync(new IntPtr(0x12345678));

        Assert.Null(result);
    }

    [Fact]
    public async Task CaptureWindowDiagnosticAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var service = new Win32DiagnosticCaptureService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CaptureWindowDiagnosticAsync((nint)0x1234, cts.Token));
    }
}
