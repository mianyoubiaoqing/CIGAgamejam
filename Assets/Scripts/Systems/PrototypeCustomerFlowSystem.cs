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
        [SerializeField, Min(1)] private int _customersPerDay = 5;
        [SerializeField, Min(0.1f)] private float _spawnInterval = 0.85f;
        [SerializeField, Min(0.1f)] private float _cellsPerSecond = 1.176f;

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
            EventBus<OnRevenueChanged>.Subscribe(HandleRevenueChanged);
            EventBus<OnGroupScareRequested>.Subscribe(HandleGroupScareRequested);
            EventBus<OnDayStartScareQuotaRequested>.Subscribe(HandleDayStartScareQuotaRequested);
        }

        private void OnDestroy()
        {
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
            EventBus<OnRevenueChanged>.Unsubscribe(HandleRevenueChanged);
            EventBus<OnGroupScareRequested>.Unsubscribe(HandleGroupScareRequested);
            EventBus<OnDayStartScareQuotaRequested>.Unsubscribe(HandleDayStartScareQuotaRequested);
        }

private void Update()
        {
            if (!_isSimulating || _routeSystem == null || _routeSystem.CustomerRoute.Count == 0)
                return;

            float deltaTime = Time.deltaTime;
            _timer += deltaTime;
            while (_spawnedToday < _customersPerDayForCurrentDay && _timer >= _spawnInterval)
            {
                _timer -= _spawnInterval;
                SpawnCustomer();
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
            _timer = 0f;
            _spawnedToday = 0;
            _dayStartScareQuota = 0;
            _activeCustomers.Clear();
            _customersPerDayForCurrentDay = Mathf.Max(1, ResolveCustomersForCurrentFavorability());
            _toolResolutionSystem?.ResolveDayStartTools();
            PublishFlow();
            EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage("进入白天: 老板先随机破坏一个可破坏陷阱，顾客开始进店。"));
        }

private void CompleteDaySimulation()
        {
            _isSimulating = false;
            _gamePhaseSystem?.CompleteDaySimulation();
            EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage("白天结束: 查看信心变化，准备进入下一夜。"));
        }

private void SpawnCustomer()
        {
            GridPosition start = _routeSystem.CustomerRoute[0];
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
            _activeCustomers.Add(new MovingCustomer(customer));
            _spawnedToday++;
            EventBus<OnPrototypeCustomerMoved>.Publish(
                new OnPrototypeCustomerMoved(customer.CustomerId, start, start.X, start.Y, customer.State));
        }

private void AdvanceCustomer(int index, float deltaTime)
        {
            MovingCustomer moving = _activeCustomers[index];
            if (moving.Context.HasLeftStore)
            {
                AdvanceEscapingCustomer(index, ref moving, deltaTime);
                return;
            }

            IReadOnlyList<GridPosition> route = _routeSystem.CustomerRoute;
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
                    _economySystem?.RecordCustomerPurchase(moving.Context);
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

        private void HandleRevenueChanged(OnRevenueChanged e)
        {
            _customersPerDayForCurrentDay = Mathf.Max(1, ResolveCustomersForFavorability(e.CurrentRevenueIndex));
        }

        private int ResolveCustomersForCurrentFavorability()
        {
            return _economySystem != null
                ? ResolveCustomersForFavorability(_economySystem.CurrentRevenueIndex)
                : _customersPerDay;
        }

        private int ResolveCustomersForFavorability(float favorability)
        {
            if (favorability < 35f)
                return Mathf.Max(1, Mathf.RoundToInt(_customersPerDay * 0.4f));

            if (favorability < 65f)
                return Mathf.Max(1, Mathf.RoundToInt(_customersPerDay * 0.7f));

            return _customersPerDay;
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
            public int RouteIndex;
            public float Progress;
            public bool HasPurchased;
            public bool EscapeStarted;
            public int EscapeRouteIndex;
            public float EscapeProgress;
            public List<GridPosition> EscapeRoute;

            public MovingCustomer(CustomerContext context)
            {
                Context = context;
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
