using System;
using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class ToolInventorySystem : MonoBehaviour
    {
        [SerializeField] private ToolStockDefinition[] _blackBossSupport = Array.Empty<ToolStockDefinition>();
        [SerializeField] private bool _carryUnusedTools = true;

        private readonly Dictionary<ToolConfig, ToolStockState> _stocks = new();
        private bool _hasStarted;

        public IReadOnlyDictionary<ToolConfig, ToolStockState> Stocks => _stocks;

        private void OnEnable()
        {
            EventBus<OnDayStarted>.Subscribe(HandleDayStarted);
        }

        private void Start()
        {
            if (!_hasStarted)
                RebuildForNewDay();
        }

        private void OnDestroy()
        {
            EventBus<OnDayStarted>.Unsubscribe(HandleDayStarted);
        }

        public int GetCount(ToolConfig tool)
        {
            return tool != null && _stocks.TryGetValue(tool, out ToolStockState stock) ? stock.Count : 0;
        }

        public bool TryConsume(ToolConfig tool)
        {
            if (tool == null || !_stocks.TryGetValue(tool, out ToolStockState stock) || stock.Count <= 0)
                return false;

            stock.Count--;
            _stocks[tool] = stock;
            EventBus<OnToolInventoryChanged>.Publish(new OnToolInventoryChanged(tool, stock.Count, stock.Source));
            return true;
        }

        public void AddTool(ToolConfig tool, int count, ToolStockSource source)
        {
            if (tool == null || count <= 0) return;

            if (!_stocks.TryGetValue(tool, out ToolStockState stock))
                stock = new ToolStockState(0, source);

            stock.Count += count;
            stock.Source = source;
            _stocks[tool] = stock;
            EventBus<OnToolInventoryChanged>.Publish(new OnToolInventoryChanged(tool, stock.Count, source));
        }

        public void RebuildForNewDay()
        {
            if (!_carryUnusedTools || !_hasStarted)
                _stocks.Clear();
            else
                MarkExistingToolsAsCarriedOver();

            for (int i = 0; i < _blackBossSupport.Length; i++)
                AddTool(_blackBossSupport[i].Tool, _blackBossSupport[i].Count, ToolStockSource.BlackBossSupport);

            _hasStarted = true;
        }

        private void HandleDayStarted(OnDayStarted e)
        {
            RebuildForNewDay();
        }

        private void MarkExistingToolsAsCarriedOver()
        {
            var keys = new List<ToolConfig>(_stocks.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                ToolConfig key = keys[i];
                ToolStockState stock = _stocks[key];
                stock.Source = ToolStockSource.CarriedOver;
                _stocks[key] = stock;
                EventBus<OnToolInventoryChanged>.Publish(new OnToolInventoryChanged(key, stock.Count, stock.Source));
            }
        }
    }

    [Serializable]
    public struct ToolStockDefinition
    {
        public ToolConfig Tool;
        [Min(0)] public int Count;
    }

    public struct ToolStockState
    {
        public int Count;
        public ToolStockSource Source;

        public ToolStockState(int count, ToolStockSource source)
        {
            Count = count;
            Source = source;
        }
    }
}
