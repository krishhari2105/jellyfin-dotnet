using System;
using System.Collections.Generic;
using System.Linq;

namespace JellyfinTizen.Models
{

    public class PlaybackInfoResponse
    {
        public List<MediaSourceInfo> MediaSources { get; set; }
        public string PlaySessionId { get; set; }

        public void Validate()
        {
            if (MediaSources == null)
                throw new InvalidOperationException("PlaybackInfoResponse.MediaSources cannot be null");
            if (MediaSources.Count == 0)
                throw new InvalidOperationException("PlaybackInfoResponse must have at least one MediaSource");
            if (string.IsNullOrWhiteSpace(PlaySessionId))
                throw new InvalidOperationException("PlaybackInfoResponse.PlaySessionId is required");

            foreach (var src in MediaSources)
                src.Validate();
        }
    }

    public class MediaSourceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool SupportsDirectPlay { get; set; }
        public bool SupportsDirectStream { get; set; }
        public bool SupportsTranscoding { get; set; }
        public string TranscodingUrl { get; set; }
        public string TranscodingContainer { get; set; }
        public string TranscodingSubProtocol { get; set; }
        public List<string> TranscodingReasons { get; set; }
        public string Protocol { get; set; }
        public string Container { get; set; }
        public long? Size { get; set; }
        public int? Bitrate { get; set; }
        // Note: DirectStreamUrl is often null, so we construct it manually if DirectPlay is supported
        public List<MediaStream> MediaStreams { get; set; }
        public int? DefaultAudioStreamIndex { get; set; }
        public int? DefaultSubtitleStreamIndex { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidOperationException("MediaSourceInfo.Id is required");
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("MediaSourceInfo.Name is required");
            if (string.IsNullOrWhiteSpace(Protocol))
                throw new InvalidOperationException("MediaSourceInfo.Protocol is required");
            if (MediaStreams != null)
            {
                foreach (var stream in MediaStreams)
                    stream.Validate();
            }
        }
    }
    // The main payload sent to /Sessions/Capabilities/Full
    public class ClientCapabilitiesDto
    {
        public List<string> PlayableMediaTypes { get; set; } = new List<string> { "Audio", "Video" };
        public List<string> SupportedCommands { get; set; } = new List<string> { "Play", "Browse" };
        public bool SupportsMediaControl { get; set; } = true;
        public DeviceProfile DeviceProfile { get; set; }

        public void Validate()
        {
            if (PlayableMediaTypes == null || PlayableMediaTypes.Count == 0)
                throw new InvalidOperationException("ClientCapabilitiesDto.PlayableMediaTypes cannot be empty");
            if (SupportedCommands == null || SupportedCommands.Count == 0)
                throw new InvalidOperationException("ClientCapabilitiesDto.SupportedCommands cannot be empty");
            if (DeviceProfile != null)
                DeviceProfile.Validate();
        }
    }

    public class DeviceProfile
    {
        public string Name { get; set; }
        public List<DirectPlayProfile> DirectPlayProfiles { get; set; }
        public List<TranscodingProfile> TranscodingProfiles { get; set; }
        public List<SubtitleProfile> SubtitleProfiles { get; set; }
        public List<CodecProfile> CodecProfiles { get; set; }
        public int? MaxStreamingBitrate { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("DeviceProfile.Name is required");
            if (MaxStreamingBitrate.HasValue && MaxStreamingBitrate.Value <= 0)
                throw new InvalidOperationException("DeviceProfile.MaxStreamingBitrate must be positive if set");
            if (DirectPlayProfiles != null)
            {
                foreach (var p in DirectPlayProfiles)
                    p.Validate();
            }
            if (TranscodingProfiles != null)
            {
                foreach (var p in TranscodingProfiles)
                    p.Validate();
            }
            if (SubtitleProfiles != null)
            {
                foreach (var p in SubtitleProfiles)
                    p.Validate();
            }
            if (CodecProfiles != null)
            {
                foreach (var p in CodecProfiles)
                    p.Validate();
            }
        }
    }

    public class DirectPlayProfile
    {
        public string Container { get; set; } // "mkv,mp4"
        public string Type { get; set; }      // "Video"
        public string VideoCodec { get; set; } // "h264,hevc"
        public string AudioCodec { get; set; } // "aac,ac3"

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Container))
                throw new InvalidOperationException("DirectPlayProfile.Container is required");
            if (string.IsNullOrWhiteSpace(Type))
                throw new InvalidOperationException("DirectPlayProfile.Type is required");
        }
    }

    public class TranscodingProfile
    {
        public string Container { get; set; } // "ts"
        public string Type { get; set; }      // "Video"
        public string VideoCodec { get; set; }
        public string AudioCodec { get; set; }
        public string Protocol { get; set; }  // "hls"
        public string Context { get; set; }   // "Streaming"

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Container))
                throw new InvalidOperationException("TranscodingProfile.Container is required");
            if (string.IsNullOrWhiteSpace(Type))
                throw new InvalidOperationException("TranscodingProfile.Type is required");
            if (string.IsNullOrWhiteSpace(Protocol))
                throw new InvalidOperationException("TranscodingProfile.Protocol is required");
        }
    }

    public class SubtitleProfile
    {
        public string Format { get; set; }
        public string Method { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Format))
                throw new InvalidOperationException("SubtitleProfile.Format is required");
            if (string.IsNullOrWhiteSpace(Method))
                throw new InvalidOperationException("SubtitleProfile.Method is required");
        }
    }

    public class CodecProfile
    {
        public string Type { get; set; }
        public string Codec { get; set; }
        public List<ProfileCondition> Conditions { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Type))
                throw new InvalidOperationException("CodecProfile.Type is required");
            if (string.IsNullOrWhiteSpace(Codec))
                throw new InvalidOperationException("CodecProfile.Codec is required");
            if (Conditions != null)
            {
                foreach (var c in Conditions)
                    c.Validate();
            }
        }
    }

    public class ProfileCondition
    {
        public string Condition { get; set; } // "LessThanEqual"
        public string Property { get; set; }  // "Width"
        public string Value { get; set; }
        public bool IsRequired { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Condition))
                throw new InvalidOperationException("ProfileCondition.Condition is required");
            if (string.IsNullOrWhiteSpace(Property))
                throw new InvalidOperationException("ProfileCondition.Property is required");
            if (string.IsNullOrWhiteSpace(Value))
                throw new InvalidOperationException("ProfileCondition.Value is required");
        }
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
        public int? AudioStreamIndex { get; set; }
        public int? SubtitleStreamIndex { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ItemId))
                throw new InvalidOperationException("PlaybackProgressInfo.ItemId is required");
            if (string.IsNullOrWhiteSpace(SessionId))
                throw new InvalidOperationException("PlaybackProgressInfo.SessionId is required");
            if (string.IsNullOrWhiteSpace(PlaySessionId))
                throw new InvalidOperationException("PlaybackProgressInfo.PlaySessionId is required");
            if (string.IsNullOrWhiteSpace(MediaSourceId))
                throw new InvalidOperationException("PlaybackProgressInfo.MediaSourceId is required");
            if (VolumeLevel < 0 || VolumeLevel > 100)
                throw new InvalidOperationException("PlaybackProgressInfo.VolumeLevel must be 0-100");
            if (string.IsNullOrWhiteSpace(EventName))
                throw new InvalidOperationException("PlaybackProgressInfo.EventName is required");
        }
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
        public bool IsDefault { get; set; }
        public bool IsForced { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string VideoRange { get; set; }
        public int? Channels { get; set; }
        public string ChannelLayout { get; set; }
        public int? BitRate { get; set; }
        public int? SampleRate { get; set; }
        public string Profile { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Type))
                throw new InvalidOperationException("MediaStream.Type is required");
            if (Width.HasValue && Width.Value <= 0)
                throw new InvalidOperationException("MediaStream.Width must be positive if set");
            if (Height.HasValue && Height.Value <= 0)
                throw new InvalidOperationException("MediaStream.Height must be positive if set");
            if (Channels.HasValue && Channels.Value <= 0)
                throw new InvalidOperationException("MediaStream.Channels must be positive if set");
            if (BitRate.HasValue && BitRate.Value < 0)
                throw new InvalidOperationException("MediaStream.BitRate cannot be negative");
            if (SampleRate.HasValue && SampleRate.Value <= 0)
                throw new InvalidOperationException("MediaStream.SampleRate must be positive if set");
        }
    }

    public class MediaSegmentInfo
    {
        public string Id { get; set; }
        public string ItemId { get; set; }
        public string Type { get; set; }
        public long StartTicks { get; set; }
        public long EndTicks { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidOperationException("MediaSegmentInfo.Id is required");
            if (string.IsNullOrWhiteSpace(ItemId))
                throw new InvalidOperationException("MediaSegmentInfo.ItemId is required");
            if (string.IsNullOrWhiteSpace(Type))
                throw new InvalidOperationException("MediaSegmentInfo.Type is required");
            if (EndTicks < StartTicks)
                throw new InvalidOperationException("MediaSegmentInfo.EndTicks must be >= StartTicks");
        }
    }
}
