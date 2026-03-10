using JellyfinTizen.Models;

namespace JellyfinTizen.Utils
{
    public static class JellyfinImageUrlBuilder
    {
        public static string BuildBackdropUrl(
            JellyfinMovie item,
            string serverUrl,
            string apiKey,
            int maxWidth = 1920,
            int backdropQuality = 90,
            int thumbQuality = 90,
            int primaryQuality = 95,
            string fallbackBackdropItemId = null)
        {
            if (item == null)
                return null;

            if (item.HasBackdrop)
                return BuildImageUrl(serverUrl, item.Id, "Backdrop", maxWidth, backdropQuality, apiKey);

            if (item.HasThumb)
                return BuildImageUrl(serverUrl, item.Id, "Thumb", maxWidth, thumbQuality, apiKey);

            if (item.HasPrimary)
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

            return $"{serverUrl}/Items/{itemId}/Images/{imageType}/0?maxWidth={maxWidth}&quality={quality}&api_key={apiKey}";
        }
    }
}
