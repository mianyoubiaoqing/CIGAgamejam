# 顾客只能在有 ground 瓦片的地方走

## 需求

用 `ground` Tilemap 层作为通行依据：格子上有 ground tile → 可通行；没有 → 不可通行。替代当前 `IsRouteWalkable` 的 cell type 硬编码。

---

## Step 1：TilemapGridBridge 增加 ground 引用

**文件**：`Assets/Scripts/Tilemap/TilemapGridBridge.cs`

### 1.1 新增字段

```csharp
[SerializeField] private Tilemap _groundTilemap;
```

### 1.2 新增方法

```csharp
public bool HasGroundTile(GridPosition position)
{
    if (_groundTilemap == null) return false;
    return _groundTilemap.HasTile(new Vector3Int(position.X, position.Y, 0));
}
```

`HasTile` 底层是 O(1) 哈希查找，性能安全。

---

## Step 2：GridSystem.IsRouteWalkable 改用 ground

**文件**：`Assets/Scripts/Systems/GridSystem.cs`

第 102-112 行原代码：

```csharp
public bool IsRouteWalkable(GridPosition position)
{
    if (!IsInBounds(position)) return false;
    if (!TryGetCellType(position, out GridCellType cellType)) return false;

    return cellType != GridCellType.Wall
        && cellType != GridCellType.Warehouse
        && cellType != GridCellType.Restroom
        && cellType != GridCellType.FortuneTree
        && cellType != GridCellType.Blocked;
}
```

改为：

```csharp
public bool IsRouteWalkable(GridPosition position)
{
    if (!IsInBounds(position)) return false;

    // 优先使用 ground tilemap 判断可通行
    if (_tilemapBridge != null)
        return _tilemapBridge.HasGroundTile(position);

    // 回退：基于 cell type 的硬编码判断
    if (!TryGetCellType(position, out GridCellType cellType)) return false;
    return cellType != GridCellType.Wall
        && cellType != GridCellType.Warehouse
        && cellType != GridCellType.Restroom
        && cellType != GridCellType.FortuneTree
        && cellType != GridCellType.Blocked;
}
```

---

## Step 3：场景绑定

在场景中将 `ground` Tilemap 拖入 `TilemapGridBridge._groundTilemap`。

---

## 涉及文件

| 文件 | 改动 |
|------|------|
| `Assets/Scripts/Tilemap/TilemapGridBridge.cs` | + `_groundTilemap` 字段，+ `HasGroundTile()` 方法 |
| `Assets/Scripts/Systems/GridSystem.cs` | `IsRouteWalkable` 改用 ground 判断，保留 fallback |
| `Assets/Scenes/Game.unity` | 绑定 `_groundTilemap` 引用 |

## 验证

1. 运行游戏，顾客正常沿 BFS 路线走
2. 在 Scene 中挖掉某格的 ground tile → 顾客不再走那格，BFS 自动绕路
3. 有 wall/shelf tile 但无 ground tile 的位置 → 顾客不可通行
4. `_tilemapBridge == null` 时走旧 cell type 逻辑，行为不变
