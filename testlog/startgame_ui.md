# 开始游戏 UI

## 需求

场景加载后显示开始界面，玩家点击"开始游戏"后才进入游戏。不需要主菜单或设置界面。

---

## 当前问题

`GamePhaseSystem._beginOnStart = true`，场景加载后立即 `BeginGame()` → NightPlanning，玩家没有准备时间。

---

## 方案

### Step 1：GamePhaseSystem 关闭自动开始

**文件**：`Assets/Scripts/Systems/GamePhaseSystem.cs`

```csharp
// 第 8 行
[SerializeField] private bool _beginOnStart = true;   // 改为 false
```

改为 `false` 后，场景加载后停留在 `GamePhase.None`，所有系统已初始化但不开始游戏。

### Step 2：开始界面（场景中预先放置）

在场景的 `Prototype HUD` Canvas 下直接创建 UI 层级，不额外加 Canvas。

| 层级 | 内容 |
|------|------|
| Start Screen (RectTransform) | 全屏覆盖层，初始 active: true |
| ├─ Background (Image) | 深色半透明背景 `(0.03, 0.03, 0.04, 0.95)` |
| ├─ Title Text (Text) | 游戏标题 |
| ├─ Subtitle (Text) | 副标题或操作提示 |
| └─ Start Button (Button) | "开始游戏" 按钮 |

布局参考（锚点全屏）：

```
┌─────────────────────────────────┐
│  (anchor 0,0 → 1,1)             │
│                                 │
│       游戏标题（居中大号）          │
│                                 │
│         [ 开始游戏 ]             │
│                                 │
│    操作提示（底部小字）            │
└─────────────────────────────────┘
```

### Step 3：StartGameUI 组件

**新增文件**：`Assets/Scripts/UI/StartGameUI.cs`

```csharp
namespace CIGAgamejam
{
    public sealed class StartGameUI : MonoBehaviour
    {
        [SerializeField] private GamePhaseSystem _gamePhaseSystem;
        [SerializeField] private Button _startButton;

        private void Awake()
        {
            _startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            _gamePhaseSystem?.BeginGame();
            gameObject.SetActive(false);  // 隐藏开始界面
        }
    }
}
```

### Step 4：HUD 初始隐藏

场景中 `Prototype HUD` Canvas 下的游戏 HUD 面板（Top Bar、Bottom Bar、Log Panel）初始 `active: false`。

`PrototypeHudView` 新增：

```csharp
private bool _hasGameStarted;

private void HandleGamePhaseChanged(OnGamePhaseChanged e)
{
    if (e.NewPhase == GamePhase.NightPlanning && !_hasGameStarted)
    {
        _hasGameStarted = true;
        // 激活 HUD 面板
        ShowHUD();
    }
    RefreshAll();
}

private void ShowHUD()
{
    // 激活预先放置的 HUD 面板
    // 这些引用可以通过 SerializeField 绑定，也可以按名称查找
    Transform canvas = transform.Find("Prototype HUD");
    canvas?.Find("Top Bar")?.gameObject.SetActive(true);
    canvas?.Find("Bottom Bar")?.gameObject.SetActive(true);
    canvas?.Find("Log Panel")?.gameObject.SetActive(true);
}
```

> 或者场景中 `Prototype HUD` Canvas 下放一个 HUD Root 容器，初始 inactive，点击开始后由 StartGameUI 激活。

### Step 5：场景绑定

1. `GamePhaseSystem._beginOnStart` 去掉勾选
2. 在 `Prototype HUD` Canvas 下创建 Start Screen UI 层级
3. 拖入 `StartGameUI` 组件，绑定 `_gamePhaseSystem` 和 `_startButton`
4. HUD 面板（Top Bar / Bottom Bar / Log）初始 `active: false`

---

## 涉及文件

| 文件 | 变更 |
|------|------|
| `Assets/Scripts/Systems/GamePhaseSystem.cs` | `_beginOnStart` 默认值改为 `false` |
| `Assets/Scripts/Systems/PrototypeHudView.cs` | 新增 `_hasGameStarted` + 初始隐藏逻辑 |
| `Assets/Scripts/UI/StartGameUI.cs` | **新增** — 开始界面按钮处理 |
| `Assets/Scenes/Game.unity` | 创建 Start Screen UI + 绑定引用 |

## 验证

1. 打开场景 → 只看到开始界面，HUD 不可见
2. 点击"开始游戏" → 界面消失，HUD 出现，进入 NightPlanning
3. 后续功能不变
