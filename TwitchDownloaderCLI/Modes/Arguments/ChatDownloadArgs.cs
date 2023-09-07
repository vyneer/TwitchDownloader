using CommandLine;
using TwitchDownloaderCore.Chat;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("chatdownload", HelpText = "Downloads the chat from a VOD or clip")]
    public class ChatDownloadArgs
    {
        [Option('u', "url", Required = false, HelpText = "Stream URL.")]
        public string URL { get; set; }

        [Option('s', "startTime", Required = false, HelpText = "Chat start time.")]
        public string StartTime { get; set; }

        [Option('e', "endTime", Required = false, HelpText = "Chat end time.")]
        public string EndTime { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file. File extension will be used to determine download type. Valid extensions are: .json, .html, and .txt.")]
        public string OutputFile { get; set; }

        [Option("compression", Default = ChatCompression.None, HelpText = "Compresses an output json chat file using a specified compression, usually resulting in 40-90% size reductions. Valid values are: None, Gzip.")]
        public ChatCompression Compression { get; set; }
        
        [Option('E', "embed-images", Default = false, HelpText = "Embed first party emotes, badges, and cheermotes into the chat download for offline rendering.")]
        public bool EmbedData { get; set; }

        [Option("timestamp-format", Default = TimestampFormat.Relative, HelpText = "Sets the timestamp format for .txt chat logs. Valid values are: Utc, UtcFull, Relative, and None")]
        public TimestampFormat TimeFormat { get; set; }

        [Option("chat-connections", Default = 4, HelpText = "Number of downloading connections for chat")]
        public int ChatConnections { get; set; }

        [Option("temp-path", Default = "", HelpText = "Path to temporary folder to use for cache.")]
        public string TempFolder { get; set; }

        [Option("webp-emotes", Default = false, HelpText = "Render animated emotes as webp instead of gif.")]
        public bool WebpEmotes { get; set; }

        [Option("ffmpeg-path", HelpText = "Path to ffmpeg executable.")]
        public string FfmpegPath { get; set; }
    }
}
