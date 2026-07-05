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

        [MenuItem("CIGAgamejam/Tilemap/Configure Visual Tilemap Bridge")]
        public static void Migrate()
        {
            GridSystem gridSystem = Object.FindObjectOfType<GridSystem>();
            PrototypeWorldView worldView = Object.FindObjectOfType<PrototypeWorldView>();
            if (gridSystem == null)
            {
                Debug.LogError("[TilemapGridBaker] Scene GridSystem is missing.");
                return;
            }

            GameObject root = GameObject.Find("Shop Tilemaps");
            if (root == null)
            {
                Debug.LogError("[TilemapGridBaker] Shop Tilemaps root is missing.");
                return;
            }

            Tilemap ground = FindTilemap(root.transform, "ground");
            Tilemap wall = FindTilemap(root.transform, "wall");
            Tilemap shelf = FindTilemap(root.transform, "shelf");
            Tilemap bathroomDoor = FindTilemapWithTag("BATH_door");
            if (ground == null || wall == null || shelf == null)
            {
                Debug.LogError("[TilemapGridBaker] Required visual Tilemaps are missing. Expected ground, wall, and shelf.");
                return;
            }

            Tilemap toolOverlay = EnsureTilemap(root.transform, "Tool Overlay", 30);
            Tilemap stateOverlay = EnsureTilemap(root.transform, "State Overlay", 20);
            EnsureTilemap(root.transform, "Editor Preview", 100);

            TilemapGridBridge bridge = gridSystem.GetComponent<TilemapGridBridge>();
            if (bridge == null) bridge = Undo.AddComponent<TilemapGridBridge>(gridSystem.gameObject);
            SetReference(bridge, "_groundTilemap", ground);
            SetVisualLayers(bridge, ground, wall, shelf, bathroomDoor);
            SetReference(gridSystem, "_tilemapBridge", bridge);
            if (worldView != null) SetReference(worldView, "_tilemapBridge", bridge);
            TilemapOverlayController overlay = gridSystem.GetComponent<TilemapOverlayController>();
            if (overlay == null) overlay = Undo.AddComponent<TilemapOverlayController>(gridSystem.gameObject);
            SetReference(overlay, "_toolOverlay", toolOverlay);
            SetReference(overlay, "_stateOverlay", stateOverlay);

            EditorSceneManager.MarkSceneDirty(gridSystem.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(bathroomDoor != null
                ? "[TilemapGridBaker] Visual Tilemap Bridge configured with bathroom door logic."
                : "[TilemapGridBaker] Visual Tilemap Bridge configured. No bathroom door Tilemap was found.");
        }

        [MenuItem("CIGAgamejam/Tilemap/Validate Visual Tilemap Logic")]
        public static void Validate()
        {
            TilemapGridBridge bridge = Object.FindObjectOfType<TilemapGridBridge>();
            if (bridge == null || !bridge.TryReadCells(out var cells, out BoundsInt bounds))
            {
                Debug.LogError("[TilemapGridBaker] Visual Tilemap logic is missing or empty.");
                return;
            }

            int expected = bounds.size.x * bounds.size.y;
            if (cells.Count != expected)
                Debug.LogError($"[TilemapGridBaker] Logic map has holes: {cells.Count}/{expected} cells.");
            else
                Debug.Log($"[TilemapGridBaker] Logic map valid: {cells.Count} cells, bounds {bounds}.");
        }

        private static Tilemap FindTilemap(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child != null ? child.GetComponent<Tilemap>() : null;
        }

        private static Tilemap FindTilemapWithTag(string tag)
        {
            foreach (Tilemap tilemap in Object.FindObjectsOfType<Tilemap>(true))
                if (tilemap.gameObject.tag == tag)
                    return tilemap;

            return null;
        }

        private static Tilemap EnsureTilemap(Transform parent, string name, int sortingOrder)
        {
            Transform child = parent.Find(name);
            GameObject go = child != null ? child.gameObject : new GameObject(name);
            if (child == null) go.transform.SetParent(parent, false);
            Tilemap tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) tilemap = go.AddComponent<Tilemap>();
            TilemapRenderer renderer = go.GetComponent<TilemapRenderer>();
            if (renderer == null) renderer = go.AddComponent<TilemapRenderer>();
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

        private static void SetVisualLayers(
            TilemapGridBridge bridge,
            Tilemap ground,
            Tilemap wall,
            Tilemap shelf,
            Tilemap bathroomDoor)
        {
            var serialized = new SerializedObject(bridge);
            SerializedProperty layers = serialized.FindProperty("_visualLayers");
            layers.arraySize = bathroomDoor != null ? 4 : 3;
            SetVisualLayer(layers.GetArrayElementAtIndex(0), ground, GridCellType.Floor);
            SetVisualLayer(layers.GetArrayElementAtIndex(1), wall, GridCellType.Wall);
            SetVisualLayer(layers.GetArrayElementAtIndex(2), shelf, GridCellType.Warehouse);
            if (bathroomDoor != null)
                SetVisualLayer(layers.GetArrayElementAtIndex(3), bathroomDoor, GridCellType.Restroom);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bridge);
        }

        private static void SetVisualLayer(SerializedProperty layer, Tilemap tilemap, GridCellType cellType)
        {
            layer.FindPropertyRelative("Tilemap").objectReferenceValue = tilemap;
            layer.FindPropertyRelative("CellType").enumValueIndex = (int)cellType;
        }

    }
}
