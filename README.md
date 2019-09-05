# Discord Rich Presence Plugin for MusicBee

This [MusicBee](http://getmusicbee.com) plugin shows your currently playing song as Rich Presence on [Discord](https://discordapp.com/).

![Example Screenshot](https://i.imgur.com/cTKx7p8.png)

## Requirements

- MusicBee v3.x (Untested on v2.x)
- Visual C++ Redistributable for Visual Studio 2015/7

## Installation

- Download the (non-source code) zip file from the [latest release](https://github.com/Kuunikal/mb_DiscordRichPresence/releases/latest)

Either
- Load the plugin through MusicBee
  - Edit -> Preferences (Ctrl+O) -> Plugins -> Add Plugin -> Select the zip file

or
- Copy the DLL files to the Plugins folder
  - Extract the zip file
  - Copy the DLL files to the `MusicBee\Plugins` directory (most likely `C:\Program Files (x86)\MusicBee\Plugins` or `%appdata%\MusicBee\Plugins`)
  - Re/start MusicBee

## Usage

- Make sure you have MusicBee selected to be displayed as your Game Activity on Discord
  - User Settings -> Games -> Display currently running game as status message.
  - Add it! -> Select MusicBee

### Creating a developer application for custom album artwork

If you want to include your own album art (up to 150 albums!), you can easily do so by creating a Discord developer app.

- Go to [Discord's developer application page](https://discordapp.com/developers/applications/me)
- Create a new app. Call it something like MusicBee.
- Copy the client ID (located in the *app details* section at the top of the page)
- Paste it into [line 47 of the main file](https://github.com/Kuunikal/mb_DiscordRichPresence/blob/master/mb_DiscordRichPresence.cs#L47)
- On the webpage of your new developer app, click **Enable Rich Presence** at the bottom of the page.
- Make sure to include the playing/paused icons. They are available for download [here](https://imgur.com/a/WCZgD).
- You should be good to go. Keep the following in mind when uploading album images:
  - Characters like spaces turn into **underscores**. (found on [this line](https://github.com/Kuunikal/mb_DiscordRichPresence/blob/master/mb_DiscordRichPresence.cs#L80). You can add options for other characters, too) For example, if you are listening to *Since I Left You* by The Avalanches, the uploaded album name would have to be named "since_i_left_you".

## Support

Feel free to contact me (Kuunikal) on Discord if you need help. My info is in the screenshot.
