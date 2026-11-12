# 久坐深蹲锁 GetUpLock

[English](README.md) · [项目官网](https://up.xjwisdomsoft.cn/)

**不需要靠自律，让坚持变得简单一点！**

这是一款为久坐人群打造的软件，默认每 20 分钟锁住电脑屏幕，必须做10 个深蹲，屏幕才能解锁。如果你没有摄像头，可以选择 20 秒倒计时给自己留一段活动时间。## 让坚持变得简单一点 :P

**深蹲的好处：**

1. **增强臀腿力量。** 规律练习后，从低沙发站起来、爬楼梯等需要下肢发力的动作会更省力。[美国疾控中心力量训练指南](https://www.cdc.gov/physicalactivity/downloads/growing_stronger.pdf)
2. **塑造臀腿曲线。** 持续 9 周后，臀大肌和大腿肌肉都有增长，尤其适合以塑形为目标的人。
3. **帮助维持骨骼健康。** 保持这类锻炼对维护肌肉和骨骼健康更有意义。[英国 NHS 更年期运动建议](https://www.nhs.uk/conditions/menopause-and-perimenopause/things-you-can-do/)
4. **训练后睾酮短暂升高。** 有训练经验的男性完成多组负重深蹲后，研究人员测到了睾酮短时升高。至于平时的水平，汇总 11 项随机试验的研究发现，睾酮原本正常、平时活动较少的男性经过数周训练，静息睾酮平均变化很小。[深蹲后激素变化实验](https://pubmed.ncbi.nlm.nih.gov/24276305/) · [长期训练研究综述](https://pubmed.ncbi.nlm.nih.gov/35134000/)

## 下载安装

在本仓库的 **Releases** 页面选择安装包。两个版本均适用于 64 位 Windows 10 和 Windows 11，安装后无需注册即可使用。

安装后，每次登录 Windows 都会自动启动软件。右键托盘图标，取消勾选 **开机启动** 即可关闭。更新版本时会保留你的选择。

| 版本      | 安装包文件名前缀                   | 客户反馈                                           |
| ------- | -------------------------- | ---------------------------------------------- |
| 简体中文    | `GetUpLockPublic-Setup-`   | 在软件里用微信扫描腾讯问卷小程序码                              |
| English | `GetUpLockPublicEN-Setup-` | [在浏览器打开英文 Tally 问卷](https://tally.so/r/VpQzxg) |

## 第一次使用

1. 右键单击右下角托盘图标，选 **现在就做** 即可体验一组。需要定时提醒时，在 **间隔提醒** 中选择 20、25、30 或 45 分钟。
2. 站到摄像头能看清的位置，等软件识别好站姿。每次下蹲再站起，右侧计数加一。左侧海报无需点击。
3. 达到目标后自动解锁。没有摄像头或想用倒计时，可以在托盘选择 **关闭摄像头识别**，也可以在运动中点 **关闭摄像头**。倒计时满 20 秒后解锁。软件会记住设置，重新开启识别后从下一组生效。

注意：如果临时有事，点 **本次跳过** 或按 **Ctrl + Shift + Alt + Q**。默认第一次可以输入密码 `1234` 跳过。如果你想下一次连续跳过时，则必须需按顺序输入 A–Z，这样就是防止软件成为形式主义。完成一组后恢复密码跳过。开会前也可以在 **暂停提醒** 里选 30 分钟、1 小时，或暂停到手动恢复或重启。

## 隐私与联网

公开版在本机处理摄像头画面，不录制视频，不上传画面或运动记录。中文版的设置和完成记录保存在 `%LOCALAPPDATA%\GetUpLockPublic`，英文版保存在 `%LOCALAPPDATA%\GetUpLockPublicEN`。

## 从源码构建

在 64 位 Windows 上安装 .NET Framework 4.8，在仓库根目录打开 PowerShell。

```powershell
.\build.ps1 -Language zh
.\build\app\GetUpLock.exe

.\build.ps1 -Language en
.\build\en\app\GetUpLock.exe
```

生成安装包还需要 NSIS 3。例如，运行 `.\build.ps1 -Language zh -Installer -NsisPath 'C:\Program Files (x86)\NSIS\makensis.exe'` 即可生成中文版。构建脚本会从仓库内解压 MediaPipe 引擎，并校验引擎及模型的哈希。第三方许可和声明位于 `vendor/mediapipe/`。
