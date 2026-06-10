using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PortraMeta.Core.Interfaces;

namespace PortraMeta.Data.Services;

public class MediaInfoService(ILogger<MediaInfoService> logger) : IMediaInfoService
{
    private bool? _available;

    internal string ExecutableName { get; init; } = "mediainfo";

    public async Task<MediaInfoResult?> ProbeAsync(string filePath, CancellationToken ct = default)
    {
        if (_available == false) return null;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ExecutableName,
                    ArgumentList = { "--Output=JSON", filePath },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeoutCts.Token);

            if (process.ExitCode != 0)
            {
                logger.LogWarning("mediainfo exited with code {Code} for {File}", process.ExitCode, filePath);
                return null;
            }

            _available = true;
            return ParseOutput(output, filePath);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // mediainfo not installed
            if (_available != false)
            {
                logger.LogWarning("mediainfo is not installed or not on PATH; media info extraction disabled");
                _available = false;
            }
            return null;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("mediainfo timed out for {File}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to run mediainfo for {File}", filePath);
            return null;
        }
    }

    internal MediaInfoResult? ParseOutput(string json, string filePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("media", out var media)
                || !media.TryGetProperty("track", out var tracks))
                return null;

            JsonElement? general = null, video = null, audio = null;

            foreach (var track in tracks.EnumerateArray())
            {
                var type = track.GetProperty("@type").GetString();
                switch (type)
                {
                    case "General": general = track; break;
                    case "Video": video ??= track; break;  // first video stream
                    case "Audio": audio ??= track; break;  // first audio stream
                }
            }

            return new MediaInfoResult(
                DurationSeconds: ParseDouble(general, "Duration"),
                VideoCodec: GetString(video, "Format"),
                AudioCodec: GetString(audio, "Format"),
                Width: ParseInt(video, "Width"),
                Height: ParseInt(video, "Height"),
                FrameRate: ParseDouble(video, "FrameRate"),
                BitRate: ParseLong(general, "OverallBitRate")
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse mediainfo output for {File}", filePath);
            return null;
        }
    }

    private static string? GetString(JsonElement? element, string property)
    {
        if (element?.TryGetProperty(property, out var val) == true)
            return val.GetString();
        return null;
    }

    private static double? ParseDouble(JsonElement? element, string property)
    {
        var s = GetString(element, property);
        return s is not null && double.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static int? ParseInt(JsonElement? element, string property)
    {
        var s = GetString(element, property);
        return s is not null && int.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static long? ParseLong(JsonElement? element, string property)
    {
        var s = GetString(element, property);
        return s is not null && long.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
