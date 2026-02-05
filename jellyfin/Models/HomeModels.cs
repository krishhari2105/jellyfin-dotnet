using System.Collections.Generic;

namespace JellyfinTizen.Models
{
    public enum HomeRowKind
    {
        Libraries,
        NextUp,
        ContinueWatching,
        RecentlyAdded
    }

    public class HomeItemData
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string ImageUrl { get; set; }
        public JellyfinLibrary Library { get; set; }
        public JellyfinMovie Media { get; set; }
    }

    public class HomeRowData
    {
        public string Title { get; set; }
        public HomeRowKind Kind { get; set; }
        public List<HomeItemData> Items { get; set; } = new();
    }
}
