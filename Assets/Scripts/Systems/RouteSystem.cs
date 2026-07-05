using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class RouteSystem : MonoBehaviour
    {
        public readonly struct RouteVariant
        {
            public int ForkIndex { get; }
            public IReadOnlyList<GridPosition> Tail { get; }

            public RouteVariant(int forkIndex, List<GridPosition> tail)
            {
                ForkIndex = forkIndex;
                Tail = tail;
            }
        }

        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private Vector2Int _entrance = new(0, 2);
        [SerializeField] private Vector2Int _checkout = new(5, 2);
        [SerializeField] private Vector2Int _exit = new(7, 2);
        [SerializeField] private Vector2Int[] _routeOverride;

        private readonly List<GridPosition> _customerRoute = new();
        private readonly List<RouteVariant> _routeVariants = new();
        private readonly HashSet<GridPosition> _reservedRouteBlocks = new();
        private bool _hasConfigError;

        public IReadOnlyList<GridPosition> CustomerRoute => _customerRoute;
        public IReadOnlyList<RouteVariant> AvailableVariants => _routeVariants;
        public GridPosition Entrance => new(_entrance);
        public GridPosition Checkout => new(_checkout);
        public GridPosition Exit => new(_exit);

        private void Awake()
        {
            if (_gridSystem == null)
            {
                Debug.LogError("[RouteSystem] GridSystem is not assigned.");
                _hasConfigError = true;
                return;
            }

            RebuildCustomerRoute();
        }

        public bool RebuildCustomerRoute()
        {
            if (_hasConfigError) return false;

            _customerRoute.Clear();
            _routeVariants.Clear();
            if (TryBuildRouteOverride())
            {
                GenerateRouteVariants();
                EventBus<OnRouteChanged>.Publish(new OnRouteChanged(true, _customerRoute.Count));
                return true;
            }

            if (!TryFindRoute(Entrance, Checkout, _reservedRouteBlocks, out List<GridPosition> toCheckout)
                || !TryFindRoute(Checkout, Exit, _reservedRouteBlocks, out List<GridPosition> toExit))
            {
                EventBus<OnRouteChanged>.Publish(new OnRouteChanged(false, 0));
                return false;
            }

            _customerRoute.AddRange(toCheckout);
            for (int i = 1; i < toExit.Count; i++)
                _customerRoute.Add(toExit[i]);

            GenerateRouteVariants();
            EventBus<OnRouteChanged>.Publish(new OnRouteChanged(true, _customerRoute.Count));
            return true;
        }

        public void SetReservedRouteBlocks(IEnumerable<GridPosition> blockedCells)
        {
            _reservedRouteBlocks.Clear();
            if (blockedCells != null)
            {
                foreach (GridPosition cell in blockedCells)
                    _reservedRouteBlocks.Add(cell);
            }

            RebuildCustomerRoute();
        }

        private bool TryBuildRouteOverride()
        {
            if (_routeOverride == null || _routeOverride.Length == 0)
                return false;

            for (int i = 0; i < _routeOverride.Length; i++)
            {
                var position = new GridPosition(_routeOverride[i]);
                if (!_gridSystem.IsRouteWalkable(position))
                {
                    _customerRoute.Clear();
                    return false;
                }

                if (_reservedRouteBlocks.Contains(position) && !position.Equals(Checkout) && !position.Equals(Exit))
                {
                    _customerRoute.Clear();
                    return false;
                }

                _customerRoute.Add(position);
            }

            return _customerRoute.Count > 0;
        }

        public bool TryFindRoute(
            GridPosition start,
            GridPosition goal,
            ICollection<GridPosition> blockedCells,
            out List<GridPosition> route)
        {
            route = new List<GridPosition>();
            if (_hasConfigError || !_gridSystem.IsRouteWalkable(start) || !_gridSystem.IsRouteWalkable(goal))
                return false;

            var frontier = new Queue<GridPosition>();
            var cameFrom = new Dictionary<GridPosition, GridPosition>();
            frontier.Enqueue(start);
            cameFrom[start] = start;

            while (frontier.Count > 0)
            {
                GridPosition current = frontier.Dequeue();
                if (current.Equals(goal))
                    break;

                foreach (GridPosition next in GetNeighbors(current))
                {
                    if (cameFrom.ContainsKey(next)) continue;
                    if (blockedCells != null && blockedCells.Contains(next) && !next.Equals(goal)) continue;
                    if (!_gridSystem.IsRouteWalkable(next)) continue;

                    frontier.Enqueue(next);
                    cameFrom[next] = current;
                }
            }

            if (!cameFrom.ContainsKey(goal))
                return false;

            GridPosition step = goal;
            while (!step.Equals(start))
            {
                route.Add(step);
                step = cameFrom[step];
            }

            route.Add(start);
            route.Reverse();
            return true;
        }

        private void GenerateRouteVariants()
        {
            _routeVariants.Clear();
            if (_customerRoute.Count < 3)
                return;

            int checkoutIndex = _customerRoute.IndexOf(Checkout);
            if (checkoutIndex < 0)
                return;

            for (int i = 1; i < _customerRoute.Count - 1; i++)
            {
                GridPosition previous = _customerRoute[i - 1];
                GridPosition corner = _customerRoute[i];
                GridPosition next = _customerRoute[i + 1];
                int previousDeltaX = corner.X - previous.X;
                int previousDeltaY = corner.Y - previous.Y;
                int nextDeltaX = next.X - corner.X;
                int nextDeltaY = next.Y - corner.Y;
                if (previousDeltaX == nextDeltaX && previousDeltaY == nextDeltaY)
                    continue;

                foreach (GridPosition neighbor in GetNeighbors(corner))
                {
                    if (neighbor.Equals(previous) || neighbor.Equals(next))
                        continue;
                    if (!_gridSystem.IsRouteWalkable(neighbor) || _reservedRouteBlocks.Contains(neighbor))
                        continue;

                    List<GridPosition> tail = TryBuildAlternativeTail(neighbor, corner, i, checkoutIndex);
                    if (tail == null || tail.Count == 0)
                        continue;

                    _routeVariants.Add(new RouteVariant(i, tail));
                    break;
                }
            }
        }

        private List<GridPosition> TryBuildAlternativeTail(
            GridPosition start,
            GridPosition blockedCorner,
            int forkIndex,
            int checkoutIndex)
        {
            var blockedCells = new HashSet<GridPosition>(_reservedRouteBlocks)
            {
                blockedCorner
            };

            if (forkIndex < checkoutIndex)
            {
                if (!TryFindRoute(start, Checkout, blockedCells, out List<GridPosition> toCheckout))
                    return null;

                var tail = new List<GridPosition>(toCheckout.Count + _customerRoute.Count - checkoutIndex - 1);
                tail.AddRange(toCheckout);
                for (int i = checkoutIndex + 1; i < _customerRoute.Count; i++)
                    tail.Add(_customerRoute[i]);
                return tail;
            }

            return TryFindRoute(start, Exit, blockedCells, out List<GridPosition> toExit)
                ? toExit
                : null;
        }

        private IEnumerable<GridPosition> GetNeighbors(GridPosition position)
        {
            yield return new GridPosition(position.X + 1, position.Y);
            yield return new GridPosition(position.X - 1, position.Y);
            yield return new GridPosition(position.X, position.Y + 1);
            yield return new GridPosition(position.X, position.Y - 1);
        }
    }
}
