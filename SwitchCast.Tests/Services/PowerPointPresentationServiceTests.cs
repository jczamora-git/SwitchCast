using Moq;
using SwitchCast.Models;
using SwitchCast.Services.PowerPoint;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PowerPointPresentationServiceTests
{
    private readonly Mock<IPresentationSlideRenderer> _mockRenderer;
    private readonly PowerPointPresentationService _service;

    public PowerPointPresentationServiceTests()
    {
        _mockRenderer = new Mock<IPresentationSlideRenderer>();
        _mockRenderer.Setup(r => r.IsAvailable).Returns(true);
        _service = new PowerPointPresentationService(_mockRenderer.Object);
    }

    [Fact]
    public void PowerPointSource_InitializesWithCorrectTypeAndDefaults()
    {
        var ppt = new PowerPointSource(@"C:\Presentations\Deck.pptx")
        {
            SlideCount = 10,
            CurrentSlideIndex = 1
        };

        Assert.Equal(SourceType.PowerPoint, ppt.Type);
        Assert.Equal(@"C:\Presentations\Deck.pptx", ppt.FilePath);
        Assert.Equal("Deck.pptx", ppt.Title);
        Assert.Equal(10, ppt.SlideCount);
        Assert.Equal(1, ppt.CurrentSlideIndex);
        Assert.Equal("Slide 1 of 10", ppt.SlideProgressText);
        Assert.False(ppt.IsLiveSlideshow);
    }

    [Fact]
    public void PowerPointSource_CurrentSlideImagePath_ClampsIndexCorrectly()
    {
        var paths = new List<string> { "slide_1.png", "slide_2.png", "slide_3.png" };
        var ppt = new PowerPointSource(@"C:\Deck.pptx")
        {
            SlideCount = 3,
            CurrentSlideIndex = 2,
            SlideImagePaths = paths
        };

        Assert.Equal("slide_2.png", ppt.CurrentSlideImagePath);

        var first = ppt with { CurrentSlideIndex = 0 };
        Assert.Equal("slide_1.png", first.CurrentSlideImagePath);

        var overflow = ppt with { CurrentSlideIndex = 99 };
        Assert.Equal("slide_3.png", overflow.CurrentSlideImagePath);
    }

    [Fact]
    public async Task PowerPointService_SlideNavigation_AdvancesAndRecedesWithinBounds()
    {
        var paths = new List<string> { "s1.png", "s2.png", "s3.png" };
        var ppt = new PowerPointSource(@"C:\Deck.pptx")
        {
            SlideCount = 3,
            CurrentSlideIndex = 1,
            SlideImagePaths = paths
        };

        await _service.LoadPresentationAsync(ppt);

        Assert.Equal(1, _service.CurrentSlideIndex);
        Assert.Equal(3, _service.TotalSlideCount);

        // Next slide
        bool next1 = await _service.NextSlideAsync();
        Assert.True(next1);
        Assert.Equal(2, _service.CurrentSlideIndex);

        bool next2 = await _service.NextSlideAsync();
        Assert.True(next2);
        Assert.Equal(3, _service.CurrentSlideIndex);

        // Clamped at end
        bool nextAtEnd = await _service.NextSlideAsync();
        Assert.False(nextAtEnd);
        Assert.Equal(3, _service.CurrentSlideIndex);

        // Previous slide
        bool prev1 = await _service.PreviousSlideAsync();
        Assert.True(prev1);
        Assert.Equal(2, _service.CurrentSlideIndex);

        // GoToSlide
        bool jump = await _service.GoToSlideAsync(1);
        Assert.True(jump);
        Assert.Equal(1, _service.CurrentSlideIndex);

        // Jump out of bounds clamps to max slide (3)
        bool jumpClamped = await _service.GoToSlideAsync(99);
        Assert.True(jumpClamped);
        Assert.Equal(3, _service.CurrentSlideIndex);

        // Jump again to out of bounds when already at clamped end returns false (no change)
        bool jumpSame = await _service.GoToSlideAsync(99);
        Assert.False(jumpSame);
        Assert.Equal(3, _service.CurrentSlideIndex);
    }

    [Fact]
    public async Task PowerPointService_ImportNonExistentFile_CapturesErrorSafely()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.ImportPresentationAsync(@"C:\NonExistent\Missing.pptx"));

        Assert.NotNull(_service.LastErrorMessage);
    }

    [Fact]
    public async Task PowerPointLiveService_WhenPowerPointNotInstalled_HandlesGracefully()
    {
        var liveService = new PowerPointLiveService();
        // Regardless of whether PowerPoint is installed on this dev box, StartSlideshow on non-existent file returns false safely
        bool started = await liveService.StartSlideshowAsync(@"C:\NonExistent\File.pptx");
        Assert.False(started);
        Assert.False(liveService.IsLiveSlideshowRunning);
    }
}
