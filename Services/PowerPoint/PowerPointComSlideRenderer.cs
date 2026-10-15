using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SwitchCast.Services.PowerPoint;

/// <summary>
/// Static slide renderer using attended Microsoft PowerPoint COM automation to export slide images.
/// Follows strict STA threading rules and releases COM wrappers deterministically.
/// </summary>
public sealed class PowerPointComSlideRenderer : IPresentationSlideRenderer
{
    private const string PowerPointProgId = "PowerPoint.Application";

    public bool IsAvailable
    {
        get
        {
            try
            {
                var type = Type.GetTypeFromProgID(PowerPointProgId);
                return type is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public string EngineName => "Microsoft PowerPoint Automation Engine";

    public Task<SlideDeckRenderResult> RenderDeckAsync(string pptxPath, string cacheDirectory, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pptxPath))
        {
            return Task.FromResult(new SlideDeckRenderResult
            {
                Success = false,
                ErrorMessage = $"PowerPoint document not found at '{pptxPath}'."
            });
        }

        if (!IsAvailable)
        {
            return Task.FromResult(new SlideDeckRenderResult
            {
                Success = false,
                ErrorMessage = "Microsoft PowerPoint is not installed or registered on this Windows system. Please install Microsoft PowerPoint to render static .pptx slides."
            });
        }

        var tcs = new TaskCompletionSource<SlideDeckRenderResult>();

        // Office COM automation requires single-threaded apartment (STA)
        var thread = new Thread(() =>
        {
            object? pptApp = null;
            object? presentations = null;
            object? presentation = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                Directory.CreateDirectory(cacheDirectory);

                var pptType = Type.GetTypeFromProgID(PowerPointProgId);
                if (pptType is null)
                {
                    tcs.SetResult(new SlideDeckRenderResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to resolve PowerPoint COM type."
                    });
                    return;
                }

                pptApp = Activator.CreateInstance(pptType);
                if (pptApp is null)
                {
                    tcs.SetResult(new SlideDeckRenderResult
                    {
                        Success = false,
                        ErrorMessage = "Unable to start Microsoft PowerPoint application instance."
                    });
                    return;
                }

                presentations = pptType.InvokeMember("Presentations", System.Reflection.BindingFlags.GetProperty, null, pptApp, null);
                if (presentations is null)
                {
                    tcs.SetResult(new SlideDeckRenderResult
                    {
                        Success = false,
                        ErrorMessage = "Unable to access PowerPoint Presentations collection."
                    });
                    return;
                }

                // Open(FileName, ReadOnly: -1, Untitled: 0, WithWindow: 0)
                // WithWindow = 0 (msoFalse) keeps PowerPoint from stealing focus or showing a window
                object[] openArgs = [pptxPath, -1, 0, 0];
                presentation = presentations.GetType().InvokeMember("Open", System.Reflection.BindingFlags.InvokeMethod, null, presentations, openArgs);

                if (presentation is null)
                {
                    tcs.SetResult(new SlideDeckRenderResult
                    {
                        Success = false,
                        ErrorMessage = "PowerPoint was unable to open the presentation document."
                    });
                    return;
                }

                var slides = presentation.GetType().InvokeMember("Slides", System.Reflection.BindingFlags.GetProperty, null, presentation, null);
                int slideCount = (int)(slides?.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, slides, null) ?? 0);

                if (slideCount <= 0)
                {
                    tcs.SetResult(new SlideDeckRenderResult
                    {
                        Success = false,
                        ErrorMessage = "Presentation contains no slides."
                    });
                    return;
                }

                var imagePaths = new List<string>();

                // Export individual slides to PNG
                for (int i = 1; i <= slideCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string slideImagePath = Path.Combine(cacheDirectory, $"slide_{i}.png");
                    object? slide = slides?.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, slides, [i]);

                    if (slide is not null)
                    {
                        try
                        {
                            // slide.Export(FileName, FilterName: "PNG", ScaleWidth: 1920, ScaleHeight: 1080)
                            slide.GetType().InvokeMember("Export", System.Reflection.BindingFlags.InvokeMethod, null, slide, [slideImagePath, "PNG", 1920, 1080]);
                            if (File.Exists(slideImagePath))
                            {
                                imagePaths.Add(slideImagePath);
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(slide);
                        }
                    }
                }

                if (slides is not null)
                {
                    Marshal.ReleaseComObject(slides);
                }

                tcs.SetResult(new SlideDeckRenderResult
                {
                    Success = true,
                    SlideCount = imagePaths.Count,
                    SlideImagePaths = imagePaths
                });
            }
            catch (OperationCanceledException)
            {
                tcs.SetCanceled();
            }
            catch (COMException comEx)
            {
                Diagnostics.SwitchCastDiagnostics.LogException(nameof(PowerPointComSlideRenderer), "RenderDeckAsync (COM)", comEx, sourceDetails: Path.GetFileName(pptxPath));
                tcs.SetResult(new SlideDeckRenderResult
                {
                    Success = false,
                    ErrorMessage = $"PowerPoint COM export error (0x{comEx.ErrorCode:X8}): {comEx.Message}"
                });
            }
            catch (Exception ex)
            {
                Diagnostics.SwitchCastDiagnostics.LogException(nameof(PowerPointComSlideRenderer), "RenderDeckAsync", ex, sourceDetails: Path.GetFileName(pptxPath));
                tcs.SetResult(new SlideDeckRenderResult
                {
                    Success = false,
                    ErrorMessage = $"Slide rendering failed (0x{ex.HResult:X8}): {ex.Message}"
                });
            }
            finally
            {
                try
                {
                    if (presentation is not null)
                    {
                        presentation.GetType().InvokeMember("Close", System.Reflection.BindingFlags.InvokeMethod, null, presentation, null);
                        Marshal.ReleaseComObject(presentation);
                    }

                    if (presentations is not null)
                    {
                        Marshal.ReleaseComObject(presentations);
                    }

                    if (pptApp is not null)
                    {
                        pptApp.GetType().InvokeMember("Quit", System.Reflection.BindingFlags.InvokeMethod, null, pptApp, null);
                        Marshal.ReleaseComObject(pptApp);
                    }
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"[PowerPointComSlideRenderer] Cleanup error: {cleanupEx.Message}");
                }
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }
}
