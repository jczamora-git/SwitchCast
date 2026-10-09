using SwitchCast.Models;
using Xunit;

namespace SwitchCast.Tests.Models;

public class MediaFileSourceTests
{
    [Fact]
    public void ImageMediaSource_Properties_FormatCorrectly()
    {
        var image = new ImageMediaSource
        {
            Id = @"media-file://C:/Images/Sample.png",
            Title = "Sample.png",
            FilePath = @"C:\Images\Sample.png",
            Width = 1920,
            Height = 1080,
            FileSizeBytes = 1024 * 1024 * 2 + 512 * 1024, // 2.5 MB
            IsAvailable = true
        };

        Assert.Equal(SourceType.Image, image.Type);
        Assert.Equal(1920, image.Width);
        Assert.Equal(1080, image.Height);
        Assert.Equal("2.5 MB", image.FormattedFileSize);
        Assert.Equal("Image", image.CategoryLabel);
        Assert.Equal("\uEB9F", image.TypeGlyph);
    }

    [Fact]
    public void VideoMediaSource_Properties_FormatCorrectly()
    {
        var video = new VideoMediaSource
        {
            Id = @"media-file://C:/Videos/Demo.mp4",
            Title = "Demo.mp4",
            FilePath = @"C:\Videos\Demo.mp4",
            Width = 3840,
            Height = 2160,
            Duration = TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(35)),
            FileSizeBytes = 1024 * 1024 * 150, // 150 MB
            IsAvailable = true,
            IsLooping = true
        };

        Assert.Equal(SourceType.Video, video.Type);
        Assert.Equal(3840, video.Width);
        Assert.Equal(2160, video.Height);
        Assert.Equal("02:35", video.FormattedDuration);
        Assert.Equal("150.0 MB", video.FormattedFileSize);
        Assert.Equal("Video", video.CategoryLabel);
        Assert.Equal("\uE714", video.TypeGlyph);
        Assert.True(video.IsLooping);
    }

    [Fact]
    public void VideoMediaSource_LongDuration_FormatsWithHours()
    {
        var video = new VideoMediaSource
        {
            Id = @"media-file://C:/Videos/Keynote.mp4",
            Title = "Keynote.mp4",
            FilePath = @"C:\Videos\Keynote.mp4",
            Duration = TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(15)).Add(TimeSpan.FromSeconds(20)),
            IsAvailable = true
        };

        Assert.Equal("01:15:20", video.FormattedDuration);
    }
}
