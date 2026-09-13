using Android.Media;
using Java.Nio;

namespace MomentaryMomentos.Services;

/// <summary>
/// Surface-to-surface video re-encoder for Android.
///
/// The trim path (<see cref="VideoTrimService.TrimVideoAsync"/>) is a lossless remux, which keeps
/// the source bitrate — so a 4K clip stays ~85 MB even after it is cut to 10 seconds. This
/// re-encodes at a sane delivery bitrate instead: a decoder renders straight onto the encoder's
/// input Surface, so frames never round-trip through managed memory.
///
/// Resolution is deliberately left alone. Scaling during a surface transcode relies on the buffer
/// queue resizing frames, which Qualcomm's encoder refuses outright — it rejects any input buffer
/// whose dimensions differ from the configured output ("Graphic buf 3840x2160 doesn't match
/// configured 1920x1080") and the codec then throws on the next dequeue. Real downscaling needs a
/// full EGL/GLES pipeline to render a scaled quad, which is a large amount of device-specific code.
///
/// It also isn't necessary: file size is bitrate times duration, and phone cameras record at
/// wasteful editing bitrates (~72 Mbps for 4K). Re-encoding the same 4K frames at 12 Mbps is a
/// ~6x reduction and still comfortably above streaming-quality 4K.
///
/// Audio is copied through untouched. A 10-second AAC track is well under a megabyte, so it is
/// buffered in memory and written after the video, rather than interleaving two live pipelines.
/// </summary>
internal static class VideoCompressor
{
    private const string EncoderMime      = "video/avc";
    private const int    DequeueTimeoutUs = 10_000;

    /// <summary>Bits per pixel per second. 1920x1080 lands at ~8 Mbps, which is visually clean for short clips.</summary>
    private const double BitsPerPixel = 4.0;

    public static string? Compress(
        string inputPath,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var outputPath = Path.Combine(FileSystem.CacheDirectory, $"cmp_{Guid.NewGuid():N}.mp4");

        MediaExtractor? videoExtractor = null;
        MediaCodec?     decoder        = null;
        MediaCodec?     encoder        = null;
        MediaMuxer?     muxer          = null;
        Android.Views.Surface? inputSurface = null;

        try
        {
            videoExtractor = new MediaExtractor();
            videoExtractor.SetDataSource(inputPath);

            var videoTrack = FindTrack(videoExtractor, "video/");
            if (videoTrack < 0) return null;

            var sourceFormat = videoExtractor.GetTrackFormat(videoTrack);
            var sourceWidth  = sourceFormat.GetInteger(MediaFormat.KeyWidth);
            var sourceHeight = sourceFormat.GetInteger(MediaFormat.KeyHeight);
            var durationUs   = sourceFormat.ContainsKey(MediaFormat.KeyDuration)
                ? sourceFormat.GetLong(MediaFormat.KeyDuration)
                : 0L;

            // Output dimensions must match the decoder's, or Qualcomm's encoder rejects every
            // input buffer. Size comes down through bitrate alone.
            var targetBitrate = TargetBitrate(sourceWidth, sourceHeight);
            if (!NeedsBitrateReduction(sourceFormat, targetBitrate))
            {
                // Already efficiently encoded — re-encoding would only lose quality.
                Diagnostics.Trace("video.compress", "source bitrate already low; skipping re-encode");
                return null;
            }

            var targetFormat = MediaFormat.CreateVideoFormat(EncoderMime, sourceWidth, sourceHeight)!;
            targetFormat.SetInteger(MediaFormat.KeyColorFormat, (int)MediaCodecCapabilities.Formatsurface);
            targetFormat.SetInteger(MediaFormat.KeyBitRate, targetBitrate);
            targetFormat.SetInteger(MediaFormat.KeyFrameRate, FrameRateOf(sourceFormat));
            targetFormat.SetInteger(MediaFormat.KeyIFrameInterval, 1);

            Diagnostics.Trace("video.compress",
                $"re-encoding {sourceWidth}x{sourceHeight} at {targetBitrate / 1_000_000.0:F1} Mbps");

            encoder = MediaCodec.CreateEncoderByType(EncoderMime);
            encoder!.Configure(targetFormat, null, null, MediaCodecConfigFlags.Encode);
            inputSurface = encoder.CreateInputSurface();
            encoder.Start();

            var sourceMime = sourceFormat.GetString(MediaFormat.KeyMime)!;
            decoder = MediaCodec.CreateDecoderByType(sourceMime);
            decoder!.Configure(sourceFormat, inputSurface, null, 0);
            decoder.Start();

            videoExtractor.SelectTrack(videoTrack);

            muxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
            muxer.SetOrientationHint(RotationOf(sourceFormat, inputPath));

            var audio = ReadAudioTrack(inputPath);

            var muxerVideoTrack = TranscodeVideo(
                videoExtractor, decoder, encoder, muxer, audio, durationUs, progress, ct);

            if (muxerVideoTrack < 0) return null;

            progress?.Report(1.0);
            muxer.Stop();

            return File.Exists(outputPath) ? outputPath : null;
        }
        catch (OperationCanceledException)
        {
            TryDelete(outputPath);
            throw;
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, "video.compress", new Dictionary<string, string>
            {
                ["inputPath"] = inputPath,
            });
            TryDelete(outputPath);
            return null;
        }
        finally
        {
            try { decoder?.Stop(); decoder?.Release(); } catch { /* ignored */ }
            try { encoder?.Stop(); encoder?.Release(); } catch { /* ignored */ }
            try { inputSurface?.Release(); }            catch { /* ignored */ }
            try { muxer?.Release(); }                   catch { /* ignored */ }
            try { videoExtractor?.Release(); }          catch { /* ignored */ }
        }
    }

    /// <summary>
    /// Pumps the decoder into the encoder's input surface and the encoder into the muxer.
    /// Returns the muxer's video track index, or -1 if the encoder never produced a format.
    /// </summary>
    private static int TranscodeVideo(
        MediaExtractor extractor,
        MediaCodec decoder,
        MediaCodec encoder,
        MediaMuxer muxer,
        List<AudioSample> audio,
        long durationUs,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var bufferInfo      = new MediaCodec.BufferInfo();
        var muxerVideoTrack = -1;
        var muxerAudioTrack = -1;
        var muxerStarted    = false;
        var inputDone       = false;
        var decoderDone     = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // ── Feed the decoder from the extractor ──────────────────────────
            if (!inputDone)
            {
                var inIndex = decoder.DequeueInputBuffer(DequeueTimeoutUs);
                if (inIndex >= 0)
                {
                    var buffer = decoder.GetInputBuffer(inIndex)!;
                    var size   = extractor.ReadSampleData(buffer, 0);

                    if (size < 0)
                    {
                        decoder.QueueInputBuffer(inIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                        inputDone = true;
                    }
                    else
                    {
                        decoder.QueueInputBuffer(inIndex, 0, size, extractor.SampleTime, 0);
                        extractor.Advance();
                    }
                }
            }

            // ── Decoder output goes straight onto the encoder's surface ──────
            if (!decoderDone)
            {
                var outIndex = decoder.DequeueOutputBuffer(bufferInfo, DequeueTimeoutUs);
                if (outIndex >= 0)
                {
                    var endOfStream = bufferInfo.Flags.HasFlag(MediaCodecBufferFlags.EndOfStream);

                    // render:true hands the frame to the encoder's input surface
                    decoder.ReleaseOutputBuffer(outIndex, render: bufferInfo.Size > 0);

                    if (endOfStream)
                    {
                        encoder.SignalEndOfInputStream();
                        decoderDone = true;
                    }
                }
            }

            // ── Drain the encoder into the muxer ─────────────────────────────
            var encIndex = encoder.DequeueOutputBuffer(bufferInfo, DequeueTimeoutUs);
            if (encIndex == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                if (!muxerStarted)
                {
                    muxerVideoTrack = muxer.AddTrack(encoder.OutputFormat!);
                    if (audio.Count > 0 && audio[0].Format is not null)
                        muxerAudioTrack = muxer.AddTrack(audio[0].Format!);

                    muxer.Start();
                    muxerStarted = true;
                }
            }
            else if (encIndex >= 0)
            {
                var encoded = encoder.GetOutputBuffer(encIndex)!;

                // Codec config bytes belong in the track format, not the stream.
                if (bufferInfo.Flags.HasFlag(MediaCodecBufferFlags.CodecConfig))
                {
                    bufferInfo.Size = 0;
                }

                if (bufferInfo.Size > 0 && muxerStarted)
                {
                    encoded.Position(bufferInfo.Offset);
                    encoded.Limit(bufferInfo.Offset + bufferInfo.Size);
                    muxer.WriteSampleData(muxerVideoTrack, encoded, bufferInfo);

                    if (durationUs > 0)
                        progress?.Report(Math.Clamp((double)bufferInfo.PresentationTimeUs / durationUs, 0, 0.99));
                }

                var encoderDone = bufferInfo.Flags.HasFlag(MediaCodecBufferFlags.EndOfStream);
                encoder.ReleaseOutputBuffer(encIndex, render: false);

                if (encoderDone)
                {
                    WriteAudio(muxer, muxerAudioTrack, audio);
                    return muxerVideoTrack;
                }
            }
        }
    }

    // ── Audio passthrough ────────────────────────────────────────────────────

    private sealed record AudioSample(byte[] Data, long TimeUs, MediaCodecBufferFlags Flags, MediaFormat? Format);

    /// <summary>
    /// Reads the compressed audio track into memory so it can be muxed after the video without
    /// running two live pipelines. Short clips only — a 10-second AAC track is a few hundred KB.
    /// </summary>
    private static List<AudioSample> ReadAudioTrack(string inputPath)
    {
        var samples = new List<AudioSample>();
        MediaExtractor? extractor = null;

        try
        {
            extractor = new MediaExtractor();
            extractor.SetDataSource(inputPath);

            var track = FindTrack(extractor, "audio/");
            if (track < 0) return samples;

            var format = extractor.GetTrackFormat(track);
            extractor.SelectTrack(track);

            var buffer = ByteBuffer.Allocate(256 * 1024)!;
            while (true)
            {
                buffer.Clear();
                var size = extractor.ReadSampleData(buffer, 0);
                if (size < 0) break;

                var bytes = new byte[size];
                buffer.Position(0);
                buffer.Get(bytes, 0, size);

                samples.Add(new AudioSample(
                    bytes,
                    extractor.SampleTime,
                    (MediaCodecBufferFlags)extractor.SampleFlags,
                    samples.Count == 0 ? format : null));

                extractor.Advance();
            }
        }
        catch (Exception ex)
        {
            // Audio is a nice-to-have; a silent momento beats a failed import.
            Diagnostics.Report(ex, "video.compress.audio");
            samples.Clear();
        }
        finally
        {
            try { extractor?.Release(); } catch { /* ignored */ }
        }

        return samples;
    }

    private static void WriteAudio(MediaMuxer muxer, int audioTrack, List<AudioSample> samples)
    {
        if (audioTrack < 0 || samples.Count == 0) return;

        var info = new MediaCodec.BufferInfo();
        foreach (var sample in samples)
        {
            var buffer = ByteBuffer.Wrap(sample.Data)!;
            info.Offset              = 0;
            info.Size                = sample.Data.Length;
            info.PresentationTimeUs  = sample.TimeUs;
            info.Flags               = sample.Flags;
            muxer.WriteSampleData(audioTrack, buffer, info);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int FindTrack(MediaExtractor extractor, string mimePrefix)
    {
        for (var i = 0; i < extractor.TrackCount; i++)
        {
            var mime = extractor.GetTrackFormat(i).GetString(MediaFormat.KeyMime) ?? string.Empty;
            if (mime.StartsWith(mimePrefix, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    /// <summary>
    /// A delivery bitrate for the given frame size. Caps at 12 Mbps, which keeps a 10-second clip
    /// near 15 MB at any resolution while still looking good on a phone.
    /// </summary>
    private static int TargetBitrate(int width, int height)
        => (int)Math.Clamp(width * (double)height * BitsPerPixel, 1_000_000, 12_000_000);

    /// <summary>
    /// True when the source is wastefully encoded enough to be worth re-encoding. When the format
    /// omits the bitrate we assume it is worth it — we only get here because the file is oversized.
    /// </summary>
    private static bool NeedsBitrateReduction(MediaFormat format, int targetBitrate)
    {
        try
        {
            if (!format.ContainsKey(MediaFormat.KeyBitRate)) return true;
            return format.GetInteger(MediaFormat.KeyBitRate) > targetBitrate * 1.2;
        }
        catch
        {
            return true;
        }
    }

    private static int FrameRateOf(MediaFormat format)
    {
        try
        {
            if (format.ContainsKey(MediaFormat.KeyFrameRate))
            {
                var fps = format.GetInteger(MediaFormat.KeyFrameRate);
                // 60fps doubles the size for no benefit on a 10-second memento.
                if (fps > 0) return Math.Min(fps, 30);
            }
        }
        catch { /* some devices store frame-rate as a float and throw here */ }
        return 30;
    }

    private static int RotationOf(MediaFormat format, string inputPath)
    {
        try
        {
            if (format.ContainsKey("rotation-degrees"))
                return format.GetInteger("rotation-degrees");
        }
        catch { /* fall through to the retriever */ }

        try
        {
            using var retriever = new MediaMetadataRetriever();
            retriever.SetDataSource(inputPath);
            if (int.TryParse(retriever.ExtractMetadata(MetadataKey.VideoRotation), out var degrees))
                return degrees;
        }
        catch { /* orientation is cosmetic — never fail the transcode over it */ }

        return 0;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignored */ }
    }
}
