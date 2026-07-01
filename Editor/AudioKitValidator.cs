using System.Collections.Generic;
using UnityEngine;

namespace Kobapps.AudioKit.Editor
{
    public enum ValidationSeverity { Info, Warning, Error }

    /// <summary>One finding from the validation gate.</summary>
    public struct ValidationIssue
    {
        public ValidationSeverity Severity;
        public string Message;
        public int GroupIndex;   // -1 if not group-scoped
        public int BusIndex;     // -1 if not bus-scoped

        public ValidationIssue(ValidationSeverity severity, string message, int groupIndex = -1, int busIndex = -1)
        {
            Severity = severity;
            Message = message;
            GroupIndex = groupIndex;
            BusIndex = busIndex;
        }
    }

    /// <summary>
    /// Design-time correctness gate for an <see cref="AudioDatabaseAsset"/>. Detects missing clips,
    /// duplicate names, orphaned bus references, empty groups and clips over a length budget. Pure
    /// (no side effects) so it can run from a menu item, the manager window or a CI editor script.
    /// </summary>
    public static class AudioKitValidator
    {
        public const float DefaultMaxClipSeconds = 15f;

        public static List<ValidationIssue> Validate(AudioDatabaseAsset db, float maxClipSeconds = DefaultMaxClipSeconds)
        {
            var issues = new List<ValidationIssue>();
            if (db == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "No database assigned."));
                return issues;
            }

            // Buses: duplicate / empty names.
            var busNames = new HashSet<string>();
            for (int b = 0; b < db.buses.Count; b++)
            {
                var bus = db.buses[b];
                if (string.IsNullOrWhiteSpace(bus.busName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Bus {b} has an empty name.", -1, b));
                else if (!busNames.Add(bus.busName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Duplicate bus name '{bus.busName}'.", -1, b));
            }

            // Groups.
            var groupNames = new HashSet<string>();
            for (int g = 0; g < db.groups.Count; g++)
            {
                var grp = db.groups[g];

                if (string.IsNullOrWhiteSpace(grp.groupName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Group {g} has an empty name.", g));
                else if (!groupNames.Add(grp.groupName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Duplicate group name '{grp.groupName}'.", g));

                if (grp.variations == null || grp.variations.Count == 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Group '{grp.groupName}' has no variations.", g));
                }
                else
                {
                    for (int v = 0; v < grp.variations.Count; v++)
                    {
                        var variation = grp.variations[v];
                        if (variation.clip == null)
                        {
                            issues.Add(new ValidationIssue(ValidationSeverity.Error,
                                $"Group '{grp.groupName}', variation {v} ('{variation.name}') is missing its AudioClip.", g));
                        }
                        else if (variation.clip.length > maxClipSeconds)
                        {
                            issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                                $"Group '{grp.groupName}', clip '{variation.clip.name}' is {variation.clip.length:0.0}s (> {maxClipSeconds:0}s budget). Consider streaming or a music bus.", g));
                        }
                    }
                }

                if (!string.IsNullOrEmpty(grp.busName) && !busNames.Contains(grp.busName))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error,
                        $"Group '{grp.groupName}' references bus '{grp.busName}' which does not exist.", g));
                }

                if (grp.voiceLimit < 1)
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Group '{grp.groupName}' has a voice limit < 1.", g));

                if (grp.volumeMin > grp.volumeMax || grp.pitchMin > grp.pitchMax)
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Group '{grp.groupName}' has an inverted min/max range.", g));
            }

            // Playlists (Phase 2).
            var playlistNames = new HashSet<string>();
            for (int p = 0; p < db.playlists.Count; p++)
            {
                var pl = db.playlists[p];
                if (string.IsNullOrWhiteSpace(pl.playlistName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Playlist {p} has an empty name."));
                else if (!playlistNames.Add(pl.playlistName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Duplicate playlist name '{pl.playlistName}'."));

                if (pl.tracks == null || pl.tracks.Count == 0)
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Playlist '{pl.playlistName}' has no tracks."));
                else
                    for (int t = 0; t < pl.tracks.Count; t++)
                        if (pl.tracks[t].clip == null && string.IsNullOrEmpty(pl.tracks[t].addressableKey))
                            issues.Add(new ValidationIssue(ValidationSeverity.Error,
                                $"Playlist '{pl.playlistName}', track {t} ('{pl.tracks[t].name}') has no clip or addressable key."));

                if (!string.IsNullOrEmpty(pl.busName) && !busNames.Contains(pl.busName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error,
                        $"Playlist '{pl.playlistName}' references bus '{pl.busName}' which does not exist."));
            }

            if (db.prewarmVoices > db.voiceCapacity)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Prewarm count exceeds voice capacity; it will be clamped."));

            if (db.groups.Count == 0)
                issues.Add(new ValidationIssue(ValidationSeverity.Info, "Database has no groups yet."));

            return issues;
        }

        public static void CountBySeverity(List<ValidationIssue> issues, out int errors, out int warnings, out int infos)
        {
            errors = warnings = infos = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                switch (issues[i].Severity)
                {
                    case ValidationSeverity.Error: errors++; break;
                    case ValidationSeverity.Warning: warnings++; break;
                    default: infos++; break;
                }
            }
        }
    }
}
