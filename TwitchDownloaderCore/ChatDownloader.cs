using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.DGGObjects;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class ChatDownloader
    {
        private readonly ChatDownloadOptions downloadOptions;
        private static HttpClient httpClient = new HttpClient();

        public ChatDownloader(ChatDownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "DGGDownloader");
        }

        private static async Task DownloadSection(string chatStart, string chatEnd, List<DGGEmote> emotes, SortedSet<Comment> comments, object commentLock, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            DateTime chatStartDT = DateTime.Parse(chatStart);
            int errorCount = 0;

            List<DGGFlair> dggFlairsResponse = new List<DGGFlair>();
            try
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri("https://cdn.destiny.gg/flairs/flairs.json"),
                    Method = HttpMethod.Get,
                };

                using (var httpResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    httpResponse.EnsureSuccessStatusCode();
                    dggFlairsResponse = await httpResponse.Content.ReadFromJsonAsync<List<DGGFlair>>(options: null, cancellationToken);
                }

                errorCount = 0;
            }
            catch (HttpRequestException)
            {
                if (++errorCount > 10)
                {
                    throw;
                }

                await Task.Delay(1_000 * errorCount, cancellationToken);
            }

            List<VyneerChatLog> commentResponse = new List<VyneerChatLog>();
            try
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"https://vyneer.me/tools/rawlogs?from={chatStart}&to={chatEnd}"),
                    Method = HttpMethod.Get,
                };

                using (var httpResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    httpResponse.EnsureSuccessStatusCode();
                    commentResponse = await httpResponse.Content.ReadFromJsonAsync<List<VyneerChatLog>>(options: null, cancellationToken);
                }

                errorCount = 0;
            }
            catch (HttpRequestException)
            {
                if (++errorCount > 10)
                {
                    throw;
                }

                await Task.Delay(1_000 * errorCount, cancellationToken);
            }

            progress.Report(new ProgressReport(ReportType.NewLineStatus, "Processing chat messages..."));
            var convertedComments = ConvertComments(commentResponse, dggFlairsResponse, emotes, chatStartDT);
            lock (commentLock)
            {
                foreach (var comment in convertedComments)
                {
                    comments.Add(comment);
                }
            }
            progress.Report(new ProgressReport(ReportType.NewLineStatus, "Processing done"));
        }

        private static List<Comment> ConvertComments(List<VyneerChatLog> logs, List<DGGFlair> allFlairs, List<DGGEmote> allEmotes, DateTime startTime)
        {
            List<Comment> returnList = new List<Comment>();
            var comboCount = 0;
            for (int i = 0; i < logs.Count; i++)
            {
                if (i > 0 && logs[i].message == logs[i-1].message && allEmotes.Any(e => e.prefix == logs[i].message))
                {
                    comboCount = comboCount == 0 ? 2 : comboCount + 1;
                } else {
                    comboCount = 0;
                }
                logs[i].comboCount = comboCount;
            }
            for (int i = logs.Count - 1; i >= 0 ; i--)
            {
                if (i < logs.Count - 1 && logs[i+1].comboCount == 2)
                {
                    logs[i].comboCount = 1;
                }
            }
            for (int i = 0; i < logs.Count; i++)
            {
                Comment newComment = new Comment();
                var oldComment = logs[i];
                newComment._id = Guid.NewGuid().ToString();
                newComment.created_at = DateTime.Parse(oldComment.time);
                newComment.content_offset_seconds = (newComment.created_at - startTime).TotalMilliseconds / 1000.0;
                Commenter commenter = new Commenter();
                commenter.display_name = oldComment.username;
                newComment.commenter = commenter;
                Message message = new Message();
                message.body = oldComment.message;
                message.bits_spent = 0;
                message.fragments = new List<Fragment>();
                message.fragments.Add(new Fragment(){
                    text = oldComment.message
                });
                message.user_badges = oldComment.features.Substring(1, oldComment.features.Length-2).Split(",");
                List<DGGFlair> flairs = new List<DGGFlair>();
                foreach (var item in message.user_badges)
                {
                    var flair = allFlairs.Find(i => i.name == item);
                    if (flair is not null)
                    {
                        flairs.Add(flair);
                    }
                }
                flairs = flairs.OrderBy(i => i.priority).ToList();
                message.user_color = flairs.Count > 0 ? flairs[0].color : "#dcdcdc";
                message.user_color = message.user_color == "" ? "#dcdcdc" : message.user_color;
                message.rainbow_color = flairs.Count > 0 && flairs[0].rainbowColor;
                message.comboCount = oldComment.comboCount;
                newComment.message = message;

                returnList.Add(newComment);
            }

            return returnList;
        }

        public async Task DownloadAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(downloadOptions.StartTime) || string.IsNullOrWhiteSpace(downloadOptions.EndTime))
            {
                throw new NullReferenceException("You have to set both start time and end time");
            }

            List<Comment> comments = new List<Comment>();
            ChatRoot chatRoot = new() { FileInfo = new() { Version = ChatRootVersion.CurrentVersion, CreatedAt = DateTime.Now }, streamer = new(), video = new(), comments = comments };

            double videoStart = ((DateTimeOffset)DateTime.Parse(downloadOptions.StartTime)).ToUnixTimeSeconds();
            double videoEnd = ((DateTimeOffset)DateTime.Parse(downloadOptions.EndTime)).ToUnixTimeSeconds();
            double videoDuration = 0.0;
            int connectionCount = downloadOptions.ConnectionCount;

            // GqlVideoResponse videoInfoResponse = await TwitchHelper.GetVideoInfo(int.Parse(videoId));
            //     if (videoInfoResponse.data.video == null)
            //     {
            //         throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
            //     }

            //     chatRoot.streamer.name = videoInfoResponse.data.video.owner.displayName;
            //     chatRoot.streamer.id = int.Parse(videoInfoResponse.data.video.owner.id);
            //     videoTitle = videoInfoResponse.data.video.title;
            //     videoCreatedAt = videoInfoResponse.data.video.createdAt;
            //     videoStart = downloadOptions.CropBeginning ? downloadOptions.CropBeginningTime : 0.0;
            //     videoEnd = downloadOptions.CropEnding ? downloadOptions.CropEndingTime : videoInfoResponse.data.video.lengthSeconds;
            //     videoTotalLength = videoInfoResponse.data.video.lengthSeconds;

            //     GqlVideoChapterResponse videoChapterResponse = await TwitchHelper.GetVideoChapters(int.Parse(videoId));
            //     foreach (var responseChapter in videoChapterResponse.data.video.moments.edges)
            //     {
            //         VideoChapter chapter = new()
            //         {
            //             id = responseChapter.node.id,
            //             startMilliseconds = responseChapter.node.positionMilliseconds,
            //             lengthMilliseconds = responseChapter.node.durationMilliseconds,
            //             _type = responseChapter.node._type,
            //             description = responseChapter.node.description,
            //             subDescription = responseChapter.node.subDescription,
            //             thumbnailUrl = responseChapter.node.thumbnailURL,
            //             gameId = responseChapter.node.details.game?.id ?? null,
            //             gameDisplayName = responseChapter.node.details.game?.displayName ?? null,
            //             gameBoxArtUrl = responseChapter.node.details.game?.boxArtURL ?? null
            //         };
            //         chatRoot.video.chapters.Add(chapter);
            //     }

            videoDuration = videoEnd - videoStart;
            chatRoot.video.start = 0;
            chatRoot.video.end = videoDuration;
            chatRoot.video.length = videoDuration;

            SortedSet<Comment> commentsSet = new SortedSet<Comment>(new SortedCommentComparer());
            object commentLock = new object();
            List<Task> tasks = new List<Task>();
            List<int> percentages = new List<int>(connectionCount);
            List<DGGEmote> dggEmotes = new List<DGGEmote>();

            if (downloadOptions.EmbedData && (downloadOptions.DownloadFormat is ChatFormat.Json))
            {
                progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Downloading + Embedding Images" });
                chatRoot.embeddedData = new EmbeddedData();

                dggEmotes = await EmoteHelper.GetDGGEmoteData(downloadOptions.TempFolder, downloadOptions.WebpEmotes, progress, cancellationToken: cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                foreach (DGGEmote emote in dggEmotes)
                {
                    EmbedEmoteData newEmote = new EmbedEmoteData();
                    newEmote.id = emote.prefix;
                    newEmote.imageScale = 1;
                    newEmote.data = emote.imageData;
                    newEmote.name = emote.prefix;
                    newEmote.width = emote.width;
                    newEmote.height = emote.height;
                    chatRoot.embeddedData.thirdParty.Add(newEmote);
                }
            }

            tasks.Add(DownloadSection(downloadOptions.StartTime, downloadOptions.EndTime, dggEmotes, commentsSet, commentLock, progress, cancellationToken));
            await Task.WhenAll(tasks);

            comments = commentsSet.DistinctBy(x => x._id).ToList();
            chatRoot.comments = comments;

            progress.Report(new ProgressReport(ReportType.NewLineStatus, "Writing output file"));
            switch (downloadOptions.DownloadFormat)
            {
                case ChatFormat.Json:
                    await ChatJson.SerializeAsync(downloadOptions.Filename, chatRoot, downloadOptions.Compression, cancellationToken);
                    break;
                default:
                    throw new NotImplementedException("Requested output chat format is not implemented");
            }
        }
    }
}