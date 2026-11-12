# GetUpLock

[简体中文](README_zh.md) · [Project website](https://up.xjwisdomsoft.cn/)

**Make movement breaks a habit without relying on willpower.**

If you spend long stretches at your desk, GetUpLock locks your screen every 20 minutes by default. Do 10 squats in view of your camera to unlock it. If you don’t have a camera, choose the 20-second timer and use that time to get up and move.

## A habit you can stick with :P

**Benefits of squats**

1. **Stronger legs and glutes.** Regular practice can make everyday movements easier, from getting up off a low sofa to climbing stairs. [CDC strength training guide](https://www.cdc.gov/physicalactivity/downloads/growing_stronger.pdf)
2. **More muscle in your glutes and thighs.** If building muscle is your goal, squats work both areas. In a nine-week study, people who trained with barbell squats gained muscle in their glutes and thighs. [Nine-week squat training study](https://pubmed.ncbi.nlm.nih.gov/37461495/)
3. **Support for bone health.** Regular weight-bearing exercise such as squats can help keep muscles and bones healthy. [NHS exercise guidance for menopause](https://www.nhs.uk/conditions/menopause-and-perimenopause/things-you-can-do/)
4. **A short-term rise in testosterone after training.** In one study, researchers measured a temporary rise in testosterone after men with strength training experience performed several sets of weighted squats. A review of 11 randomized trials found that, in men who were relatively inactive and started with normal testosterone levels, several weeks of training had little effect on resting testosterone on average. [Study of hormone levels after squats](https://pubmed.ncbi.nlm.nih.gov/24276305/) · [Review of training and resting testosterone](https://pubmed.ncbi.nlm.nih.gov/35134000/)

## Install

Open this repository’s **Releases** page and choose a Windows x64 installer. Both editions run on Windows 10 and 11. You can start using them without an account.

GetUpLock starts automatically when you sign in to Windows. To turn this off, right-click the tray icon and clear **Start with Windows**. Updates preserve your choice.

| Edition | Installer name begins with | Feedback                                                      |
| ------- | -------------------------- | ------------------------------------------------------------- |
| English | `GetUpLockPublicEN-Setup-` | [English feedback form on Tally](https://tally.so/r/VpQzxg)   |
| 简体中文    | `GetUpLockPublic-Setup-`   | Scan the WeChat code in the app to open Tencent Questionnaire |

## Try your first break

1. Right-click the tray icon and choose **Start now**. For scheduled breaks, choose **Reminder interval** and pick 20, 25, 30 or 45 minutes.
2. Stand comfortably until the camera recognizes your starting position. Squat and stand back up to add a rep. The counter is on the right, and the poster on the left requires no interaction.
3. The screen unlocks when you reach the target. To use the 20-second timer, select **Turn off camera tracking** in the tray or **Camera off** during a break. The screen unlocks when the timer runs out. The app remembers your choice. Turning tracking back on takes effect at the next break.

If something comes up, select **Skip** or press **Ctrl + Shift + Alt + Q**. By default, you can skip once with the password `1234`. To skip again in a row, type the letters A–Z in order. This extra step makes it harder to dismiss every break out of habit. Once you complete a set, you can use the password again. Before a meeting, choose **Pause reminders** for 30 minutes, one hour, or until you resume reminders manually or restart the app.

## Privacy and network use

The public editions process camera frames on the PC. They do not record video or upload frames or workout history. Local settings and completed sets are stored under `%LOCALAPPDATA%\GetUpLockPublicEN` for English and `%LOCALAPPDATA%\GetUpLockPublic` for Chinese.

## Build from source

Use 64-bit Windows, .NET Framework 4.8 and PowerShell. Run these commands from the repository root.

```powershell
.\build.ps1 -Language en
.\build\en\app\GetUpLock.exe

.\build.ps1 -Language zh
.\build\app\GetUpLock.exe
```

To create a Chinese installer, install NSIS 3 and run `.\build.ps1 -Language zh -Installer -NsisPath 'C:\Program Files (x86)\NSIS\makensis.exe'`. The build extracts the bundled MediaPipe engine and verifies both its hash and the pose model’s hash. Third-party licenses and notices are in `vendor/mediapipe/`.
