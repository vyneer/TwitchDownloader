using AutoUpdaterDotNET;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using TwitchDownloaderWPF.Properties;
using Xabe.FFmpeg.Downloader;
using Newtonsoft.Json.Linq;
using static TwitchDownloaderWPF.App;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static PageChatDownload pageChatDownload = new PageChatDownload();
        public static PageChatRender pageChatRender = new PageChatRender();

        public MainWindow()
        {
            InitializeComponent();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        private void btnChatDownload_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = pageChatDownload;
        }

        private void btnChatRender_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = pageChatRender;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AppSingleton.RequestAppThemeChange();

            Main.Content = pageChatDownload;
            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }

            if (!File.Exists("ffmpeg.exe"))
            {
                try
                {
                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full);
                }
                catch (Exception ex)
                {
                    if (MessageBox.Show(string.Format(Translations.Strings.UnableToDownloadFfmpegFull, "https://ffmpeg.org/download.html" , $"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}ffmpeg.exe"),
                            Translations.Strings.UnableToDownloadFfmpeg, MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                    {
                        Process.Start(new ProcessStartInfo("https://ffmpeg.org/download.html") { UseShellExecute = true });
                    }

                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            Version currentVersion = new Version("0.2.1");
            Title = $"DGG Downloader v{currentVersion}";
#if !DEBUG
            if (AppContext.BaseDirectory.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
            {
                // If the app is in user profile, the updater probably doesn't need administrator permissions
                AutoUpdater.RunUpdateAsAdmin = false;
            }
            AutoUpdater.HttpUserAgent = "AutoUpdater";
            AutoUpdater.InstalledVersion = currentVersion;
            AutoUpdater.ParseUpdateInfoEvent += AutoUpdaterOnParseUpdateInfoEvent;
            AutoUpdater.Start("https://api.github.com/repos/vyneer/TwitchDownloader/releases/latest");
#endif
        }

        private void AutoUpdaterOnParseUpdateInfoEvent(ParseUpdateInfoEventArgs args)
        {
            var windowsRegex = new Regex(@"DGGDownloaderGUI-Debug-\d\.\d\.\d-Windows-x64", RegexOptions.IgnoreCase);
            JObject json = JObject.Parse(args.RemoteData);
            var assets = json["assets"].Value<JArray>();
            List<dynamic> lst = assets.ToObject<List<dynamic>>();
            var correct = lst.Find(a => windowsRegex.IsMatch((string)a["name"]));
            args.UpdateInfo = new UpdateInfoEventArgs
            {
                CurrentVersion = json["tag_name"].Value<string>(),
                ChangelogURL = json["html_url"].Value<string>(),
                DownloadURL = correct.browser_download_url,
            };
        }
    }
}
