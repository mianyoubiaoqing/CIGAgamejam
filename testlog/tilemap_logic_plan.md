# 顾客路线拐角随机转向实施计划

## 需求

当前所有顾客走同一条共享路线（Entrance → Checkout → Exit 的 BFS 路径）。需要在路线拐角处增加随机分支，使顾客有不同行走路径。

用户确认：
- 替代路径从拐角后独立走到出口（不汇入主路线）
- 每个拐角 ~50% 概率触发
- 任何方向变化（左转/右转/掉头）都算拐角

---

## Step 1：RouteVariant 数据结构

**文件**：`Assets/Scripts/Systems/RouteSystem.cs`

新增：

```csharp
public struct RouteVariant
{
    public int ForkIndex;              // 主路线中的拐角索引
    public List<GridPosition> Tail;    // 从拐角到出口的替代路径
}
```

RouteSystem 内部新增字段：

```csharp
private List<RouteVariant> _routeVariants = new();

public IReadOnlyList<RouteVariant> AvailableVariants => _routeVariants;
```

---

## Step 2：拐角检测 + 替代路线生成

在 `RebuildCustomerRoute()` 末尾（第 57 行 `return true` 之前）调用 `GenerateRouteVariants()`：

```csharp
private void GenerateRouteVariants()
{
    _routeVariants.Clear();
    if (_customerRoute.Count < 3) return;

    for (int i = 1; i < _customerRoute.Count - 1; i++)
    {
        // 检测方向变化
        Vector2Int prevDir = _customerRoute[i].ToVector2Int() - _customerRoute[i-1].ToVector2Int();
        Vector2Int nextDir = _customerRoute[i+1].ToVector2Int() - _customerRoute[i].ToVector2Int();
        if (prevDir == nextDir) continue;

        // 尝试每个可行走邻居（除了主路线的下一个格子）
        foreach (GridPosition neighbor in GetNeighbors(_customerRoute[i]))
        {
            if (neighbor.Equals(_customerRoute[i+1])) continue;
            if (!_gridSystem.IsRouteWalkable(neighbor)) continue;

            List<GridPosition> tail = TryBuildAlternativeTail(neighbor, _customerRoute[i]);
            if (tail != null && tail.Count > 1)
            {
                _routeVariants.Add(new RouteVariant {
                    ForkIndex = i,
                    Tail = tail
                });
                break; // 每个拐角最多一个替代
            }
        }
    }
}
```

新增方法（注意：`GetNeighbors` 和 `TryFindRoute` 已存在）：

```csharp
private List<GridPosition> TryBuildAlternativeTail(GridPosition start, GridPosition blockedCell)
{
    // 判断拐角位置在 Checkout 之前还是之后
    int checkoutIndex = _customerRoute.IndexOf(Checkout);

    // 找到拐角在主路线中的位置
    int forkIndex = _customerRoute.IndexOf(blockedCell);

    var blocked = new List<GridPosition> { blockedCell };

    if (forkIndex <= checkoutIndex)
    {
        // 拐角在 Checkout 之前或就在 Checkout：BFS 到 Checkout
        if (TryFindRoute(start, Checkout, blocked, out var toCheckout) && toCheckout.Count > 0)
        {
            var tail = new List<GridPosition>(toCheckout);
            // 接上 Checkout → Exit 段（跳过 Checkout 避免重复）
            for (int j = checkoutIndex + 1; j < _customerRoute.Count; j++)
                tail.Add(_customerRoute[j]);
            return tail;
        }
    }
    else
    {
        // 拐角在 Checkout 之后：BFS 直接到 Exit
        if (TryFindRoute(start, Exit, blocked, out var toExit) && toExit.Count > 0)
            return toExit;
    }

    return null;
}
```

注意：第 99 行 `TryFindRoute` 的签名是 `TryFindRoute(start, goal, blockedCells, out route)`，第三个参数是 `ICollection<GridPosition>`。传 `List<GridPosition>` 没问题。

---

## Step 3：顾客个性化路线

**文件**：`Assets/Scripts/Systems/PrototypeCustomerFlowSystem.cs`

### 3.1 MovingCustomer 新增 PersonalRoute

第 313-335 行的 `MovingCustomer` struct 增加字段：

```csharp
private struct MovingCustomer
{
    public CustomerContext Context;
    public List<GridPosition> PersonalRoute;  // ← 新增
    public int RouteIndex;
    public float Progress;
    public bool HasPurchased;
    // ... escape 字段不变

    public MovingCustomer(CustomerContext context, List<GridPosition> personalRoute)
    {
        Context = context;
        PersonalRoute = personalRoute;
        RouteIndex = 0;
        Progress = 0f;
        HasPurchased = false;
        EscapeStarted = false;
        EscapeRouteIndex = 0;
        EscapeProgress = 0f;
        EscapeRoute = null;
    }
}
```

### 3.2 SpawnCustomer 使用个性化路线

第 95-112 行的 `SpawnCustomer()`：

```csharp
private void SpawnCustomer()
{
    GridPosition start = _routeSystem.CustomerRoute[0];
    var customer = new CustomerContext(_nextCustomerId++, start);

    List<GridPosition> personalRoute = BuildPersonalRoute();

    // ... pre-existing scare quota check ...

    _activeCustomers.Add(new MovingCustomer(customer, personalRoute));
    _spawnedToday++;

    // OnPrototypeCustomerMoved 事件发布用 personalRoute[0]
    GridPosition spawnPos = personalRoute[0];
    EventBus<OnPrototypeCustomerMoved>.Publish(
        new OnPrototypeCustomerMoved(customer.CustomerId, spawnPos, spawnPos.X, spawnPos.Y, customer.State));
}
```

### 3.3 BuildPersonalRoute 方法

新增方法，从入口走到出口，在每个有替代方案的拐角掷骰 50%：

```csharp
private List<GridPosition> BuildPersonalRoute()
{
    var mainRoute = _routeSystem.CustomerRoute;
    var variants = _routeSystem.AvailableVariants;

    if (mainRoute.Count == 0) return new List<GridPosition>();

    // 遍历主路线每个格子，检查是否有替代方案
    for (int i = 1; i < mainRoute.Count - 1; i++)
    {
        // 检测方向变化
        GridPosition prev = mainRoute[i - 1];
        GridPosition curr = mainRoute[i];
        GridPosition next = mainRoute[i + 1];
        Vector2Int prevDir = new(curr.X - prev.X, curr.Y - prev.Y);
        Vector2Int nextDir = new(next.X - curr.X, next.Y - curr.Y);
        if (prevDir == nextDir) continue;

        // 查找是否有对应拐角的替代方案
        RouteVariant? matchingVariant = null;
        for (int v = 0; v < variants.Count; v++)
        {
            if (variants[v].ForkIndex == i)
            {
                matchingVariant = variants[v];
                break;
            }
        }

        if (matchingVariant == null || matchingVariant.Value.Tail == null || matchingVariant.Value.Tail.Count == 0)
            continue;

        // 50% 概率走替代路线
        if (Random.value < 0.5f)
        {
            var route = new List<GridPosition>(i + 1 + matchingVariant.Value.Tail.Count);
            // 走主路线到拐角
            for (int j = 0; j <= i; j++)
                route.Add(mainRoute[j]);
            // 接上替代尾段（跳过第 0 项避免拐角格重复）
            for (int j = 1; j < matchingVariant.Value.Tail.Count; j++)
                route.Add(matchingVariant.Value.Tail[j]);
            return route;
        }
    }

    // 没走替代路线 → 完整主路线
    return new List<GridPosition>(mainRoute);
}
```

### 3.4 AdvanceCustomer 使用 PersonalRoute

第 123 行：

```csharp
// 原：IReadOnlyList<GridPosition> route = _routeSystem.CustomerRoute;
IReadOnlyList<GridPosition> route = moving.PersonalRoute;
```

同样检查 `route.Count == 0` 的地方（第 43 行还要保留对 `_routeSystem.CustomerRoute.Count == 0` 的检查，确保主线存在）。

第 146-148 行的 `OnPrototypeCustomerMoved` 发布——需要检查 `moving.PersonalRoute` 的边界：

```csharp
int nextRouteIndex = Mathf.Min(moving.RouteIndex + 1, route.Count - 1);
```

---

## Step 4：路线重建时重新生成替代方案

`RebuildCustomerRoute()` 中需要在路线构建完成后调用 `GenerateRouteVariants()`：

```csharp
public bool RebuildCustomerRoute()
{
    // ... existing logic ...
    _customerRoute.AddRange(toCheckout);
    for (int i = 1; i < toExit.Count; i++)
        _customerRoute.Add(toExit[i]);

    GenerateRouteVariants();  // ← 新增

    EventBus<OnRouteChanged>.Publish(new OnRouteChanged(true, _customerRoute.Count));
    return true;
}
```

同样在 `TryBuildRouteOverride()` 成功后也要生成：

```csharp
private bool TryBuildRouteOverride()
{
    // ... existing logic ...
    if (_customerRoute.Count > 0)
    {
        GenerateRouteVariants();  // ← 新增
        return true;
    }
    return false;
}
```

---

## 涉及文件清单

| 文件 | 变更内容 |
|------|---------|
| `Assets/Scripts/Systems/RouteSystem.cs` | 新增 `_routeVariants` / `AvailableVariants` / `GenerateRouteVariants()` / `TryBuildAlternativeTail()` / `RouteVariant` struct；`RebuildCustomerRoute()` 末尾和 `TryBuildRouteOverride()` 成功时调用生成 |
| `Assets/Scripts/Systems/PrototypeCustomerFlowSystem.cs` | `MovingCustomer` 增加 `PersonalRoute` 字段和构造函数参数；`SpawnCustomer()` 调用 `BuildPersonalRoute()`；新增 `BuildPersonalRoute()` 方法；`AdvanceCustomer()` 使用 `moving.PersonalRoute` 替代 `_routeSystem.CustomerRoute` |

## 未改动文件

- `PrototypeWorldView.cs`（路线标记沿用主路线）
- `GridSystem.cs`
- `SecurityPatrolSystem.cs`
- `ToolResolutionSystem.cs`
- `GamePhaseSystem.cs`
- `EconomySystem.cs`
