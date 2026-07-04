using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CIGAgamejam.Editor
{
    public static class TilemapGridBaker
    {
        private const string TileFolder = "Assets/Configs/GameplayTiles";

        [MenuItem("CIGAgamejam/Tilemap/Migrate GridConfig To Gameplay Logic")]
        public static void Migrate()
        {
            GridSystem gridSystem = Object.FindObjectOfType<GridSystem>();
            PrototypeWorldView worldView = Object.FindObjectOfType<PrototypeWorldView>();
            GridConfig config = GetReference<GridConfig>(gridSystem, "_config");
            if (gridSystem == null || config == null)
            {
                Debug.LogError("[TilemapGridBaker] Scene GridSystem or GridConfig is missing.");
                return;
            }

            GameObject root = GameObject.Find("Shop Tilemaps");
            if (root == null)
            {
                root = new GameObject("Shop Tilemaps");
                root.AddComponent<Grid>();
            }

            Tilemap logic = EnsureTilemap(root.transform, "Gameplay Logic", -100);
            Tilemap toolOverlay = EnsureTilemap(root.transform, "Tool Overlay", 30);
            Tilemap stateOverlay = EnsureTilemap(root.transform, "State Overlay", 20);
            EnsureTilemap(root.transform, "Editor Preview", 100);

            Dictionary<GridCellType, GameplayTile> tiles = EnsureGameplayTiles();
            logic.ClearAllTiles();
            for (int y = config.MinY; y < config.MaxYExclusive; y++)
            for (int x = config.MinX; x < config.MaxXExclusive; x++)
                logic.SetTile(new Vector3Int(x, y, 0), tiles[GridCellType.Floor]);

            foreach (GridCellDefinition cell in config.CellOverrides)
                logic.SetTile(
                    new Vector3Int(cell.Position.x, cell.Position.y, 0),
                    tiles[cell.CellType]);

            TilemapGridBridge bridge = gridSystem.GetComponent<TilemapGridBridge>();
            if (bridge == null) bridge = Undo.AddComponent<TilemapGridBridge>(gridSystem.gameObject);
            SetReference(bridge, "_gameplayTilemap", logic);
            SetReference(gridSystem, "_tilemapBridge", bridge);
            if (worldView != null) SetReference(worldView, "_tilemapBridge", bridge);
            TilemapOverlayController overlay = gridSystem.GetComponent<TilemapOverlayController>();
            if (overlay == null) overlay = Undo.AddComponent<TilemapOverlayController>(gridSystem.gameObject);
            SetReference(overlay, "_toolOverlay", toolOverlay);
            SetReference(overlay, "_stateOverlay", stateOverlay);

            logic.GetComponent<TilemapRenderer>().enabled = false;
            EditorUtility.SetDirty(logic);
            EditorSceneManager.MarkSceneDirty(gridSystem.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TilemapGridBaker] Migrated {config.Width * config.Height} cells to Gameplay Logic.");
        }

        [MenuItem("CIGAgamejam/Tilemap/Validate Gameplay Logic")]
        public static void Validate()
        {
            TilemapGridBridge bridge = Object.FindObjectOfType<TilemapGridBridge>();
            if (bridge == null || !bridge.TryReadCells(out var cells, out BoundsInt bounds))
            {
                Debug.LogError("[TilemapGridBaker] Gameplay Logic is missing or empty.");
                return;
            }

            int expected = bounds.size.x * bounds.size.y;
            if (cells.Count != expected)
                Debug.LogError($"[TilemapGridBaker] Logic map has holes: {cells.Count}/{expected} cells.");
            else
                Debug.Log($"[TilemapGridBaker] Logic map valid: {cells.Count} cells, bounds {bounds}.");
        }

        private static Tilemap EnsureTilemap(Transform parent, string name, int sortingOrder)
        {
            Transform child = parent.Find(name);
            GameObject go = child != null ? child.gameObject : new GameObject(name);
            if (child == null) go.transform.SetParent(parent, false);
            Tilemap tilemap = go.GetComponent<Tilemap>() ?? go.AddComponent<Tilemap>();
            TilemapRenderer renderer = go.GetComponent<TilemapRenderer>() ?? go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private static Dictionary<GridCellType, GameplayTile> EnsureGameplayTiles()
        {
            if (!AssetDatabase.IsValidFolder(TileFolder))
            {
                Directory.CreateDirectory(TileFolder);
                AssetDatabase.Refresh();
            }

            var result = new Dictionary<GridCellType, GameplayTile>();
            foreach (GridCellType type in System.Enum.GetValues(typeof(GridCellType)))
            {
                string path = $"{TileFolder}/{type}.asset";
                GameplayTile tile = AssetDatabase.LoadAssetAtPath<GameplayTile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<GameplayTile>();
                    AssetDatabase.CreateAsset(tile, path);
                }

                var serialized = new SerializedObject(tile);
                serialized.FindProperty("_cellType").enumValueIndex = (int)type;
                serialized.FindProperty("_walkable").boolValue = IsWalkable(type);
                serialized.FindProperty("_canBeDestroyed").boolValue =
                    type == GridCellType.Warehouse || type == GridCellType.FortuneTree;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                result[type] = tile;
            }
            return result;
        }

        private static bool IsWalkable(GridCellType type)
        {
            return type != GridCellType.Wall
                && type != GridCellType.Warehouse
                && type != GridCellType.Restroom
                && type != GridCellType.FortuneTree
                && type != GridCellType.Blocked;
        }

        private static T GetReference<T>(Object target, string property) where T : Object
        {
            if (target == null) return null;
            return new SerializedObject(target).FindProperty(property).objectReferenceValue as T;
        }

        private static void SetReference(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
}
