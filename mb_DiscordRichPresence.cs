using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

using System.Threading.Tasks;

using DiscordRPC;

using Newtonsoft.Json.Linq;
using System.Linq;
using System.Runtime.Serialization;
using System.IO;

using EpikLastFMApi;
using DiscordRPC.Logging;
using System.Runtime.InteropServices;

namespace MusicBeePlugin
{
    public class CurrentSongInfo
    {
        public string Artist { get; set; }
        public string Track { get; set; }
        public string Album { get; set; }
        public bool Playing { get; set; }
        public int Index { get; set; }
        public int TotalTracks { get; set; }
        public string ImageUrl { get; set; }
        public string YearStr { get; set; }
        public string Url { get; set; }
    }

    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();
        
        private DiscordRpcClient rpcClient = new DiscordRpcClient("519949979176140821");

        private string imageSize = "medium"; // small, medium, large, extralarge, mega

        private static Dictionary<string, string> albumArtCache = new Dictionary<string, string>();

        public Plugin.Configuration config = new Plugin.Configuration();
        public Plugin.Configuration newConfig = new Plugin.Configuration();

        private LastFM_API FmApi = new LastFM_API("cba04ed41dff8bfb9c10835ee747ba94"); // LastFM Api key taken from MusicBee
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
            about.ConfigurationPanelHeight = 48;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            if (!rpcClient.IsInitialized)
            {
                rpcClient.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
                rpcClient.Initialize();
            }

            return about;
        }

        private async Task FetchArt(string track, string artist, string albumArtist, string album)
        {
            string key = $"{albumArtist}_{album}";

            if (!albumArtCache.ContainsKey(key))
            {
                string mainArtist = albumArtist.Split(new [] { ", ", "; " }, StringSplitOptions.None)[0];

                string url = await FmApi.AlbumGetInfo(AlbumGetInfo_FindAlbumImg, album, mainArtist);

                if (string.IsNullOrEmpty(url))
                    url = await FmApi.AlbumGetInfo(AlbumGetInfo_FindAlbumImg, album, albumArtist);

                if (string.IsNullOrEmpty(url))
                    url = await FmApi.AlbumGetInfo(AlbumGetInfo_FindAlbumImg, album, artist, track);

                if (string.IsNullOrEmpty(url))
                    url = await FmApi.AlbumSearch(AlbumSearch_FindAlbumImg, album, mainArtist);

                if (string.IsNullOrEmpty(url))
                    url = await FmApi.AlbumSearch(AlbumSearch_FindAlbumImg, album);

                if (string.IsNullOrEmpty(url))
                    albumArtCache.Add(key, "unknown");
                else
                    albumArtCache.Add(key, url);
            }
        }

        private string AlbumSearch_FindAlbumImg(JObject Json, string ArtistRequest, string AlbumRequest)
        {
            Dictionary<string, string> ImageList = new Dictionary<string, string>();

            dynamic DJson = Json;

            JArray Albums = DJson.results.albummatches.album;

            foreach (dynamic Album in Albums)
            {
                string Artist = Album.artist;
                bool ArtistUnknown = string.IsNullOrWhiteSpace(ArtistRequest) | string.IsNullOrWhiteSpace(Artist);
                bool IsVarious = (ArtistRequest.ToLower() == "va" | ArtistRequest.ToLower() == "various artists");

                if (Artist.ToLower() == ArtistRequest.ToLower() | ArtistUnknown | IsVarious)
                {
                    string name = Album.name;
                    JArray Images = Album.image;

                    bool FoundAlbum = (name == AlbumRequest | name.ToLower() == AlbumRequest.ToLower() | name.ToLower().Replace(" ", "") == AlbumRequest.ToLower().Replace(" ", ""));
                    bool FoundArtist = (Artist.ToLower() == ArtistRequest.ToLower());

                    if (FoundAlbum | FoundArtist | (IsVarious & FoundAlbum))
                    {
                        foreach (dynamic Image in Images)
                        {
                            string url = Image["#text"];
                            string size = Image["size"];
                            if (!string.IsNullOrEmpty(url) & !string.IsNullOrEmpty(size))
                                ImageList.Add(size, url);
                        }
                        if (ImageList.Count > 0)
                            break;
                    }
                }
            }

            if (ImageList.Count == 0)
                return "";

            return ImageList.ContainsKey(imageSize) ? ImageList[imageSize] : ImageList.Values.Last();
        }

        private string AlbumGetInfo_FindAlbumImg(JObject Json)
        {
            Dictionary<string, string> ImageList = new Dictionary<string, string>();

            dynamic DJson = Json;

            JArray Images = DJson.album.image;

            foreach (dynamic Image in Images)
            {
                string url = Image["#text"];
                string size = Image["size"];
                if (!string.IsNullOrEmpty(url) & !string.IsNullOrEmpty(size))
                    ImageList.Add(size, url);
            }

            if (ImageList.Count == 0)
                return "";

            return ImageList.ContainsKey(imageSize) ? ImageList[imageSize] : ImageList.Values.Last();
        }

        private void UpdatePresence(CurrentSongInfo SongInfo)
        {
            RichPresence presence = new RichPresence();
            presence.Assets = new Assets();
            presence.Party = new Party();
            presence.Timestamps = new Timestamps();

            presence.Assets.LargeImageKey = "albumart";

            string yearStr  = SongInfo.YearStr;
            string album    = SongInfo.Album;
            string imageUrl = SongInfo.ImageUrl;
            string track    = SongInfo.Track;
            string artist   = SongInfo.Artist;
            string url      = SongInfo.Url;
            bool playing    = SongInfo.Playing;
            int index       = SongInfo.Index;
            int totalTracks = SongInfo.TotalTracks;

            string year = null;

            if (yearStr.Length > 0 && config.showYear)
            {
                DateTime result;

                if (DateTime.TryParse(yearStr, out result))
                    year = result.Year.ToString();
                else
                    if (yearStr.Length == 4)
                        if (DateTime.TryParseExact(yearStr, "yyyy", null, System.Globalization.DateTimeStyles.None, out result))
                            year = result.Year.ToString();
            }

            presence.Assets.LargeImageText = $"{album}" + ( (year != null && config.showYear) ? $" ({year})" : "" );

            if (imageUrl != "" && imageUrl != "unknown")
                presence.Assets.LargeImageKey = imageUrl;

            string bitrate = mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Bitrate);
            string codec = mbApiInterface.NowPlaying_GetFileProperty(Plugin.FilePropertyType.Kind);

            presence.State = $"by {artist}";
            presence.Details = $"{track} [{mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Duration)}]";

            // Set the small image to the playback status.
            if (playing)
            {
                presence.Assets.SmallImageKey = "playing";
                presence.Assets.SmallImageText = $"{bitrate.Replace("k", "kbps")} [{codec}]";
            }
            else
            {
                presence.Assets.SmallImageKey = "paused";
                presence.Assets.SmallImageText = "Paused";
            }

            presence.Party.Size = index;
            presence.Party.Max = totalTracks;

            long now = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            long duration = this.mbApiInterface.NowPlaying_GetDuration() / 1000;
            long end = now + duration;

            if (playing)
            {
                long pos = (this.mbApiInterface.Player_GetPosition() / 1000);
                presence.Timestamps.Start = new DateTime(1970, 1, 1).AddSeconds(now - pos);

                if (duration != -1)
                    presence.Timestamps.End = new DateTime(1970, 1, 1).AddSeconds(end - pos);

                if (url.StartsWith("http"))
                {
                    presence.Buttons = new DiscordRPC.Button[]
                    {
                        new DiscordRPC.Button()
                        {
                            Label = "Listen to stream",
                            Url = url,
                        }
                    };
                }
            }

            rpcClient.SetPresence(presence);
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
                showYear.Text = "Show year next to album";
                showYear.Height = 16;
                showYear.ForeColor = Color.FromArgb(mbApiInterface.Setting_GetSkinElementColour(SkinElement.SkinInputPanelLabel, ElementState.ElementStateDefault, ElementComponent.ComponentForeground));
                showYear.Checked = newConfig.showYear;
                showYear.CheckedChanged += ShowYearValueChanged;

                Label customArtworkUrlLabel = new Label();
                customArtworkUrlLabel.Height = 16;
                customArtworkUrlLabel.Width = 128;
                customArtworkUrlLabel.ForeColor = Color.FromArgb(mbApiInterface.Setting_GetSkinElementColour(SkinElement.SkinInputPanelLabel, ElementState.ElementStateDefault, ElementComponent.ComponentForeground));
                customArtworkUrlLabel.Text = "Custom Artwork URL";
                customArtworkUrlLabel.Top = 24;
                customArtworkUrlLabel.TextAlign = ContentAlignment.MiddleLeft;

                TextBox customArtworkUrl = (TextBox) mbApiInterface.MB_AddPanel(configPanel, PluginPanelDock.TextBox);
                customArtworkUrl.Height = 16;
                customArtworkUrl.Width = 192;
                customArtworkUrl.ForeColor = Color.FromArgb(mbApiInterface.Setting_GetSkinElementColour(SkinElement.SkinInputPanelLabel, ElementState.ElementStateDefault, ElementComponent.ComponentForeground));
                customArtworkUrl.Text = newConfig.customArtworkUrl;
                customArtworkUrl.TextChanged += CustomArtworkUrlValueChanged;
                customArtworkUrl.Top = 24;
                customArtworkUrl.Left = customArtworkUrlLabel.Width;

                configPanel.Controls.AddRange(new Control[] { showYear, customArtworkUrlLabel, customArtworkUrl });
            }
            return false;
        }

        private void ShowYearValueChanged(object sender, EventArgs args) {
            newConfig.showYear = (sender as CheckBox).Checked;
        }

        private void CustomArtworkUrlValueChanged(object sender, EventArgs args)
        {
            newConfig.customArtworkUrl = (sender as TextBox).Text;
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
            rpcClient.ClearPresence();
            rpcClient.Dispose();
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
            string url = mbApiInterface.NowPlaying_GetFileUrl();
            int position = mbApiInterface.Player_GetPosition();

            string originalArtist = artist;

            string[] tracks = null;
            mbApiInterface.NowPlayingList_QueryFilesEx(null, ref tracks);
            int index = Array.IndexOf(tracks, url);

            // Check if there isn't an artist for the current song. If so, replace it with "(unknown artist)".
            if (string.IsNullOrEmpty(artist))
            {
                if (!string.IsNullOrEmpty(albumArtist))
                    artist = albumArtist;
                else
                    artist = "(unknown artist)";
            }

            if (artist.Length > 128)
            {
                if (!string.IsNullOrEmpty(albumArtist) && albumArtist.Length <= 128)
                    artist = albumArtist;
                else
                    artist = artist.Substring(0, 122) + "...";
            }

            if (type == NotificationType.PluginStartup)
                LoadConfig(mbApiInterface.Setting_GetPersistentStoragePath());

            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                case NotificationType.PlayStateChanged:
                case NotificationType.TrackChanged:
                    bool isPlaying = mbApiInterface.Player_GetPlayState() == PlayState.Playing;

                    Task.Run(async () =>
                    {
                        try
                        {
                            string imageUrl = "";
                            if (config.customArtworkUrl != "")
                                imageUrl = config.customArtworkUrl + "?" + (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                            else
                            {
                                await FetchArt(trackTitle, originalArtist, albumArtist, album);

                                imageUrl = albumArtCache[$"{albumArtist}_{album}"];
                            }

                            UpdatePresence(new CurrentSongInfo
                            {
                                Artist = artist,
                                Track = trackTitle,
                                Album = album,
                                Playing = isPlaying,
                                Index = index + 1,
                                TotalTracks = tracks.Length,
                                ImageUrl = imageUrl,
                                YearStr = year,
                                Url = url,
                            });
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
                this.customArtworkUrl = "";
            }

            public bool showYear { get; set; }
            public string customArtworkUrl { get; set; }

            public object Clone()
            {
                return base.MemberwiseClone();
            }
        }
    }
}
