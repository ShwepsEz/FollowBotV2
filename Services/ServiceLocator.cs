using System;
using System.Collections.Generic;

namespace FollowBotV2.Services
{
    public class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return service as T;
            throw new InvalidOperationException($"Service {typeof(T)} not registered.");
        }
    }
}