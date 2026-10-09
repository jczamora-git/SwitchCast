using SwitchCast.Models;
using SwitchCast.Services.Media;
using Xunit;

namespace SwitchCast.Tests.Services;

public class MediaDiscoveryServiceTests
{
    private readonly MediaDiscoveryService _service;

    public MediaDiscoveryServiceTests()
    {
        _service = new MediaDiscoveryService();
    }

    [Theory]
    [InlineData(".png", true)]
    [InlineData("test.png", true)]
    [InlineData("IMAGE.JPG", true)]
    [InlineData("photo.jpeg", true)]
    [InlineData("graphic.bmp", true)]
    [InlineData("animated.gif", true)]
    [InlineData("modern.webp", true)]
    [InlineData("document.pdf", false)]
    [InlineData("video.mp4", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupportedImage_ReturnsExpectedResult(string? input, bool expected)
    {
        bool actual = _service.IsSupportedImage(input!);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(".mp4", true)]
    [InlineData("video.mp4", true)]
    [InlineData("CLIP.M4V", true)]
    [InlineData("recording.wmv", true)]
    [InlineData("movie.mov", true)]
    [InlineData("legacy.avi", true)]
    [InlineData("stream.mkv", true)]
    [InlineData("image.png", false)]
    [InlineData("audio.mp3", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupportedVideo_ReturnsExpectedResult(string? input, bool expected)
    {
        bool actual = _service.IsSupportedVideo(input!);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CreateMediaSourceFromFileAsync_NullOrEmpty_ReturnsNull()
    {
        var result1 = await _service.CreateMediaSourceFromFileAsync(string.Empty);
        var result2 = await _service.CreateMediaSourceFromFileAsync("   ");

        Assert.Null(result1);
        Assert.Null(result2);
    }

    [Fact]
    public async Task CreateMediaSourceFromFileAsync_UnsupportedFormat_ReturnsNull()
    {
        var result = await _service.CreateMediaSourceFromFileAsync(@"C:\Files\document.pdf");
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateMediaSourceFromFileAsync_NonExistentImage_ReturnsUnavailableImageSource()
    {
        var path = @"C:\NonExistentFolder_12345\Image_Not_Found.png";
        var result = await _service.CreateMediaSourceFromFileAsync(path);

        Assert.NotNull(result);
        Assert.IsType<ImageMediaSource>(result);
        Assert.Equal(SourceType.Image, result.Type);
        Assert.Equal("Image_Not_Found.png", result.Title);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task CreateMediaSourceFromFileAsync_NonExistentVideo_ReturnsUnavailableVideoSource()
    {
        var path = @"C:\NonExistentFolder_12345\Video_Not_Found.mp4";
        var result = await _service.CreateMediaSourceFromFileAsync(path);

        Assert.NotNull(result);
        Assert.IsType<VideoMediaSource>(result);
        Assert.Equal(SourceType.Video, result.Type);
        Assert.Equal("Video_Not_Found.mp4", result.Title);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task LoadPersistedMediaAsync_MultiplePaths_ReturnsSourcesList()
    {
        var paths = new List<string>
        {
            @"C:\NonExistent\Image1.png",
            @"C:\NonExistent\Video1.mp4",
            @"C:\NonExistent\IgnoredDoc.docx"
        };

        var results = await _service.LoadPersistedMediaAsync(paths);

        Assert.Equal(2, results.Count);
        Assert.Equal(SourceType.Image, results[0].Type);
        Assert.Equal(SourceType.Video, results[1].Type);
    }
}
