using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class RouteSystem : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private Vector2Int _entrance = new(0, 2);
        [SerializeField] private Vector2Int _checkout = new(5, 2);
        [SerializeField] private Vector2Int _exit = new(7, 2);

        private readonly List<GridPosition> _customerRoute = new();
        private readonly HashSet<GridPosition> _reservedRouteBlocks = new();
        private bool _hasConfigError;

        public IReadOnlyList<GridPosition> CustomerRoute => _customerRoute;
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
            if (!TryFindRoute(Entrance, Checkout, _reservedRouteBlocks, out List<GridPosition> toCheckout)
                || !TryFindRoute(Checkout, Exit, _reservedRouteBlocks, out List<GridPosition> toExit))
            {
                EventBus<OnRouteChanged>.Publish(new OnRouteChanged(false, 0));
                return false;
            }

            _customerRoute.AddRange(toCheckout);
            for (int i = 1; i < toExit.Count; i++)
                _customerRoute.Add(toExit[i]);

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

        private IEnumerable<GridPosition> GetNeighbors(GridPosition position)
        {
            yield return new GridPosition(position.X + 1, position.Y);
            yield return new GridPosition(position.X - 1, position.Y);
            yield return new GridPosition(position.X, position.Y + 1);
            yield return new GridPosition(position.X, position.Y - 1);
        }
    }
}
