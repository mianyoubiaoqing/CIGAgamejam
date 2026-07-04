using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CIGAgamejam
{
    public sealed class CoreArchitectureTests
    {
        private readonly List<Object> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void GridRejectsSecondToolOnOccupiedCell()
        {
            GridConfig gridConfig = CreateAsset<GridConfig>();
            SetPrivateField(gridConfig, "_width", 2);
            SetPrivateField(gridConfig, "_height", 2);

            GridSystem gridSystem = CreateComponent<GridSystem>("GridSystem");
            SetPrivateField(gridSystem, "_config", gridConfig);
            gridSystem.InitializeGrid();

            ToolConfig tool = CreateTool("clown_box", GridCellType.Floor);

            bool firstPlaced = gridSystem.TryPlaceTool(tool, new GridPosition(0, 0), out PlacedTool first);
            bool secondPlaced = gridSystem.TryPlaceTool(tool, new GridPosition(0, 0), out _);

            Assert.IsTrue(firstPlaced);
            Assert.IsNotNull(first);
            Assert.IsFalse(secondPlaced);
            Assert.AreEqual(PlacementResult.CellOccupied, gridSystem.CanPlaceTool(tool, new GridPosition(0, 0)));
        }

        [Test]
        public void AttachedAndReplacementToolsCanShareABaseTile()
        {
            GridConfig gridConfig = CreateAsset<GridConfig>();
            SetPrivateField(gridConfig, "_width", 1);
            SetPrivateField(gridConfig, "_height", 1);
            SetPrivateField(gridConfig, "_cellOverrides", new[]
            {
                new GridCellDefinition { Position = Vector2Int.zero, CellType = GridCellType.Warehouse }
            });

            GridSystem gridSystem = CreateComponent<GridSystem>("GridSystem");
            SetPrivateField(gridSystem, "_config", gridConfig);
            gridSystem.InitializeGrid();

            ToolConfig attached = CreateTool("water", GridCellType.Warehouse);
            SetPrivateField(attached, "_placementKind", ToolPlacementKind.ModifyPuzzle);
            ToolConfig replacement = CreateTool("fake", GridCellType.Warehouse);
            SetPrivateField(replacement, "_placementKind", ToolPlacementKind.ReplacePuzzle);

            Assert.IsTrue(gridSystem.TryPlaceTool(attached, new GridPosition(0, 0), out PlacedTool attachedTool));
            Assert.IsTrue(gridSystem.TryPlaceTool(replacement, new GridPosition(0, 0), out PlacedTool replacementTool));
            Assert.IsTrue(gridSystem.TryGetTileState(new GridPosition(0, 0), out PuzzleTileState state));
            Assert.AreSame(attachedTool, state.AttachedTool);
            Assert.AreSame(replacementTool, state.ReplacementTool);
            Assert.AreEqual(GridCellType.Warehouse, state.BaseCellType);
        }

        [Test]
        public void RemovingToolReleasesItsPlacementSlot()
        {
            GridConfig gridConfig = CreateAsset<GridConfig>();
            SetPrivateField(gridConfig, "_width", 1);
            SetPrivateField(gridConfig, "_height", 1);

            GridSystem gridSystem = CreateComponent<GridSystem>("GridSystem");
            SetPrivateField(gridSystem, "_config", gridConfig);
            gridSystem.InitializeGrid();

            ToolConfig tool = CreateTool("clown", GridCellType.Floor);
            Assert.IsTrue(gridSystem.TryPlaceTool(tool, new GridPosition(0, 0), out PlacedTool placedTool));
            Assert.IsTrue(gridSystem.RemoveToolFromBoard(placedTool));
            Assert.AreEqual(PlacementResult.Success, gridSystem.CanPlaceTool(tool, new GridPosition(0, 0)));
        }

        [Test]
        public void CampaignConfigClampsMaxDaysToStartingDay()
        {
            CampaignConfig config = CreateAsset<CampaignConfig>();
            SetPrivateField(config, "_startingDay", 5);
            SetPrivateField(config, "_maxDays", 3);

            LogAssert.Expect(LogType.Error, "[CampaignConfig] MaxDays cannot be lower than StartingDay. Reset to StartingDay.");
            config.Validate();

            Assert.AreEqual(5, config.StartingDay);
            Assert.AreEqual(5, config.MaxDays);
        }

        [Test]
        public void ScareEffectMarksCustomerAsLeftStore()
        {
            var customer = new CustomerContext(12, new GridPosition(1, 1));
            var effect = new ToolEffectDefinition
            {
                EffectType = ToolEffectType.ScareCustomerAway,
                Amount = 10f,
                Chance = 1f
            };

            var handler = new ScareCustomerAwayEffectHandler();
            handler.Resolve(new ToolEffectContext(null, effect, customer));

            Assert.IsTrue(customer.HasLeftStore);
            Assert.IsTrue(customer.WasScaredAway);
            Assert.AreEqual(CustomerState.Scared, customer.State);
        }

        [Test]
        public void EconomyUsesFavorabilityDesignValues()
        {
            EconomySystem economySystem = CreateEconomySystem(100f, 20f);
            SetPrivateField(economySystem, "_currentRevenueIndex", 100f);
            SetPrivateField(economySystem, "_hasConfigError", false);
            float lastValue = economySystem.CurrentRevenueIndex;

            void CaptureRevenue(OnRevenueChanged eventData) => lastValue = eventData.CurrentRevenueIndex;
            EventBus<OnRevenueChanged>.Subscribe(CaptureRevenue);
            try
            {
                economySystem.RecordCustomerPurchase(new CustomerContext(1, new GridPosition(0, 0)));
                Assert.AreEqual(105f, lastValue);

                InvokePrivateMethod(
                    economySystem,
                    "HandleCustomerLeftStore",
                    new OnCustomerLeftStore(1, ToolEffectType.ScareCustomerAway, CustomerState.Scared));
                Assert.AreEqual(95f, lastValue);

                InvokePrivateMethod(
                    economySystem,
                    "HandleFavorabilityDeltaRequested",
                    new OnFavorabilityDeltaRequested(-5f, 1, "Anger"));
                Assert.AreEqual(90f, lastValue);
            }
            finally
            {
                EventBus<OnRevenueChanged>.Unsubscribe(CaptureRevenue);
            }
        }

        [Test]
        public void RepeatedAngerTriggersApplyFavorabilityPenaltyEachTime()
        {
            EconomySystem economySystem = CreateEconomySystem(100f, 20f);
            SetPrivateField(economySystem, "_currentRevenueIndex", 100f);
            SetPrivateField(economySystem, "_hasConfigError", false);
            float lastValue = economySystem.CurrentRevenueIndex;

            void CaptureRevenue(OnRevenueChanged eventData) => lastValue = eventData.CurrentRevenueIndex;
            EventBus<OnRevenueChanged>.Subscribe(CaptureRevenue);
            try
            {
                InvokePrivateMethod(
                    economySystem,
                    "HandleFavorabilityDeltaRequested",
                    new OnFavorabilityDeltaRequested(-5f, 1, "AngerA"));
                Assert.AreEqual(95f, lastValue);

                InvokePrivateMethod(
                    economySystem,
                    "HandleFavorabilityDeltaRequested",
                    new OnFavorabilityDeltaRequested(-5f, 1, "AngerB"));
                Assert.AreEqual(90f, lastValue);
            }
            finally
            {
                EventBus<OnRevenueChanged>.Unsubscribe(CaptureRevenue);
            }
        }

        [Test]
        public void AngerEffectPublishesPenaltyEveryTimeItResolves()
        {
            int penaltyEvents = 0;
            float totalDelta = 0f;
            void CapturePenalty(OnFavorabilityDeltaRequested eventData)
            {
                penaltyEvents++;
                totalDelta += eventData.Delta;
            }

            EventBus<OnFavorabilityDeltaRequested>.Subscribe(CapturePenalty);
            try
            {
                var customer = new CustomerContext(1, new GridPosition(0, 0));
                var effect = new ToolEffectDefinition
                {
                    EffectType = ToolEffectType.ReduceFavorability,
                    Chance = 1f
                };
                var handler = new ReduceFavorabilityEffectHandler();

                handler.Resolve(new ToolEffectContext(null, effect, customer));
                handler.Resolve(new ToolEffectContext(null, effect, customer));

                Assert.AreEqual(CustomerState.Angry, customer.State);
                Assert.AreEqual(2, penaltyEvents);
                Assert.AreEqual(-10f, totalDelta);
            }
            finally
            {
                EventBus<OnFavorabilityDeltaRequested>.Unsubscribe(CapturePenalty);
            }
        }

        [Test]
        public void FakeGoodsAngersCustomerAndDisablesSourceWithoutLeavingStore()
        {
            GridConfig gridConfig = CreateAsset<GridConfig>();
            SetPrivateField(gridConfig, "_width", 2);
            SetPrivateField(gridConfig, "_height", 2);

            GridSystem gridSystem = CreateComponent<GridSystem>("GridSystem");
            SetPrivateField(gridSystem, "_config", gridConfig);
            gridSystem.InitializeGrid();

            ToolConfig tool = CreateTool("fake_goods", GridCellType.Floor);
            SetPrivateField(tool, "_effects", new[]
            {
                new ToolEffectDefinition { EffectType = ToolEffectType.ReplaceGoodsWithFake, Chance = 1f }
            });
            tool.Validate();
            Assert.IsTrue(gridSystem.TryPlaceTool(tool, new GridPosition(0, 0), out PlacedTool placedTool));

            bool angered = false;
            ToolDisableReason eventReason = ToolDisableReason.None;
            void CaptureAnger(OnCustomerAngered eventData) => angered = true;
            void CaptureDisable(OnToolDisabled eventData) => eventReason = eventData.Reason;

            EventBus<OnCustomerAngered>.Subscribe(CaptureAnger);
            EventBus<OnToolDisabled>.Subscribe(CaptureDisable);
            try
            {
                var customer = new CustomerContext(1, new GridPosition(0, 0));
                var handler = new ReplaceGoodsWithFakeEffectHandler();
                handler.Resolve(new ToolEffectContext(placedTool, tool.Effects[0], customer));

                Assert.IsFalse(customer.HasLeftStore);
                Assert.IsTrue(customer.BoughtFakeGoods);
                Assert.AreEqual(CustomerState.Angry, customer.State);
                Assert.IsTrue(placedTool.IsDisabled);
                Assert.AreEqual(ToolDisableReason.Effect, placedTool.DisableReason);
                Assert.AreEqual(ToolDisableReason.Effect, eventReason);
                Assert.IsTrue(angered);
            }
            finally
            {
                EventBus<OnCustomerAngered>.Unsubscribe(CaptureAnger);
                EventBus<OnToolDisabled>.Unsubscribe(CaptureDisable);
            }
        }

        [Test]
        public void ManualResolveToolMarksObjectDestroyed()
        {
            GridConfig gridConfig = CreateAsset<GridConfig>();
            SetPrivateField(gridConfig, "_width", 2);
            SetPrivateField(gridConfig, "_height", 2);
            SetPrivateField(gridConfig, "_cellOverrides", new[]
            {
                new GridCellDefinition { Position = Vector2Int.zero, CellType = GridCellType.Warehouse }
            });

            GridSystem gridSystem = CreateComponent<GridSystem>("GridSystem");
            SetPrivateField(gridSystem, "_config", gridConfig);
            gridSystem.InitializeGrid();

            ToolConfig tool = CreateTool("boiling_water", GridCellType.Warehouse);
            SetPrivateField(tool, "_triggerTiming", ToolTriggerTiming.OnManualResolve);
            SetPrivateField(tool, "_effects", new[]
            {
                new ToolEffectDefinition { EffectType = ToolEffectType.DestroyObject, Chance = 1f }
            });
            tool.Validate();

            GameObject resolutionObject = CreateInactiveObject("ToolResolutionSystem");
            ToolResolutionSystem resolutionSystem = resolutionObject.AddComponent<ToolResolutionSystem>();
            SetPrivateField(resolutionSystem, "_gridSystem", gridSystem);
            resolutionObject.SetActive(true);
            SetPrivateField(resolutionSystem, "_hasConfigError", false);
            InvokePrivateMethod(resolutionSystem, "RegisterDefaultHandlers");

            Assert.IsTrue(gridSystem.TryPlaceTool(tool, new GridPosition(0, 0), out _));
            resolutionSystem.ResolveManual(new GridPosition(0, 0));
            Assert.IsTrue(resolutionSystem.DestroyedObjects.Contains(new GridPosition(0, 0)));
        }

        [Test]
        public void ToolConfigCanCarryMultipleEffectsForFutureTools()
        {
            ToolConfig tool = CreateTool("fake_discount", GridCellType.Warehouse);
            var effects = new[]
            {
                new ToolEffectDefinition { EffectType = ToolEffectType.ReplaceGoodsWithFake, Amount = 8f, Chance = 1f },
                new ToolEffectDefinition { EffectType = ToolEffectType.ModifyPurchaseCost, Amount = 3f, Chance = 1f }
            };
            SetPrivateField(tool, "_effects", effects);

            Assert.AreEqual(2, tool.Effects.Length);
            Assert.AreEqual(ToolEffectType.ReplaceGoodsWithFake, tool.Effects[0].EffectType);
            Assert.AreEqual(ToolEffectType.ModifyPurchaseCost, tool.Effects[1].EffectType);
        }

        [Test]
        public void ToolSelfDisablesAfterRemovingCustomerWhenChanceIsCertain()
        {
            GridConfig gridConfig = CreateAsset<GridConfig>();
            SetPrivateField(gridConfig, "_width", 2);
            SetPrivateField(gridConfig, "_height", 2);

            GridSystem gridSystem = CreateComponent<GridSystem>("GridSystem");
            SetPrivateField(gridSystem, "_config", gridConfig);
            gridSystem.InitializeGrid();

            ToolConfig tool = CreateTool("clown_box", GridCellType.Floor);
            SetPrivateField(tool, "_canBeDisabledByBoss", false);
            SetPrivateField(tool, "_disableChanceAfterRemovingCustomer", 1f);
            SetPrivateField(tool, "_effects", new[]
            {
                new ToolEffectDefinition { EffectType = ToolEffectType.ScareCustomerAway, Chance = 1f }
            });
            tool.Validate();

            Assert.IsTrue(gridSystem.TryPlaceTool(tool, new GridPosition(0, 0), out PlacedTool placedTool));

            var resolutionObject = new GameObject("ToolResolutionSystem");
            resolutionObject.SetActive(false);
            _createdObjects.Add(resolutionObject);
            ToolResolutionSystem resolutionSystem = resolutionObject.AddComponent<ToolResolutionSystem>();
            SetPrivateField(resolutionSystem, "_gridSystem", gridSystem);
            resolutionObject.SetActive(true);
            SetPrivateField(resolutionSystem, "_hasConfigError", false);
            InvokePrivateMethod(resolutionSystem, "RegisterDefaultHandlers");

            ToolDisableReason eventReason = ToolDisableReason.None;
            void CaptureDisable(OnToolDisabled eventData) => eventReason = eventData.Reason;

            EventBus<OnToolDisabled>.Subscribe(CaptureDisable);
            try
            {
                var customer = new CustomerContext(1, new GridPosition(0, 0));
                resolutionSystem.ResolveCustomerEnterCell(customer);

                Assert.IsTrue(customer.HasLeftStore);
                Assert.IsTrue(placedTool.IsDisabled);
                Assert.AreEqual(ToolDisableReason.AfterRemovingCustomer, placedTool.DisableReason);
                Assert.AreEqual(ToolDisableReason.AfterRemovingCustomer, eventReason);
            }
            finally
            {
                EventBus<OnToolDisabled>.Unsubscribe(CaptureDisable);
            }
        }

        [Test]
        public void RouteSystemRebuildsRouteAroundReservedBlocks()
        {
            GridConfig gridConfig = CreateAsset<GridConfig>();
            SetPrivateField(gridConfig, "_width", 3);
            SetPrivateField(gridConfig, "_height", 2);

            GridSystem gridSystem = CreateComponent<GridSystem>("GridSystem");
            SetPrivateField(gridSystem, "_config", gridConfig);
            gridSystem.InitializeGrid();

            GameObject routeObject = CreateInactiveObject("RouteSystem");
            RouteSystem routeSystem = routeObject.AddComponent<RouteSystem>();
            SetPrivateField(routeSystem, "_gridSystem", gridSystem);
            SetPrivateField(routeSystem, "_entrance", new Vector2Int(0, 0));
            SetPrivateField(routeSystem, "_checkout", new Vector2Int(2, 0));
            SetPrivateField(routeSystem, "_exit", new Vector2Int(2, 1));
            routeObject.SetActive(true);

            routeSystem.SetReservedRouteBlocks(new[] { new GridPosition(1, 0) });

            Assert.IsTrue(routeSystem.CustomerRoute.Count > 3);
            Assert.IsFalse(routeSystem.CustomerRoute.Contains(new GridPosition(1, 0)));
        }

        [Test]
        public void SecurityPatrolDisablesVisibleTool()
        {
            GridConfig gridConfig = CreateAsset<GridConfig>();
            SetPrivateField(gridConfig, "_width", 3);
            SetPrivateField(gridConfig, "_height", 3);

            GridSystem gridSystem = CreateComponent<GridSystem>("GridSystem");
            SetPrivateField(gridSystem, "_config", gridConfig);
            gridSystem.InitializeGrid();

            ToolConfig tool = CreateTool("clown_box", GridCellType.Floor);
            Assert.IsTrue(gridSystem.TryPlaceTool(tool, new GridPosition(1, 0), out PlacedTool placedTool));

            GameObject patrolObject = CreateInactiveObject("SecurityPatrolSystem");
            SecurityPatrolSystem patrolSystem = patrolObject.AddComponent<SecurityPatrolSystem>();
            SetPrivateField(patrolSystem, "_gridSystem", gridSystem);
            SetPrivateField(patrolSystem, "_patrolPath", new[] { Vector2Int.zero });
            SetPrivateField(patrolSystem, "_visionRange", 1);
            patrolObject.SetActive(true);

            patrolSystem.BeginNightPatrol();

            Assert.IsTrue(placedTool.IsDisabled);
            Assert.AreEqual(ToolDisableReason.SecurityPatrol, placedTool.DisableReason);
            Assert.IsFalse(gridSystem.PlacedTools.Contains(placedTool));
            Assert.AreEqual(PlacementResult.Success, gridSystem.CanPlaceTool(tool, new GridPosition(1, 0)));
        }

        [Test]
        public void SecurityPatrolAdvancesConfiguredStepsPerTurn()
        {
            GridConfig gridConfig = CreateAsset<GridConfig>();
            SetPrivateField(gridConfig, "_width", 4);
            SetPrivateField(gridConfig, "_height", 1);

            GridSystem gridSystem = CreateComponent<GridSystem>("GridSystem");
            SetPrivateField(gridSystem, "_config", gridConfig);
            gridSystem.InitializeGrid();

            GameObject patrolObject = CreateInactiveObject("SecurityPatrolSystem");
            SecurityPatrolSystem patrolSystem = patrolObject.AddComponent<SecurityPatrolSystem>();
            SetPrivateField(patrolSystem, "_gridSystem", gridSystem);
            SetPrivateField(patrolSystem, "_patrolPath", new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0)
            });
            SetPrivateField(patrolSystem, "_stepsPerTurn", 2);
            patrolObject.SetActive(true);

            patrolSystem.BeginNightPatrol();
            patrolSystem.AdvancePatrolTurn();

            Assert.AreEqual(new GridPosition(2, 0), patrolSystem.CurrentPosition);
        }

        private ToolConfig CreateTool(string id, GridCellType allowedCellType)
        {
            ToolConfig tool = CreateAsset<ToolConfig>();
            SetPrivateField(tool, "_id", id);
            SetPrivateField(tool, "_allowedCellTypes", new[] { allowedCellType });
            SetPrivateField(tool, "_footprint", new[] { Vector2Int.zero });
            SetPrivateField(tool, "_triggerOffsets", new[] { Vector2Int.zero });
            SetPrivateField(tool, "_useLimit", 1);
            tool.Validate();
            return tool;
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateInactiveObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(asset);
            return asset;
        }

        private EconomySystem CreateEconomySystem(float startingValue, float threshold)
        {
            EconomyConfig economyConfig = CreateAsset<EconomyConfig>();
            SetPrivateField(economyConfig, "_startingRevenueIndex", startingValue);
            SetPrivateField(economyConfig, "_bankruptcyThreshold", threshold);

            GameObject economyObject = CreateInactiveObject("EconomySystem");
            EconomySystem economySystem = economyObject.AddComponent<EconomySystem>();
            SetPrivateField(economySystem, "_config", economyConfig);
            economyObject.SetActive(true);
            return economySystem;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field, $"Missing private field {fieldName}");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo method = target.GetType().GetMethod(methodName, flags);
            Assert.IsNotNull(method, $"Missing private method {methodName}");
            method.Invoke(target, null);
        }

        private static void InvokePrivateMethod(object target, string methodName, object argument)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo method = target.GetType().GetMethod(methodName, flags);
            Assert.IsNotNull(method, $"Missing private method {methodName}");
            method.Invoke(target, new[] { argument });
        }
    }
}
