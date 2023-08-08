using System.Collections.Generic;

namespace TwitchDownloaderCore.DGGObjects
{

    public class DGGEmote
    {
        public string prefix { get; set; }
        public int height { get; set; }
        public int width { get; set; }

        public byte[] imageData { get; set; }
        public List<DGGEmoteImage> image { get; set; }
    }

    public class DGGEmoteImage
    {
        public string url { get; set; }
        public string mime { get; set; }
        public int height { get; set; }
        public int width { get; set; }
    }
}
