using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

        public async Task<string> AlbumSearch(string Album, Func<JObject, string, string> FindUrl)
        {
            try
            {
                string Url = $"{BaseURL}?method=album.search&album={UriEnc(Album)}";
                JObject Json = await JsonResponse(Url);

                return FindUrl(Json, Album);
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("Unable to make a request.");
                return "";
            }
        }

        public async Task<string> AlbumGetInfo(string Artist, string Album, Func<JObject, string> FindUrl)
        {
            try
            {
                string Url = $"{BaseURL}?method=album.getinfo&artist={UriEnc(Artist)}&album={UriEnc(Album)}";
                JObject Json = await JsonResponse(Url);

                return FindUrl(Json);
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("Unable to make a request.");
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
