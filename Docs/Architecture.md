# CIGAgamejam Runtime Architecture

## Goal

This project uses a lightweight event-driven Unity architecture for a puzzle and route-planning game about sabotaging a shop within a limited number of days.

The first runtime scaffold supports:

- Night planning and day simulation phases.
- A campaign day limit with failure when the shop is not bankrupted in time.
- Grid-based tool placement.
- Data-driven tools and traps.
- Extensible tool effects through effect handlers.
- Revenue reduction and bankruptcy victory.

## Core Loop

1. `GamePhaseSystem` enters `NightPlanning`.
2. Player places tools through `PlacementSystem`.
3. `GamePhaseSystem` enters `DaySimulation`.
4. `BossInterferenceSystem` disables one eligible tool.
5. Customer movement systems call `ToolResolutionSystem` when customers enter trigger cells or purchase.
6. `EconomySystem` applies revenue penalties from resolved tool effects.
7. `BankruptcySystem` ends the game with `ShopBankrupted` if revenue reaches the threshold.
8. If the day result is not a win, `CampaignProgressSystem` advances the day or triggers `TimeLimitFailed`.

## Ownership Rules

| Data | Owner | Access Pattern |
| --- | --- | --- |
| Current phase | `GamePhaseSystem` | `OnGamePhaseChanged` event or `CurrentPhase` getter |
| Current day and max days | `CampaignProgressSystem` | `OnDayStarted`, `OnDayEnded`, `OnDayLimitReached` |
| Grid cell types and occupancy | `GridSystem` | Public placement/query methods |
| Placed tool instances | `GridSystem` | Read-only list and trigger queries |
| Placement validation | `PlacementSystem` + `GridSystem` | `TryPlaceTool` |
| Tool effect execution | `ToolResolutionSystem` | Trigger methods and `IToolEffectHandler` |
| Revenue index | `EconomySystem` | `OnRevenueChanged` and public getters |
| Bankruptcy game end | `BankruptcySystem` | Listens to revenue changes |

## Adding a New Tool

Most new tools should be added without code:

1. Create a `ToolConfig` asset.
2. Set `Id`, `DisplayName`, `Category`, `AllowedCellTypes`, and `Footprint`.
3. Set `TriggerTiming` and `TriggerOffsets`.
4. Set `CanBeDisabledByBoss`.
5. Add one or more `ToolEffectDefinition` entries.

Write code only when no existing `ToolEffectType` can express the new behavior. In that case:

1. Add a new value to `ToolEffectType`.
2. Create a class implementing `IToolEffectHandler`.
3. Register it in `ToolResolutionSystem.RegisterDefaultHandlers`.
4. Add an EditMode test for the handler.

## Current Tool Mapping

| Design Tool | Suggested Config |
| --- | --- |
| Smith Agent | `ToolCategory.Tax`, `OnDayStart`, `ModifyPurchaseCost` |
| Boiling Water | `ToolCategory.Destroy`, `OnManualResolve`, `DestroyObject`, not disableable |
| Clown Box | `ToolCategory.Scare`, `OnCustomerPassFrontCell`, `ScareCustomerAway` |
| Fake Goods | `ToolCategory.FakeGoods`, `OnCustomerPurchase`, `ReplaceGoodsWithFake` |
| Envelope | `ToolCategory.Bribe`, `OnCustomerEnterCell`, `BribeSecurity`, use limit 1 |

## Verification Scope

The first EditMode tests cover:

- Grid occupancy rejection.
- Campaign day-limit config validation.
- Tool effects being executable through standalone handlers.
- `ToolConfig` carrying multiple effects for future tool combinations.

Unity CLI was not available in the local shell during creation, so run EditMode tests from the Unity Test Runner after opening the project.
