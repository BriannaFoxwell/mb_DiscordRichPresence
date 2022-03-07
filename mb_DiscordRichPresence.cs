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
using System.Runtime.Serialization;
using System.IO;
using System.Diagnostics;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();
        private DiscordRpc.RichPresence presence = new DiscordRpc.RichPresence();

        private string APPLICATION_ID = "519949979176140821";
        //private string LASTFM_API_KEY = "cba04ed41dff8bfb9c10835ee747ba94"; // taken from MusicBee
        private string LASTFM_BASE_URL = "https://ws.audioscrobbler.com/2.0/?method=album.getinfo&api_key=cba04ed41dff8bfb9c10835ee747ba94&format=json";

        private static HttpClient Client = new HttpClient();

        private static Dictionary<string, string> albumArtCache = new Dictionary<string, string>();

        public Plugin.Configuration config = new Plugin.Configuration();
        public Plugin.Configuration newConfig = new Plugin.Configuration();

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
            about.ConfigurationPanelHeight = 24;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

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

        private async Task FetchArt(string track, string artist, string albumArtist, string album)
        {
            string url = LASTFM_BASE_URL;
            bool fetch = false;
            string key = "";

            if (albumArtist != null && album != null && !albumArtCache.ContainsKey($"{albumArtist}_{album}"))
            {
                fetch = true;
                url += $"&artist={HttpUtility.UrlEncode(albumArtist)}&album={HttpUtility.UrlEncode(album)}";
                key = $"{albumArtist}_{album}";
            }

            if (artist != null && album != null && albumArtist != null && albumArtCache.ContainsKey($"{albumArtist}_{album}") && (albumArtCache[$"{albumArtist}_{album}"] == "" || albumArtCache[$"{albumArtist}_{album}"] == "unknown") && !albumArtCache.ContainsKey($"{artist}_{album}"))
            {
                fetch = true;
                url += $"&artist={HttpUtility.UrlEncode(artist)}&album={HttpUtility.UrlEncode(album)}";
                key = $"{artist}_{album}";
            }

            if (artist != null && album != null && albumArtCache.ContainsKey($"{artist}_{album}") && (albumArtCache[$"{artist}_{album}"] == "" || albumArtCache[$"{artist}_{album}"] == "unknown") && !albumArtCache.ContainsKey($"{artist}_{track}")) {
                fetch = true;
                url += $"&artist={HttpUtility.UrlEncode(artist)}&track={HttpUtility.UrlEncode(track)}";
                key = $"{artist}_{track}";
            }

            if (fetch)
            {
                HttpResponseMessage lastFmInfoResp = await Client.GetAsync(url);
                if (lastFmInfoResp.IsSuccessStatusCode)
                {
                    string jsonString = await lastFmInfoResp.Content.ReadAsStringAsync();
                    JObject jsonData = JsonConvert.DeserializeObject<JObject>(jsonString);
                    try
                    {
                        var images = jsonData["album"]["image"].Children<JObject>();
                        string albumArtUrl = (string)((JObject)images.Where(x => (string)x["size"] == "large").FirstOrDefault())["#text"];
                        albumArtCache.Add(key, albumArtUrl);
                    }
                    catch
                    {
                        albumArtCache.Add(key, "unknown");
                    }
                }
                else
                {
                    albumArtCache.Add(key, "unknown");
                }
            }
        }

        private async Task UpdatePresence(string artist, string track, string album, bool playing, int index, int totalTracks, string albumArtist, string yearStr)
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

            if (year != null && config.showYear)
            {
                presence.largeImageText = $"{album} ({year})";
            }
            else
            {
                presence.largeImageText = album;
            }

            await FetchArt(track, artist, albumArtist, album);

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
                newConfig = (Configuration) config.Clone();

                Panel configPanel = (Panel) Panel.FromHandle(panelHandle);

                CheckBox showYear = new CheckBox();
                showYear.Name = "ShowYear";
                showYear.Text = "Show year next to album";
                showYear.Height = 16;
                showYear.ForeColor = Color.FromArgb(mbApiInterface.Setting_GetSkinElementColour(SkinElement.SkinInputPanelLabel, ElementState.ElementStateDefault, ElementComponent.ComponentForeground));
                showYear.Checked = newConfig.showYear;
                showYear.CheckedChanged += ShowYearValueChanged;

                configPanel.Controls.AddRange(new Control[] { showYear });
            }
            return false;
        }

        private void ShowYearValueChanged(object sender, EventArgs args) {
            newConfig.showYear = (sender as CheckBox).Checked;
        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            // save any persistent settings in a sub-folder of this path
            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
            config = newConfig;
            SaveConfig(dataPath);
        }

        private void SaveConfig(string dataPath)
        {
            DataContractSerializer dataContractSerializer = new DataContractSerializer(typeof(Plugin.Configuration));
            FileStream fileStream = new FileStream(Path.Combine(dataPath, "mb_DiscordRichPresence.xml"), FileMode.Create);
            dataContractSerializer.WriteObject(fileStream, config);
            fileStream.Close();
        }

        private void LoadConfig(string dataPath)
        {
            DataContractSerializer dataContractSerializer = new DataContractSerializer(typeof(Plugin.Configuration));
            FileStream fileStream = new FileStream(Path.Combine(dataPath, "mb_DiscordRichPresence.xml"), FileMode.Open);
            config = (Plugin.Configuration) dataContractSerializer.ReadObject(fileStream);
            fileStream.Close();
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

            if (type == NotificationType.PluginStartup)
                LoadConfig(mbApiInterface.Setting_GetPersistentStoragePath());

            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                case NotificationType.PlayStateChanged:
                case NotificationType.TrackChanged:
                    PlayState state = mbApiInterface.Player_GetPlayState();
                    bool isPlaying = state == PlayState.Playing;

                    // reduce wasting rich presence broadcasting ratelimits from loading state when switching tracks/skipping
                    if (state != PlayState.Loading && state != PlayState.Undefined && state != PlayState.Stopped)
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

        public class Configuration : ICloneable
        {
            public Configuration()
            {
                this.showYear = true;
            }

            public bool showYear { get; set; }

            public object Clone()
            {
                return base.MemberwiseClone();
            }
        }
    }
}
