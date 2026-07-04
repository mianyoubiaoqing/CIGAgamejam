using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CIGAgamejam
{
    public sealed class PrototypeWorldView : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private RouteSystem _routeSystem;
        [SerializeField] private SecurityPatrolSystem _securityPatrolSystem;
        [SerializeField] private TilemapGridBridge _tilemapBridge;
        [SerializeField, Min(0.1f)] private float _actorMoveSpeed = 5f;
        [SerializeField, Min(0.02f)] private float _routeMarkerSizeRatio = 0.08f;
        [SerializeField, Min(0.02f)] private float _customerMarkerSizeRatio = 0.22f;
        [SerializeField, Min(0.02f)] private float _securityMarkerSizeRatio = 0.24f;
        [SerializeField, Min(0.02f)] private float _toolMarkerSizeRatio = 0.26f;
        [Header("Security Patrol Path")]
        [SerializeField, Min(0.02f)] private float _patrolPathMarkerSizeRatio = 0.06f;
        [SerializeField] private Color _patrolPathColor = new(0.5f, 0.5f, 0.5f, 0.35f);
        [Header("Tilemap Feedback")]
        [SerializeField] private Tilemap _warehouseTilemap;
        [SerializeField] private TileBase _fakeGoodsShelfTile;
        [SerializeField] private bool _tintFakeGoodsShelf = true;
        [Header("Placement Preview")]
        [SerializeField, Range(0.05f, 1f)] private float _previewAlpha = 0.45f;

        private readonly Dictionary<GridPosition, SpriteRenderer> _cellRenderers = new();
        private readonly Dictionary<GridPosition, GameObject> _destroyedObjectMarkers = new();
        private readonly Dictionary<PlacedTool, GameObject> _toolMarkers = new();
        private readonly Dictionary<int, GameObject> _customerMarkers = new();
        private readonly List<GameObject> _routeMarkers = new();
        private readonly List<GameObject> _patrolPathMarkers = new();
        private Sprite _sprite;
        private GameObject _securityMarker;
        private GameObject _placementPreview;
        private SpriteRenderer _placementPreviewRenderer;

        public float CellSize => ResolveCellSize();

        private void Awake()
        {
            _sprite = CreateUnitSprite();
            RebuildRouteMarkers();
        }

        private void OnEnable()
        {
            EventBus<OnToolPlaced>.Subscribe(HandleToolPlaced);
            EventBus<OnToolDisabled>.Subscribe(HandleToolDisabled);
            EventBus<OnSecurityPatrolMoved>.Subscribe(HandleSecurityMoved);
            EventBus<OnSecurityPatrolPathChanged>.Subscribe(HandlePatrolPathChanged);
            EventBus<OnSecurityPatrolPathCleared>.Subscribe(HandlePatrolPathCleared);
            EventBus<OnRouteChanged>.Subscribe(HandleRouteChanged);
            EventBus<OnPrototypeCustomerMoved>.Subscribe(HandleCustomerMoved);
            EventBus<OnPrototypeCustomerRemoved>.Subscribe(HandleCustomerRemoved);
            EventBus<OnWorldObjectDestroyed>.Subscribe(HandleWorldObjectDestroyed);
        }

        private void OnDestroy()
        {
            EventBus<OnToolPlaced>.Unsubscribe(HandleToolPlaced);
            EventBus<OnToolDisabled>.Unsubscribe(HandleToolDisabled);
            EventBus<OnSecurityPatrolMoved>.Unsubscribe(HandleSecurityMoved);
            EventBus<OnSecurityPatrolPathChanged>.Unsubscribe(HandlePatrolPathChanged);
            EventBus<OnSecurityPatrolPathCleared>.Unsubscribe(HandlePatrolPathCleared);
            EventBus<OnRouteChanged>.Unsubscribe(HandleRouteChanged);
            EventBus<OnPrototypeCustomerMoved>.Unsubscribe(HandleCustomerMoved);
            EventBus<OnPrototypeCustomerRemoved>.Unsubscribe(HandleCustomerRemoved);
            EventBus<OnWorldObjectDestroyed>.Unsubscribe(HandleWorldObjectDestroyed);
        }

        public bool TryWorldToGrid(Vector3 worldPosition, out GridPosition gridPosition)
        {
            if (!IsBridgeReady())
            {
                gridPosition = new GridPosition(0, 0);
                return false;
            }

            gridPosition = _tilemapBridge.WorldToCell(worldPosition);
            return _gridSystem != null && _gridSystem.IsInBounds(gridPosition);
        }

        public Vector3 GridToWorld(GridPosition position)
        {
            return IsBridgeReady() ? _tilemapBridge.CellToWorld(position) : Vector3.zero;
        }

        private void BuildGrid()
        {
            if (_gridSystem == null) return;

            for (int y = _gridSystem.MinY; y < _gridSystem.MaxYExclusive; y++)
            for (int x = _gridSystem.MinX; x < _gridSystem.MaxXExclusive; x++)
            {
                var position = new GridPosition(x, y);
                GridCellType cellType = GridCellType.Floor;
                _gridSystem.TryGetCellType(position, out cellType);

                GameObject cell = CreateSquare($"Cell {x},{y}", GridToWorld(position), CellSize, ResolveCellColor(cellType), 0);
                _cellRenderers[position] = cell.GetComponent<SpriteRenderer>();
                BuildCellDetail(cell.transform, cellType);
            }
        }

        private Color ResolveCellColor(GridCellType cellType)
        {
            return cellType switch
            {
                GridCellType.Wall => new Color(0.23f, 0.13f, 0.08f),
                GridCellType.Warehouse => new Color(0.79f, 0.70f, 0.58f),
                GridCellType.Security => new Color(0.84f, 0.78f, 0.66f),
                GridCellType.Entrance => new Color(0.84f, 0.78f, 0.66f),
                GridCellType.Checkout => new Color(0.86f, 0.78f, 0.62f),
                GridCellType.Restroom => new Color(0.45f, 0.36f, 0.24f),
                GridCellType.FortuneTree => new Color(0.18f, 0.55f, 0.22f),
                GridCellType.Exit => new Color(0.84f, 0.78f, 0.66f),
                GridCellType.Blocked => new Color(0.05f, 0.05f, 0.05f),
                _ => new Color(0.72f, 0.69f, 0.58f)
            };
        }

        private void BuildCellDetail(Transform parent, GridCellType cellType)
        {
            AddCellBorder(parent, new Color(0.33f, 0.30f, 0.24f, 0.65f));

            switch (cellType)
            {
                case GridCellType.Wall:
                    AddWoodStripes(parent);
                    break;
                case GridCellType.Warehouse:
                    AddShelfDetail(parent);
                    break;
                case GridCellType.Checkout:
                    AddFloorPattern(parent);
                    AddCheckoutDetail(parent);
                    break;
                case GridCellType.Restroom:
                    AddDoorDetail(parent);
                    break;
                case GridCellType.Entrance:
                    AddFloorPattern(parent);
                    AddDoorMark(parent, new Color(0.52f, 0.84f, 0.48f, 0.85f));
                    break;
                case GridCellType.Exit:
                    AddFloorPattern(parent);
                    AddDoorMark(parent, new Color(0.64f, 0.48f, 0.86f, 0.85f));
                    break;
                case GridCellType.Security:
                    AddFloorPattern(parent);
                    AddSecurityTileMark(parent);
                    break;
                case GridCellType.Floor:
                    AddFloorPattern(parent);
                    break;
            }
        }

        private void AddFloorPattern(Transform parent)
        {
            Color lineColor = new(0.48f, 0.46f, 0.39f, 0.42f);
            CreateRect(parent, "Floor Diagonal A", Vector3.zero, new Vector2(CellSize * 1.3f, CellSize * 0.045f), lineColor, 1, 45f);
            CreateRect(parent, "Floor Diagonal B", Vector3.zero, new Vector2(CellSize * 1.3f, CellSize * 0.045f), lineColor, 1, -45f);
            CreateRect(parent, "Floor Center", Vector3.zero, new Vector2(CellSize * 0.18f, CellSize * 0.18f), new Color(0.50f, 0.48f, 0.41f, 0.35f), 1, 45f);
        }

        private void AddWoodStripes(Transform parent)
        {
            Color stripeColor = new(0.13f, 0.07f, 0.04f, 0.45f);
            for (int i = -2; i <= 2; i++)
                CreateRect(parent, "Wood Stripe", new Vector3(i * CellSize * 0.18f, 0f, -0.01f), new Vector2(CellSize * 0.035f, CellSize * 0.88f), stripeColor, 1);

            CreateRect(parent, "Wall Top Trim", new Vector3(0f, CellSize * 0.42f, -0.02f), new Vector2(CellSize * 0.95f, CellSize * 0.06f), new Color(0.86f, 0.80f, 0.67f), 2);
            CreateRect(parent, "Wall Bottom Trim", new Vector3(0f, -CellSize * 0.42f, -0.02f), new Vector2(CellSize * 0.95f, CellSize * 0.06f), new Color(0.86f, 0.80f, 0.67f), 2);
        }

        private void AddShelfDetail(Transform parent)
        {
            CreateRect(parent, "Shelf Top", new Vector3(0f, CellSize * 0.25f, -0.02f), new Vector2(CellSize * 0.80f, CellSize * 0.10f), new Color(0.91f, 0.85f, 0.72f), 2);
            CreateRect(parent, "Shelf Body", Vector3.zero, new Vector2(CellSize * 0.78f, CellSize * 0.46f), new Color(0.47f, 0.40f, 0.34f), 2);
            CreateRect(parent, "Shelf Line A", new Vector3(0f, CellSize * 0.09f, -0.03f), new Vector2(CellSize * 0.70f, CellSize * 0.035f), new Color(0.17f, 0.14f, 0.13f), 3);
            CreateRect(parent, "Shelf Line B", new Vector3(0f, -CellSize * 0.08f, -0.03f), new Vector2(CellSize * 0.70f, CellSize * 0.035f), new Color(0.17f, 0.14f, 0.13f), 3);
        }

        private void AddCheckoutDetail(Transform parent)
        {
            CreateRect(parent, "Counter", Vector3.zero, new Vector2(CellSize * 0.72f, CellSize * 0.58f), new Color(0.91f, 0.82f, 0.62f), 2);
            CreateRect(parent, "Register", new Vector3(0f, CellSize * 0.08f, -0.03f), new Vector2(CellSize * 0.38f, CellSize * 0.24f), new Color(0.35f, 0.31f, 0.25f), 3);
            CreateRect(parent, "Receipt", new Vector3(CellSize * 0.20f, CellSize * 0.22f, -0.04f), new Vector2(CellSize * 0.16f, CellSize * 0.20f), new Color(0.96f, 0.92f, 0.78f), 4);
        }

        private void AddDoorDetail(Transform parent)
        {
            CreateRect(parent, "Door Panel", Vector3.zero, new Vector2(CellSize * 0.62f, CellSize * 0.74f), new Color(0.43f, 0.34f, 0.23f), 2);
            CreateRect(parent, "Door Plate", new Vector3(0f, CellSize * 0.10f, -0.03f), new Vector2(CellSize * 0.36f, CellSize * 0.16f), new Color(0.55f, 0.50f, 0.38f), 3);
            CreateRect(parent, "Door Knob", new Vector3(CellSize * 0.24f, -CellSize * 0.10f, -0.04f), new Vector2(CellSize * 0.06f, CellSize * 0.06f), new Color(0.83f, 0.75f, 0.54f), 4);
        }

        private void AddDoorMark(Transform parent, Color color)
        {
            CreateRect(parent, "Door Mark", Vector3.zero, new Vector2(CellSize * 0.42f, CellSize * 0.12f), color, 2);
        }

        private void AddSecurityTileMark(Transform parent)
        {
            CreateRect(parent, "Security Mark", Vector3.zero, new Vector2(CellSize * 0.42f, CellSize * 0.42f), new Color(0.40f, 0.55f, 0.70f, 0.55f), 2, 45f);
        }

        private void AddCellBorder(Transform parent, Color color)
        {
            float thickness = CellSize * 0.025f;
            CreateRect(parent, "Border Top", new Vector3(0f, CellSize * 0.5f - thickness * 0.5f, -0.04f), new Vector2(CellSize, thickness), color, 4);
            CreateRect(parent, "Border Bottom", new Vector3(0f, -CellSize * 0.5f + thickness * 0.5f, -0.04f), new Vector2(CellSize, thickness), color, 4);
            CreateRect(parent, "Border Left", new Vector3(-CellSize * 0.5f + thickness * 0.5f, 0f, -0.04f), new Vector2(thickness, CellSize), color, 4);
            CreateRect(parent, "Border Right", new Vector3(CellSize * 0.5f - thickness * 0.5f, 0f, -0.04f), new Vector2(thickness, CellSize), color, 4);
        }

        private void RebuildRouteMarkers()
        {
            for (int i = 0; i < _routeMarkers.Count; i++)
                if (_routeMarkers[i] != null)
                    Destroy(_routeMarkers[i]);
            _routeMarkers.Clear();

            if (_routeSystem == null) return;

            IReadOnlyList<GridPosition> route = _routeSystem.CustomerRoute;
            float markerSize = CellSize * _routeMarkerSizeRatio;
            for (int i = 0; i < route.Count; i++)
            {
                GameObject marker = CreateSquare($"Route {i}", GridToWorld(route[i]) + new Vector3(0f, 0f, -0.08f), markerSize, new Color(0.20f, 0.25f, 0.95f, 0.25f), 5);
                _routeMarkers.Add(marker);
            }
        }

        private void HandleToolPlaced(OnToolPlaced e)
        {
            if (e.Tool == null || e.Tool.Config == null) return;

            ApplyToolTileFeedback(e.Tool);

            GameObject marker = CreateSquare(
                $"Tool {e.Tool.InstanceId}",
                GridToWorld(e.Tool.Origin) + new Vector3(0f, 0f, -0.14f),
                CellSize * _toolMarkerSizeRatio,
                new Color(0.95f, 0.28f, 0.18f),
                8);
            _toolMarkers[e.Tool] = marker;
        }

        public void ShowPlacementPreview(ToolConfig tool, GridPosition position, bool isLegal)
        {
            if (tool == null)
            {
                HidePlacementPreview();
                return;
            }

            if (_placementPreview == null)
            {
                _placementPreview = new GameObject("Placement Preview");
                _placementPreview.transform.SetParent(transform, false);
                _placementPreviewRenderer = _placementPreview.AddComponent<SpriteRenderer>();
                _placementPreviewRenderer.sortingOrder = 47;
            }

            _placementPreview.SetActive(true);
            _placementPreview.transform.position = GridToWorld(position) + new Vector3(0f, 0f, -0.21f);
            _placementPreview.transform.localScale = Vector3.one * (CellSize * 0.72f);
            _placementPreviewRenderer.sprite = tool.Icon != null ? tool.Icon : _sprite;
            _placementPreviewRenderer.color = isLegal
                ? new Color(1f, 1f, 1f, _previewAlpha)
                : new Color(1f, 0.12f, 0.08f, _previewAlpha + 0.18f);
        }

        public void HidePlacementPreview()
        {
            if (_placementPreview != null)
                _placementPreview.SetActive(false);
        }

        private void ApplyToolTileFeedback(PlacedTool tool)
        {
            if (tool.Config == null || tool.Config.Id != "fake_goods")
                return;

            GridPosition[] cells = tool.OccupiedCells;
            for (int i = 0; i < cells.Length; i++)
                ApplyFakeGoodsShelfFeedback(cells[i]);
        }

        private void ApplyFakeGoodsShelfFeedback(GridPosition position)
        {
            if (_warehouseTilemap != null)
            {
                var cell = new Vector3Int(position.X, position.Y, 0);
                if (_warehouseTilemap.HasTile(cell))
                {
                    if (_fakeGoodsShelfTile != null)
                    {
                        _warehouseTilemap.SetTile(cell, _fakeGoodsShelfTile);
                        return;
                    }

                    if (_tintFakeGoodsShelf)
                    {
                        _warehouseTilemap.SetTileFlags(cell, TileFlags.None);
                        _warehouseTilemap.SetColor(cell, Color.Lerp(Color.white, Color.gray, 0.5f));
                        return;
                    }
                }
            }

            if (_cellRenderers.TryGetValue(position, out SpriteRenderer renderer) && renderer != null)
                renderer.color = Color.Lerp(renderer.color, Color.gray, 0.5f);
        }

        private void HandleToolDisabled(OnToolDisabled e)
        {
            if (e.Tool == null || !_toolMarkers.TryGetValue(e.Tool, out GameObject marker) || marker == null)
                return;

            SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>();
            renderer.color = new Color(0.35f, 0.35f, 0.35f);
        }

        private void HandleSecurityMoved(OnSecurityPatrolMoved e)
        {
            MoveSecurityMarker(e.Position);
        }

        private void HandlePatrolPathChanged(OnSecurityPatrolPathChanged e)
        {
            ClearPatrolPathMarkers();
            if (e.Path == null || e.Path.Count == 0)
                return;

            float markerSize = CellSize * _patrolPathMarkerSizeRatio;
            for (int i = 0; i < e.Path.Count; i++)
            {
                GameObject marker = CreateSquare(
                    $"Patrol Path {i}",
                    GridToWorld(e.Path[i]) + new Vector3(0f, 0f, -0.07f),
                    markerSize,
                    _patrolPathColor,
                    6);
                _patrolPathMarkers.Add(marker);
            }
        }

        private void HandlePatrolPathCleared(OnSecurityPatrolPathCleared e)
        {
            ClearPatrolPathMarkers();
        }

        private void HandleRouteChanged(OnRouteChanged e)
        {
            RebuildRouteMarkers();
        }

        private void HandleCustomerMoved(OnPrototypeCustomerMoved e)
        {
            Vector3 targetPosition = CustomerWorldPosition(e.GridX, e.GridY, e.CustomerId);
            if (!_customerMarkers.TryGetValue(e.CustomerId, out GameObject marker) || marker == null)
            {
                marker = CreateSquare($"Customer {e.CustomerId}", targetPosition, CellSize * _customerMarkerSizeRatio, ResolveCustomerColor(e.State), 10);
                _customerMarkers[e.CustomerId] = marker;
            }

            SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.color = ResolveCustomerColor(e.State);
            marker.transform.position = targetPosition;
        }

        private void HandleCustomerRemoved(OnPrototypeCustomerRemoved e)
        {
            if (!_customerMarkers.TryGetValue(e.CustomerId, out GameObject marker))
                return;

            if (marker != null)
                Destroy(marker);
            _customerMarkers.Remove(e.CustomerId);
        }

        private void HandleWorldObjectDestroyed(OnWorldObjectDestroyed e)
        {
            if (!_cellRenderers.TryGetValue(e.Position, out SpriteRenderer renderer) || renderer == null)
            {
                if (_destroyedObjectMarkers.ContainsKey(e.Position))
                    return;

                GameObject marker = CreateSquare(
                    $"Destroyed {e.Position.X},{e.Position.Y}",
                    GridToWorld(e.Position) + new Vector3(0f, 0f, -0.12f),
                    CellSize * 0.72f,
                    new Color(0.18f, 0.18f, 0.18f, 0.72f),
                    7);
                _destroyedObjectMarkers[e.Position] = marker;
                return;
            }

            renderer.color = new Color(0.28f, 0.28f, 0.28f);
        }

        private void MoveSecurityMarker(GridPosition position)
        {
            Vector3 targetPosition = SecurityWorldPosition(position);
            if (_securityMarker == null)
            {
                _securityMarker = CreateSquare("Security", targetPosition, CellSize * _securityMarkerSizeRatio, new Color(0.05f, 0.05f, 0.05f), 9);
                AddSmoothMover(_securityMarker, targetPosition);
                return;
            }

            MoveMarker(_securityMarker, targetPosition);
        }

        private void ClearPatrolPathMarkers()
        {
            for (int i = 0; i < _patrolPathMarkers.Count; i++)
                if (_patrolPathMarkers[i] != null)
                    Destroy(_patrolPathMarkers[i]);

            _patrolPathMarkers.Clear();
        }

        private Vector3 CustomerWorldPosition(float gridX, float gridY, int customerId)
        {
            Vector3 gridPosition = GridToWorld(gridX, gridY);
            return gridPosition + ResolveCustomerOffset(customerId) + new Vector3(0f, 0f, -0.18f);
        }

        private Vector3 GridToWorld(float gridX, float gridY)
        {
            if (!IsBridgeReady())
                return Vector3.zero;

            Tilemap coordinateTilemap = _tilemapBridge.CoordinateTilemap;
            if (coordinateTilemap != null)
            {
                int cellX = Mathf.FloorToInt(gridX);
                int cellY = Mathf.FloorToInt(gridY);
                Vector3 cellCenter = coordinateTilemap.GetCellCenterWorld(new Vector3Int(cellX, cellY, 0));
                Vector3 cellSize = coordinateTilemap.layoutGrid != null ? coordinateTilemap.layoutGrid.cellSize : Vector3.one;
                return cellCenter + new Vector3((gridX - cellX) * cellSize.x, (gridY - cellY) * cellSize.y, 0f);
            }

            return _tilemapBridge.CellToWorld(new GridPosition(Mathf.FloorToInt(gridX), Mathf.FloorToInt(gridY)));
        }

        private Vector3 SecurityWorldPosition(GridPosition position)
        {
            return GridToWorld(position) + new Vector3(0f, 0f, -0.19f);
        }

        private Vector3 ResolveCustomerOffset(int customerId)
        {
            int lane = Mathf.Abs(customerId) % 3;
            float cellSize = CellSize;
            float xOffset = (lane - 1) * cellSize * 0.10f;
            return new Vector3(xOffset, cellSize * 0.06f, 0f);
        }

        private void AddSmoothMover(GameObject marker, Vector3 targetPosition)
        {
            PrototypeSmoothMover mover = marker.AddComponent<PrototypeSmoothMover>();
            mover.SetMoveSpeed(_actorMoveSpeed);
            mover.SetTarget(targetPosition);
        }

        private static void MoveMarker(GameObject marker, Vector3 targetPosition)
        {
            PrototypeSmoothMover mover = marker.GetComponent<PrototypeSmoothMover>();
            if (mover != null)
            {
                mover.SetTarget(targetPosition);
                return;
            }

            marker.transform.position = targetPosition;
        }

        private static Color ResolveCustomerColor(CustomerState state)
        {
            return state switch
            {
                CustomerState.Angry => new Color(1f, 0.35f, 0.08f),
                CustomerState.Scared => new Color(0.82f, 0.22f, 0.95f),
                _ => new Color(0.1f, 0.45f, 1f)
            };
        }

        private GameObject CreateSquare(string objectName, Vector3 position, float size, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size, size, 1f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        private void CreateRect(Transform parent, string objectName, Vector3 localPosition, Vector2 size, Color color, int sortingOrder, float zRotation = 0f)
        {
            float inverseCellSize = CellSize > 0f ? 1f / CellSize : 1f;
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPosition.x * inverseCellSize, localPosition.y * inverseCellSize, localPosition.z);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
            go.transform.localScale = new Vector3(size.x * inverseCellSize, size.y * inverseCellSize, 1f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private bool IsBridgeReady()
        {
            if (_tilemapBridge != null && _tilemapBridge.IsReady)
                return true;

            Debug.LogError("[PrototypeWorldView] TilemapGridBridge is required for all world/grid coordinate conversion.");
            return false;
        }

        private float ResolveCellSize() => _tilemapBridge != null && _tilemapBridge.IsReady ? _tilemapBridge.CellSize : 1f;

        private static Sprite CreateUnitSprite()
        {
            Texture2D texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        }
    }
}
