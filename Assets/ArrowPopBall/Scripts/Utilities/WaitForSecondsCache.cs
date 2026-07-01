using System.Collections.Generic;
using UnityEngine;

namespace Game.Utilities
{
    /// <summary>
    /// WaitForSeconds 캐시 - 동일 시간값의 WaitForSeconds 재사용으로 GC 할당 방지
    /// </summary>
    public static class WaitForSecondsCache
    {
        private static readonly Dictionary<float, WaitForSeconds> _cache = new Dictionary<float, WaitForSeconds>();

        /// <summary>
        /// 캐시된 WaitForSeconds 반환 (없으면 생성 후 캐시)
        /// </summary>
        public static WaitForSeconds Get(float seconds)
        {
            if (!_cache.TryGetValue(seconds, out var wait))
            {
                wait = new WaitForSeconds(seconds);
                _cache[seconds] = wait;
            }
            return wait;
        }
    }
}
