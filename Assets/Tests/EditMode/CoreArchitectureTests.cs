using System.Collections.Generic;
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
        public void CampaignConfigClampsMaxDaysToStartingDay()
        {
            CampaignConfig config = CreateAsset<CampaignConfig>();
            SetPrivateField(config, "_startingDay", 5);
            SetPrivateField(config, "_maxDays", 3);

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

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(asset);
            return asset;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field, $"Missing private field {fieldName}");
            field.SetValue(target, value);
        }
    }
}
