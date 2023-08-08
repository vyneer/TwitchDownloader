using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal static class DownloadChat
    {
        internal static void Download(ChatDownloadArgs inputOptions)
        {
            var downloadOptions = GetDownloadOptions(inputOptions);

            ChatDownloader chatDownloader = new(downloadOptions);
            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;
            chatDownloader.DownloadAsync(progress, new CancellationToken()).Wait();
        }

        private static ChatDownloadOptions GetDownloadOptions(ChatDownloadArgs inputOptions)
        {
            if (inputOptions.StartTime is null || inputOptions.EndTime is null)
            {
                Console.WriteLine("[ERROR] - Start or end time cannot be null!");
                Environment.Exit(1);
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