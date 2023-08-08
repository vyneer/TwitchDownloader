using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using YoutubeDLSharp;

namespace TwitchDownloaderCLI.Modes
{
    internal static class DownloadChat
    {
        internal static void Download(ChatDownloadArgs inputOptions)
        {
            var downloadOptions = Task.Run<ChatDownloadOptions>(async () => await GetDownloadOptionsAsync(inputOptions));

            ChatDownloader chatDownloader = new(downloadOptions.Result);
            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;
            chatDownloader.DownloadAsync(progress, new CancellationToken()).Wait();
        }

        private static async Task<ChatDownloadOptions> GetDownloadOptionsAsync(ChatDownloadArgs inputOptions)
        {
            if ((inputOptions.StartTime is null || inputOptions.EndTime is null) && inputOptions.URL is null)
            {
                Console.WriteLine("[ERROR] - Please set start/end time or the stream URL");
                Environment.Exit(1);
            }

            if (inputOptions.URL is not null && inputOptions.StartTime is null && inputOptions.EndTime is null)
            {
                if (!File.Exists("yt-dlp") || !File.Exists("yt-dlp.exe"))
                {
                    Console.WriteLine("[STATUS] - Installing yt-dlp");
                    await YoutubeDLSharp.Utils.DownloadYtDlp();
                    Console.WriteLine("[STATUS] - yt-dlp installed");
                }
                var ytdl = new YoutubeDL();
                var res = await ytdl.RunVideoDataFetch(inputOptions.URL);
                Console.WriteLine("[STATUS] - Loading info...");
                YoutubeDLSharp.Metadata.VideoData video = res.Data;
                if (video.WasLive.Value) {
                    var startTime = video.ReleaseTimestamp?.ToUniversalTime();
                    inputOptions.StartTime = startTime?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    inputOptions.EndTime = (startTime?.AddSeconds((double)video.Duration.Value))?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    Console.WriteLine("[STATUS] - Loaded info successfully...");
                } else {
                    Console.WriteLine("[ERROR] - Invalid video");
                    Environment.Exit(1);
                }
            }

            var fileExtension = Path.GetExtension(inputOptions.OutputFile)!.ToLower();

            ChatDownloadOptions downloadOptions = new()
            {
                DownloadFormat = fileExtension switch
                {
                    ".json" => ChatFormat.Json,
                    _ => throw new NotSupportedException($"{fileExtension} is not a valid chat file extension.")
                },
                StartTime = inputOptions.StartTime,
                EndTime = inputOptions.EndTime,
                EmbedData = inputOptions.EmbedData,
                Filename = inputOptions.Compression is ChatCompression.Gzip
                    ? inputOptions.OutputFile + ".gz"
                    : inputOptions.OutputFile,
                Compression = inputOptions.Compression,
                TimeFormat = inputOptions.TimeFormat,
                ConnectionCount = inputOptions.ChatConnections,
                TempFolder = inputOptions.TempFolder
            };

            return downloadOptions;
        }
    }
}