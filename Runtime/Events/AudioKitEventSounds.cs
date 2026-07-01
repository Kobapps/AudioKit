using System;
using System.Collections.Generic;
using UnityEngine;
using AK = Kobapps.AudioKit.AudioKit;

namespace Kobapps.AudioKit
{
    /// <summary>The Unity event that fires a trigger entry.</summary>
    public enum AudioEventTrigger
    {
        Awake = 0,
        Start = 1,
        OnEnable = 2,
        OnDisable = 3,
        OnDestroy = 4,

        CollisionEnter = 10,
        CollisionExit = 11,
        TriggerEnter = 12,
        TriggerExit = 13,

        CollisionEnter2D = 20,
        CollisionExit2D = 21,
        TriggerEnter2D = 22,
        TriggerExit2D = 23,

        ParticleCollision = 30,

        /// <summary>Fires when a named AudioKit custom event is raised.</summary>
        CustomEvent = 40,
    }

    /// <summary>One trigger and the ordered list of actions it performs.</summary>
    [Serializable]
    public class AudioTriggerEntry
    {
        [Tooltip("The Unity event that fires this entry (lifecycle, 2D/3D physics, particle, or a custom event).")]
        public AudioEventTrigger trigger = AudioEventTrigger.Start;

        [Tooltip("Custom-event name to listen for (only used when trigger is CustomEvent).")]
        public string customEventName = "";

        [Tooltip("Actions run, in order, when this trigger fires.")]
        public List<AudioAction> actions = new List<AudioAction>();
    }

    /// <summary>
    /// Master-Audio-style "Event Sounds": maps Unity lifecycle, physics and custom-event triggers to
    /// declarative AudioKit actions (play/stop groups, fade/mute/duck buses, fire custom events).
    /// Custom-event listeners are subscribed on enable and released on disable; the cached delegates
    /// are built once so enabling/disabling never allocates.
    /// </summary>
    [AddComponentMenu("AudioKit/AudioKit Event Sounds")]
    public sealed class AudioKitEventSounds : MonoBehaviour
    {
        [Tooltip("Trigger → actions mappings. Each entry runs its actions when its trigger fires.")]
        [SerializeField] private List<AudioTriggerEntry> entries = new List<AudioTriggerEntry>();

        // Cached per-entry delegates for custom-event (un)subscription — built once, no per-enable alloc.
        private Action[] _customHandlers;

        public List<AudioTriggerEntry> Entries => entries;

        private void Awake()
        {
            BuildCustomHandlers();
            Dispatch(AudioEventTrigger.Awake);
        }

        private void Start() => Dispatch(AudioEventTrigger.Start);

        private void OnEnable()
        {
            SubscribeCustomEvents();
            Dispatch(AudioEventTrigger.OnEnable);
        }

        private void OnDisable()
        {
            Dispatch(AudioEventTrigger.OnDisable);
            UnsubscribeCustomEvents();
        }

        private void OnDestroy() => Dispatch(AudioEventTrigger.OnDestroy);

        // --- Physics (3D) -----------------------------------------------------------------------
        private void OnCollisionEnter(Collision _) => Dispatch(AudioEventTrigger.CollisionEnter);
        private void OnCollisionExit(Collision _) => Dispatch(AudioEventTrigger.CollisionExit);
        private void OnTriggerEnter(Collider _) => Dispatch(AudioEventTrigger.TriggerEnter);
        private void OnTriggerExit(Collider _) => Dispatch(AudioEventTrigger.TriggerExit);

        // --- Physics (2D) -----------------------------------------------------------------------
        private void OnCollisionEnter2D(Collision2D _) => Dispatch(AudioEventTrigger.CollisionEnter2D);
        private void OnCollisionExit2D(Collision2D _) => Dispatch(AudioEventTrigger.CollisionExit2D);
        private void OnTriggerEnter2D(Collider2D _) => Dispatch(AudioEventTrigger.TriggerEnter2D);
        private void OnTriggerExit2D(Collider2D _) => Dispatch(AudioEventTrigger.TriggerExit2D);

        // --- Particles --------------------------------------------------------------------------
        private void OnParticleCollision(GameObject _) => Dispatch(AudioEventTrigger.ParticleCollision);

        /// <summary>Run every action of every entry registered for <paramref name="trigger"/>.</summary>
        public void Dispatch(AudioEventTrigger trigger)
        {
            for (int e = 0; e < entries.Count; e++)
            {
                var entry = entries[e];
                if (entry == null || entry.trigger != trigger)
                    continue;
                RunActions(entry);
            }
        }

        private void RunActions(AudioTriggerEntry entry)
        {
            var list = entry.actions;
            for (int i = 0; i < list.Count; i++)
                list[i]?.Execute(transform);
        }

        private void BuildCustomHandlers()
        {
            _customHandlers = new Action[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.trigger == AudioEventTrigger.CustomEvent)
                    _customHandlers[i] = () => RunActions(entry); // built once, cached
            }
        }

        private void SubscribeCustomEvents()
        {
            if (_customHandlers == null) BuildCustomHandlers();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.trigger == AudioEventTrigger.CustomEvent && _customHandlers[i] != null)
                    AK.SubscribeEvent(entry.customEventName, _customHandlers[i]);
            }
        }

        private void UnsubscribeCustomEvents()
        {
            if (_customHandlers == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.trigger == AudioEventTrigger.CustomEvent && _customHandlers[i] != null)
                    AK.UnsubscribeEvent(entry.customEventName, _customHandlers[i]);
            }
        }
    }
}
