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
            SetField(routeSystem, "_routeOverride", BuildCustomerLoopRoute());
            SetField(routeSystem, "_detourWaypoints", new[]
            {
                new Vector2Int(4, 3),
                new Vector2Int(6, 7),
                new Vector2Int(3, 8),
                new Vector2Int(4, 6)
            });
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
                new ToolStockDefinition { Tool = tools[2], Count = 1 },
                new ToolStockDefinition { Tool = tools[3], Count = 1 }
            });

            SetField(nightTurnSystem, "_securityPatrolSystem", securityPatrolSystem);

            SetField(customerFlowSystem, "_routeSystem", routeSystem);
            SetField(customerFlowSystem, "_toolResolutionSystem", toolResolutionSystem);
            SetField(customerFlowSystem, "_economySystem", economySystem);
            SetField(customerFlowSystem, "_gamePhaseSystem", gamePhaseSystem);

            SetField(worldView, "_gridSystem", gridSystem);
            SetField(worldView, "_routeSystem", routeSystem);
            SetField(worldView, "_securityPatrolSystem", securityPatrolSystem);
            SetField(worldView, "_actorMoveSpeed", 5f);
            SetField(worldView, "_routeMarkerSizeRatio", 0.08f);
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
                CreateTool("clown_box", "\u5c0f\u4e11\u76d2", "Spooks one customer into leaving. Place near aisles for maximum effect.", ToolCategory.Scare, ToolTriggerTiming.OnCustomerPassFrontCell, ToolTriggerAreaMode.ExactOffsets, true, true, new[] { GridCellType.Wall, GridCellType.Floor }, ToolEffectType.ScareCustomerAway, 10f, 0.35f),
                CreateTool("fake_goods", "\u5047\u8d27", "Replaces a customer's purchase with defective goods. Works in warehouse areas.", ToolCategory.FakeGoods, ToolTriggerTiming.OnCustomerEnterCell, ToolTriggerAreaMode.CustomerProximity, true, true, new[] { GridCellType.Warehouse }, ToolEffectType.ReplaceGoodsWithFake, 8f, 0.2f),
                CreateTool("bribe_envelope", "\u4fe1\u5c01", "Bribes a security guard to look the other way. Place along patrol routes.", ToolCategory.Bribe, ToolTriggerTiming.OnCustomerEnterCell, ToolTriggerAreaMode.CustomerProximity, true, true, new[] { GridCellType.Security, GridCellType.Floor }, ToolEffectType.BribeSecurity, 10f, 0.4f),
                CreateTool("boiling_water", "\u5f00\u6c34", "Destroys a nearby object on command. Manual trigger - use with precision.", ToolCategory.Destroy, ToolTriggerTiming.OnManualResolve, ToolTriggerAreaMode.ExactOffsets, true, false, new[] { GridCellType.Floor, GridCellType.Warehouse }, ToolEffectType.DestroyObject, 5f, 0f, false)
            };
        }

        private static Vector2Int[] BuildCustomerLoopRoute()
        {
            return new[]
            {
                new Vector2Int(0, 6),
                new Vector2Int(1, 6),
                new Vector2Int(1, 7),
                new Vector2Int(1, 8),
                new Vector2Int(1, 9),
                new Vector2Int(1, 10),
                new Vector2Int(2, 10),
                new Vector2Int(3, 10),
                new Vector2Int(4, 10),
                new Vector2Int(5, 10),
                new Vector2Int(6, 10),
                new Vector2Int(7, 10),
                new Vector2Int(8, 10),
                new Vector2Int(8, 9),
                new Vector2Int(8, 8),
                new Vector2Int(8, 7),
                new Vector2Int(8, 6),
                new Vector2Int(8, 5),
                new Vector2Int(8, 4),
                new Vector2Int(8, 3),
                new Vector2Int(8, 2),
                new Vector2Int(8, 1),
                new Vector2Int(7, 1),
                new Vector2Int(6, 1),
                new Vector2Int(5, 1),
                new Vector2Int(4, 1),
                new Vector2Int(3, 1),
                new Vector2Int(2, 1),
                new Vector2Int(1, 1),
                new Vector2Int(1, 2),
                new Vector2Int(1, 3),
                new Vector2Int(1, 4),
                new Vector2Int(1, 5),
                new Vector2Int(1, 6),
                new Vector2Int(0, 6)
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
