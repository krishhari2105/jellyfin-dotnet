using System.Collections.Generic;
using System.Linq;
using JellyfinTizen.Models;

namespace JellyfinTizen.Core
{
    public static class ProfileBuilder
    {
        public static DeviceProfile BuildTizenProfile(
            bool forceBurnIn = false,
            bool disableSubtitles = false,
            bool preferTsOnlyHls = false,
            bool preferTsHlsFirst = false)
        {
            var profile = new DeviceProfile
            {
                Name = "Samsung Smart TV",
                
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
                    // Keep direct-play rules container-specific. A single DirectPlayProfile is
                    // treated as a broad allow-list, so mixing unrelated containers/codecs here
                    // can make the server assume unsupported combinations are valid.
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mp4,m4v",
                        VideoCodec = "h264,hevc,mpeg2video,vc1,msmpeg4v2,vp8,vp9,av1",
                        // MP4/ISOBMFF can carry VP9 and AV1, but direct play still depends on
                        // decoder support on the target TV model.
                        // DTS FIX: We intentionally omit "dts,dca" from every video profile.
                        // Jellyfin will see the file has DTS, see the active profile lacks it,
                        // and trigger audio transcoding while still allowing video copy.
                        AudioCodec = "aac,mp3,ac3,eac3,ac4,mp2,pcm_s16le,pcm_s24le,aac_latm,opus,flac,vorbis"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mov",
                        VideoCodec = "h264",
                        AudioCodec = "aac,mp3,ac3,eac3,pcm_s16le,pcm_s24le"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mkv",
                        VideoCodec = "h264,hevc,mpeg2video,vc1,msmpeg4v2,vp8,vp9,av1",
                        AudioCodec = "aac,mp3,ac3,eac3,ac4,mp2,pcm_s16le,pcm_s24le,aac_latm,opus,flac,vorbis"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "webm",
                        VideoCodec = "vp8,vp9,av1",
                        AudioCodec = "vorbis,opus"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "ts,mpegts",
                        VideoCodec = "h264,hevc,vc1,mpeg2video",
                        AudioCodec = "aac,mp3,ac3,eac3,mp2,pcm_s16le,pcm_s24le,aac_latm,opus,flac,vorbis"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "m2ts",
                        VideoCodec = "h264,hevc,vc1,mpeg2video",
                        AudioCodec = "aac,mp3,ac3,eac3,mp2,pcm_s16le,pcm_s24le,aac_latm,flac"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "avi",
                        VideoCodec = "h264,mpeg4,msmpeg4,divx,xvid",
                        AudioCodec = "mp3,ac3,mp2,pcm_s16le"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mpg,mpeg,vob,vro",
                        AudioCodec = "ac3,mp2,pcm_s16le"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "flv,3gp",
                        AudioCodec = "aac,mp3"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Audio",
                        Container = "opus,mp3,aac,m4a,m4b,flac,webma,wma,wav,ogg"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Audio",
                        Container = "webm",
                        AudioCodec = "opus,webma"
                    }
                },
                TranscodingProfiles = new List<TranscodingProfile>
                {

                    new TranscodingProfile
                    {
                        Container = "mp4",
                        Type = "Video",
                        AudioCodec = "ac3,eac3,aac,mp3,opus,ac4",
                        VideoCodec = "hevc,h264,av1,vp9",
                        Context = "Streaming",
                        Protocol = "hls"
                    },
                    new TranscodingProfile
                    {
                        Container = "ts",
                        Type = "Video",
                        AudioCodec = "ac3,eac3,aac,mp3,opus",
                        VideoCodec = "hevc,h264",
                        Context = "Streaming",
                        Protocol = "hls"
                    },

                    new TranscodingProfile
                    {
                        Container = "mkv",
                        Type = "Video",
                        AudioCodec = "ac3,eac3,aac,mp3,ac4,mp2,pcm_s16le,pcm_s24le,aac_latm,opus,flac,vorbis",
                        VideoCodec = "h264,hevc,mpeg2video,vc1,msmpeg4v2,vp8,vp9,av1",
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
            else if (disableSubtitles)
            {
                // Explicit subtitle-off negotiation mode:
                // advertise no subtitle delivery methods so server doesn't negotiate
                // subtitle burn-in while subtitles are disabled by user choice.
                profile.SubtitleProfiles = new List<SubtitleProfile>();
            }
            else
            {
                // External (Download) mode
                profile.SubtitleProfiles = new List<SubtitleProfile>
                {
                    new SubtitleProfile { Format = "vtt", Method = "External" },
                    new SubtitleProfile { Format = "srt", Method = "External" },
                    new SubtitleProfile { Format = "subrip", Method = "External" },
                    new SubtitleProfile { Format = "ass", Method = "External" },
                    new SubtitleProfile { Format = "ssa", Method = "External" },
                    // Keep Embed as secondary option for DirectPlay compatibility
                    new SubtitleProfile { Format = "vtt", Method = "Embed" },
                    new SubtitleProfile { Format = "srt", Method = "Embed" },
                    new SubtitleProfile { Format = "subrip", Method = "Embed" },
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
            else if (preferTsOnlyHls)
            {
                profile.TranscodingProfiles = profile.TranscodingProfiles.FindAll(p =>
                    !string.Equals(p.Type, "Video", System.StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(p.Protocol, "hls", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Container, "ts", System.StringComparison.OrdinalIgnoreCase));
            }
            else if (preferTsHlsFirst)
            {
                var preferredTsProfiles = profile.TranscodingProfiles
                    .Where(p =>
                        string.Equals(p.Type, "Video", System.StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.Protocol, "hls", System.StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.Container, "ts", System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (preferredTsProfiles.Count > 0)
                {
                    var remainingProfiles = profile.TranscodingProfiles
                        .Where(p => !preferredTsProfiles.Contains(p))
                        .ToList();
                    preferredTsProfiles.AddRange(remainingProfiles);
                    profile.TranscodingProfiles = preferredTsProfiles;
                }
            }

            return profile;
        }
    }
}
