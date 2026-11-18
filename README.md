# File Type Analyzer

Windows 桌面小工具：用一个界面同时跑两套文件类型识别引擎，结果并排对比。

- **TrID** —— 基于文件签名的识别，使用托管引擎 `TrID.dll`（NuGet 包 TriDEngine 1.2.4，约 6300 条签名定义编译进程序集），给出最匹配的 5 项文件类型和置信度。纯 .NET 实现，无 32 位原生依赖，进程保持 AnyCPU。
- **Magika** —— Google Magika 的 CLI（AI 模型），给出标签、MIME、描述和置信分数。

两个引擎的结果会做一致性评估，用来判断"签名与模型是否给出同一结论"。

## 界面

- 拖拽文件或点"打开"选文件，选完自动分析；
- 工具栏只有三个图标：打开、明暗主题切换、设置（语言 / 引擎更新）；
- 深色 / 浅色两套主题一键切换，图标为 Font Awesome 字形，随程序打包，不依赖系统字体；
- 中英双语，语言包放在 `lang/` 下的 JSON 里，可自行增改。

## 引擎更新

- Magika：调用 `magika -V` 读本地版本，与 GitHub `cli/v*` 最新 tag 比对；下载 Windows zip 后同目录暂存、实跑 `-V` 校验版本、备份 `.bak`、原子替换，失败回滚。
- TrID：定义随程序集打包，无需更新（设置窗口里只显示状态，不提供更新按钮）。

## 构建

需要 .NET 8 SDK。项目根目录下有两个 csproj（`Program.csproj` 是历史残留），构建时请显式指定：

```bat
dotnet build FileTypeAnalyzer.csproj
```

或直接双击 `编译.bat`（支持 `编译.bat release`）。

### 运行依赖

TrID 引擎改为托管实现后，只需要 `magika.exe`（可选，缺省时 Magika 面板显示未安装）：

| 文件 | 说明 |
| --- | --- |
| `TrID.dll` | 托管 TrID 引擎，程序集引用，构建时自动复制到输出目录 |
| `magika.exe` | Magika CLI，可在设置里检查并下载更新 |

不再需要 `TrIDLib.dll`（32 位原生 DLL）和 `triddefs.trd`：签名定义已随 `TrID.dll` 打包。

## 许可与致谢

- TrID：© Marco Pontello，<https://mark0.net/soft-trid-e.html>
- Magika：Google，Apache-2.0
- 图标字体：Font Awesome Free 6，SIL OFL 1.1（许可原文见 `assets/FontAwesome-LICENSE.txt`）

作者：小恐龙 · <https://xkonglong.com>
