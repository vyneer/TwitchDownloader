using HandyControl.Tools.Extension;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using WpfAnimatedGif;
using YoutubeDLSharp;
using System.Collections.Generic;
using Newtonsoft.Json;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace TwitchDownloaderWPF
{
    public enum DownloadType { Clip, Video }
    /// <summary>
    /// Interaction logic for PageChatDownload.xaml
    /// </summary>
    public partial class PageChatDownload : Page
    {

        public DownloadType downloadType;
        public string downloadId;
        public int streamerId;
        public DateTime currentVideoTime;
        public TimeSpan vodLength;
        private CancellationTokenSource _cancellationTokenSource;

        public PageChatDownload()
        {
            InitializeComponent();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(true, false);
        }

        private void SetEnabled(bool isEnabled, bool isClip)
        {
            PullInfo.IsEnabled = isEnabled;
            radioCompressionNone.IsEnabled = isEnabled;
            radioCompressionGzip.IsEnabled = isEnabled;
            emoteFormatGif.IsEnabled = isEnabled;
            emoteFormatWebp.IsEnabled = isEnabled;
            SplitBtnDownload.IsEnabled = isEnabled;
        }

        private void UpdateActionButtons(bool isDownloading)
        {
            if (isDownloading)
            {
                PullInfo.Visibility = Visibility.Collapsed;
                SplitBtnDownload.Visibility = Visibility.Collapsed;
                BtnCancel.Visibility = Visibility.Visible;
                return;
            }
            PullInfo.Visibility = Visibility.Visible;
            SplitBtnDownload.Visibility = Visibility.Visible;
            BtnCancel.Visibility = Visibility.Collapsed;
        }

        public static string ValidateUrl(string text)
        {
            var vodClipIdMatch = Regex.Match(text, @"(?<=^|(?:clips\.)?twitch\.tv\/(?:videos|\S+\/clip)?\/?)[\w-]+?(?=$|\?)");
            return vodClipIdMatch.Success
                ? vodClipIdMatch.Value
                : null;
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.AppendText(message + Environment.NewLine)
            );
        }

        public ChatDownloadOptions GetOptions(string filename)
        {
            ChatDownloadOptions options = new ChatDownloadOptions();

            options.DownloadFormat = ChatFormat.Json;

            if (radioCompressionNone.IsChecked == true)
                options.Compression = ChatCompression.None;
            else if (radioCompressionGzip.IsChecked == true)
                options.Compression = ChatCompression.Gzip;

            if (emoteFormatWebp.IsChecked == true)
                options.WebpEmotes = true;
            else options.WebpEmotes = false;

            options.EmbedData = true;
            options.Filename = filename;
            return options;
        }

        private void OnProgressChanged(ProgressReport progress)
        {
            switch (progress.ReportType)
            {
                case ReportType.Percent:
                    statusProgressBar.Value = (int)progress.Data;
                    break;
                case ReportType.NewLineStatus or ReportType.SameLineStatus:
                    statusMessage.Text = (string)progress.Data;
                    break;
                case ReportType.Log:
                    AppendLog((string)progress.Data);
                    break;
            }
        }

        public void SetImage(string imageUri, bool isGif)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(imageUri, UriKind.Relative);
            image.EndInit();
            if (isGif)
            {
                ImageBehavior.SetAnimatedSource(statusImage, image);
            }
            else
            {
                ImageBehavior.SetAnimatedSource(statusImage, null);
                statusImage.Source = image;
            }
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            WindowSettings settings = new WindowSettings();
            settings.ShowDialog();
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void numChatDownloadConnections_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.Save();
            }
        }

        private void checkEmbed_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedEmotes = true;
                Settings.Default.Save();
            }
        }

        private void checkEmbed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedEmotes = false;
                Settings.Default.Save();
            }
        }

        private async void PullInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!streamURL.Text.Contains("kick.com/video/"))
                {
                    if (!File.Exists("yt-dlp.exe"))
                    {
                        AppendLog("[STATUS] - Installing yt-dlp");
                        await YoutubeDLSharp.Utils.DownloadYtDlp();
                        AppendLog("[STATUS] - yt-dlp installed");
                    }
                    var ytdl = new YoutubeDL();
                    var res = await ytdl.RunVideoDataFetch(streamURL.Text);
                    AppendLog("[STATUS] - Loading info...");
                    YoutubeDLSharp.Metadata.VideoData video = res.Data;
                    if (video.WasLive.Value)
                    {
                        textStreamer.Text = video.Uploader;
                        textTitle.Text = video.Title;
                        imgThumbnail.Source = await ThumbnailService.GetThumb(video.Thumbnail);
                        var st = video.ReleaseTimestamp?.ToUniversalTime();
                        startTime.Text = st?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        endTime.Text = (st?.AddSeconds((double)video.Duration.Value))?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        AppendLog("[STATUS] - Loaded info successfully...");
                    }
                    else
                    {
                        AppendLog("[ERROR] - Invalid video");
                    }
                }
                else
                {
                    var headers = new Dictionary<string, string>(){
                        { "accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8" },
                        { "accept-language", "en-US,en;q=0.5" },
                        { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36" }
                    };
                    var kick = new KickService(headers);
                    var kickVodSplit = streamURL.Text.Split("/");
                    var kickVodId = kickVodSplit[^1];
                    var result = kick.Get($"https://kick.com/api/v1/video/{kickVodId}");
                    if (result.Status == 200)
                    {
                        var video = JsonConvert.DeserializeObject<KickVideoResponse>(result.Body);
                        textStreamer.Text = video.Livestream.Channel.Name;
                        textTitle.Text = video.Livestream.Title;
                        var thumbSplit = video.Source.Split("/");
                        imgThumbnail.Source = await ThumbnailService.GetThumb($"https://images.kick.com/video_thumbnails/{thumbSplit[6]}/{thumbSplit[12]}/720.webp");
                        var st = DateTime.ParseExact(video.Livestream.CreatedAt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                        startTime.Text = st.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        endTime.Text = (st.AddMilliseconds((double)video.Livestream.Duration)).ToString("yyyy-MM-ddTHH:mm:ssZ");
                        AppendLog("[STATUS] - Loaded info successfully...");
                    }
                    else
                    {
                        AppendLog($"[ERROR] - HTTP error {result.Status}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
            }
        }

        private async void SplitBtnDownload_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            if (radioCompressionNone.IsChecked == true)
                saveFileDialog.Filter = "JSON Files | *.json";
            else if (radioCompressionGzip.IsChecked == true)
                saveFileDialog.Filter = "GZip JSON Files | *.json.gz";

            var videoStart = ((DateTimeOffset)DateTime.Parse(startTime.Text));
            var videoEnd = ((DateTimeOffset)DateTime.Parse(endTime.Text));
            vodLength = videoStart - videoEnd;

            saveFileDialog.FileName = FilenameService.GetFilename(Settings.Default.TemplateChat, textTitle.Text, downloadId, DateTime.Parse(startTime.Text).ToUniversalTime(), textStreamer.Text, TimeSpan.Zero, vodLength);

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ChatDownloadOptions downloadOptions = GetOptions(saveFileDialog.FileName);
                downloadOptions.StartTime = startTime.Text;
                downloadOptions.EndTime = endTime.Text;

                ChatDownloader currentDownload = new ChatDownloader(downloadOptions);

                SetEnabled(false, false);

                SetImage("Images/ppOverheat.gif", true);
                statusMessage.Text = Translations.Strings.StatusDone;
                _cancellationTokenSource = new CancellationTokenSource();
                UpdateActionButtons(true);

                Progress<ProgressReport> downloadProgress = new Progress<ProgressReport>(OnProgressChanged);

                try
                {
                    await currentDownload.DownloadAsync(downloadProgress, _cancellationTokenSource.Token);
                    statusMessage.Text = Translations.Strings.StatusDone;
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && _cancellationTokenSource.IsCancellationRequested)
                {
                    statusMessage.Text = Translations.Strings.StatusCanceled;
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex)
                {
                    statusMessage.Text = Translations.Strings.StatusError;
                    SetImage("Images/peepoSad.png", false);
                    AppendLog(Translations.Strings.ErrorLog + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                statusProgressBar.Value = 0;
                _cancellationTokenSource.Dispose();
                UpdateActionButtons(false);
                SetEnabled(true, false);

                currentDownload = null;
                GC.Collect();
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            statusMessage.Text = Translations.Strings.StatusCanceling;
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        private void ClearEmotesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var emoteFolder = Path.Combine(
                        Path.GetTempPath(),
                        Path.Combine("DGGDownloader", "dggEmotes"));

                if (Directory.Exists(emoteFolder))
                {
                    Directory.Delete(emoteFolder, true);
                    AppendLog($"Cleared dgg emote folder - {emoteFolder}");
                } else
                {
                    AppendLog($"No dgg emote folder found - {emoteFolder}");
                }
            } catch { }

        }
    }
}