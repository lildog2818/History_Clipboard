# 剪贴板历史（轻量版）

一个类似 Ditto、但更轻量简洁的 Windows 剪贴板历史工具。纯原生 C# / .NET 8 + WPF 实现，**零第三方 NuGet 依赖**（仅 .NET BCL + Windows SDK + Win32 P/Invoke）。

## 功能

- 后台常驻（托盘图标），实时记录剪贴板历史：纯文本、HTML/富文本（CF_HTML + CF_RTF）、图片、文件列表。
- 全局热键唤起悬浮搜索条，毛玻璃质感（Win11 Mica / Win10 亚克力），黑白双主题（跟随系统/手动切换）。
- 搜索/过滤、置顶/收藏、条目备注与标签、粘贴为纯文本、删除/清空。
- 图片 OCR（Windows 自带 OCR，无第三方库）：图片可被文字搜索、可复制图中文字。
- Snipaste 式截图与贴图：区域截图，截后可「复制 / OCR / 贴图 / 取消」，截图自动入库。
- 应用内复制（复制条目文字、复制 OCR 文字、复制图片、粘贴时的剪贴板写入）不会重复入库。
- 自定义历史保存目录。
- 开机自启、单实例（重复启动自动唤起搜索条）。

## 默认快捷键

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl + \``（反引号） | 唤起/关闭搜索条 |
| `Ctrl + Alt + A` | 区域截图 |

搜索条内：

| 按键 | 功能 |
| --- | --- |
| `↑` / `↓` | 选择条目 |
| `Enter` | 粘贴（保留格式） |
| `Ctrl + Enter` 或 `Shift + Enter` | 粘贴为纯文本 |
| `1` - `9` | 快速粘贴第 N 条（列表获焦时） |
| `Ctrl + C` | 复制选中文字 / 复制图中文字 |
| `Ctrl + P` | 置顶 / 取消置顶 |
| `F2` | 编辑备注与标签 |
| `Del` | 删除 |
| `Esc` | 关闭 |
| 右键 | 更多操作（粘贴、复制、贴图、置顶、备注、删除） |

贴图窗口：拖动移动、滚轮缩放、双击或 `Esc` 关闭、右键复制/关闭/全部关闭。

## 运行要求

- Windows 10 1809+ 或 Windows 11（Mica 效果仅 Win11，其余自动回退亚克力/纯色）。
- 默认构建为**框架依赖**单文件，需要安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（未安装时系统会提示并引导下载）。
- 图片 OCR 依赖系统已安装的 Windows OCR 语言包（设置 → 语言 → 添加「光学字符识别」语言，默认按 简体中文 → 繁体中文 → 英文 顺序尝试；缺语言包时图片仍正常，仅不可搜文字）。

## 数据存储

- 默认目录：`%LOCALAPPDATA%\ClipboardHistory\`（`history.json` + `images\`）。
- 可在托盘菜单「选择保存目录…」中更改，历史会自动迁移到新目录。

## 构建与发布

```powershell
# 框架依赖（小体积，约 25MB，需 .NET 8 Desktop Runtime）
dotnet publish ClipboardHistory.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish

# 自包含（免运行时，体积更大）
dotnet publish ClipboardHistory.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

> 说明：发布包约 25MB，其中应用本体仅约 0.2MB，其余约 24MB 为微软 Windows SDK 投影（`Microsoft.Windows.SDK.NET.dll`），用于内置 OCR。若不启用 OCR，体积可降至 ~0.5MB。

## 已知限制

- 无法向「以管理员身份运行」的窗口粘贴（Windows UIPI 隔离，暂未做提权辅助进程）。
- 截图在「混合 DPI 多显示器」下可能坐标偏移（单一显示器或统一缩放不受影响）。
- 默认不排除密码管理器等敏感应用，如需「忽略指定应用」可作为后续增强。
- 贴图仅保留核心操作（拖动/缩放/关闭/复制），Snipaste 的旋转、取色、马赛克、鼠标穿透等高级功能未包含。
