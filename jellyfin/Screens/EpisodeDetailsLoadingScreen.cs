using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JellyfinTizen.Core;
using JellyfinTizen.Models;

namespace JellyfinTizen.Screens
{
    public class EpisodeDetailsLoadingScreen : LoadingScreen
    {
        private readonly JellyfinMovie _episode;
        private bool _started;

        public EpisodeDetailsLoadingScreen(JellyfinMovie episode)
            : base("Loading episode...")
        {
            _episode = episode;
        }

        public override void OnShow()
        {
            base.OnShow();
            if (_started)
                return;

            _started = true;
            _ = LoadAndNavigateAsync();
        }

        private async Task LoadAndNavigateAsync()
        {
            var shownAt = DateTime.UtcNow;
            List<MediaStream> subtitleStreams = null;
            List<MediaSourceInfo> mediaSources = null;

            try
            {
                var subtitleTask = AppState.Jellyfin.GetSubtitleStreamsAsync(_episode.Id);
                var playbackTask = AppState.Jellyfin.GetPlaybackInfoAsync(_episode.Id);
                await Task.WhenAll(subtitleTask, playbackTask);

                subtitleStreams = subtitleTask.Result ?? new List<MediaStream>();
                mediaSources = playbackTask.Result?.MediaSources ?? new List<MediaSourceInfo>();
            }
            catch
            {
                subtitleStreams = subtitleStreams ?? new List<MediaStream>();
                mediaSources = mediaSources ?? new List<MediaSourceInfo>();
            }

            if (mediaSources.Count == 0)
            {
                mediaSources.Add(new MediaSourceInfo
                {
                    Id = _episode.Id,
                    Name = "Default"
                });
            }

            var elapsedMs = (DateTime.UtcNow - shownAt).TotalMilliseconds;
            if (elapsedMs < 280)
            {
                await Task.Delay((int)(280 - elapsedMs));
            }

            NavigationService.Navigate(
                new EpisodeDetailsScreen(_episode, subtitleStreams, mediaSources),
                addToStack: false,
                animated: false
            );
        }
    }
}
