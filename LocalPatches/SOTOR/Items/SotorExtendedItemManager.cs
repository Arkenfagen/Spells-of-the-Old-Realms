using System;
using System.Collections.Generic;
using SOTOR.Items;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace SOTOR.Items
{

    public static class SotorExtendedItemManager
    {
        private static readonly Dictionary<string, List<string>> _itemTraits =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private static readonly object Gate = new object();

        public static bool HasTraits(ItemObject item)
        {
            if (item == null) return false;
            lock (Gate) { return _itemTraits.ContainsKey(item.StringId); }
        }

        private static readonly Dictionary<string, bool> _lockIdCache =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private static string[] _modifierSuffixes;
        private static int _cacheStamp;

        public static bool HasTraitsByLockId(string lockId)
        {
            if (string.IsNullOrEmpty(lockId)) return false;
            if (HasTraitsById(lockId)) return true;

            lock (Gate)
            {
                if (_cacheStamp != _itemTraits.Count)
                {
                    _lockIdCache.Clear();
                    _cacheStamp = _itemTraits.Count;
                }
                if (_lockIdCache.TryGetValue(lockId, out bool cached)) return cached;
            }

            bool found = false;
            try
            {
                if (_modifierSuffixes == null)
                {
                    var list = new List<string>();
                    foreach (var m in MBObjectManager.Instance.GetObjectTypeList<ItemModifier>())
                        if (m != null && !string.IsNullOrEmpty(m.StringId)) list.Add(m.StringId);
                    _modifierSuffixes = list.ToArray();
                }
                foreach (var suffix in _modifierSuffixes)
                {
                    if (lockId.Length <= suffix.Length) continue;
                    if (!lockId.EndsWith(suffix, StringComparison.Ordinal)) continue;
                    if (HasTraitsById(lockId.Substring(0, lockId.Length - suffix.Length)))
                    {
                        found = true;
                        break;
                    }
                }
            }
            catch { }

            lock (Gate) { _lockIdCache[lockId] = found; }
            return found;
        }

        public static bool HasTraitsById(string itemId)
        {
            if (itemId == null) return false;
            lock (Gate) { return _itemTraits.ContainsKey(itemId); }
        }

        public static List<SotorItemTrait> GetTraitsOfItem(ItemObject item)
        {
            List<string> ids;
            lock (Gate)
            {
                if (item == null || !_itemTraits.TryGetValue(item.StringId, out var stored))
                    return new List<SotorItemTrait>();

                ids = new List<string>(stored);
            }
            return SotorItemTraitManager.GetTraits(ids);
        }

        public static List<string> GetTraitIdsOfItem(string itemId)
        {
            lock (Gate)
            {
                return _itemTraits.TryGetValue(itemId ?? "", out var ids)
                    ? new List<string>(ids)
                    : new List<string>();
            }
        }

        public static void RegisterItemTraits(string itemId, IEnumerable<string> traitIds)
        {
            if (string.IsNullOrEmpty(itemId) || traitIds == null) return;
            var list = new List<string>();
            foreach (var id in traitIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (SotorItemTraitManager.GetTrait(id) == null)
                {
                    SotorLog.Warn($"RegisterItemTraits: unknown trait '{id}' on item {itemId}");
                    continue;
                }
                if (!list.Contains(id)) list.Add(id);
            }
            if (list.Count == 0) return;

            lock (Gate) { _itemTraits[itemId] = list; }
        }

        public static void UnregisterItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            lock (Gate) { _itemTraits.Remove(itemId); }
        }

        public static IEnumerable<string> AllRegisteredItemIds
        {
            get { lock (Gate) { return new List<string>(_itemTraits.Keys); } }
        }

        public static void Clear()
        {
            lock (Gate) { _itemTraits.Clear(); }
        }
    }
}
