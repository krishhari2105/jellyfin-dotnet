using System.Collections.Generic;

namespace JellyfinTizen.Models
{

    public class PlaybackInfoResponse
    {
        public List<MediaSourceInfo> MediaSources { get; set; }
        public string PlaySessionId { get; set; }
    }

    public class MediaSourceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool SupportsDirectPlay { get; set; }
        public bool SupportsTranscoding { get; set; }
        public string TranscodingUrl { get; set; }
        public List<string> TranscodingReasons { get; set; }
        // Note: DirectStreamUrl is often null, so we construct it manually if DirectPlay is supported
        public List<MediaStream> MediaStreams { get; set; }
    }
    // The main payload sent to /Sessions/Capabilities/Full
    public class ClientCapabilitiesDto
    {
        public List<string> PlayableMediaTypes { get; set; } = new List<string> { "Audio", "Video" };
        public List<string> SupportedCommands { get; set; } = new List<string> { "Play", "Browse" };
        public bool SupportsMediaControl { get; set; } = true;
        public DeviceProfile DeviceProfile { get; set; }
    }

    public class DeviceProfile
    {
        public string Name { get; set; }
        public List<DirectPlayProfile> DirectPlayProfiles { get; set; }
        public List<TranscodingProfile> TranscodingProfiles { get; set; }
        public List<SubtitleProfile> SubtitleProfiles { get; set; }
        public List<CodecProfile> CodecProfiles { get; set; }
        public int? MaxStreamingBitrate { get; set; }
    }

    public class DirectPlayProfile
    {
        public string Container { get; set; } // "mkv,mp4"
        public string Type { get; set; }      // "Video"
        public string VideoCodec { get; set; } // "h264,hevc"
        public string AudioCodec { get; set; } // "aac,ac3"
    }

    public class TranscodingProfile
    {
        public string Container { get; set; } // "ts"
        public string Type { get; set; }      // "Video"
        public string VideoCodec { get; set; }
        public string AudioCodec { get; set; }
        public string Protocol { get; set; }  // "hls"
        public string Context { get; set; }   // "Streaming"
    }

    public class SubtitleProfile
    {
        public string Format { get; set; }
        public string Method { get; set; }
    }

    public class CodecProfile
    {
        public string Type { get; set; }
        public string Codec { get; set; }
        public List<ProfileCondition> Conditions { get; set; }
    }

    public class ProfileCondition
    {
        public string Condition { get; set; } // "LessThanEqual"
        public string Property { get; set; }  // "Width"
        public string Value { get; set; }
        public bool IsRequired { get; set; }
    }
    
    // For Reporting (Dashboard)
    public class PlaybackProgressInfo
    {
        public string ItemId { get; set; }
        public string SessionId { get; set; }
        public string PlaySessionId { get; set; }
        public long PositionTicks { get; set; }
        public bool IsPaused { get; set; }
        public bool IsMuted { get; set; }
        public string MediaSourceId { get; set; }
        public int VolumeLevel { get; set; } = 100;
        public string PlayMethod { get; set; } = "DirectPlay";
        public string EventName { get; set; } // "TimeUpdate", "Pause", "Stop"
    }

    public class MediaStream
    {
        public int Index { get; set; }
        public string Type { get; set; }
        public string Language { get; set; }
        public string DisplayTitle { get; set; }
        public string Codec { get; set; }
        public string DeliveryUrl { get; set; }
        public bool IsExternal { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string VideoRange { get; set; }
        public int? Channels { get; set; }
        public string ChannelLayout { get; set; }
    }

    public class MediaSegmentInfo
    {
        public string Id { get; set; }
        public string ItemId { get; set; }
        public string Type { get; set; }
        public long StartTicks { get; set; }
        public long EndTicks { get; set; }
    }
}
