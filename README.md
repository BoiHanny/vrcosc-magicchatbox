![MagicOSC_icon](https://user-images.githubusercontent.com/114599052/194428052-3e5d0018-4a96-405d-b2e2-c7db16d02940.png)
[![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/BoiHanny/vrcosc-magicchatbox?color=%23512BD4&label=%20&style=plastic)](https://github.com/BoiHanny/vrcosc-magicchatbox/releases/latest)
[![downloads](https://img.shields.io/github/downloads/BoiHanny/vrcosc-magicchatbox/total?color=%23512BD4&label=Total%20download&logo=docusign&logoColor=white&style=plastic)](https://tooomm.github.io/github-release-stats/?username=BoiHanny&repository=vrcosc-magicchatbox)
[![GitHub Release Date - Published_At](https://img.shields.io/github/release-date/BoiHanny/vrcosc-magicchatbox?color=%23512BD4&label=Last%20update&style=plastic)](https://github.com/BoiHanny/vrcosc-magicchatbox/releases)
[![GitHub top language](https://img.shields.io/github/languages/top/BoiHanny/vrcosc-magicchatbox?color=%23512BD4&style=plastic)](https://github.com/search?q=repo%3ABoiHanny%2Fvrcosc-magicchatbox++language%3AC%23&type=code)
[![NET](https://img.shields.io/badge/.NET%206-Runtime%20-%23512BD4?style=plastic)](https://dotnet.microsoft.com/en-us/download)
[![Discord](https://img.shields.io/discord/1078818850218450994?color=%23512BD4&label=VR%20OSC&logo=discord&logoColor=white&style=plastic)](https://discord.gg/ZaSFwBfhvG)
[![Coffee](https://img.shields.io/badge/Send-A%20Coffee-FFDD00?style=plastic&logo=buymeacoffee&logoColor=white)](https://www.buymeacoffee.com/BoiHanny)
--

[![Download zip](https://custom-icon-badges.herokuapp.com/badge/-Download-%23512BD4?style=for-the-badge&logo=download&logoColor=white "Download")](https://github.com/BoiHanny/vrcosc-magicchatbox/releases/download/v0.8.885/MagicChatbox-0.8.885.zip)
[![Download zip](https://custom-icon-badges.herokuapp.com/badge/-Scan%20at%20VirusTotal-blue?style=for-the-badge&logo=virustotal&logoColor=white "virustotal")](https://www.virustotal.com/gui/file/4e4afed7cbed28bdae6da8ca0ad87c8bb9cdc72b27ff1c1348af502815a63910/detection)
<!-- END LATEST DOWNLOAD BUTTON -->

**Introducing MagicChatbox: the ultimate companion for all your VRChat needs! Whether you're on desktop or in VR, we've got you covered with our compact and modern UI.**


![Version_0 8](https://github.com/BoiHanny/vrcosc-magicchatbox/assets/114599052/f8394c0a-699b-43ce-b5dd-70b4f6fa2f26)



### Download and Installation
1. Download the [zip file](https://github.com/BoiHanny/vrcosc-magicchatbox/releases/download/v0.8.885/MagicChatbox-0.8.885.zip).
2. Make sure you have [.NET 6](https://dotnet.microsoft.com/en-us/download) installed.
3. Right-click and choose the option 'extract all'.
4. By default it will create a new folder in your download folder (you can also extract the content to other locations).
5. When extracted, the folder will open.
6. Run the file 'MagicChatbox.exe'.

[**Please make sure OSC is enabled in VRChat**](https://youtu.be/OHjN_q6RqGY?t=80)

![image](https://github.com/BoiHanny/vrcosc-magicchatbox/assets/114599052/616efa5c-9126-4364-8782-975b1d2bb5db)


### Features

- **Extended Media Support:** We've introduced `MediaLink` to the integration tab and incorporated support for YouTube Music, SoundCloud, and even YouTube videos with the help of the `Windows Media API`. 

- **VR mode:** MagicChatbox shows "In VR" on the UI, and you have the option to display your local time in-game. MagicChatbox also provides a Spotify integration that shows the currently playing song, adding to the immersive experience. You can enable or disable this feature from the options menu.

- **Desktop mode:** MagicChatbox shows "On desktop" on the UI, and displays which application you are currently focused on. It's a great way to keep track of your messages while multitasking. The Spotify integration also shows the currently playing song, making it easier to keep up with your favorite tunes.

- **Heart Rate Display**: Display your heart rate in real-time using Pulsoid-supported devices. This feature requires a 'BRO plan' from Pulsoid.

- **Enhanced Heart Rate Monitoring:** Enhanced your heart rate monitoring experience with the addition of "Smooth Heart Rate" and a "Heart Rate Trend Indicator" under `Options > Heart Monitoring Options`. Additionally, we've optimized the `HeartRateConnector` to provide smoother and more accurate heart rate readings. 

- **Status tab:** One of the most exciting changes we made back in version 0.4.0 is the addition of the Status tab, which provides an easy way to manage your status items. You can sort your status items based on creation date, recent usage, and if they are a favorite. Each status item has three types of interactions - activate, delete, and favorite - and you can quickly add new items using the input box.

- **Personal Message integration:** In addition to the Status tab, we have also added a new Personal Message integration, allowing you to easily share messages with others.

- **Chatting:** allows you to quickly send messages of up to 140 characters. The UI displays the last five messages in a scroll viewer that fades out, and you can copy or resend any of these messages. Additionally, when sending a new message, you can see a countdown of how long it will last (you can set this value in options), and there is a stop button to clear the message instantly in VRChat. You can also clear your message history (last 5 messages) with the Clear History button.

- **Chat Message Editing**: Your chat messages are no longer set in stone! This feature lets you edit sent messages with two modes at your disposal:
   - **'Live' mode** for real-time editing. See changes as you type!
   - **'On Confirm' mode** lets you revise at leisure and hit `ENTER` to apply changes.

- **Time options:** MagicChatbox offers an option to show only the current time in VR, without the "my time:" prefix. You can choose to display the time in a 24-hour format, making it easier to read. u can also set a custom time zone and auto-apply or toggle daylight saving time.

- **In-app updates**: Keep MagicChatbox up-to-date with the in-app update feature. It communicates with the GitHub API and pulls the .ZIP file from the latest branch.

- **Simplified Beta Updates:** A new update module for beta builds has been introduced, offering seamless beta updates and an option to downgrade if you want to switch back to a stable version. An option has also been added to opt-out of the alpha channel.

- **OSC customization:** We have updated our codebase to support UTF-8 and made the switch from Sharp OSC to CoreOSC-VRC-UTF8, which was a collaborative effort with VRCWizard. MagicChatbox also provides an option to change the OSC IP and port from the options menu, allowing for more customization.

- **2nd VRChat Output Option:** We've introduced a new feature under `Options > App Options > 2nd VRChat output`, enabling you to send OSC messages to a second VRChat session.
 
- **Integration Separator Toggle:** A new option to switch the integration separator from '┆' to 'Enter' for cleaner VRChat interactions.

- **Improved Windows Activity Integration & Control in Settings:** We've made improvements to the Windows activity integration, providing more accurate information about your current Windows activity. You can now manage the Window Activity feature directly from the settings for easier control.

- **Enhanced Application Names Setting:** Customize the names of your applications in the settings for a more personalized experience.

- **Local save and version checker:** We have implemented a local JSON file to save your status items, ensuring they are always available to you. The application also features a built-in version checker that informs you if you have the latest version, if a new version is available, or if you are running a preview version.

- **Overload feature:** Finally, MagicChatbox features a unique overload feature that disables some integrations automatically if the number of characters in your message exceeds 140. The order of disabled integrations will be as follows: Personal Message, Windows Activity, Current Time, and finally Spotify. MagicChatbox will try to fill the 144 character cap, but when above it, it will disable the integration.

- **Text to Speech:** allows you to communicate better with users in VRChat, you have a wide range of voices/languages. In settings, you can "Toggle VRChat voice on TTS", "Stop current playing TTS on new chat" and select the output device. We recommend using VoiceMeter or just a virtual audio cable.

- **Options** tab allows you to customize your experience by enabling/disabling options.

### Contact
Have any questions, suggestions, or feedback regarding MagicChatbox? We'd love to hear from you! Feel free to reach out to us through one of the following channels:

- **Discord:**  [![](https://dcbadge.vercel.app/api/server/ZaSFwBfhvG)](https://discord.gg/ZaSFwBfhvG)
- **Github Issues:**  [Report a bug or submit a feature request](https://github.com/BoiHanny/vrcosc-magicchatbox/issues)
- Wiki: [Help & more](https://github.com/BoiHanny/vrcosc-magicchatbox/wiki)

We're committed to providing the best possible experience for our users, and your feedback helps us make MagicChatbox even better.
