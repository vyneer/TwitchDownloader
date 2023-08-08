using TwitchDownloaderCore.Chat;

namespace TwitchDownloaderCore.Options
{
    public class ChatDownloadOptions
    {
        public ChatFormat DownloadFormat { get; set; } = ChatFormat.Json;
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Filename { get; set; }
        public ChatCompression Compression { get; set; } = ChatCompression.None;
        public bool EmbedData { get; set; }
        public int ConnectionCount { get; set; } = 1;
        public TimestampFormat TimeFormat { get; set; }
        public string FileExtension
        {
            get
            {
                return DownloadFormat switch
                {
                    ChatFormat.Json when Compression is ChatCompression.None => "json",
                    ChatFormat.Json when Compression is ChatCompression.Gzip => "json.gz",
                    _ => ""
                };
            }
        }
        public string TempFolder { get; set; }
    }
}
