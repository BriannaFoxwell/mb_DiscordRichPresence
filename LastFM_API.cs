using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EpikLastFMApi
{
    class LastFM_API
    {
        private string BaseURL = "https://ws.audioscrobbler.com/2.0/";
        private string key { get; set; }
        public LastFM_API(string _key) { key = _key; }

        public async Task<string> AlbumSearch(Func<JObject, string, string, string> FindValue, string Album, string Artist = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Album))
                    throw new ArgumentNullException();

                string Url = $"{BaseURL}?method=album.search&album={UriEnc(Album)}";
                JObject Json = await JsonResponse(Url);

                return FindValue(Json, Artist, Album);
            }
            catch
            { 
                return "";
            }
        }

        public async Task<string> AlbumGetInfo(Func<JObject, string> FindValue, string Album, string Artist = "", string Track = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Album) | string.IsNullOrWhiteSpace(Artist))
                    throw new ArgumentNullException();

                string Url = $"{BaseURL}?method=album.getinfo&album={UriEnc(Album)}";

                if (!string.IsNullOrWhiteSpace(Artist))
                    Url += $"&artist={UriEnc(Artist)}";
                if (!string.IsNullOrWhiteSpace(Track))
                    Url += $"&track={UriEnc(Track)}";

                JObject Json = await JsonResponse(Url);

                return FindValue(Json);
            }
            catch
            {
                return "";
            }
        }

        private async Task<JObject> JsonResponse(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage Resp = await client.GetAsync(url + $"&api_key={key}&format=json");
                if (Resp.IsSuccessStatusCode)
                    return JObject.Parse(await Resp.Content.ReadAsStringAsync());
            }
            throw new HttpRequestException();
        }

        private string UriEnc(string a)
        {
            return Uri.EscapeDataString(a);
        }
    }
}
