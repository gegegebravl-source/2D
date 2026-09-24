using System;
using System.Collections.Generic;

namespace EXFIL.Core
{
    /// <summary>
    /// Tiny service locator. Systems register themselves on boot (see GameBootstrap),
    /// gameplay code asks for them by type. Keeps modules decoupled and testable.
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private static readonly Dictionary<Type, object> _pending = new Dictionary<Type, object>();

        public static void Register<T>(T instance) where T : class
        {
            _services[typeof(T)] = instance;
        }

        public static T Get<T>() where T : class
        {
            object value;
            if (_services.TryGetValue(typeof(T), out value))
                return (T)value;

            object pending;
            if (_pending.TryGetValue(typeof(T), out pending))
                return (T)pending;

            throw new InvalidOperationException(
                "[EXFIL] Service " + typeof(T).Name + " is not registered. " +
                "Check GameBootstrap registration order.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            object value;
            if (_services.TryGetValue(typeof(T), out value))
            {
                service = (T)value;
                return true;
            }
            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        public static void Clear()
        {
            _services.Clear();
            _pending.Clear();
        }
    }
}
