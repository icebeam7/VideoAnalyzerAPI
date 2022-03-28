using System;
using System.Linq;
using System.Net.Http;
using System.Globalization;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;

using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using VideoAnalyzerAPI.Models;
using VideoAnalyzerAPI.Helpers;

namespace VideoAnalyzerAPI.Controllers
{
    public class VideoInfoController : Controller
    {
        private readonly ILogger<VideoInfoController> _logger;

        public VideoInfoController(ILogger<VideoInfoController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        private async Task<string> GetToken()
        {
            var token = string.Empty;

            var tokenResponse = await clientAuth.GetAsync(Constants.TokenService);

            if (tokenResponse.IsSuccessStatusCode)
            {
                var content = await tokenResponse.Content.ReadAsStringAsync();
                token = content.Substring(1, content.Length - 2);
            }

            return token;
        }

        [HttpPost]
        public async Task<ActionResult> Results(ExplorerViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(Constants.VideoIndexerAccessToken))
                Constants.VideoIndexerAccessToken = await GetToken();

            List<VideoResultClean> results = new List<VideoResultClean>();

            var searchUrl = $"{Constants.SearchVideos}{Constants.VideoIndexerAccessToken}{Constants.QueryParameter}{vm.Phrase}";
            var response = await client.GetAsync(searchUrl);

            if (response.IsSuccessStatusCode)
            {
                var videoResponse = await response.Content.ReadAsStringAsync();
                var videoInfo = JsonConvert.DeserializeObject<VideoInfo>(videoResponse);

                if (videoInfo.results.Count > 0)
                {
                    foreach (var item in videoInfo.results)
                    {
                        var thumbnail = await GetThumbnail(item.id, item.thumbnailId);

                        results.Add(new VideoResultClean()
                        {
                            id = item.id,
                            name = item.name,
                            thumbnailId = item.thumbnailId,
                            thumbnail = $"data:image/png;base64,{thumbnail}"
                        });
                    }
                }
            }

            return View(results);
        }

        private async Task<string> GetThumbnail(string videoId, string thumbnailId)
        {
            var thumbnailResponse = await client.GetAsync($"Videos/{videoId}/Thumbnails/{thumbnailId}?format=Base64&accessToken={Constants.VideoIndexerAccessToken}");

            return thumbnailResponse.IsSuccessStatusCode
                ? await thumbnailResponse.Content.ReadAsStringAsync()
                : string.Empty;
        }


        // GET: VideoInfo/Details/5
        public async Task<ActionResult> Details(string id)
        {
            var insights = new VideoResultInsights();
            insights.Id = id;
            insights.KeyFrameList = new List<KeyFrameClean>();

            var labels = new List<Label>();

            var downloadUriResponse = await client.GetAsync($"Videos/{id}/SourceFile/DownloadUrl?accessToken={Constants.VideoIndexerAccessToken}");

            if (downloadUriResponse.IsSuccessStatusCode)
            {
                var url = await downloadUriResponse.Content.ReadAsStringAsync();
                insights.VideoUri = url.Substring(1, url.Length - 2);
            }

            var indexResponse = await client.GetAsync($"Videos/{id}/Index?reTranslate=False&includeStreamingUrls=True?accessToken={Constants.VideoIndexerAccessToken}");

            if (indexResponse.IsSuccessStatusCode)
            {
                var indexContent = await indexResponse.Content.ReadAsStringAsync();
                var videoIndex = JsonConvert.DeserializeObject<VideoIndex>(indexContent);

                if (videoIndex != null)
                {
                    insights.Faces = videoIndex.summarizedInsights.faces.ToList();
                    insights.Brands = videoIndex.summarizedInsights.brands.ToList();
                    insights.Keywords = videoIndex.summarizedInsights.keywords.ToList();

                    var video = videoIndex.videos.FirstOrDefault();

                    if (video != null)
                    {
                        foreach (var shot in video.insights.shots)
                        {
                            foreach (var keyFrame in shot.keyFrames)
                            {
                                foreach (var instance in keyFrame.instances)
                                {
                                    var thumbnail = await GetThumbnail(id, instance.thumbnailId);

                                    insights.KeyFrameList.Add(new KeyFrameClean()
                                    {
                                        Start = instance.start,
                                        End = instance.end,
                                        ThumbnailId = instance.thumbnailId,
                                        Thumbnail = $"data:image/png;base64,{thumbnail}"
                                    });
                                }
                            }
                        }

                        labels = video.insights.labels.ToList();
                    }
                }
            }

            var labelsClean = new List<LabelClean>();

            foreach (var item in labels)
            {
                var ac = new List<AppearanceClean>();

                if (item.instances != null)
                {
                    foreach (var app in item.instances)
                    {
                        ac.Add(new AppearanceClean()
                        {
                            StartTime = ConvertTime(app.start),
                            EndTime = ConvertTime(app.end)
                        });
                    }
                }

                labelsClean.Add(new LabelClean()
                {
                    name = item.name,
                    id = item.id,
                    appearances = ac
                });
            }

            foreach (var item in insights.KeyFrameList)
            {
                var startTime = ConvertTime(item.Start);
                var endTime = ConvertTime(item.End);

                foreach (var label in labelsClean)
                {
                    if (label.appearances.Any(x => startTime >= x.StartTime && endTime <= x.EndTime))
                        item.Labels += $"{label.name},  ";
                }
            }

            return View(insights);
        }

        private static readonly HttpClient client =
            CreateHttpClient($"{Constants.VideoIndexerBaseUrl}/{Constants.AccountId}/",
                "Ocp-Apim-Subscription-Key",
                Constants.SubscriptionKey);

        private static readonly HttpClient clientAuth =
            CreateHttpClient($"{Constants.AuthBaseUrl}/{Constants.AccountId}/",
                "Ocp-Apim-Subscription-Key",
                Constants.SubscriptionKey);

        private static HttpClient CreateHttpClient(string url, string headerKey, string key)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(headerKey))
                client.DefaultRequestHeaders.Add(headerKey, key);

            return client;
        }

        private double ConvertTime(string time)
        {
            var culture = CultureInfo.CurrentCulture;
            var format = "H:mm:ss.f"; //0:00:02.7

            if (time.Length < 9)
                time += ".0";

            return DateTime.ParseExact(time.Substring(0, 9), format, culture).TimeOfDay.TotalSeconds;
        }
    }
}
