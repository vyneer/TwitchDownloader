using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using PuppeteerSharp;
using ImageMagick;
using TwitchDownloaderCore.DGGObjects;

namespace TwitchDownloaderCore
{
    public static class EmoteHelper
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<List<DGGEmote>> GetDGGEmoteData(string tempFolder, bool webp, IProgress<ProgressReport> progress, CancellationToken cancellationToken = new())
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
				htmlBody += $@"<div class=""emote {item.prefix}""></div>";
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
			Thread.Sleep(10000);

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
					try
					{
                        var width = await page.EvaluateExpressionAsync<decimal>($"(() => {{return parseFloat(window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('width').slice(0, -2)) + parseInt(window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('margin-right').slice(0, -2))}})()");
                        var height = await page.EvaluateExpressionAsync<decimal>($"(() => {{return parseFloat(window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('height').slice(0, -2))}})()");
                        var animationDuration = await page.EvaluateExpressionAsync<double>($@"(() => {{
          									const durArray = window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('animation-duration').split(',');
											const iterArray = window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('animation-iteration-count').split(',');
											const dur = durArray.reduce((acc, cur, index) => {{
												if (iterArray.length === durArray.length) {{
                                                    const iAi = (iterArray[index] === 'infinite') ? 1 : iterArray[index];
													return acc + (parseFloat(cur.slice(0, -1)) * parseFloat(iAi));
												}} else if (iterArray.length === 1) {{
                                                    const iAi = (iterArray[0] === 'infinite') ? 1 : iterArray[0];
													return acc + (parseFloat(cur.slice(0, -1)) * parseFloat(iAi));
												}} else {{
													return acc + (parseFloat(cur.slice(0, -1)));
												}}
											}}, 0);
          									const delay = window.getComputedStyle(document.querySelector('.{item.prefix}'), null).getPropertyValue('animation-delay').split(',').reduce((acc, cur) => acc + parseFloat(cur.slice(0, -1)), 0);
          									return dur + (delay < 0 ? 0 : delay);
        								}})()");
                        var animationDurationBefore = await page.EvaluateExpressionAsync<double>($@"(() => {{
          									const durArray = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::before').getPropertyValue('animation-duration').split(',');
											const iterArray = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::before').getPropertyValue('animation-iteration-count').split(',');
											const dur = durArray.reduce((acc, cur, index) => {{
												if (iterArray.length === durArray.length) {{
                                                    const iAi = (iterArray[index] === 'infinite') ? 1 : iterArray[index];
													return acc + (parseFloat(cur.slice(0, -1)) * parseFloat(iAi));
												}} else if (iterArray.length === 1) {{
                                                    const iAi = (iterArray[0] === 'infinite') ? 1 : iterArray[0];
													return acc + (parseFloat(cur.slice(0, -1)) * parseFloat(iAi));
												}} else {{
													return acc + (parseFloat(cur.slice(0, -1)));
												}}
											}}, 0);
          									const delay = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::before').getPropertyValue('animation-delay').split(',').reduce((acc, cur) => acc + parseFloat(cur.slice(0, -1)), 0);
          									return dur + (delay < 0 ? 0 : delay);
        								}})()");
                        var animationDurationAfter = await page.EvaluateExpressionAsync<double>($@"(() => {{
          									const durArray = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::after').getPropertyValue('animation-duration').split(',');
											const iterArray = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::after').getPropertyValue('animation-iteration-count').split(',');
											const dur = durArray.reduce((acc, cur, index) => {{
												if (iterArray.length === durArray.length) {{
                                                    const iAi = (iterArray[index] === 'infinite') ? 1 : iterArray[index];
													return acc + (parseFloat(cur.slice(0, -1)) * parseFloat(iAi));
												}} else if (iterArray.length === 1) {{
                                                    const iAi = (iterArray[0] === 'infinite') ? 1 : iterArray[0];
													return acc + (parseFloat(cur.slice(0, -1)) * parseFloat(iAi));
												}} else {{
													return acc + (parseFloat(cur.slice(0, -1)));
												}}
											}}, 0);
          									const delay = window.getComputedStyle(document.querySelector('.{item.prefix}'), '::after').getPropertyValue('animation-delay').split(',').reduce((acc, cur) => acc + parseFloat(cur.slice(0, -1)), 0);
          									return dur + (delay < 0 ? 0 : delay);
        								}})()");
                        var dur = animationDuration + animationDurationAfter + animationDurationBefore;

                        var backgroundPosition = await page.EvaluateExpressionAsync<bool>($@"(() => {{
          									return window.getComputedStyle(document.querySelector('.{item.prefix}')).getPropertyValue('background-position').split(' ')[0] !== '0%' 
        								}})()");

                        if (dur < 0)
                        {
                            dur = 0;
                        }

                        if (dur != 0)
                        {
                            var durMs = dur * 1000;
                            var date = DateTime.Now;

                            Queue<byte[]> fq = new Queue<byte[]>();
                            var frameDelays = new List<int>();
                            Stopwatch sw = new Stopwatch();

                            EventHandler<MessageEventArgs> handler = async (sender, e) => {
                                if (e.MessageID == "Page.screencastFrame")
                                {
                                    await cdp.SendAsync("Page.screencastFrameAck", new
                                    {
                                        sessionId = e.MessageData.Value<int>("sessionId"),
                                    });
                                    sw.Stop();
                                    var frame = Convert.FromBase64String(e.MessageData.Value<string>("data"));
                                    fq.Enqueue(frame);
                                    frameDelays.Add((int)sw.ElapsedMilliseconds / 10);
                                    sw.Reset();
                                    sw.Start();
                                }
                            };

                            cdp.MessageReceived += handler;

                            await page.SetViewportAsync(new ViewPortOptions
                            {
                                Width = (int)width,
                                Height = (int)height,
                            });

                            await page.EvaluateExpressionAsync($@"(() => {{
								document.querySelectorAll('.emote').forEach(e => e.style.display = 'none');
							}})()");

                            await page.EvaluateExpressionAsync($@"(() => {{
								const emote = document.querySelector('.{item.prefix}');
								emote.style.display = '';
							}})()");

                            Thread.Sleep(20);

                            await cdp.SendAsync("Page.startScreencast", new
                            {
                                everyNthFrame = 1,
                                format = "png",
                                quality = 100,
                            });

                            Thread.Sleep((int)durMs);

                            await cdp.SendAsync("Page.stopScreencast");

                            await page.EvaluateExpressionAsync($@"(() => {{
								const emote = document.querySelector('.{item.prefix}')
								emote.style.display = 'none'
							}})()");

                            var imageCollection = new MagickImageCollection();
                            var imageCounter = 0;
                            Process ffmpeg = new Process();
                            if (webp)
                            {
                                var framerate = (int)Math.Round(fq.Count / dur);
                                framerate = framerate == 0 ? 1 : framerate;
                                ffmpeg.StartInfo.FileName = @"ffmpeg.exe";
                                ffmpeg.StartInfo.Arguments = $"-loglevel error -f png_pipe -r {framerate} -i - -y -an -r 60 -vcodec libwebp_anim -loop 1 -q:v 100 -lossless 1 -threads 1 {tempFolder}\\dggEmotes\\{item.prefix}.webp";
                                ffmpeg.StartInfo.UseShellExecute = false;
                                ffmpeg.StartInfo.RedirectStandardInput = true;
                                // ffmpeg.StartInfo.RedirectStandardOutput = true;
                                ffmpeg.Start();
                            }
                            while (fq.Count > 0)
                            {
                                if (webp)
                                {
                                    ffmpeg.StandardInput.BaseStream.Write(fq.Dequeue());
                                }
                                else
                                {
                                    try
                                    {
                                        var img = new MagickImage(fq.Dequeue());
                                        imageCollection.Add(img);
                                        imageCollection[imageCounter].AnimationIterations = 1;
                                        imageCollection[imageCounter].AnimationDelay = Math.Max(2, frameDelays[imageCounter]);
                                        imageCollection[imageCounter].GifDisposeMethod = imageCounter == 0 ? GifDisposeMethod.None : GifDisposeMethod.Background;
                                        imageCounter++;
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new Exception(ex.Message + item);
                                    }

                                }
                            }

                            if (webp)
                            {
                                ffmpeg.StandardInput.Close();
                                ffmpeg.WaitForExit();
                                ffmpeg.Dispose();
                            }
                            else
                            {
                                imageCollection.Write($"{tempFolder}/dggEmotes/{item.prefix}.gif");
                            }

                            cdp.MessageReceived -= handler;
                        }
                        else
                        {
                            using (var client = new HttpClient())
                            {
                                using (var s = client.GetStreamAsync(item.image[0].url))
                                {
                                    using (var fs = new FileStream($"{tempFolder}/dggEmotes/{item.prefix}.{fileType}", FileMode.OpenOrCreate))
                                    {
                                        if (fileType == "gif")
                                        {
                                            s.Result.CopyTo(fs);
                                        }
                                        else
                                        {
                                            var img = new MagickImage(s.Result);
                                            if (img.Width != (int)width)
                                            {
                                                img.Crop((int)width, (int)height);
                                            }
                                            img.Write(fs);
                                        }
                                    }
                                }
                            }
                        }
                        doneCount++;
                        int percent = (int)(doneCount / (double)DGGEmotes.Count * 100);
                        progress.Report(new ProgressReport(ReportType.NewLineStatus, $"Converting dgg emotes: {percent}%"));
                        progress.Report(new ProgressReport(percent));
                    }
					catch (PuppeteerSharp.EvaluationFailedException)
					{
                        continue;
					}

				}
			}
			await browser.CloseAsync();

			progress.Report(new ProgressReport(ReportType.NewLineStatus, "Adding emote bytes to the emote response"));
			foreach (var item in Directory.GetFiles($"{tempFolder}/dggEmotes/"))
			{
				try
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
				catch (Exception ex)
				{
					throw new Exception(ex.Message + item);
				}
			}

			progress.Report(new ProgressReport(ReportType.NewLineStatus, "Added emote bytes"));
            return emoteResponse;
        }
	}
}