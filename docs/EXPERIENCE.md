# EPLAN 自动化实测经验（Platform 2.9.4 + GB 模板）

> 本文件记录通过实际调试验证的 EPLAN API 行为与正确用法。
> 同步内容也以 MCP prompt 形式注册在 `src/mcp_server.py` 中
> （`eplan_wiring_connections`、`eplan_known_pitfalls`）。
>
> 验证环境：EPLAN Electric P8 2.9.4，GB 模板（`GB_bas001.zw9`），
> 功能定义 `K`（BaseSymbol 为偏振 `KP`）、普通继电器线圈符号 `K`。
>
> **模板目录：`C:\EPLAN_Templates`**（GB_bas001.zw9 / IEC_bas001.zw9 /
> GB_tpl001.ept 等模板与 .xlsx 配置文件均在此，`new_project` 的
> `template` 参数直接填该目录下文件路径）。

---

## 1. 常用元件代号（用户维护，持续补充）

> 下表由用户持续提供，均为 GB 模板下可直接用于
> `place_device` 的 `symbol` 代号与元件含义的对应。

| 代号 | 元件 | 说明 |
|------|------|------|
| `K` | 普通继电器线圈 | `symbol='K'`；v2 竖放（引脚上下）/ v1 横放（引脚左右）。**勿用 `function_definition='K'`**（会放置偏振继电器 `KP`） |
| `S` | 常开触点 | NO / make contact |
| `O` | 常闭触点 | NC / break contact |
| `SSA` | 按钮常开触点 | pushbutton NO contact |
| `SOA` | 按钮常闭触点 | pushbutton NC contact |
| `SSAR` | 开关常开触点 | switch NO contact |
| `SOAR` | 开关常闭触点 | switch NC contact |
| `W2` | 两位置转换触点（有断点） | 2-position changeover contact, with break |
| `PLC_CBOX` | PLC 连接点 | PLC connection point |
| `FA1` | 单极断路器 | 1-pole circuit breaker |
| `FA2` | 双极断路器 | 2-pole circuit breaker |
| `FA3` | 三极断路器 | 3-pole circuit breaker |
| `FA4` | 四极断路器 | 4-pole circuit breaker |
| `F1` | 单极熔断器 | 1-pole fuse |
| `G22` | 单相整流器 | single-phase rectifier |
| `G23` | 两相整流器（次级侧三连接点） | two-phase rectifier, 3 secondary terminals |
| `G32` | 三相整流器 | three-phase rectifier |
| `G33` | 三相整流器（次级侧三连接点） | three-phase rectifier, 3 secondary terminals |
| `G3` | 三相发电机 | 3-phase generator |
| `H` | 指示灯 | indicator lamp |
| `HB` | 闪烁指示灯 | flashing indicator lamp |
| `HLED` | LED | light-emitting diode |
| `M2G` | 直流电机 | DC motor |
| `M_STEPP_1` | 步进电机 | stepper motor |
| `M3` | 三相异步电机 | 3-phase induction motor |
| `Y1` | 电磁阀 | solenoid valve |
| `C` | 电容器 | capacitor |
| `R` | 电阻 | resistor |
| `BT` | 热电偶 | thermocouple |
| `FT1` | 热继电器 | thermal overload relay |
| `X` | 端子 | terminal |
| `X2` | 两连接点端子 | terminal with 2 connection points |
| `LSW1A` | 一路径电流互感器（四连接点） | 1-path current transformer, 4 connection points |
| `L1` | 电感 | inductor |
| `LM1` | 带芯电感 | inductor with core |
| `ERDE` | 接地 | earth / ground |
| `DCPNG5` | 设备连接点（**隐藏/非图形**） | device connection point, non-graphical；**1 引脚**，放置后无可见图形，仅作连接点用（逻辑图/联锁图的标准连接点；JHM联锁逻辑图 页面实测 68 个） |

### 符号变体速查（36 个代号全部实测，GB 模板）

**通用规则（适用所有符号）**：
- 每个符号 **6 个变体**（v1~v6）
- 朝向：**v1/v3/v5 横放**（引脚 Left/Right），**v2/v4/v6 竖放**（引脚 Down/Up）
- v3 = v1 反向 180°，v4 = v2 反向；v5 = v1、v6 = v2（同向，仅图形细节差异，引脚相同）
- 下表中 v1/v2 的引脚坐标（dx,dy，mm，相对符号中心）与方向 dir；v3~v6 按通用规则推出

**引脚布局模式**：

| 模式 | 描述 | v1（横放）引脚 | v2（竖放）引脚 | 符号 |
|------|------|---------------|---------------|------|
| P2 对侧 | 2 引脚左右/上下 | 1=Left(−4,0), 2=Right(4,0) | 1=Down(0,−4), 2=Up(0,4) | K, S, O, SSA, SOA, SSAR, SOAR, FA1, F1, H, HB, HLED, Y1, C, R, FT1, L1, LM1 |
| P2 同侧远 | 2 引脚同侧 | 1=Left(−8,−16), 2=Left(−8,0) | 1=Down(16,−8), 2=Down(0,−8) | M2G |
| P2 同侧近 | 2 引脚同侧 | 1=Left(−4,−8), 2=Left(−4,0) | 1=Down(8,−4), 2=Down(0,−4) | BT |
| P3 同侧 | 3 引脚同侧 | 1=Left(−8,−8), 2=Left(−8,0), 3=Left(−8,8) | 1=Down(8,−8), 2=Down(0,−8), 3=Down(−8,−8) | G3 |
| P4 同侧 | 4 引脚同侧 | 1=Left(−8,−12), 2=Left(−8,−8), 3=Left(−8,−4), 4=Left(−8,0) | 1=Down(12,−8), 2=Down(8,−8), 3=Down(4,−8), 4=Down(0,−8) | M_STEPP_1 |
| P4 3+1 | 3 左 + 1 右 | 1-3=Left(−8,−16/−8/0), 4=Right(8,0) | 1-3=Down(16/8/0,−8), 4=Up(0,8) | M3 |
| P3 转换 | 上排 2 + 下左 1 | 1=Left(−4,−8), 2=Right(4,−8), 3=Left(−4,0) | 1=Down(8,−4), 2=Up(8,4), 3=Down(0,−4) | W2 |
| P4 双排 | 上排 2 + 下排 2 | 1/2 上排(y=−16), 3/4 下排(y=0) | 1/2 左列(x=16), 3/4 右列(x=0) | G22, FA2 |
| P4 双排近 | 下排 2 + 上排 2 | 1/2 下排(y=0), 3/4 上排(y=4) | 1/2 右列(x=0), 3/4 左列(x=−4) | LSW1A |
| P5 特 | 上 2 + 中 1 + 下 2 | G23: 3=Right(8,−8); G32: 3=Left(−8,−8) | 同侧对应方向 | G23, G32 |
| P6 三排 | 3 排 × 2 | 1/2(y=−16), 3/4(y=−8), 5/6(y=0) | 1/2(x=16), 3/4(x=8), 5/6(x=0) | FA3, G33 |
| P8 四排 | 4 排 × 2 | 1/2(y=−24), 3/4(y=−16), 5/6(y=−8), 7/8(y=0) | 1/2(x=24), 3/4(x=16), 5/6(x=8), 7/8(x=0) | FA4 |
| P1 | 1 引脚 | 1=Left(−2,0) | 1=Down(0,−2) | ERDE |
| P0 | 无引脚 | —（放置成功但 list_pins 无引脚） | — | PLC_CBOX, X, X2 |

**选变体口诀**：接线从左右来用 v1（或 v3 反向），从上下来用 v2（或 v4 反向）；
同侧引脚符号（G3/M2G/BT/M_STEPP_1）v1=全左、v2=全下、v3=全右、v4=全上。

---

## 2. 接线 / 连线（创建可见的连接线）

### 核心结论：API 无法直接画连接线

以下方式创建的连接对象全部是 **`placed: false`**，页面上画不出线，
EPLAN 的连接生成（`generate`）也不认：

- `new Connection(prj, p1, p2)`
- `conn.MakePersistent()`
- `ConnectionService.InterconnectDevices(src, tgt)`
- `conn.PlaceAsConnectionDefinitionPoint(page, pt)`（只放置 CDPNG 定义点符号）

`generate /TYPE:CONNECTIONS /REBUILDALLCONNECTIONS:1 /PROJECTNAME:"<path>"`
只能连接**引脚方向与连线方向匹配**的引脚对（同轴且方向相对）。
方向垂直的引脚（如两个 `Up` 引脚做横向连接）不会被自动连接。

### 正确做法：直角连线使用 CO 连接符号（variant 0~3），让 EPLAN 自动连接

等价于 GUI 工具栏的"添加右下角 / 左下角"工具：
放置角形连接符号 **CO**（EPLAN 内置符号，SPECIAL 库），
**CO 的引脚与元件引脚方向对齐**（同列 / 同方向）时，EPLAN **自动连线**。
CO 应放在**离元件引脚稍远**的位置，**不要紧贴**。

连接两个继电器线圈 K1、K2 的 A1 输入端示例：

```
K1 继电器线圈 @ (210, 148)      K2 继电器线圈 @ (260, 148)
        │                               │
        │  (自动垂直连接)                │  (自动垂直连接)
        ▼                               ▼
   CO v0 @ (210, 170)             CO v3 @ (260, 170)
   Down+Right                      Left+Down
        └──────────────┬───────────────┘
                       │  (自动水平连接, 水平端同高)
```

步骤：

1. 放置 K1：`function_definition='K'`, tag `-K1`, (210, 148)
2. 放置 K2：`function_definition='K'`, tag `-K2`, (260, 148)
3. 放置左 CO：`symbol='CO'`, **variant=0**（Down+Right），
   位置 = K1-A1 上方稍远 (210, 170)
4. 放置右 CO：`symbol='CO'`, **variant=3**（Left+Down），
   位置 = K2-A1 上方稍远 (260, 170)
5. EPLAN 自动生成连接：CO 的 Down 引脚与 A1 同列对齐 → 自动垂直连线；
   两个 CO 的水平端在同一高度 → 自动水平连线 → K1-A1 与 K2-A1 连通

要点：

- **CO 引脚与元件引脚方向对齐即自动连线**（如 CO 的 Down 端与 A1
  同列），CO 放在**离引脚稍远**处（如上方 ~18mm），**不要紧贴**引脚
- 选变体口诀：一个引脚**指向元件引脚方向**（A1 在符号顶部 → CO 用
  Down 端对齐），一个水平引脚**朝对侧**（左 CO 朝右用 v0，右 CO 朝左用 v3）
- 左 CO 用 v0 时**没有朝上突出的引脚**（v1 的 Up 端会朝上突出，方向不对）

### CO 符号变体（variant 0~3，全部可用）

| 变体 | 引脚方向 | 用途 |
|------|----------|------|
| **v0** | **Down + Right** | 左 CO（朝右水平端）✅ |
| v1 | Up + Right | （Up 端朝上突出，连线时不使用） |
| v2 | Left + Up | （Up 端朝上突出，连线时不使用） |
| **v3** | **Left + Down** | 右 CO（朝左水平端）✅ |

### SPECIAL 连接符号（直角/T 形/十字/中断点）

连线时除 CO（直角）外，还会用到以下连接符号
（同为 SymbolReference 类型，放置方式与 CO 相同，均在 SPECIAL 库）：

| 符号 | 名称 | 形状 | 用途 |
|------|------|------|------|
| **TLRU** | T 形线（左右，下分支） | 水平线 + 中间向下分支 | 从水平线向下接出设备 |
| **TLRO** | T 形线（左右，上分支） | 水平线 + 中间向上分支 | 从水平线向上接出设备 |
| **TOUR** | T 形线（上下，右分支） | 垂直线 + 中间向右分支 | 从垂直线向右接出设备 |
| **TOUL** | T 形线（上下，左分支） | 垂直线 + 中间向左分支 | 从垂直线向左接出设备 |
| **CR** | 十字节点 | 水平 + 垂直交叉 | 两条线交叉连接（连通） |
| **BPIN** | 中断点（连接在尾端） | 箭头（默认指向右），尾端接母线 | 母线右端终止：尾端向左接母线，箭头首端向右指向外部 |
| **BPOUT** | 中断点（连接在首端） | 箭头（默认指向右），首端接母线 | 首端连接的中断点 |
| **BP** | 中断点（两头连接） | 箭头（默认指向右），两端都可接 | 两头连接的中断点 |

中断点 BP 系列默认变体箭头指向右，无 `Function.Pins`。

命名规律：TL = T 形左右（水平主干）、TO = T 形上下（垂直主干），
R/L/U/O 表示分支方向（如 TLRU = 左右主干 + 下分支）。

用途示例：
- 一条水平母线从多个设备上方穿过，每个设备用 **TLRU**（下分支）
  接入母线 → 各设备并联
- 垂直线路中间向右分接 → **TOUR**
- 两条正交线路交叉且需连通 → **CR**（十字节点，不是直接跨越）
- 不需要连通的交叉 → 不要放 CR（或放置时避开）

### 母线（汇流排）构建标准方法（用户规范）

母线 = 水平连接线 + 分接点，**不要用两个 CO 做端点**。标准做法：

```
 X(端子,向右)        TLRU 分接×N          BPIN(尾端连接中断点)
   └──►  ───────┬──────┬──────┬──────  ◄──┤
                 │      │      │            │ (尾端向左, 箭头首端向右)
               元件   元件   元件
```

1. **左端**：`X` 端子，**variant 3（引脚向右）**——v1 引脚向左
   是方向错误；朝右接母线
2. **中间**：`TLRU` 分接点（默认 v1：左右主干 + 下分支），
   每个元件一个；**元件放在母线下方**（EPLAN y 轴向上为正）
3. **右端**：`BPIN`，**variant 0（引脚向左）**——默认 v1 引脚向下
   是方向错误；尾端向左接母线、箭头首端向右（表示母线中断，
   指向外部/后续）
4. 直线段全部由连接生成自动连线（水平/垂直均无长度限制，
   实测 142mm 水平 / 100mm 垂直）

**母线位置规范（用户规范）**：
- 母线一般画在页面**上部**（向下连接元件）或**下部**（向上连接元件），
  或**上下都画**——上母线用 TLRU（下分支）向下接元件、下母线用
  TLRO（上分支）向上接元件，元件垂直夹在两条母线之间，**回路垂直
  拉直**，方便查看
- **上母线靠近页面顶端**（如 A3 高 297mm 时上母线 y≈270、
  下母线 y≈70、元件居中 y≈170）——上母线画在页面中部（如 y=170）
  是错误布局
- 回路拉直示例：上母线 y=270、下母线 y=70，灯 H v2 放 (120,170)，
  上引脚（Up）接 TLRU(120,270) 下分支、下引脚（Down）接
  TLRO(120,70) 上分支——从下母线→灯→上母线在同一条垂直线上
- 上下母线的左右两端符号相同：左端 X v3（引脚向右）、
  右端 BPIN v0（引脚向左）

**母线绘制顺序（用户规范）**：
- **先画左右两端的端子和中断点**（左 X v3、右 BPIN v0），
  构成母线两端
- **画元件连接时，再在左右两端之间加对应的 T 形连接点**
  （上母线 TLRU 下分支 / 下母线 TLRO 上分支），每个元件一个，
  与元件同列对齐

**连接符号选型原则（用户规范，2026-08-04 自锁电路实测）**：
- **连接符号的每个方向端都代表该方向有导线**：只在该方向真正有
  线时才选用该方向的端，不要用多余的端
- **CR（十字节点）**：仅当**四个方向都有连接**时使用（如交叉
  连通）；主回路节点左侧无连接时禁用
- **TOUR（上下主干 + 右分支）**：垂直串联回路上的节点（上方来线、
  下方去线 + 向右分支接并联支路），左侧无连接——如按钮之间、
  按钮与线圈之间的汇合节点
- **CO（直角连接）**：支路端点（如继电器触点）右侧无连接时，
  用水平端朝左接主干 + 垂直端接元件引脚：上端 **CO v3**
  （Left+Down）、下端 **CO v2**（Left+Up）
- **TLRU/TLRO（左右主干 + 下/上分支）**：仅用于母线分接等
  **左右两侧都有线**的场合；单侧无连接时改用 CO


- 初始画母线时**画长一点**，左右两端尽量**靠近页面左右边缘**
  （如 A3 宽 420mm：左端 X 放 x≈30、右端 BPIN 放 x≈390），
  以便后续加入元件时不至于拥挤

### 元件标识符文本（2026-08-04 实测）

- EPLAN 2.9.4 **没有公开的标识符文本 API**：反射全部程序集无
  `IdentifierText` 类；"显示设备标识符"属性（编号 20006）是
  **只读**的（赋值抛 S063113），与 `FUNC_DEVICETAG_*` 同款
- API 的 `Function.Create` 放置元件**不触发** EPLAN 内核的标识符
  文本自动生成（手动 GUI 放置才会）；`fn.Name` 只填完整设备标识符
- **替代方案（已实现）**：放置带 tag 的元件时，add-in 在元件旁
  创建**图形文本**（`Graphics.Text`）模拟标识符：
  `Create(page, tag, 2.5)` + `Justification =
  TextBase.JustificationType.MiddleRight`（右对齐+垂直居中）+
  `Location = (x-tagDx, y)`；内容保留 `-` 前缀
- **标签偏移随符号宽度变化**（2026-08-04 对比原生标识符实测）：
  文本右边缘 = 符号包围盒左边缘外 2mm。**窄符号（触点/按钮：
  S/O/SSA/SOA/SSAR/SOAR/W2，半宽 2mm）用 `tagDx=4`**；**宽符号
  （线圈 K 等，半宽 4mm）用 `tagDx=6`**。实测触点 -S3/-S4/-K2
  右边缘均在 x-4，线圈 -K1 右边缘在 x-6
- 注意：`JustificationType` 是嵌套枚举
  `Eplan.EplApi.DataModel.Graphics.TextBase+JustificationType`；
  文本高度参数 2.5 渲染约 3.8mm
- **已知差异**：图形文本是静态对象，不随元件移动/重命名自动同步；
  与 EPLAN 原生标识符文本（手动放置的 K2 那种）行为不同

### 联锁逻辑图与 SR 块 Q 输出（2026-08-05 实测，JHM 项目）

- **表格行号映射**（A3 页面，行距 8mm，行号从上往下）：行 1=y=250、
  行 2=y=242（不接分支）、行 3~11=y=234~170（逻辑输出 0，共 9 行）、
  行 12~19=y=164~108（Q 分支行）、行 20=y=100（分支终点）；
  **表格连接点 y = 行中心 y + 2mm**（即 B 型网格 1 格，实测已有
  连接点 172~236 间隔 8mm）
- **SR 块加 Q 输出**：
  - Q 文本放 SR 块矩形内右下（块 (286,222)-(298,248)，Q @ (293,227)）
  - **DCPNG5（设备连接点，隐藏）作连接点**：紧贴块壁，引脚方向由
    变体控制——**v3 = 引脚向右（引脚 = 放置位置 +2mm）、v1 = 引脚
    向左（-2mm）**；要让引脚落在 (298,227) 需放置 @ (296,227)
  - 分支线用连接符号：CO v3(Left+Down) 转弯点、每行一个 TOUR
    （上下主干 + 右分支）接入、CO v1(Up+Right) 终点
  - **竖直段位置**：已有输出竖线（x=304）左边 2mm（x=302）
- **TOUR 串联连接**：靠放置时的增量连接生成（从上到下逐个放置）；
  PDF 导出有延迟——**等待 ~20 秒后重新导出**才能看到完整线段；
  `generate /TYPE:CONNECTIONS` 命令对 SymbolReference 连接**无效**，
  不要依赖
- **复制行内容**：用 `list_texts` 读取准确中文（设备名称/用途），
  `place_text` 填入新行；逻辑反向 = 值 0→1、触点 断开→闭合，
  输出状态列保持"关闭"

### 页面文本批量替换（TEXT: 工具，2026-08-04 实测）

- 通过 `place_device` 的 `symbol="TEXT:<from>;<to>[;<xmin>;<xmax>[;<ymin>;<ymax>]]"`
  触发：遍历页面 `AllGraphicalPlacements`，把所有**内容等于 from 且位置
  在过滤范围内的图形文本**改为 to（用于批量修改联锁逻辑图输出值等；
  页面 90 个 Text 对象实测有效）
- **参数分隔符用 `;`**（曾用 `|` 导致 from/to 含 `|` 时解析错误——
  实测把 9 个文本误写成 `0|310|330`，教训：文本内容可能包含分隔符）
- 位置过滤参数（xmin/xmax/ymin/ymax，mm）用于只改指定列，
  如只改输出列 `TEXT:1;0;310;330`
- **MultiLangString 坑**：`Contents.ToString()` 格式是
  `"语言@内容;语言@内容;..."`（如 `??_??@0;`），**不是纯文本**——
  匹配用 `Contains("@" + from + ";")`（如 `"@0;"`），直接比字符串
  永远匹配不到
- 修改用 `new MultiLangString()` + `SetAsString(to)` 再赋回 `Contents`
- 实测：JHM联锁逻辑图 =A区+MT-A104/1 页输出列 9 个 "0"→"1"，
  PDF 验证 9 个值全部变为 1


- PDF 线段分析（PyMuPDF 提取直线段）局限性很大：只提取直线段、
  依赖过滤条件、连接生成有延迟、SymbolReference 的连接不可见等
- **分析结果发现问题时，先向用户确认是否真的有问题**，
  不要自行断定（用户可在 EPLAN 界面直接查看实际情况）

**方向要点（2026-08-04 实测确认）**：
- EPLAN 页面坐标 **y 轴向上为正**：母线 y=100 时元件放 y<100
  才是"下方"。TLRU 下分支只能接下方元件（如 H 灯 v2 竖放、
  引脚 2=Up 朝上，方向相对才自动连接）；元件放母线上方
  （y>母线）无法自动连接
- 变体速查：母线左端 X 用 **v3**（引脚向右）、右端 BPIN 用 **v0**
  （引脚向左）、分接 TLRU 用默认 v1（下分支）
- **TLRU 分接点与母线同高度**（y 与母线一致，如母线 y=100 则
  TLRU 放 y=100），不要放矮（贴近元件处）；元件在母线下方

要点：
- 母线两端符号与相邻分接 TLRU 之间的直线段无需任何中继符号
- 同一母线的分接 TLRU 间距不限（自动连线）
- 0V 等回线同理：垂直直线段两端各一个连接符号即可，中间零中继
- 中断点 BP 系列：BPIN=尾端连接 / BPOUT=首端连接 / BP=两头连接，
  默认变体箭头指向右

### CO 放置的 API 注意（关键！）

- **CO 是 SymbolReference 类型，不是 Function**：
  `Function.Create(page, sv, pos1, pos2)` 抛 `ObjectCreationException`
  （S063085 "不能创建功能"）。
- 正确放置方式（add-in 已内置 fallback，`PlaceBySymbol` 失败时自动执行）：
  ```csharp
  var sr = new SymbolReference();
  sr.Create(page, sv);      // sv = CO 的 SymbolVariant (variant 0~3)
  sr.Location = pos1;       // 放置到目标位置(离引脚稍远, 引脚方向对齐)
  ```
- CO 有 variant **0**：`SymbolVariant.Initialize(symbol, variantNr)` 的
  variantNr 从 0 开始有效（v0~v3 可用，v4+ 不存在）。
- `list_connections` 只遍历 Function 的连接，**看不到 CO**
  （SymbolReference）的连接；用 PDF 导出验证图形连线。

### 其他已知无效的方式（已实测）

- `angle` 参数无效：`Function.Create(Page, SymbolVariant, PointD, PointD)`
  的两个 `PointD` 是符号**包围盒**（左下/右上角），不是旋转角。
- 公开 API **没有镜像/旋转属性**（Function / SymbolVariant /
  SymbolReference / Placement 反射确认均无），方向只能靠 variant。
- `FindSymbolVariant` 会遍历项目**所有**符号库（第一个库是 SPECIAL，
  连接符号在其中，按符号名查找即可）。

---

## 3. 已知 API 坑（全部实测）

### LockingStep 必需

- 所有**修改项目数据**的 API 调用必须在
  `using (LockingStep ls = new LockingStep())` 内执行，
  否则抛 `NoLockingStepException`（S063110 "不能生成 LockingStep 类别的对象"）。
- **只读枚举同样需要**（2026-08-04 实测）：`prj.Pages` / `p.Functions`
  的惰性加载在 timer 线程无外层 LockingStep 时报 S063110——
  list_pages / list_devices / list_pins / list_connections 全部中招
  （项目刚创建时数据已加载，偶发成功，不可依赖）。
- add-in 已在请求分发入口（`Dispatch`）统一包裹（`NeedsLockingStep`
  判定：写操作 + export 系列 + 4 个 list_* 只读枚举）；LockingStep 可嵌套。

### FunctionDefinitionLibrary

- `new FunctionDefinitionLibrary(prj)` **构造时已初始化**，再调
  `Initialize(prj)` 抛 `ObjectAlreadyCreatedException`（S063079 "对象已经生成"）。
- 用 **foreach 枚举库**（枚举器），不要用 `FunctionDefinitions` 属性
  （返回未填充的占位对象，`Identifier` 全为空）。
- GB 模板功能定义名称：继电器类别 = **`K`**（不是 `CON_RELAY_COIL`）。
  **坑：`function_definition='K'` 匹配库中第一个有 BaseSymbol 的 `K`
  定义，放置出来的是偏振继电器 `KP`**（矩形 + 内部极化标记，上下引脚）。
- **普通继电器线圈 = 符号 `K`**（图形：简单矩形 + 2 引脚，PDF 导出
  仅 6 段线段；`KP` 约 28 段）：
  - variant=2：竖放，引脚上下（Up/Down）——线圈两端竖向进线用 ✅
  - variant=1：横放，引脚左右（Left/Right）
  - 放置用 `symbol='K'`，**不要**再用 `function_definition='K'`
- `KP` = **偏振继电器**（polarized relay）线圈；与 `K` 的引脚 index
  也相反（`KP` index 0=顶端 Up / `K` v2 index 0=底端 Down）。
- GB_symbol 库 K 系列符号名（无 `KA`/`KM`/`KT` 裸名，全是带编号形式）：
  `K`、`KP`、`KA2~4`、`KM2~4`、`KR2~4`、`KS`、`KS2~4`、`KT2~3`、
  `KST`、`KRES`、`KUB`、`KUN`、`KFI`、`KFT1`、`K_3`、`K_4` 等
  （符号库全名列表可用一次"symbol variant not found"报错信息枚举）。

### 设备标签（DeviceTag）设置

- 所有 `FUNC_DEVICETAG_*` 属性（`PFunction.FUNC_DEVICETAG_FULLNAME`、
  `MAINNAME` 等）都是**只读**，`Set` 抛
  `SettingValueFailedException`（S063113 "设置一个新属性值失败"）。
- 正确方式：直接写可写属性 **`Function.Name`**（EPLAN 官方示例同款）。
- 读取也用 `fn.Name`，回退 `FUNC_DEVICETAG_FULLNAME`。

### 放置符号（Function.Create）

- 用 `new Function()` + 实例 `fn.Create(page, sv, pos1, pos2)` **一次完成**。
- 禁止：静态 `Function.Create(sv, prj)` 后再调用实例 Create
  （二次创建抛 `ObjectAlreadyCreatedException`，S063079）。
- 不能创建 Function 的符号（如 CO 连接符号）自动 fallback 到
  `SymbolReference.Create`（见第 1 节）。

### 项目生命周期

- `new_project` 创建后项目**保持打开**（2026-08-04 实测推翻旧结论）：
  对**已打开**项目再调 `open_project` 报 S063045"不能打开项目"；
  项目**关闭后** `open_project` 才能成功。add-in 已改为幂等
  （`OpenProject` 先 `FindOpenProject` 查重，已打开直接返回成功）。
  旧结论"创建后不保持打开、需显式 open_project"是错的——之前报
  S063045 正是"重复打开已打开项目"。
- 删除项目前**先关闭**（若已打开）；`CloseProject` / `DeleteProject`
  用 `FindOpenProject` 只在已打开项目中查找，
  **不要**用 `ResolveProject`（它会自动打开项目，造成冲突 / SEH 异常）。
- `p.Remove()` 删除页面偶尔抛 `SEHException`（"外部组件发生异常"），
  **重试一次即可**。
- `prj.Close()` 关闭项目同样偶尔抛 `SEHException`，且关闭**实际已生效**
  （重试再调 `Close()` 报 S063080"对象未生成"）。add-in 的
  `CloseProject` 已改为异常后重新查询打开列表，项目已不在列表中则
  视为关闭成功并返回 `ok: true`（verified）。
- `eplan_save`（XES 动作）在 API 打开的项目上返回 `false`——
  数据已直接入库，属正常现象，不是错误。
- EPLAN 崩溃后：重启 EPLAN → 删除损坏项目文件（.elk + .edb）→ 重建。

### 页面对象遍历与文本编码（2026-08-05 实测）

- **`AllPlacements` vs `AllGraphicalPlacements`**：连接符号
  （CO/TLRU/TOUR/BPIN 等 SymbolReference）**只在 `AllPlacements` 里**；
  `AllGraphicalPlacements` 只有 Text 等图形对象——遍历页面对象必须
  用 `AllPlacements`（用 AllGraphicalPlacements 删除 CO/TOUR 永远
  删不到，实测删除 0）
- **MultiLangString 中文读取**：`Contents.ToString()` 对非 ASCII
  返回损坏文本（U+FFFD，如"断开"变 `�Ͽ�`），**用
  `Contents.InternalString` 读原始字符串**（2026-08-05 实测；
  `GetString(Language)` 也可）；写入用 `SetAsString`
- **SymbolReference 删除**：`SymbolReference.Remove()`（不是
  StorableObject）在 LockingStep 内有效；`Location` 与放置坐标一致，
  可按区域/精确位置批量删除（实测 9 个 TOUR 逐个删除成功）
- **DCPNG5 位置语义**：设备连接点（隐藏，无图形），1 引脚；
  引脚 = 放置位置 ± 2mm（v1 引脚左 2mm 朝左 / v3 引脚右 2mm 朝右）；
  连接点位置 = 引脚位置

### 诊断技巧

- PDF 导出后可用 PyMuPDF 分析线段判断连接是否画出。
  坐标换算：`y_pdf = 842 - y_mm * 2.8346`（A3 横向页面）。
- `list_connections` 列出项目全部连接及其 `placed` 状态
  （`placed: true` 才是可见连接；注意只覆盖 Function 的连接）。
- `list_pins` 输出引脚**绝对坐标**与方向（`dir`），
  可据此判断符号朝向与对齐关系。

---

## 4. add-in 部署与修改（实测）

- 重新编译后的 `build/ReferenceAddIn.dll` **不能在外部直接覆盖**
  （EPLAN 通过 ShadowCopyAssemblies 机制加载，外部替换无效）。
- **只能在 EPLAN 软件内手动安装/注册 add-in**（Setup Manager 或
  添加 add-in 的界面操作），安装后重启 EPLAN 生效。
- 编译命令：`build.ps1 -EplanApiDir "C:\Program Files\EPLAN\Platform\<ver>\Bin"`
  （csc.exe 编译，成功后输出 `addin/build/ReferenceAddIn.dll`）。

## 5. 批量放置与防崩溃（实测）

### 崩溃模式

- EPLAN 2.9.4 在 Win11 上**密集批量放置符号**（约 28~36 个后）触发崩溃：
  - `ntdll.dll 0xc0000374`（堆损坏 HEAP_CORRUPTION）——最常见
  - `ucrtbase.dll 0xc0000409`（__fastfail）——偶发
- 与 add-in 代码无关（回滚版 DLL 同样触发），属 EPLAN 原生内存不稳定；
  打开/查询刚崩溃过的工程也可能复现。

### 防御策略（本次 114 个符号零崩溃验证）

- **每批放置 ≤8 个符号**，符号间 `sleep 2s`，**批间暂停 8s**
  （让 EPLAN 完成增量连接生成）
- 用 Python 脚本直接调 `eplan_bridge`（MCP 服务器空闲时独占通信目录，
  避免并发读写 command.txt/result.txt）
- 崩溃后恢复：重启 EPLAN → **删除损坏工程文件（.elk + .edb）→ 重建**
  （崩溃时工程数据可能未落盘或已损坏，不要尝试恢复）

### 连接生成与绘图细节（自锁电路实测）

- **连接生成有延迟**：放置后立即导出的 PDF 可能缺线段，等数秒或重新导出
- 水平直线段由两端连接符号自动生成，**无长度限制**（实测 142mm 间隙连通）
- 连接符号水平端必须**方向相对**（Right↔Left）才互连；
  **同向端点不连**（Right↔Right / Left↔Left——列 5 绕线背向的教训，
  且已放置的符号无法改变体，只能另布新线绕过）
- **元件引脚端点重合（元件↔元件）不自动连接**，每段连接至少一端
  必须是连接符号（CO/TLRU/TLRO/TOUR/TOUL），间距 ≤26mm 均可桥接
- 布线原则：**90° 转弯放 CO，分支放 T 形（TLRU/TLRO/TOUR/TOUL），
  直线段不放大符号**（自动生成）
- `draw_line`/`draw_polyline` 的 payload 数组在 .NET Framework 上失效
  （`JavaScriptSerializer` 返回 `ArrayList`，代码只认 `List<object>`）
  ——`place_devices`/`connect_many` 同理；需要画横线时改用
  TLRU 串（水平端互连）或放弃图形线

## 6. 本次验证的 add-in 修改清单

`addin/ReferenceAddIn.cs`：

- 请求分发统一 `LockingStep`（`Dispatch` + `IsWriteOperation`）
- `PlaceBySymbol`：单次 `new Function()` + 实例 Create；
  失败返回 null，由调用方 fallback 到 `SymbolReference.Create`
- `FindFunctionDefinition`：构造即初始化 + foreach 枚举
- `SetDeviceTag`：改用可写属性 `Function.Name`
- `CloseProject` / `DeleteProject` / 新增 `FindOpenProject`
- `OpenProject` 幂等化：先 `FindOpenProject` 查重，已打开直接返回成功
  （原实现对已打开项目报 S063045）
- `CloseProject` 异常后验证：`prj.Close()` 抛 SEHException 时重新查询
  打开列表，项目已关闭则返回成功（verified）
- `IsWriteOperation` 更名为 `NeedsLockingStep`：补入 export 系列
  （export_project / export_pages / export_pdf / export_partslist /
  export_devicelist，`new Export()` 导出 API 在 timer 线程需显式
  LockingStep）与只读枚举（list_pages / list_devices / list_pins /
  list_connections，惰性加载同样报 S063110）（2026-08-04 实测）
- `FindSymbolVariant`：遍历所有符号库（variant 从 0 起有效）
- `place_device` 支持 `angle`（无效但保留）、`list_connections`、
  `list_pins` 绝对坐标、`list_devices` 坐标等诊断能力
- `draw_line`/`draw_polyline` 的 payload 解析兼容 ArrayList
  （JavaScriptSerializer 反序列化 JSON 数组为 ArrayList，
  此前只认 List<object> 导致画线一直失败）（2026-08-05 修复）
- 新增原子操作 op（2026-08-05）：
  - `place_text`：放置图形文本（content/x/y）
  - `set_texts`：按内容+区域替换文本（MultiLangString 匹配）
  - `remove_symbols`：按区域删除 SymbolReference（用
    `AllPlacements` 遍历 + `SymbolReference.Remove()`）
  - `exec_cmd`：执行 EPLAN CLI 命令（如 generate）
  - `list_symbols`：列出页面所有 SymbolReference 的位置
  - `list_texts`：列出页面所有文本的位置和内容
    （用 `Contents.InternalString` 读原始中文）
  - 早期通过 `place_device` 的 symbol hack（`ADD_TEXT:`/`TEXT:`/
    `CMD:`/`REMOVE_SR:`）实现这些功能——**已废弃**，统一用原子 op

**架构原则（用户规范，2026-08-05）**：
- **add-in（DLL）只实现原子操作**：放置、查询、删除等单一功能
- **组合功能（文本替换、区域清理、批处理等）由 MCP 服务器工具
  或 Python 脚本实现**——脚本直接调 `eplan_bridge.send_command`，
  在 MCP 服务器空闲时独占通信目录（不要与 MCP 工具并发调用）

`src/mcp_server.py` + `src/eplan_bridge.py`：

- 新增 `eplan_list_connections` 工具
- `angle` 参数透传
- 2 个经验 prompt（`eplan_wiring_connections`、`eplan_known_pitfalls`）
- 已加入 6 个工具（2026-08-05）：eplan_place_text / eplan_set_texts /
  eplan_remove_symbols / eplan_exec_cmd / eplan_list_symbols /
  eplan_list_texts（工具共 38 个；**新工具需重启 Claude Code 会话
  才加载**——重启前用 Python 脚本直调 bridge 实现同样功能）
