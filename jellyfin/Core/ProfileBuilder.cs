using System.Collections.Generic;
using JellyfinTizen.Models;

namespace JellyfinTizen.Core
{
    public static class ProfileBuilder
    {
        public static DeviceProfile BuildTizenProfile(bool forceBurnIn = false)
        {
            var profile = new DeviceProfile
            {
                Name = "SamsungTV",
                
                MaxStreamingBitrate = 120000000, // 120 Mbps limit
               
                CodecProfiles = new List<CodecProfile>
                {
                    new CodecProfile
                    {
                        Type = "Video",
                        Codec = "h264",
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition { Condition = "EqualsAny", Property = "VideoProfile", Value = "high|main|baseline|constrained baseline|high 10", IsRequired = false },
                            new ProfileCondition { Condition = "LessThanEqual", Property = "VideoLevel", Value = "51", IsRequired = false }
                        }
                    },
                    new CodecProfile
                    {
                        Type = "Video",
                        Codec = "hevc",
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition { Condition = "EqualsAny", Property = "VideoProfile", Value = "main|main 10", IsRequired = false },
                            new ProfileCondition { Condition = "LessThanEqual", Property = "VideoLevel", Value = "183", IsRequired = false }
                        }
                    },
                    new CodecProfile
                    {
                        Type = "Video",
                        Codec = "vp9",
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition { Condition = "EqualsAny", Property = "VideoProfile", Value = "profile 0|profile 2", IsRequired = false }
                        }
                    },
                  
                },

                DirectPlayProfiles = new List<DirectPlayProfile>
                {
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mkv,mp4,m4v,mov,avi,ts,webm",
                        VideoCodec = "h264,hevc,vp9,av1",
                        // DTS FIX: We intentionally OMIT "dts,dca" from this list.
                        // Jellyfin will see the file has DTS, see this list lacks it, 
                        // and trigger a transcode (Video: Copy, Audio: AAC).
                        AudioCodec = "aac,mp3,ac3,eac3,flac,vorbis,opus"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Audio",
                        Container = "mp3,aac,flac,m4a",
                        AudioCodec = "mp3,aac,opus,flac,vorbis"
                    }
                },
                TranscodingProfiles = new List<TranscodingProfile>
                {

                    new TranscodingProfile
                    {
                        Container = "mp4",
                        Type = "Video",
                        AudioCodec = "ac3,eac3,aac",
                        VideoCodec = "hevc,h264",
                        Context = "Streaming",
                        Protocol = "hls"
                    },
                    new TranscodingProfile
                    {
                        Container = "ts",
                        Type = "Video",
                        AudioCodec = "ac3,eac3,aac",
                        VideoCodec = "h264,hevc",
                        Context = "Streaming",
                        Protocol = "hls"
                    },

                    new TranscodingProfile
                    {
                        Container = "mkv",
                        Type = "Video",
                        AudioCodec = "ac3,aac,eac3",
                        VideoCodec = "h264,hevc",
                        Context = "Streaming",
                        Protocol = "http"
                    }
                },
                SubtitleProfiles = new List<SubtitleProfile>()
            };

            if (forceBurnIn)
            {
                // Force Burn-in (Encode) for everything
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "srt", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "subrip", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "vtt", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "ass", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "ssa", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "pgs", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "pgssub", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "mov_text", Method = "Encode" });
            }
            else
            {
                // External (Download) mode
                profile.SubtitleProfiles = new List<SubtitleProfile>
                {
                    new SubtitleProfile { Format = "vtt", Method = "External" },
                    new SubtitleProfile { Format = "srt", Method = "External" },
                    new SubtitleProfile { Format = "ass", Method = "External" },
                    new SubtitleProfile { Format = "ssa", Method = "External" },
                    new SubtitleProfile { Format = "mov_text", Method = "External" },
                    new SubtitleProfile { Format = "pgs", Method = "External" },
                    new SubtitleProfile { Format = "pgssub", Method = "External" },
                    // Keep Embed as secondary option for DirectPlay compatibility
                    new SubtitleProfile { Format = "vtt", Method = "Embed" },
                    new SubtitleProfile { Format = "srt", Method = "Embed" },
                    new SubtitleProfile { Format = "ass", Method = "Embed" },
                    new SubtitleProfile { Format = "ssa", Method = "Embed" },                 
                    new SubtitleProfile { Format = "mov_text", Method = "Embed" }
                };
            }

            // When forceBurnIn is true, ensure we only support transcoding to h264
            if (forceBurnIn)
            {
                profile.TranscodingProfiles = profile.TranscodingProfiles.FindAll(p => p.VideoCodec.Contains("h264"));
                foreach (var tp in profile.TranscodingProfiles)
                {
                    tp.VideoCodec = "h264"; // Ensure only h264 for burn-in
                    tp.Container = "ts"; // TS container is most compatible with hls + burn-in
                    tp.Protocol = "hls";
                }
            }

            return profile;
        }
    }
}
