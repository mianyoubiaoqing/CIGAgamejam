using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class PrototypeWorldView : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private RouteSystem _routeSystem;
        [SerializeField] private SecurityPatrolSystem _securityPatrolSystem;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private Vector2 _origin = new(-3.5f, -2.5f);
        [SerializeField, Min(0.1f)] private float _actorMoveSpeed = 5f;

        private readonly Dictionary<GridPosition, SpriteRenderer> _cellRenderers = new();
        private readonly Dictionary<PlacedTool, GameObject> _toolMarkers = new();
        private readonly Dictionary<int, GameObject> _customerMarkers = new();
        private readonly List<GameObject> _routeMarkers = new();
        private Sprite _sprite;
        private GameObject _securityMarker;

        public float CellSize => _cellSize;

        private void Awake()
        {
            _sprite = CreateSprite();
            BuildGrid();
            RebuildRouteMarkers();
            MoveSecurityMarker(_securityPatrolSystem != null ? _securityPatrolSystem.CurrentPosition : new GridPosition(0, 0));
        }

        private void OnEnable()
        {
            EventBus<OnToolPlaced>.Subscribe(HandleToolPlaced);
            EventBus<OnToolDisabled>.Subscribe(HandleToolDisabled);
            EventBus<OnSecurityPatrolMoved>.Subscribe(HandleSecurityMoved);
            EventBus<OnRouteChanged>.Subscribe(HandleRouteChanged);
            EventBus<OnPrototypeCustomerMoved>.Subscribe(HandleCustomerMoved);
            EventBus<OnPrototypeCustomerRemoved>.Subscribe(HandleCustomerRemoved);
        }

        private void OnDestroy()
        {
            EventBus<OnToolPlaced>.Unsubscribe(HandleToolPlaced);
            EventBus<OnToolDisabled>.Unsubscribe(HandleToolDisabled);
            EventBus<OnSecurityPatrolMoved>.Unsubscribe(HandleSecurityMoved);
            EventBus<OnRouteChanged>.Unsubscribe(HandleRouteChanged);
            EventBus<OnPrototypeCustomerMoved>.Unsubscribe(HandleCustomerMoved);
            EventBus<OnPrototypeCustomerRemoved>.Unsubscribe(HandleCustomerRemoved);
        }

        public bool TryWorldToGrid(Vector3 worldPosition, out GridPosition gridPosition)
        {
            int x = Mathf.FloorToInt((worldPosition.x - _origin.x) / _cellSize + 0.5f);
            int y = Mathf.FloorToInt((worldPosition.y - _origin.y) / _cellSize + 0.5f);
            gridPosition = new GridPosition(x, y);
            return _gridSystem != null && _gridSystem.IsInBounds(gridPosition);
        }

        public Vector3 GridToWorld(GridPosition position)
        {
            return new Vector3(_origin.x + position.X * _cellSize, _origin.y + position.Y * _cellSize, 0f);
        }

        private void BuildGrid()
        {
            if (_gridSystem == null) return;

            for (int y = 0; y < _gridSystem.Height; y++)
            for (int x = 0; x < _gridSystem.Width; x++)
            {
                var position = new GridPosition(x, y);
                GameObject cell = CreateSquare($"Cell {x},{y}", GridToWorld(position), _cellSize * 0.92f, ResolveCellColor(position), 0);
                _cellRenderers[position] = cell.GetComponent<SpriteRenderer>();
                CreateLabel(cell.transform, ResolveCellLabel(position), Color.black, 0.22f, new Vector3(0f, -0.23f, -0.02f));
            }
        }

        private Color ResolveCellColor(GridPosition position)
        {
            if (_gridSystem.TryGetCellType(position, out GridCellType cellType))
            {
                return cellType switch
                {
                    GridCellType.Wall => new Color(0.25f, 0.25f, 0.25f),
                    GridCellType.Warehouse => new Color(0.95f, 0.86f, 0.55f),
                    GridCellType.Security => new Color(0.55f, 0.75f, 0.95f),
                    GridCellType.Entrance => new Color(0.6f, 0.95f, 0.65f),
                    GridCellType.Checkout => new Color(0.15f, 0.42f, 0.62f),
                    GridCellType.Restroom => new Color(0.62f, 0.1f, 0.92f),
                    GridCellType.Exit => new Color(0.8f, 0.65f, 0.95f),
                    GridCellType.Blocked => new Color(0.12f, 0.12f, 0.12f),
                    _ => new Color(0.9f, 0.9f, 0.86f)
                };
            }

            return Color.white;
        }

        private string ResolveCellLabel(GridPosition position)
        {
            if (!_gridSystem.TryGetCellType(position, out GridCellType cellType))
                return string.Empty;

            return cellType switch
            {
                GridCellType.Wall => "墙",
                GridCellType.Warehouse => "仓",
                GridCellType.Security => "保",
                GridCellType.Entrance => "入",
                GridCellType.Checkout => "柜",
                GridCellType.Restroom => "厕",
                GridCellType.Exit => "出",
                GridCellType.Blocked => "阻",
                _ => string.Empty
            };
        }

        private void RebuildRouteMarkers()
        {
            for (int i = 0; i < _routeMarkers.Count; i++)
                if (_routeMarkers[i] != null)
                    Destroy(_routeMarkers[i]);
            _routeMarkers.Clear();

            if (_routeSystem == null) return;

            IReadOnlyList<GridPosition> route = _routeSystem.CustomerRoute;
            for (int i = 0; i < route.Count; i++)
            {
                GameObject marker = CreateSquare($"Route {i}", GridToWorld(route[i]) + new Vector3(0f, 0.25f, -0.05f), 0.16f, new Color(0.15f, 0.3f, 0.95f), 2);
                _routeMarkers.Add(marker);
            }
        }

        private void HandleToolPlaced(OnToolPlaced e)
        {
            if (e.Tool == null || e.Tool.Config == null) return;

            GameObject marker = CreateSquare(
                $"Tool {e.Tool.InstanceId}",
                GridToWorld(e.Tool.Origin) + new Vector3(0f, 0f, -0.1f),
                0.54f,
                new Color(0.95f, 0.35f, 0.25f),
                3);
            CreateLabel(marker.transform, e.Tool.Config.DisplayName, Color.white, 0.18f, new Vector3(0f, 0f, -0.02f));
            _toolMarkers[e.Tool] = marker;
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

        private void HandleRouteChanged(OnRouteChanged e)
        {
            RebuildRouteMarkers();
        }

        private void HandleCustomerMoved(OnPrototypeCustomerMoved e)
        {
            if (!_customerMarkers.TryGetValue(e.CustomerId, out GameObject marker) || marker == null)
            {
                marker = CreateSquare($"Customer {e.CustomerId}", CustomerWorldPosition(e.Position), 0.32f, new Color(0.1f, 0.45f, 1f), 5);
                AddSmoothMover(marker, CustomerWorldPosition(e.Position));
                CreateLabel(marker.transform, e.CustomerId.ToString("00"), Color.white, 0.16f, Vector3.zero);
                _customerMarkers[e.CustomerId] = marker;
            }

            MoveMarker(marker, CustomerWorldPosition(e.Position));
        }

        private void HandleCustomerRemoved(OnPrototypeCustomerRemoved e)
        {
            if (!_customerMarkers.TryGetValue(e.CustomerId, out GameObject marker))
                return;

            if (marker != null)
                Destroy(marker);
            _customerMarkers.Remove(e.CustomerId);
        }

        private void MoveSecurityMarker(GridPosition position)
        {
            if (_securityMarker == null)
            {
                _securityMarker = CreateSquare("Security", SecurityWorldPosition(position), 0.42f, new Color(0.1f, 0.1f, 0.1f), 4);
                AddSmoothMover(_securityMarker, SecurityWorldPosition(position));
                CreateLabel(_securityMarker.transform, "保安", Color.white, 0.18f, Vector3.zero);
            }

            MoveMarker(_securityMarker, SecurityWorldPosition(position));
        }

        private Vector3 CustomerWorldPosition(GridPosition position)
        {
            return GridToWorld(position) + new Vector3(0.24f, 0f, -0.18f);
        }

        private Vector3 SecurityWorldPosition(GridPosition position)
        {
            return GridToWorld(position) + new Vector3(0f, 0f, -0.2f);
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

        private static void CreateLabel(Transform parent, string text, Color color, float size, Vector3 localPosition)
        {
            if (string.IsNullOrEmpty(text)) return;

            var label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            TextMesh textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.color = color;
            textMesh.fontSize = 24;
            textMesh.characterSize = size;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
        }

        private static Sprite CreateSprite()
        {
            Texture2D texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
