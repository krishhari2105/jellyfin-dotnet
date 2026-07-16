using JellyfinTizen.Models;

namespace JellyfinTizen.Utils
{
    public static class JellyfinImageUrlBuilder
    {
        // Backdrops are rendered full-screen behind a darkening gradient on a 1080p TV
        // surface, so 1280px (720p for 16:9) is plenty. Quality 50 favours performance
        // over fidelity per project preference.
        public const int DefaultBackdropMaxWidth = 1280;
        public const int DefaultBackdropQuality = 50;

        // Shared "compressed" quality for all non-backdrop images (posters, thumbs,
        // logos, avatars). Kept low deliberately: performance over quality.
        public const int DefaultImageQuality = 50;

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
            return JellyfinTizen.Core.AppState.RewriteImageUrlForTailscale(url);
        }
    }
}
