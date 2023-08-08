using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.DGGObjects
{
	public class VyneerChatLog
	{
		public string time { get; set; }
		public string username { get; set; }
		public string features { get; set; }
		public string message { get; set; }
		public int comboCount { get; set; }
	}
}
