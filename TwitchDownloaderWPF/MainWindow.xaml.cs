using AutoUpdaterDotNET;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using TwitchDownloaderWPF.Properties;
using Xabe.FFmpeg.Downloader;
using static TwitchDownloaderWPF.App;

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

            Version currentVersion = new Version("0.1.0");
            Title = $"DGG Downloader v{currentVersion}";
// #if !DEBUG
//             if (AppContext.BaseDirectory.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
//             {
//                 // If the app is in user profile, the updater probably doesn't need administrator permissions
//                 AutoUpdater.RunUpdateAsAdmin = false;
//             }
//             AutoUpdater.Start("https://downloader-update.twitcharchives.workers.dev");
// #endif
        }
    }
}
