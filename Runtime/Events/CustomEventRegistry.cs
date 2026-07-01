using System;
using System.Collections.Generic;
using Kobapps.AudioKit.Core;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// Lightweight pub/sub keyed by hashed <see cref="AudioId"/>. Subscriber lists are array-backed
    /// and firing iterates by index (no enumerator, no closure), so <see cref="Fire"/> allocates
    /// nothing. Safe against re-entrant subscribe/unsubscribe during a fire: a snapshot count is used
    /// and removals are tolerated.
    /// </summary>
    public sealed class CustomEventRegistry
    {
        private sealed class Subscribers
        {
            public Action[] Handlers = new Action[4];
            public int Count;

            public void Add(Action h)
            {
                if (Count == Handlers.Length)
                    Array.Resize(ref Handlers, Handlers.Length * 2);
                Handlers[Count++] = h;
            }

            public bool Remove(Action h)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (Handlers[i] == h)
                    {
                        // preserve order isn't important; compact by shifting tail down
                        for (int j = i; j < Count - 1; j++)
                            Handlers[j] = Handlers[j + 1];
                        Handlers[--Count] = null;
                        return true;
                    }
                }
                return false;
            }
        }

        private readonly Dictionary<uint, Subscribers> _map = new Dictionary<uint, Subscribers>(32);

        public void Subscribe(AudioId id, Action handler)
        {
            if (handler == null || id.IsNone) return;
            if (!_map.TryGetValue(id.Value, out var subs))
            {
                subs = new Subscribers();
                _map[id.Value] = subs;
            }
            subs.Add(handler);
        }

        public void Unsubscribe(AudioId id, Action handler)
        {
            if (handler == null || id.IsNone) return;
            if (_map.TryGetValue(id.Value, out var subs))
                subs.Remove(handler);
        }

        /// <summary>Invoke every subscriber for <paramref name="id"/>. Allocation free.</summary>
        public void Fire(AudioId id)
        {
            if (id.IsNone) return;
            if (!_map.TryGetValue(id.Value, out var subs))
                return;

            // Snapshot the count so handlers added during dispatch don't fire this round, and
            // guard each slot against a handler that unsubscribed a later one.
            int count = subs.Count;
            for (int i = 0; i < count && i < subs.Count; i++)
            {
                var h = subs.Handlers[i];
                if (h != null)
                    h.Invoke();
            }
        }

        public void Clear() => _map.Clear();

        public int SubscriberCount(AudioId id) =>
            _map.TryGetValue(id.Value, out var subs) ? subs.Count : 0;
    }
}
