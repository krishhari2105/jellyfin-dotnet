using JellyfinTizen.Models;

namespace JellyfinTizen.Utils
{
    public static class JellyfinImageUrlBuilder
    {
        // Details backdrops are rendered full-screen (1920x1080 on a 1080p TV panel),
        // so they must be requested at the panel's native width. Requesting 1280px
        // forced a 1.5x upscale, and quality 50 added visible JPEG blocking on top of
        // it. Only one backdrop is decoded per details screen, so the extra bytes are
        // affordable where poster/thumb grids' are not.
        public const int DefaultBackdropMaxWidth = 1920;
        public const int DefaultBackdropQuality = 90;

        // Shared "compressed" quality for all non-backdrop images (posters, thumbs,
        // logos, avatars). Kept low deliberately: performance over quality.
        public const int DefaultImageQuality = 50;

        /// <summary>
        /// Resolves the full-screen details backdrop. Preference order is the item's own
        /// backdrop, then the backdrop it inherits from its parent (the series backdrop
        /// for an episode or season), then the item's own landscape artwork. Episode
        /// still frames are therefore only used when no series backdrop exists, which
        /// keeps the details background consistent across a whole series.
        /// </summary>
        public static string BuildBackdropUrl(
            JellyfinMovie item,
            string serverUrl,
            string apiKey,
            int maxWidth = DefaultBackdropMaxWidth,
            int backdropQuality = DefaultBackdropQuality,
            int thumbQuality = DefaultBackdropQuality,
            int primaryQuality = DefaultBackdropQuality,
            string fallbackBackdropItemId = null)
        {
            if (item == null)
                return null;

            if (item.HasBackdrop)
                return BuildImageUrl(serverUrl, item.Id, "Backdrop", maxWidth, backdropQuality, apiKey);

            if (item.HasParentBackdrop && !string.IsNullOrWhiteSpace(item.ParentBackdropItemId))
                return BuildImageUrl(serverUrl, item.ParentBackdropItemId, "Backdrop", maxWidth, backdropQuality, apiKey);

            if (item.HasThumb)
                return BuildImageUrl(serverUrl, item.Id, "Thumb", maxWidth, thumbQuality, apiKey);

            if (item.UsesThumbDetailsLayout && item.HasPrimary)
                return BuildImageUrl(serverUrl, item.Id, "Primary", maxWidth, primaryQuality, apiKey);

            if (!string.IsNullOrWhiteSpace(fallbackBackdropItemId))
                return BuildImageUrl(serverUrl, fallbackBackdropItemId, "Backdrop", maxWidth, backdropQuality, apiKey);

            return null;
        }

        public static string BuildImageUrl(
            string serverUrl,
            string itemId,
            string imageType,
            int maxWidth,
            int quality,
            string apiKey)
        {
            if (string.IsNullOrWhiteSpace(serverUrl) ||
                string.IsNullOrWhiteSpace(itemId) ||
                string.IsNullOrWhiteSpace(imageType) ||
                string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var url = $"{serverUrl}/Items/{itemId}/Images/{imageType}/0?maxWidth={maxWidth}&quality={quality}&api_key={apiKey}";
            return url;
        }
    }
}
