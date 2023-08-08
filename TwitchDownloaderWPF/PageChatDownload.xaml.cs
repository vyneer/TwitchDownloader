using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
            numChatDownloadConnections.Value = Settings.Default.ChatDownloadThreads;
        }

        private void SetEnabled(bool isEnabled, bool isClip)
        {
            radioTimestampRelative.IsEnabled = isEnabled;
            radioTimestampUTC.IsEnabled = isEnabled;
            radioTimestampNone.IsEnabled = isEnabled;
            radioCompressionNone.IsEnabled = isEnabled;
            radioCompressionGzip.IsEnabled = isEnabled;
            SplitBtnDownload.IsEnabled = isEnabled;
        }

        private void UpdateActionButtons(bool isDownloading)
        {
            if (isDownloading)
            {
                SplitBtnDownload.Visibility = Visibility.Collapsed;
                BtnCancel.Visibility = Visibility.Visible;
                return;
            }
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

            options.EmbedData = true;
            options.Filename = filename;
            options.ConnectionCount = (int)numChatDownloadConnections.Value;
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
                numChatDownloadConnections.Value = Math.Clamp((int)numChatDownloadConnections.Value, 1, 50);
                Settings.Default.ChatDownloadThreads = (int)numChatDownloadConnections.Value;
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

        private async void SplitBtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (((HandyControl.Controls.SplitButton)sender).IsDropDownOpen)
            {
                return;
            }

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

                if (radioTimestampUTC.IsChecked == true)
                    downloadOptions.TimeFormat = TimestampFormat.Utc;
                else if (radioTimestampRelative.IsChecked == true)
                    downloadOptions.TimeFormat = TimestampFormat.Relative;
                else if (radioTimestampNone.IsChecked == true)
                    downloadOptions.TimeFormat = TimestampFormat.None;

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
    }
}