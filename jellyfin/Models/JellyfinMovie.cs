using System;

namespace JellyfinTizen.Models
{
    public class JellyfinMovie
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Overview { get; set; }
        public long PlaybackPositionTicks { get; set; }
        public bool Played { get; set; }
        public long RunTimeTicks { get; set; }
        public int ProductionYear { get; set; }
        public string OfficialRating { get; set; }
        public double? CommunityRating { get; set; }
        public string SeriesName { get; set; }
        public string ItemType { get; set; }
        public string SeriesId { get; set; }
        public int IndexNumber { get; set; }
        public int ParentIndexNumber { get; set; }
        public bool HasPrimary { get; set; }
        public bool HasThumb { get; set; }
        public bool HasBackdrop { get; set; }
        public bool HasLogo { get; set; }

        /// <summary>
        /// Item that owns the backdrop this item inherits (the series for an episode or
        /// season). The server only reports this when the item has no backdrop of its own.
        /// </summary>
        public string ParentBackdropItemId { get; set; }

        /// <summary>True when <see cref="ParentBackdropItemId"/> has at least one Backdrop image.</summary>
        public bool HasParentBackdrop { get; set; }

        public bool IsSeries =>
            string.Equals(ItemType, "Series", StringComparison.OrdinalIgnoreCase);

        public bool IsEpisode =>
            string.Equals(ItemType, "Episode", StringComparison.OrdinalIgnoreCase);

        public bool IsSeason =>
            string.Equals(ItemType, "Season", StringComparison.OrdinalIgnoreCase);

        public bool IsMusicVideo =>
            string.Equals(ItemType, "MusicVideo", StringComparison.OrdinalIgnoreCase);

        public bool IsVideo =>
            string.Equals(ItemType, "Video", StringComparison.OrdinalIgnoreCase);

        public bool UsesThumbDetailsLayout => IsEpisode || IsMusicVideo || IsVideo;

        public bool IsPlayableVideo => !IsSeries && !IsSeason;
    }
}
