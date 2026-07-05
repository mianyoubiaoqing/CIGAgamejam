# 保安随机巡逻路径系统

## 需求

1. 每晚随机生成保安巡逻路径
2. 保安只能在有 ground tile 的地面上移动
3. 夜晚规划阶段显示巡逻路径
4. 白天营业时路径消失，第二夜重新生成

---

## Step 1：随机路径生成算法

**文件**：`Assets/Scripts/Systems/SecurityPatrolSystem.cs`

### 1.1 替换 `_patrolPath` 为运行时生成

```csharp
// 删除旧的固定路径字段：
// [SerializeField] private Vector2Int[] _patrolPath = { ... };

// 新增参数：
[SerializeField, Min(3)] private int _minPathLength = 4;
[SerializeField, Min(3)] private int _maxPathLength = 8;
[SerializeField] private TilemapGridBridge _tilemapBridge;  // 用于 ground 检测

// 运行时数据：
private List<GridPosition> _patrolPath;
private int _patrolIndex;
private GridPosition _currentPosition;
```

### 1.2 生成算法

```csharp
public void GenerateRandomPatrolPath()
{
    if (_gridSystem == null) return;

    // 收集所有有 ground tile 的格作为候选
    List<GridPosition> candidates = new();
    for (int y = _gridSystem.MinY; y < _gridSystem.MaxYExclusive; y++)
    for (int x = _gridSystem.MinX; x < _gridSystem.MaxXExclusive; x++)
    {
        var pos = new GridPosition(x, y);
        if (_gridSystem.IsRouteWalkable(pos))
            candidates.Add(pos);
    }

    if (candidates.Count == 0) return;

    // 随机起点
    int targetLength = Random.Range(_minPathLength, _maxPathLength + 1);
    _patrolPath = new List<GridPosition>(targetLength);

    GridPosition current = candidates[Random.Range(0, candidates.Count)];
    _patrolPath.Add(current);

    var visited = new HashSet<GridPosition> { current };

    for (int i = 1; i < targetLength; i++)
    {
        // 找当前格的所有可行走邻居，且未访问过
        List<GridPosition> walkableNeighbors = new();
        foreach (GridPosition neighbor in GetNeighbors(current))
        {
            if (!visited.Contains(neighbor) && _gridSystem.IsRouteWalkable(neighbor))
                walkableNeighbors.Add(neighbor);
        }

        if (walkableNeighbors.Count == 0)
            break;  // 走到死胡同，路径到此结束

        // 随机选一个
        current = walkableNeighbors[Random.Range(0, walkableNeighbors.Count)];
        _patrolPath.Add(current);
        visited.Add(current);
    }
}
```

```csharp
private IEnumerable<GridPosition> GetNeighbors(GridPosition pos)
{
    yield return new GridPosition(pos.X + 1, pos.Y);
    yield return new GridPosition(pos.X - 1, pos.Y);
    yield return new GridPosition(pos.X, pos.Y + 1);
    yield return new GridPosition(pos.X, pos.Y - 1);
}
```

> 算法保证：路径连续、不重复走同一格、仅在 ground tile 上。当走到死胡同时自然截断。

---

## Step 2：生命周期管理

**文件**：`Assets/Scripts/Systems/SecurityPatrolSystem.cs`

### 2.1 订阅游戏阶段切换

```csharp
private void OnEnable()
{
    EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
}

private void OnDestroy()
{
    EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
}
```

```csharp
private void HandleGamePhaseChanged(OnGamePhaseChanged e)
{
    if (e.NewPhase == GamePhase.NightPlanning)
    {
        // 每夜重新生成
        GenerateRandomPatrolPath();
        ResetPosition();
        PublishPatrolPathChanged();
        BeginNightPatrol();
    }

    if (e.NewPhase == GamePhase.DaySimulation)
    {
        // 白天路径标记清除（由视图层处理）
        PublishPatrolPathCleared();
    }
}
```

### 2.2 移除 NightTurnSystem 的初始化调用

当前 `NightTurnSystem` 第 48 行调用了 `_securityPatrolSystem?.BeginNightPatrol()`——这个保留（触发移动），但 `BeginNightPatrol` 不再包含路径生成逻辑，只负责初始位置重置和工具检测。

---

## Step 3：新增事件

**文件**：`Assets/Scripts/Events/PrototypeEvents.cs`

```csharp
public readonly struct OnSecurityPatrolPathChanged
{
    public readonly IReadOnlyList<GridPosition> Path;

    public OnSecurityPatrolPathChanged(IReadOnlyList<GridPosition> path)
    {
        Path = path;
    }
}

public readonly struct OnSecurityPatrolPathCleared
{
    // 空事件，仅作通知
}
```

---

## Step 4：路径可视化

**文件**：`Assets/Scripts/Systems/PrototypeWorldView.cs`

### 4.1 新增字段和字典

```csharp
[Header("Security Patrol Path")]
[SerializeField, Min(0.02f)] private float _patrolPathMarkerSizeRatio = 0.06f;
[SerializeField] private Color _patrolPathColor = new(0.5f, 0.5f, 0.5f, 0.35f);

private readonly List<GameObject> _patrolPathMarkers = new();
```

### 4.2 订阅新事件

```csharp
// OnEnable 增加：
EventBus<OnSecurityPatrolPathChanged>.Subscribe(HandlePatrolPathChanged);
EventBus<OnSecurityPatrolPathCleared>.Subscribe(HandlePatrolPathCleared);

// OnDestroy 增加：
EventBus<OnSecurityPatrolPathChanged>.Unsubscribe(HandlePatrolPathChanged);
EventBus<OnSecurityPatrolPathCleared>.Unsubscribe(HandlePatrolPathCleared);
```

### 4.3 处理路径显示/隐藏

```csharp
private void HandlePatrolPathChanged(OnSecurityPatrolPathChanged e)
{
    // 清除旧标记
    ClearPatrolPathMarkers();

    if (e.Path == null || e.Path.Count < 2) return;

    float markerSize = CellSize * _patrolPathMarkerSizeRatio;

    // 画路径节点
    for (int i = 0; i < e.Path.Count; i++)
    {
        GameObject marker = CreateSquare(
            $"Patrol {i}",
            GridToWorld(e.Path[i]) + new Vector3(0f, 0f, -0.07f),
            markerSize,
            _patrolPathColor,
            6);
        _patrolPathMarkers.Add(marker);
    }

    // 画路径连线（小点连线）
    // (可选：用更亮的颜色画每个 step 的连线)
}

private void HandlePatrolPathCleared(OnSecurityPatrolPathCleared e)
{
    ClearPatrolPathMarkers();
}

private void ClearPatrolPathMarkers()
{
    for (int i = 0; i < _patrolPathMarkers.Count; i++)
        if (_patrolPathMarkers[i] != null)
            Destroy(_patrolPathMarkers[i]);
    _patrolPathMarkers.Clear();
}
```

> 样式参考 `RebuildRouteMarkers()`（第 223-238 行），使用同样的 `CreateSquare` 白色精灵绘制方形标记，绿色改为灰白色表示保安路线。

---

## Step 5：场景引用绑定

在场景中：
- `SecurityPatrolSystem` 新增 `_tilemapBridge` 引用 → 指向已有 `TilemapGridBridge` 实例
- `PrototypeWorldView` 新增 `_patrolPathMarkerSizeRatio` 和 `_patrolPathColor`

---

## 涉及文件

| 文件 | 变更 |
|------|------|
| `Assets/Scripts/Systems/SecurityPatrolSystem.cs` | 移除固定 `_patrolPath`；新增 `GenerateRandomPatrolPath()`、`GetNeighbors()`、`HandleGamePhaseChanged()`；`BeginNightPatrol()` 简化 |
| `Assets/Scripts/Events/PrototypeEvents.cs` | 新增 `OnSecurityPatrolPathChanged` 和 `OnSecurityPatrolPathCleared` |
| `Assets/Scripts/Systems/PrototypeWorldView.cs` | 新增路径标记渲染/清除；订阅新事件 |
| `Assets/Scripts/Systems/NightTurnSystem.cs` | 保留 `BeginNightPatrol()` 调用（不受影响） |
| `Assets/Scenes/Game.unity` | 绑定 SecurityPatrolSystem._tilemapBridge 引用 |

## 未改动

- `GridSystem.cs` — 无影响
- `RouteSystem.cs` — 无影响
- `PrototypeCustomerFlowSystem.cs` — 无影响

## 验证

### 编辑器

1. 进入 NightPlanning 阶段 → 保安出现在随机路径起点，灰色路径标记从起点延伸到终点
2. 每次进入 NightPlanning → 路径不同（重新生成）
3. 保安沿路径步进移动（每回合 `AdvancePatrolTurn()`）
4. 切换到 DaySimulation 阶段 → 路径标记消失

### 运行时

1. 第一夜：路径 A，第二夜：路径 B（不同，因为 `Random` 种子不同帧）
2. 保安不会走到没有 ground tile 的格子上（墙、货架、空白）
3. 路径最短 3 格，最长 8 格
4. 路径不重复走同一格（visited 集合约束）

## 边界情况

- **候选格不够**：`candidates.Count == 0` 时直接 return，`_patrolPath` 保持空，保安不移动
- **走到死胡同**：`walkableNeighbors.Count == 0` 时 break，路径取已有长度
- **地图很小**：`_minPathLength > 实际可达格数` 时取实际长度
- **路径只有 1 格**：保安原地不动，视野系统照常工作
