using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class PrototypeCustomerFlowSystem : MonoBehaviour
    {
        [SerializeField] private RouteSystem _routeSystem;
        [SerializeField] private ToolResolutionSystem _toolResolutionSystem;
        [SerializeField] private EconomySystem _economySystem;
        [SerializeField] private GamePhaseSystem _gamePhaseSystem;
        [SerializeField, Min(1)] private int _minCustomersPerDay = 5;
        [SerializeField, Min(1)] private int _maxCustomersPerDay = 10;
        [SerializeField, Min(0.1f)] private float _spawnInterval = 0.85f;
        [SerializeField, Min(0.1f)] private float _cellsPerSecond = 1.176f;
        [SerializeField, Range(0f, 1f)] private float _smallLoopRouteChance = 0.15f;
        [SerializeField, Min(0)] private int _minRandomWaypoints = 2;
        [SerializeField, Min(0)] private int _maxRandomWaypoints = 5;

        private readonly List<MovingCustomer> _activeCustomers = new();
        private readonly List<GridPosition> _scratchEscapeRoute = new();
        private float _timer;
        private int _nextCustomerId = 1;
        private int _spawnedToday;
        private int _customersPerDayForCurrentDay;
        private int _dayStartScareQuota;
        private bool _isSimulating;

        private void OnEnable()
        {
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
            EventBus<OnGroupScareRequested>.Subscribe(HandleGroupScareRequested);
            EventBus<OnDayStartScareQuotaRequested>.Subscribe(HandleDayStartScareQuotaRequested);
        }

        private void OnDestroy()
        {
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
            EventBus<OnGroupScareRequested>.Unsubscribe(HandleGroupScareRequested);
            EventBus<OnDayStartScareQuotaRequested>.Unsubscribe(HandleDayStartScareQuotaRequested);
        }

        private void Update()
        {
            if (!_isSimulating)
                return;

            if (!HasCustomerRoute())
                return;

            float deltaTime = Time.deltaTime;
            _timer += deltaTime;
            while (_spawnedToday < _customersPerDayForCurrentDay && _timer >= _spawnInterval)
            {
                _timer -= _spawnInterval;
                if (!TrySpawnCustomer())
                    break;
            }

            for (int i = _activeCustomers.Count - 1; i >= 0; i--)
                AdvanceCustomer(i, deltaTime);

            PublishFlow();

            if (_spawnedToday >= _customersPerDayForCurrentDay && _activeCustomers.Count == 0)
                CompleteDaySimulation();
        }

        private void HandleGamePhaseChanged(OnGamePhaseChanged e)
        {
            if (e.NewPhase == GamePhase.DaySimulation)
            {
                BeginDaySimulation();
                return;
            }

            if (e.NewPhase != GamePhase.DaySimulation)
                _isSimulating = false;
        }

        private void BeginDaySimulation()
        {
            _isSimulating = true;
            _timer = _spawnInterval;
            _spawnedToday = 0;
            _dayStartScareQuota = 0;
            _activeCustomers.Clear();
            _customersPerDayForCurrentDay = ResolveCustomersForDay();
            _toolResolutionSystem?.ResolveDayStartTools();
            PublishFlow();
            if (!HasCustomerRoute())
            {
                EventBus<OnPrototypeLogMessage>.Publish(
                    new OnPrototypeLogMessage("Day cannot spawn customers: no customer route was generated. Check that RouteSystem entrance, checkout, and exit are on walkable floor tiles."));
            }

            EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage("Day begins: the boss may remove one vulnerable trap, then customers enter the shop."));
        }

        private void CompleteDaySimulation()
        {
            _isSimulating = false;
            _gamePhaseSystem?.CompleteDaySimulation();
            EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage("Day ends: review favorability changes and prepare for the next night."));
        }

        private bool TrySpawnCustomer()
        {
            List<GridPosition> personalRoute = BuildPersonalRoute();
            if (personalRoute == null || personalRoute.Count == 0)
            {
                EventBus<OnPrototypeLogMessage>.Publish(
                    new OnPrototypeLogMessage("Day cannot spawn customers: the current customer route is empty."));
                return false;
            }

            GridPosition start = personalRoute[0];
            var customer = new CustomerContext(_nextCustomerId++, start);
            if (_dayStartScareQuota > 0)
            {
                _dayStartScareQuota--;
                customer.HasLeftStore = true;
                customer.WasScaredAway = true;
                customer.State = CustomerState.Scared;
                EventBus<OnCustomerLeftStore>.Publish(
                    new OnCustomerLeftStore(customer.CustomerId, ToolEffectType.ScareCustomerGroup, CustomerState.Scared));
            }
            _activeCustomers.Add(new MovingCustomer(customer, personalRoute));
            _spawnedToday++;
            EventBus<OnPrototypeCustomerMoved>.Publish(
                new OnPrototypeCustomerMoved(customer.CustomerId, start, start.X, start.Y, customer.State));
            return true;
        }

        private bool HasCustomerRoute()
        {
            return _routeSystem != null && _routeSystem.CustomerRoute.Count > 0;
        }

        private List<GridPosition> BuildPersonalRoute()
        {
            IReadOnlyList<GridPosition> mainRoute = _routeSystem.CustomerRoute;

            if (Random.value >= _smallLoopRouteChance
                && _routeSystem.TryBuildRandomShoppingRoute(_minRandomWaypoints, _maxRandomWaypoints, out List<GridPosition> randomRoute))
            {
                return ValidateRouteContainsCheckout(randomRoute);
            }

            return ValidateRouteContainsCheckout(new List<GridPosition>(mainRoute));
        }

        private void AdvanceCustomer(int index, float deltaTime)
        {
            MovingCustomer moving = _activeCustomers[index];
            if (moving.Context.HasLeftStore)
            {
                AdvanceEscapingCustomer(index, ref moving, deltaTime);
                return;
            }

            IReadOnlyList<GridPosition> route = moving.PersonalRoute;
            int lastRouteIndex = route.Count - 1;
            moving.Progress = Mathf.Min(lastRouteIndex, moving.Progress + deltaTime * _cellsPerSecond);
            int reachedRouteIndex = Mathf.FloorToInt(moving.Progress);

            while (moving.RouteIndex < reachedRouteIndex)
            {
                moving.RouteIndex++;
                ResolveRouteCell(ref moving, route[moving.RouteIndex]);
                if (moving.Context.HasLeftStore)
                {
                    _activeCustomers[index] = moving;
                    return;
                }
            }

            int nextRouteIndex = Mathf.Min(moving.RouteIndex + 1, lastRouteIndex);
            float segmentProgress = moving.Progress - moving.RouteIndex;
            GridPosition from = route[moving.RouteIndex];
            GridPosition to = route[nextRouteIndex];
            float gridX = Mathf.Lerp(from.X, to.X, segmentProgress);
            float gridY = Mathf.Lerp(from.Y, to.Y, segmentProgress);

            _activeCustomers[index] = moving;
            EventBus<OnPrototypeCustomerMoved>.Publish(
                new OnPrototypeCustomerMoved(moving.Context.CustomerId, moving.Context.Position, gridX, gridY, moving.Context.State));

            if (moving.Progress >= lastRouteIndex)
            {
                if (!moving.HasPurchased)
                {
                    if (TryBuildCheckoutRecoveryRoute(moving.Context.Position, out List<GridPosition> recoveryRoute))
                    {
                        Debug.LogWarning(
                            $"[PrototypeCustomerFlowSystem] Customer {moving.Context.CustomerId} reached route end without checkout. Rerouting to checkout.");
                        moving.PersonalRoute = recoveryRoute;
                        moving.RouteIndex = 0;
                        moving.Progress = 0f;
                        _activeCustomers[index] = moving;
                        return;
                    }

                    Debug.LogError(
                        $"[PrototypeCustomerFlowSystem] Customer {moving.Context.CustomerId} reached route end without checkout and no recovery route could be built.");
                }

                RemoveCustomer(index);
            }
        }

        private void ResolveRouteCell(ref MovingCustomer moving, GridPosition position)
        {
            moving.Context.Position = position;
            _toolResolutionSystem?.ResolveCustomerEnterCell(moving.Context);
            if (moving.Context.HasLeftStore)
                return;

            _toolResolutionSystem?.ResolveCustomerPassFrontCell(moving.Context);
            if (moving.Context.HasLeftStore)
                return;

            if (!moving.HasPurchased && position.Equals(_routeSystem.Checkout))
            {
                _toolResolutionSystem?.ResolveCustomerPurchase(moving.Context);
                if (!moving.Context.HasLeftStore && !moving.Context.BoughtFakeGoods)
                    _economySystem?.RecordCustomerPurchase(moving.Context);
                moving.HasPurchased = true;
            }
        }

        private void AdvanceEscapingCustomer(int index, ref MovingCustomer moving, float deltaTime)
        {
            if (!moving.EscapeStarted)
                BeginEscape(ref moving);

            if (moving.EscapeRoute == null || moving.EscapeRoute.Count == 0)
            {
                RemoveCustomer(index);
                return;
            }

            int lastRouteIndex = moving.EscapeRoute.Count - 1;
            moving.EscapeProgress = Mathf.Min(lastRouteIndex, moving.EscapeProgress + deltaTime * _cellsPerSecond * 1.35f);
            int reachedRouteIndex = Mathf.FloorToInt(moving.EscapeProgress);
            moving.EscapeRouteIndex = Mathf.Min(reachedRouteIndex, lastRouteIndex);
            moving.Context.Position = moving.EscapeRoute[moving.EscapeRouteIndex];

            int nextRouteIndex = Mathf.Min(moving.EscapeRouteIndex + 1, lastRouteIndex);
            float segmentProgress = moving.EscapeProgress - moving.EscapeRouteIndex;
            GridPosition from = moving.EscapeRoute[moving.EscapeRouteIndex];
            GridPosition to = moving.EscapeRoute[nextRouteIndex];
            float gridX = Mathf.Lerp(from.X, to.X, segmentProgress);
            float gridY = Mathf.Lerp(from.Y, to.Y, segmentProgress);

            _activeCustomers[index] = moving;
            EventBus<OnPrototypeCustomerMoved>.Publish(
                new OnPrototypeCustomerMoved(moving.Context.CustomerId, moving.Context.Position, gridX, gridY, moving.Context.State));

            if (moving.EscapeProgress >= lastRouteIndex)
                RemoveCustomer(index);
        }

        private void BeginEscape(ref MovingCustomer moving)
        {
            moving.EscapeStarted = true;
            moving.EscapeProgress = 0f;
            moving.EscapeRouteIndex = 0;
            moving.EscapeRoute = BuildEscapeRoute(moving.Context.Position);
        }

        private List<GridPosition> BuildEscapeRoute(GridPosition from)
        {
            if (_routeSystem != null
                && _routeSystem.TryFindRoute(from, _routeSystem.Entrance, null, out List<GridPosition> route)
                && route.Count > 0)
            {
                return route;
            }

            _scratchEscapeRoute.Clear();
            _scratchEscapeRoute.Add(from);
            if (_routeSystem != null && !from.Equals(_routeSystem.Entrance))
                _scratchEscapeRoute.Add(_routeSystem.Entrance);
            return new List<GridPosition>(_scratchEscapeRoute);
        }

        private List<GridPosition> ValidateRouteContainsCheckout(List<GridPosition> route)
        {
            if (route != null && route.Contains(_routeSystem.Checkout))
                return route;

            Debug.LogWarning("[PrototypeCustomerFlowSystem] Built customer route without checkout. Falling back to main route.");
            return new List<GridPosition>(_routeSystem.CustomerRoute);
        }

        private bool TryBuildCheckoutRecoveryRoute(GridPosition from, out List<GridPosition> route)
        {
            route = null;
            if (_routeSystem == null
                || !_routeSystem.TryFindRoute(from, _routeSystem.Checkout, null, out List<GridPosition> toCheckout)
                || !_routeSystem.TryFindRoute(_routeSystem.Checkout, _routeSystem.Exit, null, out List<GridPosition> toExit))
            {
                return false;
            }

            route = new List<GridPosition>(toCheckout.Count + toExit.Count - 1);
            route.AddRange(toCheckout);
            for (int i = 1; i < toExit.Count; i++)
                route.Add(toExit[i]);
            return route.Count > 0 && route.Contains(_routeSystem.Checkout);
        }

        private void RemoveCustomer(int index)
        {
            MovingCustomer moving = _activeCustomers[index];
            int customerId = moving.Context.CustomerId;
            EventBus<OnCustomerFinalized>.Publish(
                new OnCustomerFinalized(customerId, moving.Context.State, moving.HasPurchased));
            _activeCustomers.RemoveAt(index);
            EventBus<OnPrototypeCustomerRemoved>.Publish(new OnPrototypeCustomerRemoved(customerId));
        }

        private void PublishFlow()
        {
            float trend = _customersPerDayForCurrentDay > 0
                ? -(_customersPerDayForCurrentDay - _spawnedToday) / (float)_customersPerDayForCurrentDay
                : 0f;
            EventBus<OnCustomerFlowChanged>.Publish(new OnCustomerFlowChanged(_activeCustomers.Count, _spawnedToday, trend));
        }

        private int ResolveCustomersForDay()
        {
            int min = Mathf.Max(1, _minCustomersPerDay);
            int max = Mathf.Max(min, _maxCustomersPerDay);
            return Random.Range(min, max + 1);
        }

        private void HandleGroupScareRequested(OnGroupScareRequested e)
        {
            int remaining = Mathf.Max(0, e.Count);
            if (remaining == 0)
                return;

            for (int i = 0; i < _activeCustomers.Count && remaining > 0; i++)
            {
                MovingCustomer moving = _activeCustomers[i];
                if (moving.Context.HasLeftStore)
                    continue;

                if (moving.Context.CustomerId != e.PrimaryCustomerId
                    && Distance(moving.Context.Position, e.Origin) > 2)
                {
                    continue;
                }

                moving.Context.HasLeftStore = true;
                moving.Context.WasScaredAway = true;
                moving.Context.State = CustomerState.Scared;
                EventBus<OnCustomerLeftStore>.Publish(
                    new OnCustomerLeftStore(moving.Context.CustomerId, ToolEffectType.ScareCustomerGroup, CustomerState.Scared));
                _activeCustomers[i] = moving;
                remaining--;
            }
        }

        private void HandleDayStartScareQuotaRequested(OnDayStartScareQuotaRequested e)
        {
            _dayStartScareQuota += Mathf.Max(0, e.Count);
        }

        private static int Distance(GridPosition a, GridPosition b)
        {
            return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        }

        private struct MovingCustomer
        {
            public CustomerContext Context;
            public List<GridPosition> PersonalRoute;
            public int RouteIndex;
            public float Progress;
            public bool HasPurchased;
            public bool EscapeStarted;
            public int EscapeRouteIndex;
            public float EscapeProgress;
            public List<GridPosition> EscapeRoute;

            public MovingCustomer(CustomerContext context, List<GridPosition> personalRoute)
            {
                Context = context;
                PersonalRoute = personalRoute;
                RouteIndex = 0;
                Progress = 0f;
                HasPurchased = false;
                EscapeStarted = false;
                EscapeRouteIndex = 0;
                EscapeProgress = 0f;
                EscapeRoute = null;
            }
        }
    }
}
