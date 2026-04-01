using NfoForge.Core.Constants;

namespace NfoForge.Tests;

public class VideoFormatsTests
{
    [Theory]
    [InlineData("video.mp4")]
    [InlineData("video.mkv")]
    [InlineData("video.avi")]
    [InlineData("video.mov")]
    [InlineData("video.flv")]
    [InlineData("video.wmv")]
    [InlineData("video.webm")]
    public void IsVideoFile_SupportedFormats_ReturnsTrue(string fileName)
    {
        Assert.True(VideoFormats.IsVideoFile(fileName));
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("image.jpg")]
    [InlineData("readme.md")]
    [InlineData("data.json")]
    public void IsVideoFile_UnsupportedFormats_ReturnsFalse(string fileName)
    {
        Assert.False(VideoFormats.IsVideoFile(fileName));
    }

    [Theory]
    [InlineData("video.MP4")]
    [InlineData("video.Mkv")]
    [InlineData("video.AVI")]
    public void IsVideoFile_CaseInsensitive_ReturnsTrue(string fileName)
    {
        Assert.True(VideoFormats.IsVideoFile(fileName));
    }

    [Fact]
    public void IsVideoFile_EmptyString_ReturnsFalse()
    {
        Assert.False(VideoFormats.IsVideoFile(""));
    }

    [Fact]
    public void IsVideoFile_NoExtension_ReturnsFalse()
    {
        Assert.False(VideoFormats.IsVideoFile("videofile"));
    }
}
