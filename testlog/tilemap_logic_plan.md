# 视觉 Tilemap + 隐藏逻辑 Tilemap 综合实施计划

> 目标：所有影响玩法的配置在 Inspector 中可查看和修改；所有空间数据在 Scene 视图中可预览；运行状态在 Play Mode 中可观察。

## 一、当前状态 (Code Review)

### 已完成
| 模块 | 文件 | 状态 |
|------|------|------|
| `GameplayTile` | `Assets/Scripts/Tilemap/GameplayTile.cs` | ✅ 存在（CellType/Walkable/CanBeDestroyed） |
| `TilemapGridBridge` | `Assets/Scripts/Tilemap/TilemapGridBridge.cs` | ✅ 存在（TryReadCells/CellToWorld/WorldToCell） |
| `TilemapOverlayController` | `Assets/Scripts/Tilemap/TilemapOverlayController.cs` | ✅ 存在（基础 Tool/Destroyed 事件） |
| GridSystem Tilemap 集成 | `Assets/Scripts/Systems/GridSystem.cs` | ✅ 优先走 Bridge，回退 GridConfig |
| PrototypeWorldView 桥接 | `Assets/Scripts/Systems/PrototypeWorldView.cs` | ✅ 使用 Bridge（含 fallback） |
| BuildGrid 坐标修复 | `Assets/Scripts/Systems/PrototypeWorldView.cs` | ✅ 已用 MinY/MaxYExclusive |

### 未完成
| 模块 | 缺失内容 |
|------|---------|
| 场景 Tilemap 铺设 | Gameplay Logic/Tool Overlay/State Overlay Tilemap 未在场景中创建和铺设 |
| Editor 工具 | 无 TilemapGridBaker、无 Gizmo、无 Scene Validator |
| HUD | 仍通过代码动态创建 Canvas/Panel/Button/Text（300+ 行 BuildHud） |
| Bootstrap | 仍通过反射设置私有字段，运行时创建系统 |
| Config 资产 | 无 CustomerFlowConfig/SecurityPatrolConfig/GamePresentationConfig |
| 数据可视化 | Grid/Route/Patrol 无 Gizmo 显示；库存无自定义 Inspector；EventBus 不可见 |
| 场景结构 | 无 Game Flow/Economy/Grid 等分组，系统全部平铺 |

---

## 二、实施步骤

### Phase A — 基础架构加固（3 天内）

#### A1. 场景层级重构

将 `Game` 根节点下全部系统按功能分组：

```
Game Runtime
├─ Game Flow
│  ├─ CampaignProgressSystem
│  ├─ GamePhaseSystem
│  └─ BankruptcySystem
│
├─ Economy And Customers
│  ├─ EconomySystem
│  ├─ PrototypeCustomerFlowSystem
│  └─ CustomerStatisticsSystem (新增)
│
├─ Grid And Route
│  ├─ GridSystem
│  ├─ TilemapGridBridge
│  ├─ RouteSystem
│  └─ RoutePreview (新增)
│
├─ Tool Gameplay
│  ├─ ToolInventorySystem
│  ├─ PlacementSystem
│  ├─ ToolResolutionSystem
│  └─ BossInterferenceSystem
│
├─ Security
│  ├─ SecurityPatrolSystem
│  └─ PatrolPreview (新增)
│
├─ Presentation
│  ├─ PrototypeWorldView
│  ├─ PrototypeHudView
│  └─ GameResultView (新增)
│
└─ Input
   └─ PrototypeInputController
```

**禁止第二套系统**：脚本中 `FindObjectOfType<X>() != null` 检测重复。场景只保留一套实例。

#### A2. 场景 Tilemap 铺设

在已有 `Shop Tilemaps`（Grid 位置 -0.87, 0）下：

| 子 Tilemap | Renderer | SortingOrder | 用途 |
|-----------|----------|-------------|------|
| ground | 启用 | -99 | ✅ 已存在 |
| wall | 启用 | 0 | ✅ 已存在 |
| shelf | 启用 | -50 | ✅ 已存在 |
| Gameplay Logic | **禁用** | N/A | 新增：数据层，无渲染 |
| Tool Overlay | 启用 | +10 | 新增：道具显示 |
| State Overlay | 启用 | +20 | 新增：销毁/假货覆盖 |
| Editor Preview | 启用（编辑时） | +30 | 新增：路线/范围预览 |

**铺砖规则**（Gameplay Logic）：
- 参照 `Prototype_GridConfig.asset`（15x11，原点 -6,0）的数据
- 每个格子放置对应 GridCellType 的 GameplayTile
- 坐标直接对应：Tilemap cell (x,y) = GridPosition (x,y)

**Tool Overlay**：`TilemapOverlayController` 已写好基础事件订阅，只需要场景中铺好 Tilemap 并绑定引用。

**State Overlay**：`OnWorldObjectDestroyed` 已处理；假货替换需扩展 `TilemapOverlayController` 增加 `ApplyFakeGoodsFeedback(GridPosition)` 方法。

#### A3. TilemapGridBridge 补全

当前 `TryReadCells` 正确，新增：

```csharp
// 供编辑器 Gizmo 使用
public GridCellType PeekCellType(Vector3Int cell)
{
    return (_gameplayTilemap.GetTile(cell) as GameplayTile)?.CellType ?? GridCellType.Floor;
}

// 供 RouteSystem 直接查询（替代 GridSystem 的 IsRouteWalkable）
public bool IsCellWalkable(Vector3Int cell)
{
    return (_gameplayTilemap.GetTile(cell) as GameplayTile)?.Walkable ?? true;
}
```

### Phase B — 编辑器可视化（5 天内）

#### B1. ToolConfig 自定义 Inspector

目前 `Prototype_GridConfig.asset` 的引用在场景中已损坏（GUID 不存在）。本次修复并增强 `ToolConfig` 的 Inspector：

- 显示：道具 ID / 显示名称 / 分类
- 可编辑：允许放置格、占用范围（Footprint）、触发范围（TriggerOffsets）
- 可编辑：使用次数、触发时机、效果列表
- Inspector 按钮「预览触发范围」在 Scene 中高亮触发格
- 显示 Icon 预览

```csharp
// Assets/Scripts/Editor/ToolConfigEditor.cs
[CustomEditor(typeof(ToolConfig))]
public class ToolConfigEditor : UnityEditor.Editor
{
    // Footprint 网格显示
    // TriggerOffsets 高亮
    // 效果列表清晰展示
}
```

#### B2. Grid Gizmos

`GridSystem` 增加 `OnDrawGizmosSelected()`：

```
地图边界 → 白色线框
每个 Cell → 小方块 + CellType 缩写
  Floor       → 透明绿
  Wall        → 红
  Warehouse   → 黄
  Checkout    → 蓝
  Restroom    → 紫
  FortuneTree → 深绿
  Entrance    → 青色
  Exit        → 青色
  Security    → 淡蓝
  Blocked     → 黑
可通行/不可通行区别
已销毁格 → 深灰 X 标记
附着道具 → 橙色小点
替换机关 → 红色小点
```

#### B3. Route Gizmos

`RouteSystem` 增加：
- Entrance（绿三角）、Checkout（蓝$）、Exit（红三角）图标
- 顾客路线连线 + 方向箭头
- 路线序号标注
- 不合法节点红色闪烁警告
- BFS 路线与 `_routeOverride` 手写路线切换显示

Inspector 按钮：
- Rebuild Route
- Preview Route（Scene 中高亮）
- Clear Preview
- Validate Route（Check：起点可达、终点可达、无断点）

#### B4. Security Patrol Gizmos

`SecurityPatrolSystem` 增加：
- 巡逻节点（圆点）
- 节点编号
- 巡逻方向箭头连线
- 当前视野范围（带透明度圆）
- 可见格范围高亮

Scene 中允许直接拖动巡逻点（`Editor` 中使用 `Undo.RecordObject` + 坐标系转换）。

#### B5. Gameplay Logic Tilemap 可视化

编辑模式下场景切换开关：

```csharp
// Assets/Scripts/Editor/GameplayTilemapOverlay.cs
// 使用 SceneView.duringSceneGui 绘制 overlay
// 快捷键或自定义菜单开关
```

显示内容：
- `Show Logic Overlay` — 半透明颜色覆盖逻辑层
- `Show Cell Labels` — 每个 Cell 显示 CellType 缩写
- `Show Walkability` — 绿色（可通）/ 红色（不可通）
- `Show Placement Rules` — 显示每种道具可放置在哪

### Phase C — 配置资产化（3 天内）

#### C1. 新增 Config ScriptableObject

**CustomerFlowConfig** (`Assets/Scripts/Config/CustomerFlowConfig.cs`)
```csharp
[CreateAssetMenu]
public class CustomerFlowConfig : ScriptableObject
{
    public int baseCustomersPerDay = 5;
    public float spawnInterval = 0.85f;
    public float cellsPerSecond = 1.176f;
    public float escapeSpeedMultiplier = 1.35f;
    // 好感度客流倍率
    public float lowFavorabilityThreshold = 35f;
    public float lowFavorabilityMultiplier = 0.4f;
    public float mediumFavorabilityThreshold = 65f;
    public float mediumFavorabilityMultiplier = 0.7f;
}
```

**SecurityPatrolConfig** (`Assets/Scripts/Config/SecurityPatrolConfig.cs`)
```csharp
[CreateAssetMenu]
public class SecurityPatrolConfig : ScriptableObject
{
    public Vector2Int[] patrolPath;
    public int visionRange = 1;
    public int stepsPerTurn = 1;
    public bool loopPatrol = true;
    public bool canDisableTools = true;
}
```

**GamePresentationConfig** (`Assets/Scripts/Config/GamePresentationConfig.cs`)
```csharp
[CreateAssetMenu]
public class GamePresentationConfig : ScriptableObject
{
    [Header("Appearance")]
    public Color floorColor;
    public Color wallColor;
    public Color warehouseColor;
    public Color checkoutColor;
    public Color restroomColor;
    public Color fortuneTreeColor;
    public Color entranceColor;
    public Color exitColor;
    public Color blockedColor;
    public Color destroyedColor;

    [Header("Customer Colors")]
    public Color normalCustomerColor;
    public Color angryCustomerColor;
    public Color scaredCustomerColor;

    [Header("Sorting Orders")]
    public int cellSortingOrder;
    public int detailSortingOrder;
    public int toolSortingOrder;
    public int customerSortingOrder;
    public int securitySortingOrder;
}
```

#### C2. 创建资产实例

```
Assets/Configs/
├─ Campaign/MainCampaign.asset
├─ Economy/MainEconomy.asset
├─ Customer/CustomerFlow_Default.asset
├─ Security/SecurityPatrol_Default.asset
├─ Presentation/GamePresentation_Default.asset
├─ Tools/Tool_BoilingWater.asset (已有)
├─ Tools/Tool_ClownBox.asset (已有)
├─ Tools/Tool_FakeGoods.asset (已有)
├─ Tools/Tool_BribeEnvelope.asset (已有)
├─ Tools/Tool_QRCode.asset (已有)
├─ Tools/Tool_SmithAgent.asset (已有)
└─ Tilemap/GameplayTiles/* (10 个 GameplayTile 实例)
```

#### C3. 系统引用变更

- `PrototypeCustomerFlowSystem._customersPerDay` → `CustomerFlowConfig`
- `PrototypeCustomerFlowSystem._spawnInterval` → `CustomerFlowConfig`
- `PrototypeCustomerFlowSystem._cellsPerSecond` → `CustomerFlowConfig`
- `SecurityPatrolSystem._patrolPath` → `SecurityPatrolConfig`
- `SecurityPatrolSystem._visionRange` → `SecurityPatrolConfig`
- `SecurityPatrolSystem._stepsPerTurn` → `SecurityPatrolConfig`
- `PrototypeWorldView` 中颜色 → `GamePresentationConfig`
- `PrototypeWorldView` 中 SortingOrder → `GamePresentationConfig`

### Phase D — HUD 与 Bootstrap 清理（4 天内）

#### D1. HUD Prefab 化

当前 `PrototypeHudView` 动态创建 UI 的代码（`BuildHud()`、`CreateCanvas()`、`CreateButton()`、`CreatePanel()` 等 300+ 行）全部替换为 Prefab 引用。

**新建** `Assets/Prefabs/UI/GameHUD.prefab`：
- Canvas（ScreenSpaceOverlay, 1280x720）
  - Top Bar（好感度/客流/天数/昼夜指针）
  - Bottom Bar（道具按钮行 / 操作按钮行）
  - Log Panel（日志）
  - Game Result Panel（结算面板，初始隐藏）

`PrototypeHudView` 改为：
```csharp
[SerializeField] private GameHUD _hudPrefab;  // 场景中预先放置
[SerializeField] private Text _confidenceText;
[SerializeField] private Text _flowText;
[SerializeField] private Text _phaseText;
// ... 全部序列化引用，不再 transform.Find()
```

移除以下方法：`BuildHud()`、`CreateCanvas()`、`TryBindExistingHud()`、`CreateButton()`、`CreatePanel()`、`CreateText()`、`CreateLayoutText()`、`CreateStretchText()`、`CreateContainer()`、`CreateIconImage()`、`BuildDayNightTrack()`、`BuildToolButtons()`、`BuildActionButtons()`、`BuildResultPanel()`。

保留：事件处理方法、数据刷新逻辑、状态计算。

**GameResultView** 独立抽取：结算面板单独做成组件，处理游戏结束显示。

#### D2. Bootstrap 标记废弃

`PrototypeRuntimeBootstrapper.Bootstrap()`：
1. 标记为 `[Obsolete("Scene must be pre-configured. Remove after full migration.")]`
2. 移到 `Assets/Scripts/Editor/PrototypeRuntimeBootstrapper.cs`（或放到 `Editor` 目录）
3. 场景中所有系统在 Inspector 绑定引用，不再通过反射 `SetField()` 设置

#### D3. Inventory 自定义 Inspector

`ToolInventorySystem` 当前使用序列化数组。改为：
- 可展开列表显示 BlackBossSupport 和 CarriedOver
- 每项显示：道具名称、Icon、初始数量、当前数量
- Play Mode 中当前数量只读
- 增加统计显示：已放置、已消耗

### Phase E — 运行状态可视化（3 天内）

#### E1. GamePhaseSystem 状态面板

Play Mode Inspector 显示：
```
Current Phase: NightPlanning
Current Day: 2 / 5
Game Ended: false
Editor Only 调试按钮:
[Begin Game] [Start Day] [Complete Day]
[Start Next Night] [Force Bankruptcy] [End Game]
```

#### E2. EconomySystem 统计面板

```
Current Revenue: 85
Bankruptcy Threshold: 20
Today's Customers Who:
  Normal: 12
  Angry: 3
  Scared: 2
  Purchased: 8
Revenue Changes This Day: +5, -10, +5...
```

#### E3. GameplayEventMonitor

新增 `Assets/Scripts/Debug/GameplayEventMonitor.cs`（放在 Editor 或 Debug 子目录）：
```csharp
// 记录最近 N 条 EventBus 事件
// Play Mode Inspector 显示
// 支持按类型过滤、清空、暂停
```

显示：
```
[12:34:01] OnDayStarted → Day 2
[12:34:02] OnToolSelected → 小丑盒
[12:34:05] OnToolPlaced → 小丑盒 @ (5,3)
[12:34:12] OnCustomerAngered → Customer #3
[12:34:15] OnRevenueChanged → 85 (-10)
```

#### E4. CustomerStatisticsSystem

新增 `Assets/Scripts/Systems/CustomerStatisticsSystem.cs`，纯统计：
- 总生成数 / Normal / Angry / Scared
- 购买成功 / 假货上当
- 保安清理 / 史密斯专员
- 每日环比（增量显示）

### Phase F — 校验与清理（3 天内）

#### F1. GameSceneValidator

新增 `Assets/Scripts/Editor/GameSceneValidator.cs`：

```
[MenuItem("CIGAgamejam/Validate Scene")]
```

校验项：
- [ ] 系统是否重复（每个系统类只出现一次）
- [ ] 所有 MonoBehaviour 的 SerializeField 是否非空
- [ ] 配置资产是否缺失（所有 _config 引用）
- [ ] 道具 ID 是否重复（不同 ToolConfig 的 Id 不能相同）
- [ ] 库存是否包含全部默认道具
- [ ] Gameplay Logic 是否完整（所有格有 Tile）
- [ ] Entrance → Checkout → Exit BFS 是否可达
- [ ] HUD 引用是否齐全
- [ ] Sorting Layer / Order 是否正确

Inspector 提供：
- Validate Scene 按钮
- Auto Fix Safe Issues（只修空引用、断链等安全项）
- Select Problem Object 按钮

#### F2. TilemapGridBaker

`Assets/Scripts/Editor/TilemapGridBaker.cs`

```csharp
[MenuItem("CIGAgamejam/Grid/Bake GridConfig")]
// 扫描 Gameplay Logic Tilemap → 生成 .asset

[MenuItem("CIGAgamejam/Grid/Validate Map")]
// 完整性 + BFS 可达性 + 逻辑层 vs 美术层一致性

[MenuItem("CIGAgamejam/Grid/Preview Route")]
// Scene 中高亮顾客路线

[MenuItem("CIGAgamejam/Grid/Clear Preview")]
// 清除标记
```

#### F3. 死代码清理

- `BuildGrid()` — 从未被调用，移除整个方法及依赖的 `_cellRenderers` 字典
- `_toolMarkers` 字典 — 替换为 Tilemap Overlay 后移除
- `_destroyedObjectMarkers` — 替换为 State Overlay 后移除
- `_routeMarkers` — 替换为 Tilemap 路线或直接 DrawLine
- `_securityMarker` — 替换为 Tilemap 或 DrawLine
- `_origin` / `_cellSize` — 确认 Bridge 稳定后移除

### Phase G — 最终验证（2 天内）

#### G1. 验证清单

- [ ] 打开场景，无 Missing Script
- [ ] Gameplay Logic 层已铺设，Renderer 已禁用
- [ ] GridSystem Awake() 从 Bridge 正确读取数据
- [ ] Tool Overlay 事件正常渲染
- [ ] State Overlay 销毁/假货正常显示
- [ ] 顾客沿正确路线移动
- [ ] BFS 路径计算使用 Bridge 的 Walkable 数据
- [ ] 坐标转换与 Tilemap 对齐（点击检测）
- [ ] Gizmo 在选中系统时正确显示
- [ ] Validate Scene 零问题
- [ ] 运行时 Play Mode Inspector 面板只读

#### G2. 回退路径

如果 Tilemap 出现同步问题：
- `GridSystem.Awake()` 中 `_tilemapBridge.IsReady` → 不成立时走旧 GridConfig
- 持续保留 `_origin`/`_cellSize` fallback 直到确认 Tilemap 完全稳定
- 旧代码逐步移除，留有回滚 commit

---

## 影响文件总览

### 新增文件（12 个）
| 文件 | 位置 |
|------|------|
| CustomerFlowConfig.cs | Assets/Scripts/Config/ |
| SecurityPatrolConfig.cs | Assets/Scripts/Config/ |
| GamePresentationConfig.cs | Assets/Scripts/Config/ |
| CustomerStatisticsSystem.cs | Assets/Scripts/Systems/ |
| GameResultView.cs | Assets/Scripts/Systems/ |
| GameplayEventMonitor.cs | Assets/Scripts/Debug/ |
| ToolConfigEditor.cs | Assets/Scripts/Editor/ |
| GridSystemGizmos.cs | Assets/Scripts/Editor/ |
| RouteSystemGizmos.cs | Assets/Scripts/Editor/ |
| GameSceneValidator.cs | Assets/Scripts/Editor/ |
| TilemapGridBaker.cs | Assets/Scripts/Editor/ |
| GameplayTilemapOverlay.cs | Assets/Scripts/Editor/ |

### 修改文件（10 个）
| 文件 | 主要变更 |
|------|---------|
| PrototypeWorldView.cs | 替换颜色 → GamePresentationConfig，移除死代码 |
| PrototypeHudView.cs | 引用 Prefab，移除动态 UI 代码 |
| PrototypeRuntimeBootstrapper.cs | 标记 Obsolete，移到 Editor |
| PrototypeCustomerFlowSystem.cs | 引用 CustomerFlowConfig |
| SecurityPatrolSystem.cs | 引用 SecurityPatrolConfig |
| TilemapOverlayController.cs | 增加假货反馈路径 |
| GridSystem.cs | 边界稳定性增强 |
| RouteSystem.cs | 标记 Gizmo 入口点 |
| ToolInventorySystem.cs | 自定义 Inspector 集成 |
| Game.unity | 场景层级重组，新 Tilemap 层，Prefab 替换 |

### 资产文件（14+ 个）
| 资产 | 路径 |
|------|------|
| GameplayTile x10 | Assets/Configs/Tilemap/ |
| CustomerFlowConfig | Assets/Configs/Customer/ |
| SecurityPatrolConfig | Assets/Configs/Security/ |
| GamePresentationConfig | Assets/Configs/Presentation/ |
| GameHUD.prefab | Assets/Prefabs/UI/ |

---

## 数据权威关系（最终状态）

```
Gameplay Logic Tilemap（唯一权威）
  ↓ TryReadCells()
TilemapGridBridge
  ↓
GridSystem._cellTypes / _tileStates（运行时）
  ↓
RouteSystem.BFS → 顾客路线
ToolResolutionSystem → 道具结算
PrototypeWorldView → 世界坐标转换

GridConfig（编辑器烘焙快照，回退数据源）
  ↓ 仅当 Bridge 不可用时
GridSystem.InitializeGrid()（回退路径）
```

**核心原则**：
- 配置在 ScriptableObject 中可调
- 引用在场景 Inspector 中可见
- 空间关系在 Scene 中可看
- 运行状态在 Play Mode 中只读观察
- 代码只负责执行，不再偷偷生成配置和对象
