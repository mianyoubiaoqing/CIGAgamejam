# 视觉 Tilemap + 隐藏逻辑 Tilemap 重构计划

## 问题总结

目前存在两套彼此独立的数据系统：
- **场景中的 Unity Tilemap**（ground/wall/shelf）—— 负责画面
- **GridConfig ScriptableObject / GridSystem 运行时字典** —— 负责玩法逻辑
- **PrototypeWorldView 的 `_origin` + `_cellSize`** —— 独立坐标计算

三者互不关联，导致：
- 美术修改 Tilemap 后逻辑地图不会更新
- 坐标偏移（Tilemap Grid 位置 `-0.87,0` vs 手动计算 `-3.5,-2.5`）
- 销毁货架后逻辑变化但画面不变
- 同一地图需要维护两遍

---

## 方案：新增隐藏 Gameplay Logic Tilemap 作为数据唯一权威

### 新增文件（4个）

| 文件 | 用途 |
|------|------|
| `Assets/Scripts/Tilemap/GameplayTile.cs` | GameplayTile ScriptableObject，记录 GridCellType / Walkable / CanBeDestroyed |
| `Assets/Scripts/Tilemap/TilemapGridBridge.cs` | 读取 Gameplay Logic Tilemap → 填充 GridSystem |
| `Assets/Scripts/Tilemap/TilemapOverlayController.cs` | Tool/State Overlay Tilemap 的渲染控制 |
| `Assets/Scripts/Editor/TilemapGridBaker.cs` | 编辑器工具：Bake / Validate / Preview Route |

### 修改文件（3个）

| 文件 | 变更 |
|------|------|
| `Assets/Scripts/Systems/GridSystem.cs` | 新增 `InitializeFromTilemap()` 路径 |
| `Assets/Scripts/Systems/PrototypeWorldView.cs` | 替换 `_origin`/`_cellSize` → Tilemap API |
| `Assets/Scripts/Systems/PrototypeRuntimeBootstrapper.cs` | 移除 `_cellSize`/`_origin` 硬编码 |

### 场景修改

`Shop Tilemaps` 下新增 3 个子 Tilemap：
- `Gameplay Logic`（Renderer 关闭，数据层）
- `Tool Overlay`（SortingOrder +10，道具显示）
- `State Overlay`（SortingOrder +20，销毁/假货覆盖）

---

## 实施步骤

### Step 1 — GameplayTile ScriptableObject

```csharp
[CreateAssetMenu(menuName = "CIGAgamejam/Tilemap/GameplayTile")]
public class GameplayTile : TileBase {
    public GridCellType cellType;
    public bool walkable;
    public bool canBeDestroyed;
}
```

创建 10 个 `.asset` 实例（Floor, Wall, Warehouse, Checkout, Restroom, Entrance, Exit, Security, FortuneTree, Blocked）。

### Step 2 — 场景铺设 Gameplay Logic Tilemap

在 `Shop Tilemaps`（Grid 位置 -0.87,0，CellSize 1,1,1）下新增子对象"Gameplay Logic"：
- Tilemap + TilemapRenderer（禁用）
- 参照现有 `Prototype_GridConfig.asset`（15x11，原点 -6,0）的数据手动铺设 GameplayTile
- 坐标规则：`Tilemap cell (x, y)` = `GridPosition (x, y)`，无需偏移

### Step 3 — TilemapGridBridge

扫描 `_gameplayTilemap.cellBounds`，收集所有有 GameplayTile 的格子。
暴露接口：
- `GetAllTiles()` → `Dictionary<Vector3Int, GameplayTile>`
- `GetCellType(Vector3Int)` → `GridCellType`
- `CellToWorld(GridPosition)` → `_tilemap.GetCellCenterWorld()`
- `WorldToCell(Vector3)` → `GridPosition`
- `CellSize` → Grid 组件的 `cellSize.x`

### Step 4 — GridSystem 初始化

新增 `InitializeFromTilemap(TilemapGridBridge)`：
- 清空 `_cellTypes` / `_tileStates`
- 遍历 Tilemap 有 tile 的格子
- 填充 `_cellTypes[pos] = tile.cellType`
- 创建对等 `PuzzleTileState`

边界改为从桥接器获取 `MinX/MinY/MaxX/Y`。保留 `_config` 字段但标记 `[Obsolete]`；运行时优先走 Tilemap。

### Step 5 — PrototypeWorldView 坐标统一

替换：
```csharp
// 旧的独立计算
_origin + (position + 0.5f) * _cellSize

// 新的 Tilemap API
_tilemapBridge.CellToWorld(position)

// 旧的世界→网格
FloorToInt((worldPos - _origin.x) / _cellSize)

// 新的 Tilemap API
_tilemapBridge.WorldToCell(worldPos) → GridPosition
```

移除 PrototypeWorldView 中的 `_cellSize` 和 `_origin` 字段，改为引用 `_tilemapBridge`。

### Step 6 — BuildGrid() 修复

当前 `BuildGrid()` 从 `y=0; y < Height` 开始，当 `GridConfig._origin` 不为零时错位。
改为 `y = MinY; y < MaxYExclusive`。

### Step 7 — Tool/State Overlay

`TilemapOverlayController` 订阅事件：
- `OnToolPlaced` → Tool Overlay 对应格设置颜色/贴图
- `OnToolDisabled` → 变灰
- `OnWorldObjectDestroyed` → State Overlay 显示烧毁覆盖
- `ReplacementTool = FakeGoods` 检测 → 显示假货覆盖

不依赖独立 GameObject 字典。

### Step 8 — 编辑器工具

TilemapGridBaker 提供：
- `[MenuItem] Bake GridConfig` — 从 Gameplay Logic Tilemap 读取并生成 .asset
- `[MenuItem] Validate Map` — 检查完整性、BFS 可达性、逻辑层与美术层一致性
- `[MenuItem] Preview Route` — 高亮显示 BFS 路径
- `[MenuItem] Clear Preview` — 清除标记

### Step 9 — 数据迁移

1. 参照 `Prototype_GridConfig.asset` 在 Gameplay Logic Tilemap 上铺设 GameplayTile
2. Bake 生成新的 GridConfig 快照
3. 修复场景中 GridSystem._config 的引用（当前为空/损坏）

### Step 10 — 清理

移除 `PrototypeWorldView` 中的死代码：`_origin`、`_cellSize`、`BuildGrid()`、`CreateSquare()`/`CreateRect()`（若未在别处引用）、`_toolMarkers`/`_destroyedObjectMarkers` 等替代字典。移除 `PrototypeRuntimeBootstrapper` 中的 `_cellSize`/`_origin` 设置。

---

## 验证方法

1. **编辑器**：Validate 检查完整性 → Bake 生成 → Preview Route
2. **运行时**：顾客正常沿路线移动；开水销毁显示烧毁覆盖；假货显示假货覆盖；`WorldToCell` 点击检测正确
3. **回退**：`GridSystem.Awake()` 检测 `_tilemapBridge` 为 null 时走旧的 `InitializeGrid()`
