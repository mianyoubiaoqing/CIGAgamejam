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
        [SerializeField, Min(1)] private int _customersPerDay = 8;
        [SerializeField, Min(0.1f)] private float _stepSeconds = 0.65f;

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

            _timer += Time.deltaTime;
            if (_timer < _stepSeconds)
                return;

            _timer = 0f;
            StepSimulation();
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

        private void StepSimulation()
        {
            if (_spawnedToday < _customersPerDay)
                SpawnCustomer();

            for (int i = _activeCustomers.Count - 1; i >= 0; i--)
                MoveCustomer(i);

            PublishFlow();

            if (_spawnedToday >= _customersPerDay && _activeCustomers.Count == 0)
            {
                _isSimulating = false;
                _gamePhaseSystem?.CompleteDaySimulation();
                EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage("白天结束: 查看信心变化，准备进入下一夜。"));
            }
        }

        private void SpawnCustomer()
        {
            GridPosition start = _routeSystem.CustomerRoute[0];
            var customer = new CustomerContext(_nextCustomerId++, start);
            _activeCustomers.Add(new MovingCustomer(customer, 0));
            _spawnedToday++;
            EventBus<OnPrototypeCustomerMoved>.Publish(new OnPrototypeCustomerMoved(customer.CustomerId, start));
        }

        private void MoveCustomer(int index)
        {
            MovingCustomer moving = _activeCustomers[index];
            if (moving.Context.HasLeftStore)
            {
                RemoveCustomer(index);
                return;
            }

            moving.RouteIndex++;
            if (moving.RouteIndex >= _routeSystem.CustomerRoute.Count)
            {
                _economySystem?.RecordCustomerPurchase(moving.Context);
                RemoveCustomer(index);
                return;
            }

            moving.Context.Position = _routeSystem.CustomerRoute[moving.RouteIndex];
            _toolResolutionSystem?.ResolveCustomerEnterCell(moving.Context);
            _toolResolutionSystem?.ResolveCustomerPassFrontCell(moving.Context);

            if (moving.Context.Position.Equals(_routeSystem.Checkout))
            {
                _toolResolutionSystem?.ResolveCustomerPurchase(moving.Context);
                _economySystem?.RecordCustomerPurchase(moving.Context);
            }

            if (moving.Context.HasLeftStore)
            {
                RemoveCustomer(index);
                return;
            }

            _activeCustomers[index] = moving;
            EventBus<OnPrototypeCustomerMoved>.Publish(
                new OnPrototypeCustomerMoved(moving.Context.CustomerId, moving.Context.Position));
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

            public MovingCustomer(CustomerContext context, int routeIndex)
            {
                Context = context;
                RouteIndex = routeIndex;
            }
        }
    }
}
