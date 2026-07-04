using System;
using UnityEngine;

namespace CIGAgamejam
{
    [CreateAssetMenu(fileName = "ToolConfig", menuName = "CIGAgamejam/Tools/ToolConfig")]
    public sealed class ToolConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id = "tool_id";
        [SerializeField] private string _displayName = "Tool";
        [SerializeField] private ToolCategory _category = ToolCategory.Utility;
        [SerializeField] private Sprite _icon;
        [SerializeField] private GameObject _prefab;

        [Header("Placement")]
        [SerializeField] private ToolPlacementKind _placementKind = ToolPlacementKind.ReplacePuzzle;
        [SerializeField] private GridCellType[] _allowedCellTypes = { GridCellType.Floor };
        [SerializeField] private Vector2Int[] _footprint = { Vector2Int.zero };
        [SerializeField] private bool _uniquePerBoard;

        [Header("Trigger")]
        [SerializeField] private ToolTriggerTiming _triggerTiming = ToolTriggerTiming.OnCustomerEnterCell;
        [SerializeField] private Vector2Int[] _triggerOffsets = { Vector2Int.zero };
        [SerializeField, Min(1)] private int _useLimit = 1;
        [SerializeField] private bool _canBeDisabledByBoss = true;
        [SerializeField, Range(0f, 1f)] private float _disableChanceAfterRemovingCustomer;

        [Header("Effects")]
        [SerializeField] private ToolEffectDefinition[] _effects = Array.Empty<ToolEffectDefinition>();

        public string Id => _id;
        public string DisplayName => _displayName;
        public ToolCategory Category => _category;
        public Sprite Icon => _icon;
        public GameObject Prefab => _prefab;
        public ToolPlacementKind PlacementKind => _placementKind;
        public GridCellType[] AllowedCellTypes => _allowedCellTypes;
        public Vector2Int[] Footprint => _footprint;
        public bool UniquePerBoard => _uniquePerBoard;
        public ToolTriggerTiming TriggerTiming => _triggerTiming;
        public Vector2Int[] TriggerOffsets => _triggerOffsets;
        public int UseLimit => _useLimit;
        public bool CanBeDisabledByBoss => _canBeDisabledByBoss;
        public float DisableChanceAfterRemovingCustomer => _disableChanceAfterRemovingCustomer;
        public ToolEffectDefinition[] Effects => _effects;

        private void OnValidate() => Validate();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Debug.LogError($"[ToolConfig] {name} has an empty id. Reset to asset name.");
                _id = name;
            }

            if (_allowedCellTypes == null || _allowedCellTypes.Length == 0)
            {
                Debug.LogError($"[ToolConfig] {_id} has no allowed cell types. Reset to Floor.");
                _allowedCellTypes = new[] { GridCellType.Floor };
            }

            if (_footprint == null || _footprint.Length == 0)
            {
                Debug.LogError($"[ToolConfig] {_id} has no footprint. Reset to one cell.");
                _footprint = new[] { Vector2Int.zero };
            }

            if (_triggerOffsets == null || _triggerOffsets.Length == 0)
                _triggerOffsets = new[] { Vector2Int.zero };

            if (_useLimit < 1)
                _useLimit = 1;

            _disableChanceAfterRemovingCustomer = Mathf.Clamp01(_disableChanceAfterRemovingCustomer);
        }

        public bool AllowsCellType(GridCellType cellType)
        {
            if (_allowedCellTypes == null) return false;

            for (int i = 0; i < _allowedCellTypes.Length; i++)
                if (_allowedCellTypes[i] == cellType)
                    return true;

            return false;
        }

        public bool PassesDisableAfterRemovingCustomerChance()
        {
            return _disableChanceAfterRemovingCustomer > 0f
                && UnityEngine.Random.value <= _disableChanceAfterRemovingCustomer;
        }
    }

    [Serializable]
    public struct ToolEffectDefinition
    {
        public ToolEffectType EffectType;
        public float Amount;
        [Tooltip("0 means always trigger. Values from 0 to 1 are treated as probability.")]
        [Range(0f, 1f)] public float Chance;
        public GridCellType TargetCellType;

        public bool PassesChance()
        {
            return Chance <= 0f || UnityEngine.Random.value <= Chance;
        }
    }
}
