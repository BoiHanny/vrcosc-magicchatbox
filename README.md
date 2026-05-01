# MagicChatBox 

![image](https://github.com/user-attachments/assets/3e4cf513-c87e-4ad0-b9d2-b0f1c24cb6d3)

[![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/BoiHanny/vrcosc-magicchatbox?color=%23512BD4&label=%20&style=plastic)](https://github.com/BoiHanny/vrcosc-magicchatbox/releases/latest) [![downloads](https://img.shields.io/github/downloads/BoiHanny/vrcosc-magicchatbox/total?color=%23512BD4&label=Total%20download&logo=docusign&logoColor=white&style=plastic)](https://tooomm.github.io/github-release-stats/?username=BoiHanny&repository=vrcosc-magicchatbox) [![GitHub Release Date - Published_At](https://img.shields.io/github/release-date/BoiHanny/vrcosc-magicchatbox?color=%23512BD4&label=Last%20update&style=plastic)](https://github.com/BoiHanny/vrcosc-magicchatbox/releases) [![NET](https://img.shields.io/badge/.NET%2010-Runtime%20-%23512BD4?style=plastic)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) [![Discord](https://img.shields.io/discord/1078818850218450994?color=%23512BD4&label=MagicChatbox&logo=discord&logoColor=white&style=plastic)](https://discord.gg/magicchatbox)

---

`Welcome to MagicChatBox – the ultimate VRChat upgrade! Unlock new features, enhance your interactions, and take your virtual experience to the next level.`

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

---

## ✨ Feature Showcase

MagicChatBox comes packed with **18+ modules** that run simultaneously, feeding real-time information into your VRChat chatbox. Every module is fully configurable — enable only what you want, customize formatting, set display priorities, and create your perfect chatbox layout.

---

<details>
<summary><h3>💭 Personal Status</h3></summary>

> Express yourself with rotating, fully customizable status messages.

- Create **unlimited status lines** with individual on/off toggles
- Set automatic **rotation intervals** so your status cycles through messages
- Use **custom prefixes and separators** to style your chatbox output
- Priority system ensures your status only shows when higher-priority modules aren't active

</details>

<details>
<summary><h3>🧭 Window Activity</h3></summary>

> Let your friends know what you're up to — automatically.

- Detects your **currently focused Windows application** and displays it in VRChat
- Resolves **friendly display names** (shows "Visual Studio Code" instead of "Code.exe")
- Built-in **privacy filter** — add apps to a blocklist so sensitive titles are never shown
- Smart process resolution handles UWP apps, packaged apps, and traditional executables

</details>

<details>
<summary><h3>🩵 Heart Rate — Pulsoid Integration</h3></summary>

> Share your heartbeat with the world in real time.

- Connects to **Pulsoid via WebSocket** for instant BPM updates from any supported wearable
- **Smoothing algorithms** prevent erratic spikes from appearing in your chatbox
- **History tracking** with trend indicators (↑ rising, ↓ falling, → stable)
- Customizable display format with BPM icon, range indicators, and statistical overlays
- Supports Pulsoid statistics API for session averages, min/max, and time-range breakdowns

> [!IMPORTANT]
> **Heart Rate** requires an official `Pulsoid Member` subscription.

</details>

<details>
<summary><h3>🎛️ Component Stats</h3></summary>

> Show off your rig — CPU, GPU, RAM, and more.

- Real-time hardware monitoring via **LibreHardwareMonitor** sensors
- **CPU**: usage percentage and temperature
- **GPU**: load percentage, temperature, and clock speed
- **RAM**: usage with automatic **DDR generation detection** (DDR4/DDR5)
- **VRAM**: dedicated GPU memory consumption
- Choose which components to display and customize the output format

</details>

<details>
<summary><h3>🧠 IntelliChat — AI-Powered Text Enhancement</h3></summary>

> Write smarter, faster, and in any language — without leaving your chat.

- **Smart autocompletion** — start typing and let AI finish your thought
- **Grammar & spelling correction** — fix mistakes with one click
- **Translation** — translate your message to any language before sending
- **Tone adjustments** — make your text more formal, casual, friendly, or concise
- **Custom writing styles** — define named presets with specific instructions and temperature
- Powered by **OpenAI's GPT API** with configurable model selection (GPT-4o, GPT-4o-mini, etc.)
- Works directly inline in the chatting interface — no separate window needed

</details>

<details>
<summary><h3>⚗️ OpenAI & Whisper — Voice-to-Text</h3></summary>

> Turn your voice into VRChat chatbox text, hands-free.

- **Speech-to-text transcription** using OpenAI Whisper models
- Supports **25+ languages** with automatic language detection
- **Noise gating** filters out background noise below configurable thresholds
- **Silence detection** automatically stops recording when you stop talking
- **Optional translation** — speak in any language, output in your target language
- Configurable model size (base, small, medium, large) for speed vs. accuracy tradeoff
- Real-time audio level visualization while recording

</details>

<details>
<summary><h3>🔊 Text-to-Speech (TTS)</h3></summary>

> Hear your messages spoken aloud — or let others hear you.

- Converts typed chatbox messages into **spoken audio** via the TikTok TTS API
- Choose from **multiple voice options** (male, female, character voices)
- Audio playback locally or routed over **OSC** for in-world effects
- Automatic queuing for back-to-back messages

</details>

<details>
<summary><h3>⛓️‍💥 Network Stats</h3></summary>

> Monitor your connection quality right from your chatbox.

- Displays real-time **download and upload speeds** (Mbps/KBps)
- Shows **total data transferred** in the current session
- **Network utilization percentage** based on interface capacity
- Automatically detects and monitors your **active network adapter**
- Useful for diagnosing lag or showing off your connection in VRChat

</details>

<details>
<summary><h3>📆 Time & Date Status</h3></summary>

> Always know (and show) the time.

- Displays current **date and time** in your VRChat chatbox
- Fully **customizable format strings** (12h/24h, with or without seconds, date layouts)
- **Timezone override** — show a different timezone than your system clock
- Lightweight and always available as a fallback status

</details>

<details>
<summary><h3>🎶 Media Activity — Windows Media</h3></summary>

> Share what you're listening to from any media player.

- Hooks into **Windows System Media Transport Controls** (SMTC)
- Works with any app that reports media state: Spotify, YouTube, VLC, Foobar2000, etc.
- Shows **song title, artist, album**, and playback state (playing/paused)
- Automatic detection — no manual configuration needed

</details>

<details>
<summary><h3>🎵 Spotify Integration</h3></summary>

> Rich, authenticated Spotify playback display with full track details.

- Dedicated module with **full OAuth 2.0 authentication** (token refresh handled automatically)
- Displays **song name, artist, album, and playback progress** from the Spotify Web API
- Higher fidelity than Windows Media — catches podcasts, private sessions, and queue info
- Secure redirect URI handling with automatic localhost binding
- One-click login flow — authenticate once and forget about it

</details>

<details>
<summary><h3>💤 AFK Module</h3></summary>

> Let people know when you've stepped away.

- Detects **keyboard and mouse inactivity** with configurable idle threshold (seconds)
- Automatically displays an **AFK status** with customizable prefix text
- Built-in **elapsed timer** shows exactly how long you've been away
- Configurable formatting: countdown style, compact, or verbose time display
- Respects priority system — won't override higher-priority modules

</details>

<details>
<summary><h3>🎼 Soundpad Integration</h3></summary>

> Trigger sound effects remotely and show what's playing.

- Connects to **Soundpad via named pipe** for low-latency communication
- Displays the **currently playing sound** name in your chatbox
- Remote **playback control** (play, pause, stop) directly from MagicChatBox
- Browse and trigger sounds from your Soundpad library without alt-tabbing

</details>

<details>
<summary><h3>🎮 Discord Integration</h3></summary>

> Voice channel tracking and Rich Presence — fully connected.

- **Discord Rich Presence** — shows your MagicChatBox activity on your Discord profile
- **Voice channel tracking** via Discord IPC named pipe:
  - See who's in your voice channel and total participant count
  - Detect **speaking, muted, and deafened** states for each user
  - Display your own mute/deafen status in VRChat
- Secure authentication using **PKCE OAuth flow** with automatic token refresh
- Configurable client ID for custom Rich Presence applications

</details>

<details>
<summary><h3>📡 Twitch Integration</h3></summary>

> Stream status, viewers, and chat control — all from your chatbox app.

- Displays your **live/offline stream status** with real-time polling
- Shows **current viewer count, follower count**, game category, and stream title
- Built-in **chat commands**: send announcements and shoutouts to other streamers
- **Token validation** with automatic reconnection on expiry
- Configurable refresh intervals (15s–3600s) to balance API usage and freshness

</details>

<details>
<summary><h3>🔋 VR Tracker Battery</h3></summary>

> Never run out of charge mid-session again.

- Reads battery levels from **SteamVR trackers, controllers, and your headset** via OpenVR
- Displays **formatted battery percentages** per device
- **Customizable device icons** — use different emojis or symbols for each tracker type
- Scan summary shows lowest battery and total connected device count
- Warns you when batteries drop below configurable thresholds

</details>

<details>
<summary><h3>🌐 VRChat Radar</h3></summary>

> Real-time world info, player tracking, and session intelligence.

- Parses VRChat's **output_log.txt in real time** (non-blocking, read-only)
- Extracts **world name, instance type** (Public/Friends+/Private), and **region** (EU/US/JP)
- Tracks **player join/leave events** with toast notifications
- **Encounter tracking** — see how many times you've met specific players across sessions
- Session statistics: worlds visited, unique players seen, total join/leave events
- Detects **avatar loading** and **download progress** for large assets
- **Instance master detection** — know if you're the instance owner
- Crasher/avatar-block detection with per-room debounce

</details>

<details>
<summary><h3>🌤️ Weather</h3></summary>

> Current conditions in your chatbox — without checking your phone.

- Fetches live weather data from the **Open-Meteo API** (free, no API key required)
- **Multiple location modes**:
  - 🏙️ City name search
  - 📍 GPS coordinates (latitude/longitude)
  - 🌐 Automatic IP-based geolocation
- Displays **temperature, conditions description**, and weather icon/emoji
- Configurable units (Celsius/Fahrenheit) and refresh intervals
- Lightweight polling — no background WebSocket, minimal resource usage

</details>

---

## 🖥️ Interface & Design

MagicChatBox features a fully modernized WPF interface built for clarity and speed:

<details>
<summary><strong>📋 Status Page</strong> — Your live dashboard</summary>

- Real-time preview of exactly what's being sent to VRChat
- Module **priority visualization** — see which module is currently "winning"
- **Status list grouping** with batch enable/disable toggles
- Sorting by priority, name, or enabled state
- Live character count and OSC send indicator

</details>

<details>
<summary><strong>💬 Chatting Page</strong> — AI-enhanced messaging</summary>

- Direct text input with **live send to VRChat chatbox**
- Integrated **IntelliChat** toolbar (complete, fix, translate, tone shift)
- **Speech-to-text toggle** for Whisper voice input
- Spell check and word count
- Message history with re-send capability

</details>

<details>
<summary><strong>🔗 Integrations Page</strong> — All connections in one place</summary>

- Dedicated management for **Spotify, Discord, Twitch, and Pulsoid**
- Real-time **connection status indicators** (connected/disconnected/error)
- One-click OAuth login flows with token status display
- Per-integration enable/disable without losing credentials

</details>

<details>
<summary><strong>⚙️ Options Page</strong> — Modular settings</summary>

- **Per-module configuration** panels grouped by category
- Consistent checkbox/toggle/slider styling across all sections
- Search and filter to quickly find specific settings
- Settings auto-save with toast confirmation

</details>

<details>
<summary><strong>🎨 Visual Design</strong></summary>

- Unified **gradient themes** with purple/blue accent palette
- **Toast notifications** for non-intrusive status updates (replaces modal dialogs)
- **First-run TOS & privacy wizard** for new users
- Standardized **dialog ownership** — no more floating windows or z-order bugs
- Responsive layout that adapts to different window sizes

</details>

---

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
