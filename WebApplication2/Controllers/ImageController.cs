using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Text;

namespace WebService
{
    [ApiController]
    public class CatsController : ControllerBase
    {
        private static HttpClient _httpClient = new HttpClient();
        private static ConcurrentDictionary<int, byte[]> _cache = new ConcurrentDictionary<int, byte[]>();

        [HttpGet("/")]
        public IActionResult Get()
        {
            // // returns html page with form to enter url
            var content = new StringBuilder();
            content.AppendLine("<html>");
            content.AppendLine("<head><meta charset='UTF-8'></head>");
            content.AppendLine("<body>");
            content.AppendLine("<form method='post' action='/catimage'>");
            content.AppendLine("<input type='text' name='url' placeholder='Enter the URL-adress'>");
            content.AppendLine("<button type='submit'>Get status-code</button>");
            content.AppendLine("</form>");
            content.AppendLine("</body>");
            content.AppendLine("</html>");

            return Content(content.ToString(), "text/html; charset=utf-8");
        }

        [HttpPost]
        [Route("/catimage")]
        public async Task<IActionResult> GetCatImage([FromForm] string url)
        {
            // check if the URL matches the https:// pattern
            if (!IsValidUrl(url))
            {
                // transform the url to the desired form
                url = ConvertToValidUrl(url);
            }

            // send a get request to the specified url
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            // save status response code
            HttpStatusCode statusCode = response.StatusCode;

            // check if there is a cached image for this code status
            bool isCachedImageAvailable = ImageCache.CheckCacheForImage((int)statusCode);

            if (!isCachedImageAvailable)
            {
                // get an image from the service with cats by status code
                string catImageUrl = $"https://http.cat/{(int)statusCode}.jpg";

                // uploading an image
                byte[] catImageBytes = await _httpClient.GetByteArrayAsync(catImageUrl);
                // cache the image in another thread
                await Task.Run(() => ImageCache.CacheImage((int)statusCode, catImageBytes));
            }

            // returning the picture the cat image
            bool isCached = ImageCache.CheckCacheForImage((int) statusCode);
            if (isCached)
            {
                return new FileContentResult(ImageCache.GetImageFromCache((int)statusCode), "image/jpeg");
            }
            else
            {
                return Ok(new { StatusCode = (int)statusCode, isCached });
            }
        }
        private bool IsValidUrl(string url)
        {
            // check if the url matches the pattern https://..
            string pattern = @"^https:\/\/";
            return Regex.IsMatch(url, pattern);
        }

        private string ConvertToValidUrl(string url)
        {
            //  transform the url to the desired form
            if (!url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            return url;
        }

        public class ImageCache
        {

            private static TimeSpan _cacheExpirationTime = TimeSpan.FromMinutes(10);

            public static bool CheckCacheForImage(int statusCode)
            {
                {
                    return _cache.ContainsKey(statusCode);
                }
            }

            public static void CacheImage(int statusCode, byte[] imageBytes)
            {
                _cache.TryAdd(statusCode, imageBytes);

                // setting a timer to delete the cached image after the time has elapsed
                TimerCallback timerCallback = new TimerCallback(RemoveImageFromCache);
                Timer expirationTimer = new Timer(timerCallback, statusCode, _cacheExpirationTime, TimeSpan.FromMinutes(10));
            }

            private static void RemoveImageFromCache([DisallowNull] object state)
            {
                int statusCode = (int)state;
                _cache.TryRemove(statusCode, out _);
            }
            public static byte[] GetImageFromCache(int statusCode)
            {
                if (_cache.TryGetValue(statusCode, out byte[] imageBytes))
                {
                    return imageBytes; // returning the image from the cache by the key status code
                }

                return null; 
            }
        }
    }
}
