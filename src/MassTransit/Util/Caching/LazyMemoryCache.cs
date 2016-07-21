// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Util.Caching
{
    using System;    
    using System.Threading;
    using System.Threading.Tasks;
#if NETCORE
    using Microsoft.Extensions.Caching.Memory;
    using CacheItemPolicy = Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions;
    using CacheEntryRemovedReason = Microsoft.Extensions.Caching.Memory.EvictionReason;
#else
    using System.Runtime.Caching;
#endif


    public class LazyMemoryCache<TKey, TValue> :
        IDisposable
        where TValue : class
    {
        /// <summary>
        /// Formats a key as a string
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public delegate string KeyFormatter(TKey key);


        /// <summary>
        /// Provides the policy for the cache item
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        public delegate ICacheExpiration PolicyProvider(ICacheExpirationSelector selector);


        /// <summary>
        /// Returns the value for a key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public delegate Task<TValue> ValueFactory(TKey key);


        /// <summary>
        /// The callback when an item is removed from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        public delegate Task ValueRemoved(string key, TValue value, string reason);


#if NETCORE
        readonly IMemoryCache _cache;
#else
        readonly MemoryCache _cache;
#endif
        readonly KeyFormatter _keyFormatter;
        readonly PolicyProvider _policyProvider;

        readonly LimitedConcurrencyLevelTaskScheduler _scheduler;
        readonly ValueFactory _valueFactory;
        readonly ValueRemoved _valueRemoved;

        public LazyMemoryCache(string name, ValueFactory valueFactory, PolicyProvider policyProvider = null, KeyFormatter keyFormatter = null,
            ValueRemoved valueRemoved = null)
        {
            _valueFactory = valueFactory;
            _policyProvider = policyProvider ?? DefaultPolicyProvider;
            _keyFormatter = keyFormatter ?? DefaultKeyFormatter;
            _valueRemoved = valueRemoved ?? DefaultValueRemoved;

#if NETCORE
            _cache = new MemoryCache(new MemoryCacheOptions());
#else
            _cache = new MemoryCache(name);
#endif
            _scheduler = new LimitedConcurrencyLevelTaskScheduler(1);
        }

        public void Dispose()
        {
            Task.Factory.StartNew(() => _cache.Dispose(), CancellationToken.None, TaskCreationOptions.HideScheduler, _scheduler);
        }

        Task DefaultValueRemoved(string key, TValue value, string reason)
        {
            return TaskUtil.Completed;
        }

        static string DefaultKeyFormatter(TKey key)
        {
            return key.ToString();
        }

        static ICacheExpiration DefaultPolicyProvider(ICacheExpirationSelector selector)
        {
            return selector.None();
        }

        void Touch(string textKey)
        {
            Task.Factory.StartNew(() => _cache.Get(textKey), CancellationToken.None, TaskCreationOptions.HideScheduler, _scheduler);
        }

        public Task<Cached<TValue>> Get(TKey key)
        {
            return Task.Factory.StartNew(() =>
            {
                var textKey = _keyFormatter(key);

                var result = _cache.Get(textKey) as Cached<TValue>;
                if (result != null)
                {
                    if (!result.Value.IsFaulted && !result.Value.IsCanceled)
                        return result;

                    _cache.Remove(textKey);
                }

                var cacheItemValue = new CachedValue(_valueFactory, key, () => Touch(textKey));
                var cacheItemPolicy = _policyProvider(new CacheExpirationSelector(key)).Policy;

#if NETCORE
                cacheItemPolicy.RegisterPostEvictionCallback(OnCacheItemRemoved);
                var added = _cache.Set(textKey, cacheItemValue, cacheItemPolicy) != null;
#else                
                var cacheItem = new CacheItem(textKey, cacheItemValue);                
                cacheItemPolicy.RemovedCallback = OnCacheItemRemoved;
                var added = _cache.Add(cacheItem, cacheItemPolicy);
#endif
                if (!added)
                {
                    throw new InvalidOperationException($"The item was not added to the cache: {key}");
                }

                return cacheItemValue;
            }, CancellationToken.None, TaskCreationOptions.HideScheduler, _scheduler);
        }

#if NETCORE
        void OnCacheItemRemoved(object key, object value, EvictionReason reason, object substate)
        {
            var existingItem = value as CachedValue;
            if (existingItem?.IsValueCreated ?? false)
            {
                CleanupCacheItem(existingItem.Value, key as string, reason);
            }
        }
#else
        void OnCacheItemRemoved(CacheEntryRemovedArguments arguments)
        {
            var existingItem = arguments.CacheItem.Value as CachedValue;
            if (existingItem?.IsValueCreated ?? false)
            {
                CleanupCacheItem(existingItem.Value, arguments.CacheItem.Key, arguments.RemovedReason);
            }
        }
#endif

        async void CleanupCacheItem(Task<TValue> valueTask, string textKey, CacheEntryRemovedReason reason)
        {
            var value = await valueTask.ConfigureAwait(false);

            await _valueRemoved(textKey, value, reason.ToString()).ConfigureAwait(false);

            var disposable = value as IDisposable;
            disposable?.Dispose();

            var asyncDisposable = value as IAsyncDisposable;
            if (asyncDisposable != null)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }

        class CachedValue :
            Cached<TValue>
        {
            readonly Action _touch;
            readonly Lazy<Task<TValue>> _value;

            public CachedValue(ValueFactory valueFactory, TKey key, Action touch)
            {
                _touch = touch;

                _value = new Lazy<Task<TValue>>(() => valueFactory(key), LazyThreadSafetyMode.PublicationOnly);
            }

            public bool IsValueCreated => _value.IsValueCreated;

            public Task<TValue> Value => _value.Value;

            public void Touch()
            {
                _touch();
            }
        }


        public interface ICacheExpiration
        {
            CacheItemPolicy Policy { get; }
        }


        public interface ICacheExpirationSelector
        {
            /// <summary>
            /// The key for the cache item
            /// </summary>
            TKey Key { get; }

            /// <summary>
            /// The cache item should expire after being usused for the specified time period
            /// </summary>
            /// <param name="expiration"></param>
            /// <returns></returns>
            ICacheExpiration SlidingWindow(TimeSpan expiration);

            /// <summary>
            /// The cache item should expire after a set time period
            /// </summary>
            /// <param name="expiration"></param>
            /// <returns></returns>
            ICacheExpiration Absolute(DateTimeOffset expiration);

            /// <summary>
            /// The cache item should never expire
            /// </summary>
            /// <returns></returns>
            ICacheExpiration None();
        }


        class CacheExpirationSelector :
            ICacheExpirationSelector
        {
            public CacheExpirationSelector(TKey key)
            {
                Key = key;
            }

            public TKey Key { get; }

            public ICacheExpiration SlidingWindow(TimeSpan expiration)
            {
                return new CacheExpiration(new CacheItemPolicy
                {
                    SlidingExpiration = expiration
                });
            }

            public ICacheExpiration Absolute(DateTimeOffset expiration)
            {
                return new CacheExpiration(new CacheItemPolicy
                {
                    AbsoluteExpiration = expiration
                });
            }

            public ICacheExpiration None()
            {
                return new CacheExpiration(new CacheItemPolicy());
            }

            class CacheExpiration :
                ICacheExpiration
            {
                public CacheExpiration(CacheItemPolicy policy)
                {
                    Policy = policy;
                }

                public CacheItemPolicy Policy { get; }
            }
        }
    }
}