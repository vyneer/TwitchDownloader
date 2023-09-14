using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.DGGObjects
{
    public class DGGFlair {
		public string name { get; set; }
		public bool hidden { get; set; }
		public int priority { get; set; }
		public string color { get; set; }
		public bool rainbowColor { get; set; }
	}
}
