using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Utils
{
    public static class EntityIdUtils
    {
        public static string ToWireId(UnityEngine.Object obj)
        {
            return obj == null ? null : EntityId.ToULong(obj.GetEntityId()).ToString();
        }

        public static bool TryRead(JToken token, out EntityId entityId)
        {
            entityId = EntityId.None;

            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            try
            {
                ulong raw = token.Type == JTokenType.String
                    ? ulong.Parse(token.ToObject<string>())
                    : token.ToObject<ulong>();

                entityId = EntityId.FromULong(raw);
                return entityId.IsValid();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static T ObjectFromToken<T>(JToken token) where T : UnityEngine.Object
        {
            return TryRead(token, out EntityId entityId)
                ? EditorUtility.EntityIdToObject(entityId) as T
                : null;
        }
    }
}
