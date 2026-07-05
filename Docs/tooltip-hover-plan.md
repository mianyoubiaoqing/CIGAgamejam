# 道具图标悬停提示框功能设计

## 概述

为 HUD 底部道具按钮添加鼠标悬停提示框（Tooltip），显示道具名称和简介。无第三方库，全部基于 Unity uGUI 和现有架构实现。

---

## 涉及文件

| 文件 | 改动类型 |
|---|---|
| `Assets/Scripts/Config/ToolConfig.cs` | 新增 `_description` 字段 + public property |
| `Assets/Scripts/Systems/PrototypeRuntimeBootstrapper.cs` | 补充 4 个道具的英文简介数据 |
| `Assets/Scripts/Systems/PrototypeHudView.cs` | 核心：创建提示框 UI + 悬停事件逻辑 |

---

## 改动详情

### 1. ToolConfig.cs — 添加简介字段

在 `[Header("Identity")]` 区域，`_displayName` 之后增加：

```csharp
[SerializeField, TextArea] private string _description = string.Empty;
```

添加公开属性：

```csharp
public string Description => _description;
```

### 2. PrototypeRuntimeBootstrapper.cs — 填充简介

修改 `CreateDefaultTools()` 中的 4 个工具，添加英文简介：

| ID | 中文名 | 英文简介 |
|---|---|---|
| `clown_box` | 小丑盒 | "Spooks one customer into leaving. Place near aisles for maximum effect." |
| `fake_goods` | 假货 | "Replaces a customer's purchase with defective goods. Works in warehouse areas." |
| `bribe_envelope` | 信封 | "Bribes a security guard to look the other way. Place along patrol routes." |
| `boiling_water` | 开水 | "Destroys a nearby object on command. Manual trigger — use with precision." |

修改 `CreateTool()` 私有方法签名，添加 `string description` 参数，通过 `SetField` 设置。

### 3. PrototypeHudView.cs — 提示框系统

#### 3a. 新增提示框 UI 元素

- 新增字段：`_tooltipRoot` (RectTransform) 和 `_tooltipText` (Text)
- 在 `BuildHud()` 末尾（或 canvas 直接子级）创建提示框面板：
  - 背景：深色半透明面板，带 Outline 边框
  - 文字：显示 "Name\nDescription"
  - 默认 `SetActive(false)`

#### 3b. 在工具按钮上添加悬停事件

在 `CreateToolButton()` 中，为每个按钮 GameObject 添加 `EventTrigger` 组件：

- **PointerEnter**: 获取该按钮的 tool config，将提示框显示在按钮上方，内容设为 DisplayName + Description
- **PointerExit**: 隐藏提示框

#### 3c. 定位逻辑

提示框 pivot 为 `(0.5f, 0f)`（底部居中），从底部中心向上展开。  
PointerEnter 时通过 `GetWorldCorners` + canvas 坐标转换定位在按钮正上方 10px 处，并进行 Canvas 边界 Clamp。  
提示框尺寸 240×100，Text 使用 `ContentSizeFitter` 风格的贴边 offset 自适应。

#### 3d. 关键注意事项

- 提示框本身的 Image/Text 的 `raycastTarget = false`，不干扰按钮点击
- EventTrigger 挂载在按钮的根 GameObject 上（即 `buttonTransform`）

---

## UI 样式

| 元素 | 值 |
|---|---|
| 提示框背景色 | `Color(0.08f, 0.08f, 0.08f, 0.95f)` |
| 名称文字 | fontSize 15, Bold, `Color(0.98f, 0.92f, 0.78f)` |
| 描述文字 | fontSize 13, Normal, `Color.white` |
| 内边距 | 上下 8px，左右 10px |

---

## 验证方法

1. 在 Unity Editor 中打开 Game 场景
2. 进入 Play Mode，点击开始游戏进入夜晚部署阶段
3. 鼠标悬停到底部的任意道具按钮上
4. ✅ 按钮上方弹出提示框，显示道具英文名称和简介
5. 鼠标移出按钮区域
6. ✅ 提示框消失
7. 对全部 4 个道具重复验证
