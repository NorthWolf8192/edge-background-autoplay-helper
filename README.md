# Edge Background Autoplay Helper

Windows 托盘助手：保留 Microsoft Edge 原生新标签页，通过 Windows UI Automation 尝试启动视频背景。

A Windows tray utility that attempts to start video backgrounds on Edge's original new tab page through UI Automation.

**当前版本：v0.2.0，实验版。** 编译和标题识别测试已通过；是否能操作背景播放按钮，取决于你的 Edge 是否通过辅助功能接口暴露了可调用的按钮。程序不会确认视频画面是否真的开始播放。

## 下载与使用

从本仓库的 **Releases** 下载 ZIP，解压到一个可写文件夹，然后：

1. 双击 **EdgeBackgroundHelper.exe**。右下角系统托盘会出现助手图标。
2. 在 Edge 打开原生新标签页，等待视频背景及右下角三角形播放按钮加载完成。
3. 保持 Edge 在前台，把鼠标停在播放按钮上，**不点击**，按 **Ctrl + Alt + F8**。
4. 收到“已录入播放按钮”提示后，观察背景是否启动，再打开几个新标签页验证。

快捷键被占用时：右键托盘图标，选择“录入播放按钮（5 秒后）”，随后切回 Edge，将鼠标放到播放按钮上。

右键托盘图标可启用／暂停自动播放、查看状态或退出。按钮配置会保存，后续启动无需重复录入。程序不会设置开机启动。

升级前，请先在托盘退出旧版；同一时间只能运行一个助手实例。

## 判断逻辑

- 每秒唤醒一次；未启用或未录入按钮时跳过页面检查。
- 当前前台进程必须是 Edge，窗口标题须匹配中英文新标签页格式，地址栏须为空或 `edge://newtab`，选中标签须是新标签页。
- 支持窗口标题附加“和另外 X 个页面”／“and X other tabs”，忽略零宽格式字符。
- 录入时要求鼠标下的按钮属于可见网页文档、位于右下区域、名称包含播放含义且不包含暂停含义，并支持 `InvokePattern`。
- 记住名称、AutomationId、ClassName 和相对于网页右下角的位置。
- 自动处理时必须只有一个匹配按钮，并在操作前重新确认当前页面。
- 每个识别到的标签页／文档最多触发一次；同一文档里手动暂停后不再次恢复。

仅处理前台 Edge 新标签页。已经处理的页面仍需经过前面的页面检查，随后跳过按钮查找。

## 兼容性与排查

适用于带 .NET Framework 和 Windows PowerShell 5.1 的 Windows；识别规则覆盖部分中文／英文 Edge 界面。浏览器版本、语言、全屏模式、显示缩放和布局变化都可能影响识别。

| 状态 | 含义 |
| --- | --- |
| 未能确认原生新标签页 | 窗口、地址栏、选中标签或网页文档未匹配；状态会说明失败的环节。 |
| 未识别到右下角可用按钮 | 鼠标未停在目标按钮上，或按钮未通过辅助功能接口暴露。 |
| 按钮不支持明确的播放操作 | 名称缺失／含糊、当前是暂停按钮，或没有 InvokePattern。 |
| 多个匹配按钮 | 已跳过操作。 |
| 已发送播放操作 | 按钮调用没有报错；仍需观察视频确认播放效果。 |

查看托盘“查看状态”或程序旁边的 `helper.log`；启动失败查看 `startup-error.txt`。页面布局改变后可重新录入。

文档运行 ID 可能被系统重复使用。程序会保守跳过已经记录的 ID，重新启动助手可清空本次运行记录。

## 数据与权限

程序不联网，不修改注册表或 Edge 配置，不安装服务或计划任务，不要求管理员权限。通过 Windows 按钮接口调用，不移动鼠标、发送鼠标点击或抢占窗口焦点。

为识别目标会读取 Edge 的辅助功能界面。按钮配置存放在程序旁的 `selector.xml`；日志记录状态、按钮名称和错误，录入失败时可能记录实际窗口标题。没有遥测或上传功能。提交 Issue 时请自行检查日志中的窗口标题和本地路径。

## 开销

一次 20 秒样本中，16 个逻辑处理器的整机归一化 CPU 平均约 **0.024%**，最高的 1 秒平均约 **0.097%**，工作集约 **59 MiB**。这只是单次使用状态下的样本，不能代表所有场景；视频播放的开销另计入 Edge 进程。

## 从源码构建

在仓库根目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\TitleRules.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

使用系统自带的 **Windows PowerShell 5.1**；构建脚本会检查运行环境。这里的 ExecutionPolicy 参数仅对当前进程生效。

- `src/Helper.cs`：C# 程序源码。
- `scripts/build.ps1`：编译到 `dist/EdgeBackgroundHelper.exe`。
- `tests/TitleRules.Tests.ps1`：15 个标题识别检查，包含附加页面数量、零宽字符及普通网页反例。
- `scripts/package.ps1`：打包程序、源码、说明及备用启动文件，生成 SHA-256 校验文件。
- `packaging/`：可选的源码启动入口；发布包内与 Helper.cs 放在同一目录。平时直接运行 EXE 即可。

源码启动入口通过当前 PowerShell 进程编译运行，不改变系统执行策略；无需管理员权限。

## 技术参考

- [Microsoft Edge 的 UI Automation 支持](https://learn.microsoft.com/en-us/microsoft-edge/accessibility/)
- [AutomationElement.FromPoint](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationelement.frompoint)
- [InvokePattern.Invoke](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.invokepattern.invoke)

