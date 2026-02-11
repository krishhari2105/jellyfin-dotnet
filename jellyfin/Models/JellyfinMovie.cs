namespace JellyfinTizen.Models
{
    public class JellyfinMovie
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Overview { get; set; }
        public long PlaybackPositionTicks { get; set; }
        public long RunTimeTicks { get; set; }
        public string SeriesName { get; set; }
        public string ItemType { get; set; }
        public string SeriesId { get; set; }
        public int IndexNumber { get; set; }
        public int ParentIndexNumber { get; set; }
        public bool HasPrimary { get; set; }
        public bool HasThumb { get; set; }
        public bool HasBackdrop { get; set; }
        public bool HasLogo { get; set; }
    }
}
