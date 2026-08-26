# Easy Resolution Changer

Windows 双屏分辨率快速切换工具，Mac 风格圆角界面。在 Windows 11（24H2，Build 26100）上开发测试。

## 功能

- 自动检测所有接入桌面的显示器（忽略 NVIDIA GameViewer 等虚拟显示器适配器）
- 从 EDID 解析显示器真实型号（如 Samsung H25T7-3、AOC P24A2G）
- 每块显示器独立列出系统实际支持的分辨率 / 刷新率模式
- 三种操作：
  - **应用**：临时切换分辨率
  - **保存**：应用并写入注册表（重启后保持）+ 固化到 `ResolutionChanger.ini`
  - **恢复**：还原成启动时的设置
- 主屏在卡片上带"主屏"标记

## 界面

- Mac 风格圆角窗口，左上角红黄绿交通灯按钮（关闭 / 最小化 / 最大化）
- 左右两块显示器卡片，各带显示器图标、型号名称、分辨率下拉框
- 底部「恢复 / 保存 / 应用」三个按钮

## 使用方法

1. 获取 `ResolutionChanger.exe`（Release 附件或自行编译），双击运行
2. 在每块显示器卡片上选择想要的分辨率 / 刷新率
3. 点击「应用」（临时生效）或「保存」（永久生效）

首次运行无需管理员权限。

## 从源码编译

环境要求：Windows + .NET Framework 4.8（Windows 10/11 自带）

双击运行 `build.cmd`，即调用 .NET Framework 自带的 `csc.exe` 编译生成 `ResolutionChanger.exe`。

## 文件结构

| 文件 | 说明 |
| --- | --- |
| `Program.cs` | 程序入口 |
| `DisplayInfo.cs` | 显示器枚举、模式枚举、EDID 型号解析、分辨率切换（P/Invoke user32.dll） |
| `MainWindow.cs` | WPF 界面（纯 C# 代码构建 UI，无 XAML） |
| `app.manifest` | DPI PerMonitorV2 声明 |
| `build.cmd` | 一键编译脚本 |

## 技术说明

- C# + WPF，目标 .NET Framework 4.8
- 分辨率枚举与切换：`EnumDisplaySettings` / `ChangeDisplaySettingsEx`（user32.dll）
- 显示器型号：读取 `HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY` 下的 EDID，解析 0xFC 显示器名描述符
- 配对逻辑：显示器设备 ID（如 `MONITOR\SKG2512`）厂商代码与 EDID 厂商代码匹配，避免多显示器串号

## 常见问题

- **下拉框里为什么没有某种分辨率？** 列表来自 Windows 枚举的显示器真实支持模式，显示器不支持的画质不会列出
- **「保存」具体做了什么？** 调用 `ChangeDisplaySettingsEx` 并带 `CDS_UPDATEREGISTRY` 写入注册表，重启后保持；同时写入 `ResolutionChanger.ini`
- **切换失败提示"不支持的显示模式"怎么办？** 说明该模式不在模式列表中，换一个模式即可
- **虚拟显示器会出现在界面里吗？** 不会，未接入桌面（未启用镜像驱动）的适配器会被忽略

## License

GPL-2.0
