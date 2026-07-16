using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using System.Runtime.CompilerServices;

namespace JellyfinTizen.Utils
{
    public static class UiAnimator
    {
        public const int FocusDurationMs = 120;
        public const int ScrollDurationMs = 160;
        public const int PanelDurationMs = 200;
        public const int FadeInDurationMs = 150;
        public const int HeroFadeInDurationMs = 300;
        public const int BackdropFadeInDurationMs = 1000;

        private static readonly object _sync = new();
        private static readonly HashSet<Animation> _activeAnimations = new();
        private static readonly ConditionalWeakTable<ImageView, EventHandler<ImageView.ResourceReadyEventArgs>> _pendingFades = new();

        public static Animation Start(int durationMs, Action<Animation> configure, Action onFinished = null)
        {
            var animation = new Animation(durationMs)
            {
                EndAction = Animation.EndActions.StopFinal
            };

            configure?.Invoke(animation);

            animation.Finished += (_, _) =>
            {
                Cleanup(animation);
                onFinished?.Invoke();
            };

            lock (_sync)
            {
                _activeAnimations.Add(animation);
            }

            animation.Play();
            return animation;
        }

        public static Animation AnimateTo(View view, string property, object target, int durationMs, Action onFinished = null)
        {
            return Start(durationMs, animation => animation.AnimateTo(view, property, target), onFinished);
        }

        public static void Replace(ref Animation slot, Animation replacement)
        {
            StopAndDispose(ref slot);
            slot = replacement;
        }

        public static void StopAndDispose(ref Animation animation)
        {
            if (animation == null)
            {
                return;
            }

            try { animation.Stop(); } catch { }
            try { animation.Clear(); } catch { }

            Cleanup(animation);
            animation = null;
        }

        public static void StopAndDisposeAll(IDictionary<View, Animation> animations)
        {
            if (animations == null || animations.Count == 0)
            {
                return;
            }

            var snapshot = new List<Animation>(animations.Values);
            animations.Clear();

            foreach (var active in snapshot)
            {
                var animation = active;
                StopAndDispose(ref animation);
            }
        }

        private static void Cleanup(Animation animation)
        {
            lock (_sync)
            {
                _activeAnimations.Remove(animation);
            }

            try { animation.Dispose(); } catch { }
        }

        public static void CancelFadeIn(ImageView imageView)
        {
            if (imageView == null)
                return;

            if (_pendingFades.TryGetValue(imageView, out var handler))
            {
                try
                {
                    imageView.ResourceReady -= handler;
                }
                catch
                {
                    // Fail-safe
                }
                _pendingFades.Remove(imageView);
            }
        }

        public static void FadeInOnImageReady(ImageView imageView, string resourceUrl, int durationMs = FadeInDurationMs)
        {
            if (imageView == null)
                return;

            CancelFadeIn(imageView);

            imageView.Opacity = 0.0f;

            if (string.IsNullOrWhiteSpace(resourceUrl))
            {
                imageView.Opacity = 1.0f;
                imageView.ResourceUrl = null;
                return;
            }

            EventHandler<ImageView.ResourceReadyEventArgs> handler = null;
            handler = (sender, e) =>
            {
                try
                {
                    _pendingFades.Remove(imageView);
                    imageView.ResourceReady -= handler;
                    _ = AnimateTo(imageView, "Opacity", 1.0f, durationMs);
                }
                catch
                {
                    // Fail-safe
                }
            };

            _pendingFades.Add(imageView, handler);
            imageView.ResourceReady += handler;
            imageView.ResourceUrl = resourceUrl;
        }
    }
}
