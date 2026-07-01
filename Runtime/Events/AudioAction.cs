using System;
using Kobapps.AudioKit.Core;
using UnityEngine;
// Disambiguate the static facade type from the enclosing namespace segment for internal code.
using AK = Kobapps.AudioKit.AudioKit;

namespace Kobapps.AudioKit
{
    /// <summary>What an <see cref="AudioKitEventSounds"/> trigger does.</summary>
    public enum AudioActionKind
    {
        PlayGroup = 0,
        StopGroup = 1,
        FadeBus = 2,
        SetBusVolume = 3,
        MuteBus = 4,
        UnmuteBus = 5,
        DuckBus = 6,
        UnduckBus = 7,
        PauseAll = 8,
        UnpauseAll = 9,
        FireCustomEvent = 10,
    }

    /// <summary>How a <see cref="AudioActionKind.PlayGroup"/> action positions its voice.</summary>
    public enum PlaySpatialMode
    {
        TwoD = 0,
        AtThisObject = 1,
        FollowThisObject = 2,
    }

    /// <summary>One declarative action executed when a trigger fires. Serialized for the inspector.</summary>
    [Serializable]
    public class AudioAction
    {
        [Tooltip("What this action does (play/stop a group, fade/mute/duck a bus, fire a custom event, …).")]
        public AudioActionKind kind = AudioActionKind.PlayGroup;

        [Tooltip("Group name, bus name or custom-event name depending on the action kind.")]
        public string target = "";

        [Tooltip("Volume for PlayGroup / SetBusVolume / FadeBus (0–1).")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Duration in seconds for FadeBus / StopGroup fade-out.")]
        public float seconds = 0f;

        [Tooltip("Curve used by FadeBus.")]
        public FadeCurve curve = FadeCurve.Linear;

        [Tooltip("For PlayGroup: 2D, at this object's position, or following this object.")]
        public PlaySpatialMode spatial = PlaySpatialMode.TwoD;

        public void Execute(Transform context)
        {
            switch (kind)
            {
                case AudioActionKind.PlayGroup:
                    var id = AudioId.From(target);
                    switch (spatial)
                    {
                        case PlaySpatialMode.AtThisObject:
                            AK.PlayAt(id, context != null ? context.position : Vector3.zero, volume);
                            break;
                        case PlaySpatialMode.FollowThisObject:
                            AK.PlayFollow(id, context, volume);
                            break;
                        default:
                            AK.Play(id, volume);
                            break;
                    }
                    break;
                case AudioActionKind.StopGroup:
                    AK.StopGroup(target, seconds);
                    break;
                case AudioActionKind.FadeBus:
                    AK.FadeBus(target, volume, seconds, curve);
                    break;
                case AudioActionKind.SetBusVolume:
                    AK.SetBusVolume(target, volume);
                    break;
                case AudioActionKind.MuteBus:
                    AK.MuteBus(target, true);
                    break;
                case AudioActionKind.UnmuteBus:
                    AK.MuteBus(target, false);
                    break;
                case AudioActionKind.DuckBus:
                    AK.DuckBus(target);
                    break;
                case AudioActionKind.UnduckBus:
                    AK.UnduckBus(target);
                    break;
                case AudioActionKind.PauseAll:
                    AK.PauseAll();
                    break;
                case AudioActionKind.UnpauseAll:
                    AK.UnpauseAll();
                    break;
                case AudioActionKind.FireCustomEvent:
                    AK.FireEvent(target);
                    break;
            }
        }
    }
}
