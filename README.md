# MagicChatBox 

![image](https://github.com/user-attachments/assets/3e4cf513-c87e-4ad0-b9d2-b0f1c24cb6d3)

[![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/BoiHanny/vrcosc-magicchatbox?color=%23512BD4&label=%20&style=plastic)](https://github.com/BoiHanny/vrcosc-magicchatbox/releases/latest) [![downloads](https://img.shields.io/github/downloads/BoiHanny/vrcosc-magicchatbox/total?color=%23512BD4&label=Total%20download&logo=docusign&logoColor=white&style=plastic)](https://tooomm.github.io/github-release-stats/?username=BoiHanny&repository=vrcosc-magicchatbox) [![GitHub Release Date - Published_At](https://img.shields.io/github/release-date/BoiHanny/vrcosc-magicchatbox?color=%23512BD4&label=Last%20update&style=plastic)](https://github.com/BoiHanny/vrcosc-magicchatbox/releases) [![NET](https://img.shields.io/badge/.NET%2010-Runtime%20-%23512BD4?style=plastic)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) [![Discord](https://img.shields.io/discord/1078818850218450994?color=%23512BD4&label=MagicChatbox&logo=discord&logoColor=white&style=plastic)](https://discord.gg/magicchatbox)

---

`Welcome to MagicChatBox – the ultimate VRChat upgrade! Unlock new features, enhance your interactions, and take your virtual experience to the next level. Now powered by .NET 10 for faster performance, improved stability, and a modernized interface.`

> [!IMPORTANT]
> **We highly recommend reading our [Terms of Service](https://github.com/BoiHanny/vrcosc-magicchatbox/blob/master/Security.md) before you download or use MagicChatBox.**  
> It doesn't take long to get through the essential points, but it's important to understand how we value and protect your privacy, as well as the rules for using our software.

## Installation

[![Download zip](https://custom-icon-badges.herokuapp.com/badge/-Download-%23512BD4?style=for-the-badge&logo=download&logoColor=white "Download")](https://github.com/BoiHanny/vrcosc-magicchatbox/releases/latest)
[![Download zip](https://custom-icon-badges.herokuapp.com/badge/-Scan%20at%20VirusTotal-blue?style=for-the-badge&logo=virustotal&logoColor=white "virustotal")](https://www.virustotal.com/gui/file/01533802fb696b6dd746b05367fd97a5d9280e6f24cd13fa3032a784a774a290/detection)

- 🔳 [Download](https://github.com/BoiHanny/vrcosc-magicchatbox/releases/latest) **the latest release of MagicChatBox**
- 🔳 [Download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) **.NET 10 Desktop Runtime from Microsoft**
- 🔳 Extract the ZIP File into a folder
- 🔳 Run **MagicChatBox.exe**
- 🔳 You're good to go now!

If you need any further help `setting up` the program, join our `Discord Server`.

> [!IMPORTANT]
> **You need to [enable OSC](https://youtu.be/OHjN_q6RqGY?t=80) inside VRChat in order to have the program working!**

## Features

### 💭 Personal Status
Set and cycle through custom status messages displayed directly in your VRChat chatbox. Supports multiple status lines, custom prefixes, and automatic rotation intervals.

### 🧭 Window Activity
Automatically detects and displays the name of your currently focused Windows application in VRChat. Includes privacy filtering to hide sensitive window titles and resolves friendly application display names.

### 🩵 Heart Rate (Pulsoid)
Connects to Pulsoid via WebSocket to display real-time BPM from your wearable heart rate monitor. Features smoothing algorithms, history tracking, and customizable display formatting with trend indicators.

> [!IMPORTANT]
> **Heart Rate** requires an official `Pulsoid Member` subscription.

### 🎛️ Component Stats
Monitors your hardware in real time using LibreHardwareMonitor sensors. Displays CPU usage/temperature, GPU usage/temperature, RAM utilization (with DDR version detection), and VRAM consumption — all formatted for the VRChat chatbox.

### 🧠 IntelliChat
AI-powered text enhancement directly in your chat workflow. Offers smart autocompletion, grammar correction, translation to any language, and tone adjustments — all powered by OpenAI's chat API for fast inline suggestions.

### ⚗️ OpenAI & Whisper
Full OpenAI integration providing both text generation (GPT) and speech-to-text transcription (Whisper). Whisper supports multiple languages, noise gating, silence detection, and optional translation — turning your voice into VRChat chatbox text hands-free.

### 🔊 Text-to-Speech (TTS)
Converts your typed messages into spoken audio using the TikTok TTS API. Choose from multiple voice options and hear your messages played back or sent over OSC.

### ⛓️‍💥 Network Stats
Monitors your active network interface and displays real-time download/upload speeds, total data transferred, and network utilization percentage — ideal for showing connection quality in VRChat.

### 📆 Time Status
Displays the current date and time in fully customizable formats. Supports timezone configuration and flexible formatting options to match your style.

### 🎶 Media Activity
Captures playback state and metadata from any Windows media source via the system media transport controls. Shows song title, artist, album, and playback status from any media player.

### 🎵 Spotify Integration
Dedicated Spotify module with full OAuth authentication. Displays rich track details including song name, artist, album, and playback progress directly from the Spotify Web API with automatic token refresh.

### 💤 AFK Module
Detects keyboard and mouse inactivity and automatically displays an AFK status with configurable idle threshold, customizable prefix, and an elapsed-time counter showing how long you've been away.

### 🎼 Soundpad
Connects to the Soundpad application via named pipe to trigger sound effects remotely. Displays the currently playing sound and provides playback control directly from MagicChatBox.

### 🎮 Discord Integration
Full Discord Rich Presence support showing your MagicChatBox activity on your profile. Also tracks voice channel membership, speaking/mute/deafen status, and participant count via Discord IPC with secure PKCE OAuth and automatic token refresh.

### 📡 Twitch Integration
Connects to the Twitch API to display your live stream status, current viewer count, follower count, game category, and stream title. Includes built-in buttons to send chat announcements and shoutouts to other streamers.

### 🔋 VR Tracker Battery
Reads battery levels from your SteamVR trackers, controllers, and headset via OpenVR. Displays formatted battery percentages with customizable device icons so you always know when it's time to charge.

### 🌐 VRChat Radar
Parses VRChat's output log in real time to extract world information, player join/leave events, instance metadata, and session statistics. Tracks encounters with other players across sessions.

### 🌤️ Weather
Fetches current weather conditions from the Open-Meteo API. Supports location lookup by city name, GPS coordinates, or automatic IP-based detection. Displays temperature, conditions, and weather icons in your chatbox.

---

## UI & Interface Improvements

The latest releases include a full interface overhaul:

- **Redesigned Options Page** — modular per-section settings with consistent XAML styling, grouped by category for faster navigation
- **Status List Grouping & Sorting** — status items are now organized into logical groups with batch enable/disable operations
- **In-App Toast Notifications** — non-intrusive toasts replace disruptive modal dialogs for status updates and confirmations
- **Integrations Page** — dedicated page for managing all third-party connections (Spotify, Discord, Twitch, Pulsoid) with real-time connection status
- **Chatting Page** — streamlined chat interface with IntelliChat enhancements, spell check, translation controls, and speech-to-text toggle
- **TOS & Privacy Wizard** — first-run setup wizard that walks new users through terms acceptance and privacy preferences
- **Standardized Dialog Ownership** — all dialogs now properly inherit parent window context, preventing z-order and focus issues
- **Consistent Styling** — unified gradient themes, settings checkbox styles, and icon sets across all pages

---

## What's New in v0.9.x (.NET 10)

- **Runtime upgrade to .NET 10** — faster startup, reduced memory footprint, and access to the latest Windows platform APIs (Windows 10 26100+)
- **Improved resource management and thread safety** — eliminated race conditions in background services for a more stable, crash-free experience
- **OAuth reliability** — fixed Spotify redirect URI handling (127.0.0.1 vs localhost), Discord PKCE flow with refresh token support, and proper scope configuration
- **Enhanced exception handling** — graceful recovery during updates and rollbacks with structured NLog logging and clearer error reporting
- **Update & rollback safety** — improved self-update logic with safer file replacement and automatic rollback on failure
- **Spotify integration** — full OAuth-based playback display with automatic token refresh and rich track metadata
- **Discord integration** — IPC-based voice channel tracking, Rich Presence, and secure PKCE authentication
- **Twitch integration** — live stream status, follower/viewer counts, announcements, and shoutouts
- **VRChat Radar** — real-time log parsing for world info, player encounters, and session tracking
- **Weather module** — current conditions display via Open-Meteo with flexible location detection

## Support

If you need more help, feel free to click the button below:

> [!NOTE]
> **Our support team is here to assist you with any issues!**

[![SUPPORT](https://github.com/user-attachments/assets/c08772f1-3075-4590-9744-3bcbcd15cfe9)](https://discord.gg/magicchatbox)

---

### 📚 Additional Resources
- [FAQ](information/FAQ.md) – Frequently Asked Questions and Answers
- [Staff](information/Staff.md) – Meet the team behind MagicChatBox
- [Rating](information/Rating.md) – Our User Ratings
- [Contact](https://discord.gg/magicchatbox) – Create a support ticket here
- [Funding](information/Funding.md) – Our community's advocates
- [Documentation](https://github.com/BoiHanny/vrcosc-magicchatbox/wiki) – Detailed guides and manuals

---

## Legal Notice

> [!IMPORTANT]
> **Legal Notice**  
> MagicChatBox is released under a custom, source‑available proprietary license. Please review the following legal documents for important information regarding the use, modification, and distribution of MagicChatBox:
> 
> - **[Software License Agreement (SLA)](https://github.com/BoiHanny/vrcosc-magicchatbox/blob/master/License.md)**  
>   This agreement outlines the rights and restrictions for modifying, redistributing, or creating derivative works of MagicChatBox. Any modifications or forks must include this SLA and the accompanying Terms of Service.
> 
> - **[Terms of Service (TOS)](https://github.com/BoiHanny/vrcosc-magicchatbox/blob/master/Security.md)**  
>   These terms govern your conduct and usage of MagicChatBox. By running the software, you agree to abide by these Terms, which help maintain a respectful, safe, and lawful user experience.
> 
> By using MagicChatBox, you confirm that you have read, understood, and agree to be bound by these legal documents. If you do not agree, you are not permitted to use the software.

---

*Thank you for choosing MagicChatBox – we hope it enhances your VRChat experience!*
