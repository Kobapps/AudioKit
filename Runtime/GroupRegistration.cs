using System.Collections.Generic;
using Kobapps.AudioKit.Core;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// An opaque handle to a set of runtime-registered groups, returned by
    /// <see cref="IAudioService.RegisterGroups"/> and passed back to
    /// <see cref="IAudioService.UnregisterGroups"/> to remove exactly those groups.
    /// </summary>
    public sealed class GroupRegistration
    {
        internal readonly List<AudioId> Ids = new List<AudioId>();
        internal bool ManageClipMemory;

        /// <summary>Number of groups that were registered under this handle.</summary>
        public int Count => Ids.Count;
    }
}
