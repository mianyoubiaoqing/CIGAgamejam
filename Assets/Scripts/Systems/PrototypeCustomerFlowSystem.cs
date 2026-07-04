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
        private float _timer;
        private int _nextCustomerId = 1;
        private int _spawnedToday;
        private bool _isSimulating;

        private void OnEnable()
        {
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
        }

        private void OnDestroy()
        {
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
        }

private void Update()
        {
            if (!_isSimulating || _routeSystem == null || _routeSystem.CustomerRoute.Count == 0)
                return;

            float deltaTime = Time.deltaTime;
            _timer += deltaTime;
            while (_spawnedToday < _customersPerDay && _timer >= _spawnInterval)
            {
                _timer -= _spawnInterval;
                SpawnCustomer();
            }

            for (int i = _activeCustomers.Count - 1; i >= 0; i--)
                AdvanceCustomer(i, deltaTime);

            PublishFlow();

            if (_spawnedToday >= _customersPerDay && _activeCustomers.Count == 0)
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
            _activeCustomers.Clear();
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
            _activeCustomers.Add(new MovingCustomer(customer));
            _spawnedToday++;
            EventBus<OnPrototypeCustomerMoved>.Publish(
                new OnPrototypeCustomerMoved(customer.CustomerId, start, start.X, start.Y));
        }

private void AdvanceCustomer(int index, float deltaTime)
        {
            MovingCustomer moving = _activeCustomers[index];
            if (moving.Context.HasLeftStore)
            {
                RemoveCustomer(index);
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
                    RemoveCustomer(index);
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
                new OnPrototypeCustomerMoved(moving.Context.CustomerId, moving.Context.Position, gridX, gridY));

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
            _toolResolutionSystem?.ResolveCustomerPassFrontCell(moving.Context);

            if (!moving.HasPurchased && position.Equals(_routeSystem.Checkout))
            {
                _toolResolutionSystem?.ResolveCustomerPurchase(moving.Context);
                if (!moving.Context.HasLeftStore)
                    _economySystem?.RecordCustomerPurchase(moving.Context);
                moving.HasPurchased = true;
            }
        }

        private void RemoveCustomer(int index)
        {
            int customerId = _activeCustomers[index].Context.CustomerId;
            _activeCustomers.RemoveAt(index);
            EventBus<OnPrototypeCustomerRemoved>.Publish(new OnPrototypeCustomerRemoved(customerId));
        }

        private void PublishFlow()
        {
            float trend = _customersPerDay > 0 ? -(_customersPerDay - _spawnedToday) / (float)_customersPerDay : 0f;
            EventBus<OnCustomerFlowChanged>.Publish(new OnCustomerFlowChanged(_activeCustomers.Count, _spawnedToday, trend));
        }

        private struct MovingCustomer
        {
            public CustomerContext Context;
            public int RouteIndex;
            public float Progress;
            public bool HasPurchased;

            public MovingCustomer(CustomerContext context)
            {
                Context = context;
                RouteIndex = 0;
                Progress = 0f;
                HasPurchased = false;
            }
        }
    }
}
