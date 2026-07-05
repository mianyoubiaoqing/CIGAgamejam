using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CIGAgamejam
{
    public static class PrototypeRuntimeBootstrapper
    {
        // Kept as a reference for prototype sandbox setup, but no longer starts automatically.
        private static void Bootstrap()
        {
            if (UnityEngine.Object.FindObjectOfType<GamePhaseSystem>() != null)
                return;

            GameObject root = new("CIGAgamejam Prototype Runtime");
            root.SetActive(false);

            Camera camera = EnsureCamera();
            EnsureEventSystem();

            CampaignConfig campaignConfig = CreateCampaignConfig();
            EconomyConfig economyConfig = CreateEconomyConfig();
            ToolConfig[] tools = CreateDefaultTools();

            GridSystem gridSystem = root.AddComponent<GridSystem>();
            CampaignProgressSystem campaignProgressSystem = root.AddComponent<CampaignProgressSystem>();
            GamePhaseSystem gamePhaseSystem = root.AddComponent<GamePhaseSystem>();
            PlacementSystem placementSystem = root.AddComponent<PlacementSystem>();
            ToolResolutionSystem toolResolutionSystem = root.AddComponent<ToolResolutionSystem>();
            BossInterferenceSystem bossInterferenceSystem = root.AddComponent<BossInterferenceSystem>();
            EconomySystem economySystem = root.AddComponent<EconomySystem>();
            RouteSystem routeSystem = root.AddComponent<RouteSystem>();
            SecurityPatrolSystem securityPatrolSystem = root.AddComponent<SecurityPatrolSystem>();
            ToolInventorySystem inventorySystem = root.AddComponent<ToolInventorySystem>();
            NightTurnSystem nightTurnSystem = root.AddComponent<NightTurnSystem>();
            PrototypeCustomerFlowSystem customerFlowSystem = root.AddComponent<PrototypeCustomerFlowSystem>();
            PrototypeWorldView worldView = root.AddComponent<PrototypeWorldView>();
            PrototypeInputController inputController = root.AddComponent<PrototypeInputController>();
            PrototypeHudView hudView = root.AddComponent<PrototypeHudView>();

            SetField(campaignProgressSystem, "_config", campaignConfig);
            SetField(gamePhaseSystem, "_campaignProgressSystem", campaignProgressSystem);
            SetField(placementSystem, "_gridSystem", gridSystem);
            SetField(toolResolutionSystem, "_gridSystem", gridSystem);
            SetField(bossInterferenceSystem, "_gridSystem", gridSystem);
            SetField(economySystem, "_config", economyConfig);

            SetField(routeSystem, "_gridSystem", gridSystem);
            SetField(routeSystem, "_entrance", new Vector2Int(0, 6));
            SetField(routeSystem, "_checkout", new Vector2Int(1, 5));
            SetField(routeSystem, "_exit", new Vector2Int(0, 6));
            SetField(routeSystem, "_detourChance", 0.4f);

            SetField(securityPatrolSystem, "_gridSystem", gridSystem);
            SetField(securityPatrolSystem, "_patrolPath", new[]
            {
                new Vector2Int(2, 5),
                new Vector2Int(3, 5),
                new Vector2Int(6, 5),
                new Vector2Int(7, 6),
                new Vector2Int(7, 9),
                new Vector2Int(6, 10),
                new Vector2Int(3, 10),
                new Vector2Int(2, 7)
            });
            SetField(securityPatrolSystem, "_visionRange", 2);
            SetField(securityPatrolSystem, "_stepsPerTurn", 2);

            SetField(inventorySystem, "_blackBossSupport", new[]
            {
                new ToolStockDefinition { Tool = tools[0], Count = 2 },
                new ToolStockDefinition { Tool = tools[1], Count = 1 },
                new ToolStockDefinition { Tool = tools[2], Count = 1 }
            });
            SetField(inventorySystem, "_scheduledSupport", new[]
            {
                new ToolDayStockDefinition { Tool = tools[3], Count = 1, Days = new[] { 2, 4 } }
            });

            SetField(nightTurnSystem, "_securityPatrolSystem", securityPatrolSystem);

            SetField(customerFlowSystem, "_routeSystem", routeSystem);
            SetField(customerFlowSystem, "_toolResolutionSystem", toolResolutionSystem);
            SetField(customerFlowSystem, "_economySystem", economySystem);
            SetField(customerFlowSystem, "_gamePhaseSystem", gamePhaseSystem);
            SetField(customerFlowSystem, "_minCustomersPerDay", 5);
            SetField(customerFlowSystem, "_maxCustomersPerDay", 10);
            SetField(customerFlowSystem, "_smallLoopRouteChance", 0.15f);
            SetField(customerFlowSystem, "_minRandomWaypoints", 2);
            SetField(customerFlowSystem, "_maxRandomWaypoints", 5);

            SetField(worldView, "_gridSystem", gridSystem);
            SetField(worldView, "_routeSystem", routeSystem);
            SetField(worldView, "_securityPatrolSystem", securityPatrolSystem);
            SetField(worldView, "_actorMoveSpeed", 5f);
            SetField(worldView, "_showRouteMarkers", false);
            SetField(worldView, "_showAllWalkableRouteMarkers", false);
            SetField(worldView, "_customerMarkerSizeRatio", 0.22f);
            SetField(worldView, "_securityMarkerSizeRatio", 0.24f);
            SetField(worldView, "_toolMarkerSizeRatio", 0.26f);

            SetField(inputController, "_camera", camera);
            SetField(inputController, "_placementSystem", placementSystem);
            SetField(inputController, "_inventorySystem", inventorySystem);
            SetField(inputController, "_nightTurnSystem", nightTurnSystem);
            SetField(inputController, "_worldView", worldView);

            SetField(hudView, "_gamePhaseSystem", gamePhaseSystem);
            SetField(hudView, "_campaignProgressSystem", campaignProgressSystem);
            SetField(hudView, "_economySystem", economySystem);
            SetField(hudView, "_inventorySystem", inventorySystem);
            SetField(hudView, "_nightTurnSystem", nightTurnSystem);
            SetField(hudView, "_inputController", inputController);

            root.SetActive(true);
        }

        private static Camera EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = 7.1f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.18f);
            return camera;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
                return;

            GameObject eventSystem = new("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static CampaignConfig CreateCampaignConfig()
        {
            CampaignConfig config = ScriptableObject.CreateInstance<CampaignConfig>();
            config.hideFlags = HideFlags.DontSave;
            SetField(config, "_startingDay", 1);
            SetField(config, "_maxDays", 5);
            config.Validate();
            return config;
        }

        private static EconomyConfig CreateEconomyConfig()
        {
            EconomyConfig config = ScriptableObject.CreateInstance<EconomyConfig>();
            config.hideFlags = HideFlags.DontSave;
            SetField(config, "_startingRevenueIndex", 100f);
            SetField(config, "_bankruptcyThreshold", 20f);
            SetField(config, "_successfulPurchaseFavorabilityDelta", 3f);
            SetField(config, "_scaredCustomerFavorabilityPenalty", 10f);
            SetField(config, "_angryCustomerFavorabilityPenalty", 5f);
            config.Validate();
            return config;
        }

        private static ToolConfig[] CreateDefaultTools()
        {
            return new[]
            {
                CreateTool("clown_box", "Boo-tique Trap", "Place inside a wall. When a customer passes the tile in front of it, the trap triggers and scares that customer away.", ToolCategory.Scare, ToolTriggerTiming.OnCustomerPassFrontCell, ToolTriggerAreaMode.ExactOffsets, true, true, new[] { GridCellType.Wall, GridCellType.Floor }, ToolEffectType.ScareCustomerAway, 10f, 0.35f),
                CreateTool("fake_goods", "Fake Goods", "Place on a shelf tile. Replaces goods with low-saturation fake goods. Customers who buy fake goods become angry and leave without a successful purchase.", ToolCategory.FakeGoods, ToolTriggerTiming.OnCustomerEnterCell, ToolTriggerAreaMode.CustomerProximity, true, true, new[] { GridCellType.Warehouse }, ToolEffectType.ReplaceGoodsWithFake, 8f, 0.2f),
                CreateTool("bribe_envelope", "Fake Fiver", "Place on a checkout tile to bribe security. The bribed guard drives customers away once.", ToolCategory.Bribe, ToolTriggerTiming.OnCustomerEnterCell, ToolTriggerAreaMode.CustomerProximity, true, true, new[] { GridCellType.Security, GridCellType.Floor }, ToolEffectType.BribeSecurity, 10f, 0.4f),
                CreateTool("boiling_water", "Meltdown Drip", "Interacts with objects such as money trees and shelves, turning them into destroyed objects. Each customer within 1 tile of a destroyed object loses 5 favorability.", ToolCategory.Destroy, ToolTriggerTiming.OnManualResolve, ToolTriggerAreaMode.ExactOffsets, true, false, new[] { GridCellType.Floor, GridCellType.Warehouse }, ToolEffectType.DestroyObject, 5f, 0f, false)
            };
        }

        private static ToolConfig CreateTool(
            string id,
            string displayName,
            string description,
            ToolCategory category,
            ToolTriggerTiming timing,
            ToolTriggerAreaMode triggerAreaMode,
            bool consumeUseOnTrigger,
            bool disableWhenCustomerAngered,
            GridCellType[] allowedCells,
            ToolEffectType effectType,
            float amount,
            float disableChanceAfterRemovingCustomer,
            bool canBeDisabledByBoss = true)
        {
            ToolConfig config = ScriptableObject.CreateInstance<ToolConfig>();
            config.hideFlags = HideFlags.DontSave;
            SetField(config, "_id", id);
            SetField(config, "_displayName", displayName);
            SetField(config, "_description", description);
            SetField(config, "_category", category);
            SetField(config, "_allowedCellTypes", allowedCells);
            SetField(config, "_footprint", new[] { Vector2Int.zero });
            SetField(config, "_triggerOffsets", BuildTriggerOffsets());
            SetField(config, "_triggerTiming", timing);
            SetField(config, "_triggerAreaMode", triggerAreaMode);
            SetField(config, "_triggerRadius", 1);
            SetField(config, "_useLimit", 3);
            SetField(config, "_consumeUseOnTrigger", consumeUseOnTrigger);
            SetField(config, "_disableWhenCustomerAngered", disableWhenCustomerAngered);
            SetField(config, "_canBeDisabledByBoss", canBeDisabledByBoss);
            SetField(config, "_disableChanceAfterRemovingCustomer", disableChanceAfterRemovingCustomer);
            SetField(config, "_effects", new[]
            {
                new ToolEffectDefinition { EffectType = effectType, Amount = amount, Chance = 1f }
            });
            config.Validate();
            return config;
        }

        private static Vector2Int[] BuildTriggerOffsets()
        {
            return new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };
        }

        private static void SetField(object target, string fieldName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo field = target.GetType().GetField(fieldName, flags);
            if (field == null)
                throw new MissingFieldException(target.GetType().Name, fieldName);

            field.SetValue(target, value);
        }
    }
}
