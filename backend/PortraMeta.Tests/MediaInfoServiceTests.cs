using Microsoft.Extensions.Logging.Abstractions;
using PortraMeta.Data.Services;

namespace PortraMeta.Tests;

public class MediaInfoServiceTests
{
    private static MediaInfoService CreateService(string? executableName = null)
        => executableName is null
            ? new MediaInfoService(NullLogger<MediaInfoService>.Instance)
            : new MediaInfoService(NullLogger<MediaInfoService>.Instance) { ExecutableName = executableName };

    private const string ValidJson = """
        {
          "media": {
            "track": [
              { "@type": "General", "Duration": "5400.250", "OverallBitRate": "8000000" },
              { "@type": "Video", "Format": "HEVC", "Width": "1920", "Height": "1080", "FrameRate": "23.976" },
              { "@type": "Audio", "Format": "AAC" }
            ]
          }
        }
        """;

    [Fact]
    public void ParseOutput_ValidJson_MapsAllFields()
    {
        var result = CreateService().ParseOutput(ValidJson, "video.mkv");

        Assert.NotNull(result);
        Assert.Equal(5400.250, result.DurationSeconds);
        Assert.Equal("HEVC", result.VideoCodec);
        Assert.Equal("AAC", result.AudioCodec);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.Equal(23.976, result.FrameRate);
        Assert.Equal(8000000L, result.BitRate);
    }

    [Fact]
    public void ParseOutput_MultipleVideoAndAudioTracks_UsesFirstOfEach()
    {
        const string json = """
            {
              "media": {
                "track": [
                  { "@type": "General", "Duration": "100" },
                  { "@type": "Video", "Format": "AVC", "Width": "1280", "Height": "720" },
                  { "@type": "Video", "Format": "HEVC", "Width": "3840", "Height": "2160" },
                  { "@type": "Audio", "Format": "DTS" },
                  { "@type": "Audio", "Format": "AAC" }
                ]
              }
            }
            """;

        var result = CreateService().ParseOutput(json, "video.mkv");

        Assert.NotNull(result);
        Assert.Equal("AVC", result.VideoCodec);
        Assert.Equal(1280, result.Width);
        Assert.Equal(720, result.Height);
        Assert.Equal("DTS", result.AudioCodec);
    }

    [Fact]
    public void ParseOutput_MissingMediaProperty_ReturnsNull()
    {
        var result = CreateService().ParseOutput("""{ "other": 1 }""", "video.mkv");
        Assert.Null(result);
    }

    [Fact]
    public void ParseOutput_MissingTrackProperty_ReturnsNull()
    {
        var result = CreateService().ParseOutput("""{ "media": { "@ref": "video.mkv" } }""", "video.mkv");
        Assert.Null(result);
    }

    [Fact]
    public void ParseOutput_InvalidJson_ReturnsNull()
    {
        var result = CreateService().ParseOutput("not json at all", "video.mkv");
        Assert.Null(result);
    }

    [Fact]
    public void ParseOutput_MissingIndividualProperties_ReturnsNullFields()
    {
        const string json = """
            {
              "media": {
                "track": [
                  { "@type": "General" },
                  { "@type": "Video", "Format": "AVC" }
                ]
              }
            }
            """;

        var result = CreateService().ParseOutput(json, "video.mkv");

        Assert.NotNull(result);
        Assert.Equal("AVC", result.VideoCodec);
        Assert.Null(result.DurationSeconds);
        Assert.Null(result.AudioCodec);
        Assert.Null(result.Width);
        Assert.Null(result.Height);
        Assert.Null(result.FrameRate);
        Assert.Null(result.BitRate);
    }

    [Fact]
    public void ParseOutput_NonNumericValues_ReturnsNullFields()
    {
        const string json = """
            {
              "media": {
                "track": [
                  { "@type": "General", "Duration": "abc", "OverallBitRate": "" },
                  { "@type": "Video", "Width": "1920.5" }
                ]
              }
            }
            """;

        var result = CreateService().ParseOutput(json, "video.mkv");

        Assert.NotNull(result);
        Assert.Null(result.DurationSeconds);
        Assert.Null(result.BitRate);
        Assert.Null(result.Width);
    }

    [Fact]
    public async Task ProbeAsync_ExecutableNotFound_ReturnsNull()
    {
        var svc = CreateService("portrameta-nonexistent-binary-xyz");

        var result = await svc.ProbeAsync("video.mkv");

        Assert.Null(result);
    }

    [Fact]
    public async Task ProbeAsync_ExecutableNotFound_ShortCircuitsSubsequentCalls()
    {
        var svc = CreateService("portrameta-nonexistent-binary-xyz");

        Assert.Null(await svc.ProbeAsync("first.mkv"));
        Assert.Null(await svc.ProbeAsync("second.mkv"));
    }
}
