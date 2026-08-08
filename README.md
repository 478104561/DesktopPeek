# Desktop Peek

Windows 托盘小工具：鼠标悬停在桌面空白处时，把其他窗口变成半透明并可点击穿透，方便操作桌面图标；离开桌面、点任务栏、打开新窗口或按热键即可恢复。

仓库：[https://github.com/478104561/DesktopPeek](https://github.com/478104561/DesktopPeek)

---

## 一、别人下载后怎么用

### 1. 直接用现成程序（推荐）

1. 打开仓库的 [Releases](https://github.com/478104561/DesktopPeek/releases)，下载最新的 `DesktopPeek-v*.exe`  
   （若作者把 exe 放在别处，拿到单个 exe 即可，无需安装。）
2. 放到任意文件夹（例如 `D:\Apps\DesktopPeek\`）。
3. 双击运行。任务栏右下角会出现托盘图标，并弹出提示「已在后台运行」。
4. 把鼠标移到**桌面空白处**并稍作停留 → 进入透视；再操作任务栏 / 打开窗口 / 按热键 → 退出透视。

> 当前发布包为 **自包含单文件**，一般不需要再安装 .NET 运行库。  
> 系统要求：**Windows 10 / 11（64 位）**。

首次运行默认会尝试写入「开机自启」。可在托盘菜单里取消「开机自启」。

### 2. 日常操作

| 操作 | 说明 |
|------|------|
| 悬停桌面空白处 | 持续悬停达到「悬停延迟」后进入透视 |
| `Ctrl` + `` ` ``（反引号，一般在 Esc 下方） | 手动切换透视开/关 |
| `Win` + `Esc` | 紧急立即恢复所有窗口 |
| 托盘图标双击 | 手动切换透视 |
| 右键托盘图标 | 打开菜单：透明度、悬停延迟、自启、退出等 |

**托盘菜单常用项：**

- **透明度**：透视时窗口的不透明程度（数值越小越透明，默认约 50）
- **悬停延迟**：在桌面空白处需要悬停多久才触发（默认 500ms）
- **开机自启**：是否随 Windows 登录启动
- **退出**：退出程序（会先恢复窗口状态）

### 3. 配置文件位置

设置会保存在：

```text
%AppData%\DesktopPeek\config.json
```

例如：`C:\Users\<你的用户名>\AppData\Roaming\DesktopPeek\config.json`

删掉该文件再启动，会恢复默认设置。

### 4. 使用提示

- 透视中可点击桌面图标；Snipaste、PureRef、部分桌面宠物等分层窗口会在透视期间暂时移出屏幕，退出透视后自动归位。
- 若窗口状态异常，按 `Win` + `Esc` 立即恢复，或托盘菜单选「立即恢复」。
- 个别全屏 / 高权限程序可能不完全受影响；必要时以管理员身份运行（托盘菜单会显示当前权限状态）。
- 杀毒软件若拦截，请将 exe 加入信任列表后再运行。

### 5. 从源码运行（开发者 / 没有现成 exe 时）

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。
2. 克隆或下载本仓库并解压。
3. 在仓库根目录打开 PowerShell，执行：

```powershell
.\run.ps1
```

或：

```powershell
dotnet run -c Debug
```

---

## 二、怎么打包（自己编译发布）

打包后会得到可发给别人使用的单文件 exe（自包含，体积约 150MB+）。

### 环境准备

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 本仓库源码（含 `DesktopPeek.csproj`、`publish.ps1`）

### 一键打包（推荐）

在仓库根目录执行：

```powershell
# 使用 csproj 里当前版本号打包
.\publish.ps1

# 或指定新版本（会写入 DesktopPeek.csproj 再打包）
.\publish.ps1 -Version 1.1.4
```

脚本会：

1. 结束正在运行的 DesktopPeek 进程  
2. 执行 `dotnet publish`（Release / win-x64 / 单文件 / 自包含）  
3. 输出到 `publish\` 目录  

完成后可在 `publish\` 中找到：

| 文件 | 说明 |
|------|------|
| `DesktopPeek.exe` | 最新打包结果 |
| `DesktopPeek-v<版本>.exe` | 带版本号的副本，适合分发给别人 |

把带版本号的 exe 发给别人即可，无需附带其它 dll。

### 手动打包命令（等价）

```powershell
dotnet publish -c Release -r win-x64 `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --self-contained true `
  -o .\publish
```

### 更换应用图标（可选）

1. 准备一张 PNG（建议较大分辨率的方形图）。
2. 用仓库自带工具转成 ico：

```powershell
dotnet run --project tools\PngToIco -c Release -- your.png Assets\app.ico
```

3. 再执行 `.\publish.ps1`。

---

## 三、发给别人时建议怎么说明

可直接附上下面几句：

1. 下载 `DesktopPeek-v*.exe`，双击运行。  
2. 看右下角托盘图标；鼠标悬停桌面空白处即可透视。  
3. `Ctrl+`` ` 切换，`Win+Esc` 紧急恢复。  
4. 右键托盘可调透明度、悬停延迟、开机自启；不需要时可取消自启或点「退出」。

---

## 许可证与反馈

若仓库中另有 LICENSE 文件，以该文件为准。问题与建议请到 GitHub Issues 反馈。
