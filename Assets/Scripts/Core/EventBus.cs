using System;
using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public static class EventBus<T> where T : struct
    {
        private static readonly List<Action<T>> _handlers = new();
        private static readonly List<Action<T>> _pendingSubscribe = new();
        private static readonly List<Action<T>> _pendingUnsubscribe = new();
        private static bool _publishing;

        public static void Subscribe(Action<T> handler)
        {
            if (handler == null) return;

            if (_publishing)
            {
                _pendingUnsubscribe.Remove(handler);
                if (!_handlers.Contains(handler) && !_pendingSubscribe.Contains(handler))
                    _pendingSubscribe.Add(handler);
                return;
            }

            if (!_handlers.Contains(handler))
                _handlers.Add(handler);
        }

        public static void Unsubscribe(Action<T> handler)
        {
            if (handler == null) return;

            if (_publishing)
            {
                _pendingSubscribe.Remove(handler);
                if (_handlers.Contains(handler) && !_pendingUnsubscribe.Contains(handler))
                    _pendingUnsubscribe.Add(handler);
                return;
            }

            _handlers.Remove(handler);
        }

        public static void Publish(T eventData)
        {
            _publishing = true;
            for (int i = 0; i < _handlers.Count; i++)
            {
                Action<T> handler = _handlers[i];
                if (_pendingUnsubscribe.Contains(handler))
                    continue;

                try
                {
                    handler.Invoke(eventData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus<{typeof(T).Name}>] Handler threw: {e}");
                }
            }

            _publishing = false;

            for (int i = 0; i < _pendingUnsubscribe.Count; i++)
                _handlers.Remove(_pendingUnsubscribe[i]);
            _pendingUnsubscribe.Clear();

            for (int i = 0; i < _pendingSubscribe.Count; i++)
            {
                Action<T> handler = _pendingSubscribe[i];
                if (!_handlers.Contains(handler))
                    _handlers.Add(handler);
            }
            _pendingSubscribe.Clear();
        }
    }
}
