# eplan-mcp

分支于swater-kankyo/eplan-mcp-oss，基于原架构补全了AddIn和mcp服务器空置的实际操作方法

`eplan-mcp` 是一个 [MCP（Model Context Protocol）](https://modelcontextprotocol.io) 服务器：
它让 AI 助手（或任何 MCP 客户端）通过一组定义清晰的工具，驱动本机正在运行的
**EPLAN P8** 完成日常工作——打开 / 新建 / 关闭 / 保存项目，导入导出项目，
创建 / 列出 / 删除 / 复制 / 导入导出页，放置元件与宏，在引脚之间创建电气连接，
绘制直线与折线，导出 PDF / 部件列表 / 设备列表，重命名设备标签，打开项目备份等。

工具通过 **stdio** 暴露给 MCP 客户端，每个调用经 Python 桥接层转发给装在 EPLAN
内部的**加装程序（add-in）**，由它调用 EPLAN API 执行实际操作并把结果回传。
本仓库既是可直接使用的实现，也是一个**框架**：附带的参考 add-in 与文档化的消息格式，
让你可以按自己的流程继续扩展新的操作，而不必重写底层管道。

> 本文档的工具清单是**逐行核对 `src/mcp_server.py`** 写出的：共 **38 个工具**、
> **2 个提示词（prompt）**，参数名、默认值、取值范围均与代码一致。

## 目录

- [特性](#特性)
- [工作原理](#工作原理)
- [环境要求](#环境要求)
- [安装](#安装)
- [快速开始](#快速开始)
- [MCP 工具与作用](#mcp-工具与作用)
  - [1. 状态与生命周期](#1-状态与生命周期3-个)
  - [2. 项目管理](#2-项目管理8-个)
  - [3. 页管理](#3-页管理6-个)
  - [4. 设备与放置](#4-设备与放置6-个)
  - [5. 接线](#5-接线2-个)
  - [6. 图形与文本](#6-图形与文本5-个)
  - [7. 页上图形对象](#7-页上图形对象2-个)
  - [8. 导出](#8-导出3-个)
  - [9. 维护与逃生通道](#9-维护与逃生通道2-个)
- [MCP 提示词](#mcp-提示词)
- [参数约定与已知行为](#参数约定与已知行为)
- [常见工作流](#常见工作流)
- [配置](#配置)
- [文档](#文档)
- [许可证](#许可证)

## 特性

- **任意 MCP 客户端都能指挥 EPLAN。** 工具以 stdio 提供，可直接接入 Claude Code
  等支持 MCP 的助手。
- **完整的项目与页生命周期。** 打开 / 新建 / 关闭 / 保存 / 导入 / 导出项目与页，
  全部可以用自然语言驱动。
- **元件放置与自动接线。** 支持按符号（symbol）、`.ema` 宏、功能定义
  （function definition）三种方式放置元件，可单件或批量（`eplan_place_devices`）；
  可在器件引脚之间创建电气连接，可单条或批量（`eplan_connect_many`）。
- **图纸绘制能力。** 通过 EPLAN 图形 API 画直线与折线，并可指定颜色 / 线型 / 线宽。
- **清晰的三层架构。** MCP 服务器、Python 桥接层、EPLAN add-in 通过一份很小的
  消息契约解耦，传输方式可替换。
- **可扩展。** 附带参考 add-in 与文档化的请求 / 应答消息格式，新增操作只需实现
  一个 add-in handler 并添加一个工具。
- **配置化。** 路径与 EPLAN 可执行文件位置来自配置文件或环境变量，不写死在代码里。

## 工作原理

```
  MCP 客户端（如 Claude Code）
        │  MCP over stdio
        ▼
  MCP 服务器  (src/mcp_server.py)   —— 注册并暴露工具（38 个）
        │  进程内函数调用
        ▼
  桥接层      (src/eplan_bridge.py) —— 转发请求、等待结果、管理 EPLAN 生命周期
        │  id/op 消息，走可替换的传输通道
        ▼
  add-in      (EPLAN 进程内)        —— 调用 EPLAN API 执行操作
```

- **MCP 服务器**只负责定义工具面并转发调用，不直接接触 EPLAN。
- **桥接层**只依赖 Python 标准库：把请求写进共享工作目录，轮询等待 **id 匹配**的应答，
  返回结构化的 `data`；同时负责启动 / 检测 / 关闭 EPLAN。
- **add-in**是唯一调用 EPLAN API 的一层，按 `op` 分派执行并回填 `ok` / `detail` / `data`。

请求与应答是扁平的 `key=value` 多行文本；批量数据（多设备、多连线）放在
`payload=` 字段里以 **base64(JSON)** 承载，结构化结果以 `data=` 回传。
对上层使用者而言这些字段都是透明的。

更完整的设计说明见 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)。

## 环境要求

- Windows，且已安装 EPLAN P8（参考实现验证于 EPLAN Platform **2.9.4**）。
- Python 3.11 或更高版本（参考实现运行于 3.14）。
- 一个支持 MCP 的客户端（例如 Claude Code）。
- 编译 add-in 需要 EPLAN P8 自带的 API 程序集与 .NET Framework。

## 安装

详细步骤见 [`docs/INSTALL.md`](docs/INSTALL.md)，要点如下。

**1）安装 Python 依赖**

```powershell
pip install "mcp[cli]"
```

**2）准备配置**

```powershell
Copy-Item src/config.template.json config.json
```

编辑 `config.json`，至少确认共享工作目录与 EPLAN 可执行文件路径：

```json
{
  "eplan_exe": "C:\\Program Files\\EPLAN\\Platform\\2.9.4\\Bin\\EPLAN.exe",
  "variant": "Electric P8",
  "base_dir": "C:\\eplan_mcp"
}
```

**3）编译并安装 add-in**

```powershell
.\addin\build.ps1 -EplanApiDir "C:\Program Files\EPLAN\Platform\2.9.4\Bin"
```

把生成的 DLL 复制到 EPLAN 的 add-in 目录（例如
`C:\Program Files\EPLAN\Platform\2.9.4\Bin\Addins\`）。
注意：**重新编译后的 DLL 无法从外部替换已加载的文件**（EPLAN 使用
ShadowCopyAssemblies），必须在 EPLAN 内手动安装 / 更新，然后**重启 EPLAN**。

**4）注册 MCP 服务器**

以 Claude Code 为例：

```powershell
claude mcp add eplan-mcp -- python path\to\src\mcp_server.py
```

**5）验证**

让助手调用 `eplan_status`；若 EPLAN 未运行，再调用 `eplan_ensure_ready`。

## 快速开始

服务器注册好、EPLAN 已启动并加载 add-in 后，可以直接用自然语言下达指令：

- “打开 `C:\projects\Demo.elk` 这个项目。”
- “列出当前项目里所有的页。”
- “在项目里新建一页 `=A1+B1/3`。”
- “在页 `=A1+B1/3` 的 (100, 100) 位置放一个电机 `-M1`。”
- “把 `-K1:1` 接到 `-M1:2`。”
- “把项目导出成 PDF。”
- “把设备标签 `-M1` 改名为 `-P1`。”

它们分别对应 `eplan_open_project`、`eplan_list_pages`、`eplan_create_page`、
`eplan_place_device`、`eplan_connect`、`eplan_export_pdf`、`eplan_rename_tag`。

## MCP 工具与作用

下文按功能分组列出全部 **38 个工具**。除特别说明外：

- 坐标单位均为 **毫米**，页坐标系原点在图框左上角。
- 返回值为字典，`ok` 表示 add-in 是否成功执行；执行结果中的 `detail` 是文字说明，
  列表类结果放在 `data` 字段。
- 等待超时后返回 `{"ok": false, "op": <操作>, "detail": "timeout waiting for result"}`。

### 1. 状态与生命周期（3 个）

| 工具 | 参数 | 作用 |
|------|------|------|
| `eplan_status` | — | 检查 EPLAN 是否在运行、add-in 是否有应答（内部发送 `ping`）。EPLAN 未运行时直接返回 `running: false` 与提示 `EPLAN is not running; call eplan_ensure_ready`。**它不会启动 EPLAN。** |
| `eplan_ensure_ready` | `project: str = ""` | 需要时启动 EPLAN，并轮询等待 add-in 就绪（最长 60 秒）。`project` 可选，用于启动时直接打开项目。返回 `detail` 为 `EPLAN ready` / `EPLAN already running`，或启动后 add-in 无应答的失败信息。 |
| `eplan_watchdog` | — | 清理一次弹出的模态对话框，让无人值守的自动化继续执行。返回值固定为 `{"ok": true, "cleared": [...]}`。注意：框架默认的对话框处理器是**空实现**，即开箱状态下不做任何事；需要真正清窗时要通过 `register_dialog_handler()` 或 `EPLAN_MCP_DIALOG_HANDLER` 注册自己的处理器。 |

### 2. 项目管理（8 个）

| 工具 | 参数（含默认值） | 作用 |
|------|------------------|------|
| `eplan_open_project` | `project: str` | 按路径打开项目（`.elk`）。重复打开已被打开的项目是安全的（add-in 会先查找已打开项目）。 |
| `eplan_close_project` | `project: str` | 关闭项目。对已生效的关闭操作会返回 `ok: true`。 |
| `eplan_new_project` | `project: str`, `template: str` | 从模板新建项目：`project` 是**新建 `.elk` 链接文件的路径**（不是项目名），`template` 是 `.zw9` / `.zw1` 模板路径。创建后**项目处于已打开状态**，无需再调用 `eplan_open_project`。 |
| `eplan_delete_project` | `project: str` | 删除项目。建议先关闭该项目。 |
| `eplan_save` | — | 保存当前活动项目（EPLAN 动作 `XES`，不接收项目参数）。对通过 API 打开的项目，返回 `ok: false` 属正常现象。 |
| `eplan_export_project` | `out_path: str`, `project: str = ""`, `type: str = "backup"` | 导出项目：`type` 取 `backup`（默认，EPLAN 备份包）或 `pxf`（EPLAN 交换格式）。 |
| `eplan_import_project` | `src: str`, `project: str = ""`, `type: str = "pxf"` | 导入项目：`type` 取 `pxf`（默认）或 `backup`。⚠️ 与导出**默认值不对称**，导入默认走交换格式。 |
| `eplan_open_backup` | `archive: str`, `dest_name: str = ""`, `force: bool = false` | 打开（还原）项目备份文件。执行前会做预检：备份文件不存在或传了目录时返回 `backup file not found: <archive>`；EPLAN 未就绪时返回 `EPLAN not ready: <原因>`。`dest_name` 指定还原后的项目名，`force` 控制覆盖。 |

### 3. 页管理（6 个）

| 工具 | 参数（含默认值） | 作用 |
|------|------------------|------|
| `eplan_list_pages` | `project: str` | 列出项目中所有页的名称。 |
| `eplan_create_page` | `project: str`, `name: str`, `page_type: str = "Circuit"` | 新建页。`name` 形如 `=A1+B1/3`；`page_type` 可取 `Circuit`（默认）、`CircuitSingleLine`、`Overview`、`Graphics` 等。 |
| `eplan_delete_pages` | `pages: str`, `project: str` | 删除指定页。`pages` 用**分号**分隔多个页名，例如 `=A1+B1/3;=A1+B1/4`（内部改为竖线分隔后发送）。 |
| `eplan_copy_page` | `project: str`, `page: str`, `to_project: str = ""`, `new_name: str = ""` | 复制页到同一项目或另一个项目。`to_project` / `new_name` 留空表示原地复制 / 保持原名。 |
| `eplan_export_pages` | `project: str`, `pages: str`, `out_path: str`, `format: str = "pdf"`, `scheme: str = ""` | 导出指定页。`pages` 用**分号**分隔；`format` 可取 `pdf`（默认）、`dxf`、`dwg`、`png`、`tif`、`jpg`（桥接层还接受 `gif`）；`scheme` 指定 EPLAN 导出方案。该工具**不校验输出文件**。 |
| `eplan_import_pages` | `project: str`, `file: str`, `page: str = ""` | 把 DXF/DWG 文件导入到项目的某一页。 |

### 4. 设备与放置（6 个）

| 工具 | 参数（含默认值） | 作用 |
|------|------------------|------|
| `eplan_place_device` | `project: str`, `tag: str = ""`, `page: str = ""`, `symbol: str = ""`, `macro: str = ""`, `function_definition: str = ""`, `x: float = 100`, `y: float = 100`, `variant: int = 1`, `part: str = ""`, `angle: float = 0` | 放置一个元件。三种来源**任选其一**：`symbol`（符号库名称，如 `IEC_...`）、`macro`（`.ema` 宏文件路径）、`function_definition`（如 `CON_SWITCH_...`）。`tag` 为设备标签（如 `-K1`），`part` 为可选部件号，`variant` 是符号变体号。⚠️ 代码不做“只能填一个来源”的校验；⚠️ `angle` 会被透传，但它充当的是符号包围盒的第二个角点，**达不到旋转效果**——方向只能靠变体（variant）控制。 |
| `eplan_place_devices` | `project: str`, `devices: str` | 批量放置元件。`devices` 是 **JSON 数组字符串**，元素字段与 `eplan_place_device` 相同，例如 `[{"tag":"-K1","page":"=A1+B1/1","symbol":"IEC_...","x":50,"y":50}]`。解析失败时返回 `{"ok": false, "detail": "devices must be a JSON array"}`。**批量放置务必遵守防崩溃节奏，见下文。** |
| `eplan_place_macro` | `project: str`, `page: str`, `macro: str`, `x: float = 100`, `y: float = 100` | 在指定页的 (x, y) 位置放置 `.ema` 宏文件。 |
| `eplan_remove_device` | `project: str`, `tag: str` | 按设备标签删除已放置的元件。 |
| `eplan_list_devices` | `project: str`, `page: str = ""` | 列出设备（标签、符号、引脚数）。`page` 留空表示整个项目。 |
| `eplan_list_pins` | `project: str`, `tag: str` | 列出某设备的引脚信息（序号、代号、位置）。接线前先用它确认引脚序号。 |

### 5. 接线（2 个）

| 工具 | 参数（含默认值） | 作用 |
|------|------------------|------|
| `eplan_connect` | `project: str`, `from_spec: str`, `to_spec: str` | 在两个器件引脚之间创建一条电气连接。规格写法为 `TAG` 或 `TAG:PININDEX`，例如 `-K1:1` → `-M1:2`。 |
| `eplan_connect_many` | `project: str`, `wires: str` | 批量创建连接。`wires` 是 **JSON 数组字符串**，元素形如 `{"from": "-K1:1", "to": "-M1:2"}`。解析失败时返回 `{"ok": false, "detail": "wires must be a JSON array"}`。 |

> ⚠️ **接线是这套工具里最容易踩坑的部分。** EPLAN API 无法直接把连接画出来：
> `new Connection`、`MakePersistent()`、`InterconnectDevices`、
> `PlaceAsConnectionDefinitionPoint` 都停留在 `placed: false`；
> `eplan_exec_cmd("generate /TYPE:CONNECTIONS ...")` 也只连接方向匹配的引脚。
> 要让连接**可见**，需要在引脚附近（约 18 mm）放置 SPECIAL 符号库的 **CO** 转角
> （v0 向下+向右、v3 向左+向下）；同方向端口、引脚正对端口永远连不上；
> 每个线段都需要 CO/TLRU/TLRO/TOUR/TOUL，间隙不超过 26 mm。
> y 轴正方向朝上（母线 y=100 时元件应在 y<100）。
> 详细做法与元件代号表见 [`docs/EXPERIENCE.md`](docs/EXPERIENCE.md) 第 1、2 节。

### 6. 图形与文本（5 个）

| 工具 | 参数（含默认值） | 作用 |
|------|------------------|------|
| `eplan_draw_line` | `project: str`, `page: str`, `start: str`, `end: str`, `color_id: int = 0`, `style_id: int = 0`, `width: int = 1` | 画一条直线。`start` / `end` 是 `"x,y"` 字符串（毫米）。格式不合法会抛出异常。 |
| `eplan_draw_polyline` | `project: str`, `page: str`, `points: str`, `closed: bool = false`, `color_id: int = 0`, `style_id: int = 0`, `width: int = 1` | 画折线。`points` 写法为 `"x,y;x,y;..."`——**点与点之间用分号，点的坐标之间用逗号**；`closed = true` 表示闭合（如画矩形框）。 |
| `eplan_place_text` | `project: str`, `page: str`, `content: str`, `x: float = 0`, `y: float = 0` | 在 (x, y) 处放置一段图形文本。⚠️ 坐标默认值是 `0, 0`，与放置元件的 `100, 100` 不同。 |
| `eplan_set_texts` | `project: str`, `page: str`, `from_text: str`, `to_text: str`, `xmin: float = -1e18`, `xmax: float = 1e18`, `ymin: float = -1e18`, `ymax: float = 1e18` | 把内容等于 `from_text` 的图形文本替换为 `to_text`，可用 x/y 窗口（毫米）限定范围；默认窗口是无界的（覆盖整页）。 |
| `eplan_remove_symbols` | `project: str`, `page: str`, `xmin: float = -1e18`, `xmax: float = 1e18`, `ymin: float = -1e18`, `ymax: float = 1e18` | 删除指定矩形区域内的 SymbolReference（连接符号），默认覆盖整页。 |

### 7. 页上图形对象（2 个）

用于核对上面修改的结果——先读后改，避免误删。

| 工具 | 参数（含默认值） | 作用 |
|------|------------------|------|
| `eplan_list_symbols` | `project: str`, `page: str = ""` | 列出页上所有 SymbolReference（连接符号）的类型与坐标，用来确认 CO 转角、T 形分支放置是否正确。 |
| `eplan_list_texts` | `project: str`, `page: str = ""` | 列出页上所有图形文本的坐标与内容（读取中文安全）。 |

### 8. 导出（3 个）

| 工具 | 参数（含默认值） | 作用 |
|------|------------------|------|
| `eplan_export_pdf` | `out_path: str`, `scheme: str = ""`, `pages: str = ""` | 把**当前活动项目**导出为 PDF；`pages`（分号分隔）留空则导出整个项目，`scheme` 指定导出方案。执行前会先删除同名旧文件，导出后**校验文件大小 ≥ 1000 字节**，并在返回值中补充 `output_file` / `output_exists` / `output_size`。 |
| `eplan_export_partslist` | `out_path: str`, `fmt: str = ""` | 导出部件列表到文件。先删旧文件，导出后校验文件**≥ 10 字节**。 |
| `eplan_export_devicelist` | `out_path: str`, `fmt: str = ""` | 导出设备列表到文件。先删旧文件，导出后校验文件**≥ 10 字节**。 |

> 这三个导出工具都**不接受项目参数**，作用于当前活动项目。

### 9. 维护与逃生通道（2 个）

| 工具 | 参数 | 作用 |
|------|------|------|
| `eplan_list_connections` | `project: str` | 列出项目中所有连接：是否已放置、起止引脚、所在页。注意它只覆盖功能（Function）连接，**不包含 CO / SymbolReference**；连接生成有延迟，刚放置完立刻查询或导出可能缺线段，稍等或重新导出 PDF 核对。 |
| `eplan_exec_cmd` | `command: str` | 直接执行 EPLAN 命令行动作，例如 `generate /TYPE:CONNECTIONS ...`。这是逃生通道，参数由 EPLAN 决定。 |

## MCP 提示词

除了工具，服务器还注册了 2 个 `@mcp.prompt()` 提示词，用于在动手前把实测经验
喂给模型（两者都是固定文本，不重复承载经验内容，而是指向文档）：

| 提示词 | 何时使用 | 内容要点 |
|--------|----------|----------|
| `eplan_wiring_connections` | 使用 `eplan_connect` / `eplan_place_device` 做接线之前 | 指向 `docs/EXPERIENCE.md`：第 1 节常用元件代号表（symbol 代号 → 元件对应）、第 2 节接线/连线（API 无法直接画线，CO 转角 + T 形分支的正确做法）、第 5 节批量放置防崩溃策略。 |
| `eplan_known_pitfalls` | 编写或调试 add-in / 桥接层代码之前 | 同上，并额外覆盖第 3 节已知 API 坑（LockingStep、功能定义库、设备标签等）、第 4 节 add-in 部署（DLL 只能软件内手动安装）、第 6 节 add-in 修改清单。 |

## 参数约定与已知行为

这些细节决定了工具能否一次调用成功，代码逐行核对得出：

**分隔符与字符串格式**

- 列表参数用**分号**：`eplan_delete_pages`、`eplan_export_pages`、`eplan_export_pdf`
  的 `pages` 都是 `=A1+B1/1;=A1+B1/2` 这种写法（桥接层内部会转成竖线分隔）。
- 折线用**两种分隔符**：`eplan_draw_polyline` 的 `points` 是 `"x,y;x,y;..."`。
- 直线用**单点对**：`eplan_draw_line` 的 `start` / `end` 必须是恰好一个逗号的 `"x,y"`。
- 批量操作用 **JSON 数组字符串**：`eplan_place_devices(devices=...)`、
  `eplan_connect_many(wires=...)`（这类 payload 在 .NET Framework 上曾被
  反序列化成 `ArrayList` 而导致失败，add-in 已兼容该类型）。
- 单次请求只能承载一个操作（`op`），批处理与组合逻辑请放在 MCP 工具调用侧或
  Python 脚本里。

**默认值与取值**

- `eplan_place_device` / `eplan_place_macro` 默认 `x=100, y=100`；
  `eplan_place_text` 默认 `x=0, y=0`。
- `eplan_set_texts` / `eplan_remove_symbols` 的边界默认是
  `-1e18 ~ 1e18`，即**整页范围**。
- `eplan_place_device` 的 `variant` 默认 1（0 也合法，例如 CO 符号）；
  `angle` 虽被透传，但实际充当包围盒角点，**不会旋转元件**。
- `eplan_export_project` 默认 `type="backup"`，而 `eplan_import_project`
  默认 `type="pxf"`，注意二者不对称。
- `eplan_create_page` 默认 `page_type="Circuit"`。

**返回值与校验**

- 只有 `eplan_export_pdf`（≥1000 字节）、`eplan_export_partslist`（≥10 字节）、
  `eplan_export_devicelist`（≥10 字节）会校验输出文件；校验结果会与 add-in 的
  `ok` 做**逻辑与**，因此可能出现“add-in 成功但 `ok=false`”的情况，
  此时请看 `output_exists` / `output_size`。
- `eplan_export_pages`、`eplan_export_project`、`eplan_import_project`、
  `eplan_new_project` 不做输出校验。
- 桥接层的等待超时按操作分级：查询类 40–60 秒，放置/宏 180 秒，
  导出 300–600 秒，批量放置与批量接线最长 **900 秒**。

**并发与进程**

- 传输通道是**单一文件通道**（默认 `C:\eplan_mcp`：`command.txt`、
  `result.txt`、`trigger.flag`），**同一时刻只能有一个操作在飞**。
  不要一边让 MCP 工具执行操作，一边用脚本直接驱动 `eplan_bridge`。
- 没有任何工具可以退出 EPLAN；`eplan_ensure_ready` 是唯一会启动 EPLAN 的工具。
- 给服务器新增工具后，**需要重启 MCP 客户端会话**（例如重启 Claude Code）
  才会加载新工具；在此之前可以先用 Python 脚本直调 `eplan_bridge` 达到同样效果。

**批量放置防崩溃（重要）**

EPLAN 2.9.4 / Windows 11 下，密集放置约 28–36 个符号后会出现堆损坏崩溃
（`ntdll 0xc0000374`）。务必：**每批不超过 8 个符号**，符号之间间隔 2 秒，
批次之间间隔 8 秒。崩溃后需重启 EPLAN，并删除损坏的项目（`.elk` + `.edb`）。
`eplan_place_devices` 本身不会替你限速，节奏由调用方控制。

**其他实测坑（详见 [`docs/EXPERIENCE.md`](docs/EXPERIENCE.md)）**

- 设备标签属性 `FUNC_DEVICETAG_*` 只读（S063113），要改标签请写 `Function.Name`。
- `function_definition='K'` 得到的是偏振继电器 KP；要普通继电器请用 `symbol='K'`
  （v2 竖放 / v1 横放）。
- 写入操作与 `list_*` 的惰性枚举都需要 `LockingStep`（S063110）。
- `eplan_new_project` 之后项目保持打开，再次 `open_project` 会报 S063045；
  删除项目前必须先关闭。
- `page.Remove()` / `prj.Close()` 可能抛 `SEHException`，重试一次通常即可。
- 读取中文请用 `Contents.InternalString`；多语言字符串匹配用
  `Contains("@" + from + ";")`。
- 删除 CO / TOUR 要用 `AllPlacements`，而不是 `AllGraphicalPlacements`。

## 常见工作流

**先确认环境**

```
eplan_status → （未运行则）eplan_ensure_ready
```

**打开项目并查看页**

```
eplan_open_project("C:\projects\Demo.elk") → eplan_list_pages(...)
```

**新建项目并加页**

```
eplan_new_project("C:\projects\New.elk", "C:\templates\IEC_bas003.zw9")
→ eplan_create_page(project, "=A1+B1/3")
```

**放元件并接线**

```
eplan_place_device(project, tag="-K1", page="=A1+B1/3", symbol="K", x=80, y=100)
→ eplan_place_device(project, tag="-M1", page="=A1+B1/3", symbol="M", x=120, y=100)
→ eplan_list_pins(project, "-K1")          # 先确认引脚序号
→ eplan_connect(project, "-K1:1", "-M1:2")
```

批量放置请改用 `eplan_place_devices`，并遵守“每批 ≤8 个、间隔 2 秒 / 8 秒”的节奏。

**导出与收尾**

```
eplan_save() → eplan_export_pdf("C:\out\Demo.pdf") → eplan_close_project(project)
```

一份端到端的示例（包含直接用 Python 调用的版本）见
[`examples/README.md`](examples/README.md) 与
[`examples/mcp_tool_calls.md`](examples/mcp_tool_calls.md)。

## 配置

配置优先级：环境变量 > `EPLAN_MCP_CONFIG` 指向的 JSON > 根目录 `config.json` > 默认值。

| 键 / 环境变量 | 默认值 | 含义 |
|---------------|--------|------|
| `eplan_exe` / `EPLAN_EXE` | `C:\Program Files\EPLAN\Platform\2.9.4\Bin\EPLAN.exe` | EPLAN 可执行文件路径 |
| `variant` / `EPLAN_VARIANT` | `Electric P8` | 启动时使用的 EPLAN 变体 |
| `base_dir` / `EPLAN_MCP_BASE_DIR` | `C:\eplan_mcp` | 与 add-in 共享的工作目录（存放 `command.txt` / `result.txt` / `trigger.flag`） |
| `EPLAN_MCP_CONFIG` | — | 指定配置文件路径 |
| `EPLAN_MCP_DIALOG_HANDLER` | — | 提供 `get_dialog_handler()` 的模块名，用于注册对话框处理器 |

`config.json` 属于本机配置，已被 `.gitignore` 排除，不会进入版本库；
请以 [`src/config.template.json`](src/config.template.json) 为模板。

## 文档

- [`docs/INSTALL.md`](docs/INSTALL.md) —— 安装与配置步骤。
- [`docs/USAGE.md`](docs/USAGE.md) —— 工具与用法示例（英文）。
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) —— 架构说明。
- [`docs/EXPERIENCE.md`](docs/EXPERIENCE.md) —— 针对 EPLAN 2.9.4 / GB 模板的实测经验
  （元件代号表、接线做法、API 坑、批量放置防崩溃）。
- [`addin/README.md`](addin/README.md) —— add-in 的消息格式、操作表与编译说明。
- [`examples/`](examples/) —— 可运行的端到端示例。

发布打包请用 [`package.ps1`](package.ps1)（基于 `git archive`，只打包已提交内容），
不要手工压缩工作目录。

## 许可证

本项目采用 Apache License 2.0，详见 [`LICENSE`](LICENSE) 与 [`NOTICE`](NOTICE)。
