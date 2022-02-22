using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

using DiscordInterface;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();
        private DiscordRpc.RichPresence presence = new DiscordRpc.RichPresence();

        private string APPLICATION_ID = "519949979176140821";
        private string LASTFM_API_KEY = "cba04ed41dff8bfb9c10835ee747ba94"; // taken from MusicBee

        private static HttpClient Client = new HttpClient();

        private static Dictionary<string, string> albumArtCache = new Dictionary<string, string>();

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);
            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "Discord Rich Presence";
            about.Description = "Sets currently playing song as Discord Rich Presence";
            about.Author = "Harmon758 + Kuunikal + BriannaFoxwell";
            about.TargetApplication = "";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            about.Type = PluginType.General;
            about.VersionMajor = 2;  // your plugin version
            about.VersionMinor = 0;
            about.Revision = 05; // this how you do it?
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            about.ConfigurationPanelHeight = 0;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            InitialiseDiscord();

            return about;
        }

        private void InitialiseDiscord()
        {
            var handlers = new DiscordRpc.EventHandlers();

            handlers.readyCallback += HandleReadyCallback;
            handlers.errorCallback += HandleErrorCallback;
            handlers.disconnectedCallback += HandleDisconnectedCallback;

            DiscordRpc.Initialize(APPLICATION_ID, ref handlers, true, null);

        }

        private void HandleReadyCallback(ref DiscordRpc.DiscordUser user) { }
        private void HandleErrorCallback(int errorCode, string message) { }
        private void HandleDisconnectedCallback(int errorCode, string message) { }

        private async Task UpdatePresence(string artist, string track, string album, Boolean playing, int index, int totalTracks, string albumArtist, string yearStr)
        {
            presence.largeImageKey = "albumart";

            // NOTE(yui): this is very ugly
            string year = null;

            if (yearStr.Length > 0)
            {
                try
                {
                    year = DateTime.Parse(yearStr).Year.ToString();
                }
                catch (FormatException)
                {
                    if (yearStr.Length == 4)
                    {
                        year = DateTime.ParseExact(yearStr, "yyyy", null).Year.ToString();
                    }
                }
            }

            if (year != null)
            {
                presence.largeImageText = $"{album} ({year})";
            }
            else
            {
                presence.largeImageText = album;
            }


            // getting ratelimited from lastfm wr
            if (artist != null && album != null && !albumArtCache.ContainsKey($"{albumArtist}_{album}"))
            {
                HttpResponseMessage lastFmInfoResp = await Client.GetAsync($"https://ws.audioscrobbler.com/2.0/?method=album.getinfo&api_key={LASTFM_API_KEY}&artist={HttpUtility.UrlEncode(albumArtist)}&album={HttpUtility.UrlEncode(album)}&format=json");
                if (lastFmInfoResp.IsSuccessStatusCode)
                {
                    string jsonString = await lastFmInfoResp.Content.ReadAsStringAsync();
                    JObject jsonData = JsonConvert.DeserializeObject<JObject>(jsonString);
                    try { 
                        var images = jsonData["album"]["image"].Children<JObject>();
                        string albumArtUrl = (string)((JObject)images.Where(x => (string)x["size"] == "large").FirstOrDefault())["#text"];
                        albumArtCache.Add($"{albumArtist}_{album}", albumArtUrl);
                    } catch
                    {
                        albumArtCache.Add($"{artist}_{track}", "unknown");
                    }
                }
                else
                {
                    albumArtCache.Add($"{albumArtist}_{album}", "unknown");
                }
            }

            if (artist != null && album != null && albumArtist != null && albumArtCache.ContainsKey($"{albumArtist}_{album}") && (albumArtCache[$"{albumArtist}_{album}"] == "" || albumArtCache[$"{albumArtist}_{album}"] == "unknown") && !albumArtCache.ContainsKey($"{artist}_{album}"))
            {
                HttpResponseMessage lastFmInfoResp = await Client.GetAsync($"https://ws.audioscrobbler.com/2.0/?method=album.getinfo&api_key={LASTFM_API_KEY}&artist={HttpUtility.UrlEncode(artist)}&album={HttpUtility.UrlEncode(album)}&format=json");
                if (lastFmInfoResp.IsSuccessStatusCode)
                {
                    string jsonString = await lastFmInfoResp.Content.ReadAsStringAsync();
                    JObject jsonData = JsonConvert.DeserializeObject<JObject>(jsonString);
                    try
                    {
                        var images = jsonData["album"]["image"].Children<JObject>();
                        string albumArtUrl = (string)((JObject)images.Where(x => (string)x["size"] == "large").FirstOrDefault())["#text"];
                        albumArtCache.Add($"{artist}_{album}", albumArtUrl);
                    }
                    catch
                    {
                        albumArtCache.Add($"{artist}_{track}", "unknown");
                    }
                } else
                {
                    albumArtCache.Add($"{artist}_{album}", "unknown");
                }
            }

            if (artist != null && albumArtCache.ContainsKey($"{artist}_{album}") && (albumArtCache[$"{artist}_{album}"] == "" || albumArtCache[$"{artist}_{album}"] == "unknown") && !albumArtCache.ContainsKey($"{artist}_{track}"))
            {
                HttpResponseMessage lastFmInfoResp = await Client.GetAsync($"https://ws.audioscrobbler.com/2.0/?method=track.getinfo&api_key={LASTFM_API_KEY}&artist={HttpUtility.UrlEncode(artist)}&track={HttpUtility.UrlEncode(track)}&format=json");
                if (lastFmInfoResp.IsSuccessStatusCode)
                {
                    string jsonString = await lastFmInfoResp.Content.ReadAsStringAsync();
                    JObject jsonData = JsonConvert.DeserializeObject<JObject>(jsonString);
                    try { 
                        var images = jsonData["album"]["image"].Children<JObject>();
                        string albumArtUrl = (string)((JObject)images.Where(x => (string)x["size"] == "large").FirstOrDefault())["#text"];
                        albumArtCache.Add($"{artist}_{track}", albumArtUrl);
                    }
                    catch
                    {
                        albumArtCache.Add($"{artist}_{track}", "unknown");
                    }
                }
                else
                {
                    albumArtCache.Add($"{artist}_{track}", "unknown");
                }
            }


            string url = albumArtCache[$"{albumArtist}_{album}"];
            if (url != "" && url != "unknown")
            {
                presence.largeImageKey = url;
            }
            else
            {
                string url2 = albumArtCache[$"{artist}_{album}"];
                if (url2 != "" && url2 != "unknown")
                {
                    presence.largeImageKey = url2;
                }
                else
                {
                    string url3 = albumArtCache[$"{artist}_{track}"];
                    if (url3 != "" && url3 != "unknown")
                    {
                        presence.largeImageKey = url3;
                    }
                }
            }

            string bitrate = mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Bitrate);
            string codec = mbApiInterface.NowPlaying_GetFileProperty(Plugin.FilePropertyType.Kind);

            // Discord RPC doesn't like strings that are only one character long
            // NOTE(yui): unsure if this ^ was talking about the old interface or the discord client, leaving it in just in case
            if (track.Length <= 1) track += " ";
            if (artist.Length <= 1) artist += " ";

            presence.state = $"by {artist}";
            presence.details = $"{track} [{mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Duration)}]";

            // Set the small image to the playback status.
            if (playing)
            {
                presence.smallImageKey = "playing";
                presence.smallImageText = $"{bitrate.Replace("k", "kbps")} [{codec}]";
            }
            else
            {
                presence.smallImageKey = "paused";
                presence.smallImageText = "Paused";
            }

            presence.partySize = index;
            presence.partyMax = totalTracks;

            long now = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            long duration = this.mbApiInterface.NowPlaying_GetDuration() / 1000;
            long end = now + duration;

            TimeSpan t = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1));

            if (playing)
            {
                long pos = (this.mbApiInterface.Player_GetPosition() / 1000);
                presence.startTimestamp = now - pos;
                if (duration != -1)
                {
                    presence.endTimestamp = end - pos;
                }
            }
            else
            {
                presence.startTimestamp = 0;
                presence.endTimestamp = 0;
            }

            DiscordRpc.UpdatePresence(presence);
        }

        public bool Configure(IntPtr panelHandle)
        {
            // save any persistent settings in a sub-folder of this path
            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
            if (panelHandle != IntPtr.Zero)
            {
                Panel configPanel = (Panel)Panel.FromHandle(panelHandle);
                Label prompt = new Label();
                prompt.AutoSize = true;
                prompt.Location = new Point(0, 0);
                prompt.Text = "prompt:";
                TextBox textBox = new TextBox();
                textBox.Bounds = new Rectangle(60, 0, 100, textBox.Height);
                configPanel.Controls.AddRange(new Control[] { prompt, textBox });
            }
            return false;
        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            // save any persistent settings in a sub-folder of this path
            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
            DiscordRpc.Shutdown();
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            string bitrate = mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Bitrate);
            string artist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
            string albumArtist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.AlbumArtist);
            string trackTitle = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.TrackTitle);
            string album = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Album);
            string year = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Year);
            int position = mbApiInterface.Player_GetPosition();

            string[] tracks = null;
            mbApiInterface.NowPlayingList_QueryFilesEx(null, ref tracks);
            int index = Array.IndexOf(tracks, mbApiInterface.NowPlaying_GetFileUrl());

            // Check if there isn't an artist for the current song. If so, replace it with "(unknown artist)".
            if (string.IsNullOrEmpty(artist))
            {
                if (!string.IsNullOrEmpty(albumArtist))
                {
                    artist = albumArtist;
                }
                else
                {
                    artist = "(unknown artist)";
                }
            }

            if (artist.Length > 128)
            {
                if (!string.IsNullOrEmpty(albumArtist) && albumArtist.Length <= 128)
                {
                    artist = albumArtist;
                }
                else
                {
                    artist = artist.Substring(0, 122) + "...";
                }
            }

            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                    // perform startup initialisation
                case NotificationType.PlayStateChanged:
                case NotificationType.TrackChanged:
                    var isPlaying = mbApiInterface.Player_GetPlayState() == PlayState.Playing;

                    Task.Run(async () =>
                    {
                        try
                        {
                            await UpdatePresence(artist, trackTitle, album, isPlaying, index + 1, tracks.Length, albumArtist, year);
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine(err);
                        }
                    });
                    break;
            }
        }
    }
}
