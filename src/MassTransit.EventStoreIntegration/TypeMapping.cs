using System;
using System.Collections.Concurrent;

namespace MassTransit.EventStoreIntegration
{
    /// <summary>
    /// Event resolve type cache. Can be used in "default" mode when assembly name
    /// is used to compose the full type name, or in static resolve mode
    /// when you can define own mappings to be more implementation agnostic
    /// </summary>
    public static class TypeMapping
    {
        /// <summary>
        /// Add type to cache
        /// </summary>
        /// <param name="key">Type key</param>
        /// <param name="type">Type</param>
        public static void Add(string key, Type type)
        {
            Cached.Instance.TryAdd(key, type);
            Cached.ReverseInstance.TryAdd(type, key);
        }

        /// <summary>
        /// Add type to cache
        /// </summary>
        /// <param name="key">Type key</param>
        /// <typeparam name="T">Type</typeparam>
        public static void Add<T>(string key) => Add(key, typeof(T));

        /// <summary>
        /// Retrieve type from cache or find using reflections
        /// </summary>
        /// <param name="key">Type key or type name</param>
        /// <param name="assemblyNames">Optional: assembly names where to search the type in</param>
        /// <returns></returns>
        public static Type Get(string key) => Cached.Instance.ContainsKey(key) ? Cached.Instance[key] : null;

        /// <summary>
        /// Get the type name, either from mapping or full assembly name
        /// </summary>
        /// <param name="type">Type that you need a name for</param>
        /// <returns>Type name string</returns>
        public static string GetTypeName(Type type) =>
            Cached.ReverseInstance.ContainsKey(type)
                ? Cached.ReverseInstance[type]
                : type.FullName;

        private static class Cached
        {
            internal static readonly ConcurrentDictionary<string, Type> Instance =
                new ConcurrentDictionary<string, Type>();

            internal static readonly ConcurrentDictionary<Type, string> ReverseInstance =
                new ConcurrentDictionary<Type, string>();
        }
    }
}
