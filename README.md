# File Type Analyzer

Windows 桌面小工具：用一个界面同时跑两套文件类型识别引擎，结果并排对比。

- **TrID** —— 基于文件签名的识别，用 `TrIDLib.dll` + `triddefs.trd` 定义库，给出最匹配的若干文件类型和置信度。
- **Magika** —— Google Magika 的 CLI（AI 模型），给出标签、MIME、描述和置信分数。

两个引擎的结果会做一致性评估，用来判断"签名与模型是否给出同一结论"。

## 界面

- 拖拽文件或点"打开"选文件，选完自动分析；
- 工具栏只有三个图标：打开、明暗主题切换、设置（语言 / 引擎更新）；
- 深色 / 浅色两套主题一键切换，图标为 Font Awesome 字形，随程序打包，不依赖系统字体；
- 中英双语，语言包放在 `lang/` 下的 JSON 里，可自行增改。

## 引擎更新

- Magika：调用 `magika -V` 读本地版本，与 GitHub `cli/v*` 最新 tag 比对；下载 Windows zip 后同目录暂存、实跑 `-V` 校验版本、备份 `.bak`、原子替换，失败回滚。
- TrID 定义库：从 mark0.net 下载 `triddefs.zip` 解压到程序目录。

## 构建

需要 .NET 8 SDK。项目根目录下有两个 csproj（`Program.csproj` 是历史残留），构建时请显式指定：

```bat
dotnet build FileTypeAnalyzer.csproj
```

或直接双击 `编译.bat`（支持 `编译.bat release`）。

### 运行依赖

以下文件必须和 exe 在同一目录（构建时会自动复制 `TrIDLib.dll`）：

| 文件 | 说明 |
| --- | --- |
| `TrIDLib.dll` | 32 位原生 DLL，因此项目固定 `PlatformTarget = x86` |
| `triddefs.trd` | TrID 定义库，首次可用设置里的"下载更新定义库"获取 |
| `magika.exe` | Magika CLI，可在设置里下载更新 |

## 许可与致谢

- TrID：© Marco Pontello，<https://mark0.net/soft-trid-e.html>
- Magika：Google，Apache-2.0
- 图标字体：Font Awesome Free 6，SIL OFL 1.1（许可原文见 `assets/FontAwesome-LICENSE.txt`）

作者：小恐龙 · <https://xkonglong.com>
