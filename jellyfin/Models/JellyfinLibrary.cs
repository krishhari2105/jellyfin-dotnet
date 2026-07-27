using System;

namespace JellyfinTizen.Models

{
    public class JellyfinLibrary
    {
        public const string MoviesCollectionType = "movies";
        public const string TvShowsCollectionType = "tvshows";
        public const string MusicVideosCollectionType = "musicvideos";
        public const string HomeVideosCollectionType = "homevideos";

        public string Id { get; set; }
        public string Name { get; set; }
        public string CollectionType { get; set; }
        public bool HasPrimaryImage { get; set; }

        /// <summary>
        /// True when the library has a Thumb image. Thumb is natively 16:9, unlike the
        /// Primary image, which servers generate as a poster collage.
        /// </summary>
        public bool HasThumbImage { get; set; }

        /// <summary>True when the library has at least one Backdrop image (also 16:9).</summary>
        public bool HasBackdropImage { get; set; }

        public bool IsTvShows =>
            string.Equals(CollectionType, TvShowsCollectionType, StringComparison.OrdinalIgnoreCase);

        public bool IsMusicVideos =>
            string.Equals(CollectionType, MusicVideosCollectionType, StringComparison.OrdinalIgnoreCase);

        public bool IsHomeVideos =>
            string.Equals(CollectionType, HomeVideosCollectionType, StringComparison.OrdinalIgnoreCase);

        public bool UsesLandscapeGridCards => IsMusicVideos || IsHomeVideos;

        public string LibraryItemTypes =>
            IsTvShows
                ? "Series"
                : IsMusicVideos
                    ? "MusicVideo"
                    : IsHomeVideos
                        ? "Video"
                        : "Movie";

        public static bool IsSupportedCollectionType(string collectionType)
        {
            return string.Equals(collectionType, MoviesCollectionType, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(collectionType, TvShowsCollectionType, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(collectionType, MusicVideosCollectionType, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(collectionType, HomeVideosCollectionType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
