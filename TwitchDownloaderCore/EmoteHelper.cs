using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;
using ImageMagick;
using TwitchDownloaderCore.DGGObjects;

namespace TwitchDownloaderCore
{
    public static class EmoteHelper
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<List<DGGEmote>> GetDGGEmoteData(string tempFolder, IProgress<ProgressReport> progress, CancellationToken cancellationToken = new())
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<DGGEmote> emoteResponse = new();

            var emoteRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("https://cdn.destiny.gg/emotes/emotes.json", UriKind.Absolute));
            using var emoteHTTPResponse = await httpClient.SendAsync(emoteRequest, HttpCompletionOption.ResponseHeadersRead);
            emoteHTTPResponse.EnsureSuccessStatusCode();
            var DGGEmotes = await emoteHTTPResponse.Content.ReadFromJsonAsync<List<DGGEmote>>();

			int premadeEmotes = 0;

			string htmlBody = "";

			foreach (var item in DGGEmotes)
			{
				if (Directory.Exists($"{tempFolder}/dggEmotes"))
				{
					if (File.Exists($"{tempFolder}/dggEmotes/{item.prefix}.png") || File.Exists($"{tempFolder}/dggEmotes/{item.prefix}.gif"))
					{
						premadeEmotes++;
					}
				}
				htmlBody += $@"<div class=""emote {item.prefix}"" style=""display: none;""></div>";
			}

			progress.Report(new ProgressReport(ReportType.NewLineStatus, "Downloading headless Chromium"));
			using var browserFetcher = new BrowserFetcher();
			await browserFetcher.DownloadAsync();
			progress.Report(new ProgressReport(ReportType.NewLineStatus, "Download finished"));
			await using var browser = await Puppeteer.LaunchAsync(
				new LaunchOptions { Headless = true });
			var page = await browser.NewPageAsync();

			var htmlHeader = @"<meta charset=""utf-8""><title>emote</title>
  						<link rel=""stylesheet"" href=""https://cdn.destiny.gg/emotes/emotes.css"">
  						<link rel=""stylesheet"" href=""https://cdn.destiny.gg/flairs/flairs.css"">";
			
			await page.SetContentAsync($"<!DOCTYPE html><html><head>{htmlHeader}</head><body style='margin: 0;'>{htmlBody}</body></html>");

			var cdp = await page.Target.CreateCDPSessionAsync();

			await cdp.SendAsync("Emulation.setDefaultBackgroundColorOverride", new {
									color = new {
										r = 0,
										g = 0,
										b = 0,
										a = 0,
									},
								});

			if (premadeEmotes != DGGEmotes.Count)
			{
				progress.Report(new ProgressReport(ReportType.NewLineStatus, "Converting dgg emotes: 0%"));
				if (Directory.Exists($"{tempFolder}/dggEmotes"))
				{
					Directory.Delete($"{tempFolder}/dggEmotes", true);
				}
				Directory.CreateDirectory($"{tempFolder}/dggEmotes");
				var doneCount = 0;
				foreach (var item in DGGEmotes)
				{
					var fileType = item.image[0].url.Substring(item.image[0].url.Length - 3);
					if (Directory.Exists($"{tempFolder}/dggEmotes/temp-{item.prefix}"))
					{
						Directory.Delete($"{tempFolder}/dggEmotes/temp-{item.prefix}", true);
					}
					Directory.CreateDirectory($"{tempFolder}/dggEmotes/temp-{item.prefix}");
					var width = await page.EvaluateExpressionAsync<decimal>($"(() => {{return parseFloat(window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('width').slice(0, -2)) + parseInt(window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('margin-right').slice(0, -2))}})()");
					var height = await page.EvaluateExpressionAsync<decimal>($"(() => {{return parseFloat(window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('height').slice(0, -2))}})()");
					var animationDuration = await page.EvaluateExpressionAsync<double>($@"(() => {{
          									const dur = window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('animation-duration').split(',').reduce((acc, cur) => acc + parseFloat(cur.slice(0, -1)), 0)
          									const iter = window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('animation-iteration-count').split(',').reduce((acc, cur) => acc + parseFloat(cur), 0)
          									const delay = window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('animation-delay').split(',').reduce((acc, cur) => acc + parseFloat(cur.slice(0, -1)), 0)
          									return (dur * iter) + (delay < 0 ? 0 : delay)
        								}})()");
					var animationDurationBefore = await page.EvaluateExpressionAsync<double>($@"(() => {{
          									const dur = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::before').getPropertyValue('animation-duration').split(',').reduce((acc, cur) => acc + parseFloat(cur.slice(0, -1)), 0)
          									const iter = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::before').getPropertyValue('animation-iteration-count').split(',').reduce((acc, cur) => acc + parseFloat(cur), 0)
          									const delay = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::before').getPropertyValue('animation-delay').split(',').reduce((acc, cur) => acc + parseFloat(cur.slice(0, -1)), 0)
          									return (dur * iter) + (delay < 0 ? 0 : delay)
        								}})()");
					var animationDurationAfter = await page.EvaluateExpressionAsync<double>($@"(() => {{
          									const dur = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::after').getPropertyValue('animation-duration').split(',').reduce((acc, cur) => acc + parseFloat(cur.slice(0, -1)), 0)
          									const iter = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::after').getPropertyValue('animation-iteration-count').split(',').reduce((acc, cur) => acc + parseFloat(cur), 0)
          									const delay = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::after').getPropertyValue('animation-delay').split(',').reduce((acc, cur) => acc + parseFloat(cur.slice(0, -1)), 0)
          									return (dur * iter) + (delay < 0 ? 0 : delay)
        								}})()");
					var dur = animationDuration + animationDurationAfter + animationDurationBefore;

					if (dur != 0)
					{
						await page.EvaluateExpressionAsync($@"(() => {{
								const emote = document.querySelector('.{item.prefix}')
								emote.style.display = ''
							}})()");
						Thread.Sleep(1000);

						await page.EvaluateExpressionAsync($@"(() => {{
								const emote = document.querySelector('.{item.prefix}')
								emote.style.display = 'none'
							}})()");

						await page.EvaluateExpressionAsync($@"(() => {{
								const emote = document.querySelector('.{item.prefix}')
								emote.style.display = ''
							}})()");
						// Thread.Sleep(10);

						var screenshotI = 0;
						// List<long> swCounter = new List<long>();
						// var sw = new System.Diagnostics.Stopwatch();
						var durMs = dur * 1000;
						var date = DateTime.Now;

						EventHandler<MessageEventArgs> handler = async (sender, e) => {
							if (e.MessageID == "Page.screencastFrame")
							{
								screenshotI++;
								var numberString = screenshotI.ToString().PadLeft(4, '0');
								// sw.Start();
								File.WriteAllBytes($"{tempFolder}/dggEmotes/temp-{item.prefix}/{numberString}.png", Convert.FromBase64String(e.MessageData.Value<string>("data")));
								// Console.WriteLine(e.MessageData.ToString());
								// await page.ScreenshotAsync($"{tempFolder}/dggEmotes/temp-{item.prefix}/{numberString}.png", new ScreenshotOptions
								// {
								// 	Clip = new PuppeteerSharp.Media.Clip {
								// 		X = 8,
								// 		Y = 8,
								// 		Width = width,
								// 		Height = height,
								// 	},
								// 	OmitBackground = true,
								// });
								// sw.Stop();
								// swCounter.Add(sw.ElapsedMilliseconds);
								await cdp.SendAsync("Page.screencastFrameAck", new {
									sessionId = e.MessageData.Value<int>("sessionId"),
								});
							}
						};

						cdp.MessageReceived += handler;

						await page.SetViewportAsync(new ViewPortOptions{
							Width = ((int)width),
							Height = ((int)height),
						});

						await cdp.SendAsync("Page.startScreencast", new {
							everyNthFrame = 1,
							format = "png",
							quality = 100,
							// maxWidth = width,
							// maxHeight = height,
						});

						Thread.Sleep(((int)durMs));
						// while ((DateTime.Now - date).TotalMilliseconds < durMs)
						// {
						// 	// screenshotI++;
						// 	// var numberString = screenshotI.ToString().PadLeft(4, '0');
						// 	// sw.Start();
						// 	// await page.ScreenshotAsync($"{tempFolder}/dggEmotes/temp-{item.prefix}/{numberString}.png", new ScreenshotOptions
						// 	// {
						// 	// 	Clip = new PuppeteerSharp.Media.Clip {
						// 	// 		X = 8,
						// 	// 		Y = 8,
						// 	// 		Width = width,
						// 	// 		Height = height,
						// 	// 	},
						// 	// 	OmitBackground = true,
						// 	// });
						// 	// sw.Stop();
						// 	// swCounter.Add(sw.ElapsedMilliseconds);
						// }

						await cdp.SendAsync("Page.stopScreencast");

						await page.EvaluateExpressionAsync($@"(() => {{
								const emote = document.querySelector('.{item.prefix}')
								emote.style.display = 'none'
							}})()");

						var imageCollection = new MagickImageCollection();
						var imageCounter = 0;
						var images = Directory.GetFiles($"{tempFolder}/dggEmotes/temp-{item.prefix}/").OrderBy(f => f);
						// var delay = ((int)Math.Round(images.Count() / (dur * 10)));
						foreach (var frame in images)
						{
							var img = new MagickImage(frame);
							// img.Alpha(AlphaOption.Set);
							// img.ColorFuzz = new Percentage(5);
							// img.Opaque(MagickColor.FromRgb(0, 210, 13), MagickColors.None);
							imageCollection.Add(img);
							imageCollection[imageCounter].AnimationIterations = 1;
							imageCollection[imageCounter].AnimationDelay = 3;
							// imageCollection[imageCounter].AnimationDelay = imageCounter == 0 ? 0 : ((int)Math.Floor(((swCounter[imageCounter] - swCounter[imageCounter-1])/10.0)));
							imageCollection[imageCounter].GifDisposeMethod = imageCounter == 0 ? GifDisposeMethod.None : GifDisposeMethod.Background;
							imageCounter++;
						}

						imageCollection.Write($"{tempFolder}/dggEmotes/{item.prefix}.gif");
						cdp.MessageReceived -= handler;
					} else {
						using (var client = new HttpClient())
						{
							using (var s = client.GetStreamAsync(item.image[0].url))
							{
								using (var fs = new FileStream($"{tempFolder}/dggEmotes/{item.prefix}.{fileType}", FileMode.OpenOrCreate))
								{
									s.Result.CopyTo(fs);
								}
							}
						}
					}
					Directory.Delete($"{tempFolder}/dggEmotes/temp-{item.prefix}/", true);
					fileType = dur != 0 ? "gif" : fileType;
					doneCount++;
                    int percent = (int)(doneCount / (double)DGGEmotes.Count * 100);
					progress.Report(new ProgressReport(ReportType.NewLineStatus, $"Converting dgg emotes: {percent}%"));
					progress.Report(new ProgressReport(percent));
				}
			}
			await browser.CloseAsync();

			progress.Report(new ProgressReport(ReportType.NewLineStatus, "Adding emote bytes to the emote response"));
			foreach (var item in Directory.GetFiles($"{tempFolder}/dggEmotes/"))
			{
				var prefix = Path.GetFileNameWithoutExtension(item);
				var emote = DGGEmotes.Find(e => e.prefix == prefix);
				var img = new MagickImage(item);
				emote.width = img.Width;
				emote.height = img.Height;
				byte[] bytes = File.ReadAllBytes(item);
				emote.imageData = bytes;
				emoteResponse.Add(emote);
			}

			progress.Report(new ProgressReport(ReportType.NewLineStatus, "Added emote bytes"));
            return emoteResponse;
        }
	}
}