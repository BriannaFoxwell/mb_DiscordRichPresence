using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

using DiscordInterface;
using Util;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();

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
            DiscordRPC.DiscordEventHandlers handlers = new DiscordRPC.DiscordEventHandlers();
            handlers.readyCallback = HandleReadyCallback;
            handlers.errorCallback = HandleErrorCallback;
            handlers.disconnectedCallback = HandleDisconnectedCallback;
			// Kuunikal's dev app client ID
            DiscordRPC.Initialize("519949979176140821", ref handlers, true, null);
        }

        private void HandleReadyCallback() { }
        private void HandleErrorCallback(int errorCode, string message) { }
        private void HandleDisconnectedCallback(int errorCode, string message) { }

        private void UpdatePresence(string artist, string track, string album, string duration, Boolean playing)
        {
            DiscordRPC.RichPresence presence = new DiscordRPC.RichPresence();

            string bitrate = mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Bitrate);
            string codec = this.mbApiInterface.NowPlaying_GetFileProperty(Plugin.FilePropertyType.Kind);


            /* Discord RPC doesn't like strings that are only one character long, so I
			   add a space after each track to make sure it's over 1 character long */
            track = Utility.Utf16ToUtf8(track + " ");
			artist = Utility.Utf16ToUtf8("by " + artist);               // Next line, shows the artist
			// There are characters at the end of each line which Discord renders poorly 
			// (side-effect of Utf8, I guess?) so we need touse a substring instead
			presence.state = artist.Substring(0, artist.Length - 1);
			presence.details = track.Substring(0, track.Length - 1) + "[" + mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Duration) + "]";

			// Hovering over the image presents the album name
			presence.largeImageText = album;

			/* Next block  is fetching the album image from Discord's 
			   server. They don't allow spaces in their file names, so 
			   we need to convert them into underscores. */

			char[] albumArray = album.ToCharArray();    // Create a char array because we can't edit strings

			// Search album string for spaces
			for (int i = 0; i < album.Length; i++)
			{
				// If the current character is a space, turn it into an underscore
				if (album[i] == ' ') albumArray[i] = '_';
				// Otherwise, just continue on
				else albumArray[i] = album[i];
			}
			// Create a string from the array, in lowercase
			string newAlbum = new String(albumArray).ToLower();
			// Set the album art to the manipulated album string.
			presence.largeImageKey = "albumart";

            // Set the small image to the playback status.

            if (playing)
            {
                presence.smallImageKey = "playing";
            }
            if (playing)
            {
                presence.smallImageText = bitrate + "bps [" + codec + "]";
            }
            bool flag2 = !playing;
            if (flag2)
            {
                presence.smallImageKey = "paused";
            }
            bool flag3 = !playing;
            if (flag3)
            {
                presence.smallImageText = "Paused";
            }



            long now = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            string[] durations = duration.Split(new char[]
            {
                ':'
            });
            long end = now + Convert.ToInt64(durations[0]) * 60L + Convert.ToInt64(durations[1]);
            TimeSpan t = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1));
            if (playing)
            {
                presence.endTimestamp = end - (long)(this.mbApiInterface.Player_GetPosition() / 1000);
            }


            DiscordRPC.UpdatePresence(ref presence);
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
            DiscordRPC.Shutdown();
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
            string trackTitle = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.TrackTitle);
			string album = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Album);
            string duration = mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Duration);
            // mbApiInterface.NowPlaying_GetDuration();
            int position = mbApiInterface.Player_GetPosition();
			// Check if there isn't an artist for the current song. If so, replace it with "(unknown artist)".
            if (string.IsNullOrEmpty(artist)) { artist = "(unknown artist)"; }
            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                    // perform startup initialisation
                case NotificationType.PlayStateChanged:
                    switch (mbApiInterface.Player_GetPlayState())
                    {
                        case PlayState.Playing:
                            UpdatePresence(artist, trackTitle, album, duration, true);
                            break;
                        case PlayState.Paused:
                            UpdatePresence(artist, trackTitle, album, duration, false);
                            break;
                    }
                    break;
                case NotificationType.TrackChanged:
                    UpdatePresence(artist, trackTitle, album, duration, true);
                    break;
            }
        }
   }
}