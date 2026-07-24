using System.Threading.Tasks;

namespace JellyfinTizen.Core
{
    /// <summary>
    /// Allows synchronous Tizen/navigation lifecycle callbacks to close an active
    /// playback generation before the screen or application services are disposed.
    /// Calling RequestPlaybackStop more than once must return the same stop operation.
    /// </summary>
    public interface IPlaybackLifecycleHandler
    {
        Task<PlaybackStopResult> RequestPlaybackStop(PlaybackStopReason reason);
    }
}
