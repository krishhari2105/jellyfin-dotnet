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
            JellyfinMovie detailedEpisode = null;

            try
            {
                var subtitleTask = AppState.Jellyfin.GetSubtitleStreamsAsync(_episode.Id);
                var playbackTask = AppState.Jellyfin.GetPlaybackInfoAsync(_episode.Id, subtitleHandlingDisabled: true);
                var itemTask = AppState.Jellyfin.GetItemAsync(_episode.Id);
                await Task.WhenAll(subtitleTask, playbackTask, itemTask);

                subtitleStreams = subtitleTask.Result ?? new List<MediaStream>();
                mediaSources = playbackTask.Result?.MediaSources ?? new List<MediaSourceInfo>();
                detailedEpisode = MergeEpisode(_episode, itemTask.Result);
            }
            catch
            {
                subtitleStreams = subtitleStreams ?? new List<MediaStream>();
                mediaSources = mediaSources ?? new List<MediaSourceInfo>();
                detailedEpisode = detailedEpisode ?? _episode;
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
                new EpisodeDetailsScreen(detailedEpisode ?? _episode, subtitleStreams, mediaSources),
                addToStack: false,
                animated: false
            );
        }

        private static JellyfinMovie MergeEpisode(JellyfinMovie baseline, JellyfinMovie detailed)
        {
            if (detailed == null)
                return baseline;
            if (baseline == null)
                return detailed;

            return new JellyfinMovie
            {
                Id = string.IsNullOrWhiteSpace(detailed.Id) ? baseline.Id : detailed.Id,
                Name = string.IsNullOrWhiteSpace(detailed.Name) ? baseline.Name : detailed.Name,
                Overview = string.IsNullOrWhiteSpace(detailed.Overview) ? baseline.Overview : detailed.Overview,
                PlaybackPositionTicks = detailed.PlaybackPositionTicks > 0 ? detailed.PlaybackPositionTicks : baseline.PlaybackPositionTicks,
                RunTimeTicks = detailed.RunTimeTicks > 0 ? detailed.RunTimeTicks : baseline.RunTimeTicks,
                ProductionYear = detailed.ProductionYear > 0 ? detailed.ProductionYear : baseline.ProductionYear,
                OfficialRating = string.IsNullOrWhiteSpace(detailed.OfficialRating) ? baseline.OfficialRating : detailed.OfficialRating,
                CommunityRating = detailed.CommunityRating ?? baseline.CommunityRating,
                SeriesName = string.IsNullOrWhiteSpace(detailed.SeriesName) ? baseline.SeriesName : detailed.SeriesName,
                ItemType = string.IsNullOrWhiteSpace(detailed.ItemType) ? baseline.ItemType : detailed.ItemType,
                SeriesId = string.IsNullOrWhiteSpace(detailed.SeriesId) ? baseline.SeriesId : detailed.SeriesId,
                IndexNumber = detailed.IndexNumber > 0 ? detailed.IndexNumber : baseline.IndexNumber,
                ParentIndexNumber = detailed.ParentIndexNumber > 0 ? detailed.ParentIndexNumber : baseline.ParentIndexNumber,
                HasPrimary = detailed.HasPrimary || baseline.HasPrimary,
                HasThumb = detailed.HasThumb || baseline.HasThumb,
                HasBackdrop = detailed.HasBackdrop || baseline.HasBackdrop,
                HasLogo = detailed.HasLogo || baseline.HasLogo
            };
        }
    }
}
