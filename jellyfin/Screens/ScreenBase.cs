using System;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

using ThreadingTimer = System.Threading.Timer;

namespace JellyfinTizen.Screens
{
    public abstract class ScreenBase : View
    {
        protected ScreenBase()
        {
            WidthResizePolicy = ResizePolicyType.FillToParent;
            HeightResizePolicy = ResizePolicyType.FillToParent;
            BackgroundColor = Color.Black;
            Focusable = true;
        }

        public virtual void OnShow() { }
        public virtual void OnHide()
        {
            // Debug overlay disabled in release builds
        }

        protected View _debugOverlay;
        protected TextLabel _debugOverlayLabel;
        protected int _debugScrollOffset;
        protected bool _debugOverlayVisible;

        protected void CreateDebugOverlay()
        {
            // Debug overlay disabled in release builds
        }

        protected void RefreshDebugOverlay(bool autoScrollToBottom = false)
        {
            // Debug overlay disabled in release builds
        }

        protected bool TryScrollDebugOverlay(int direction)
        {
            return false;
        }

        protected void ShowDebugOverlay()
        {
            // Debug overlay disabled in release builds
        }

        public void ShowDebugOverlayPublic()
        {
            // Debug overlay disabled in release builds
        }

        protected void HideDebugOverlay()
        {
            // Debug overlay disabled in release builds
        }

        private void OnLogAdded()
        {
            // Debug overlay disabled in release builds
        }

        protected void RunOnUiThread(Action action)
        {
            if (action == null)
                return;

            try
            {
                CoreApplication.Post(action);
            }
            catch
            {
                action();
            }
        }

        protected void FireAndForget(Task task)
        {
            if (task == null)
                return;

            task.ContinueWith(
                faultedTask => { _ = faultedTask.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        protected void DisposeTimer(ref ThreadingTimer timer)
        {
            var activeTimer = timer;
            timer = null;

            try
            {
                activeTimer?.Dispose();
            }
            catch
            {
            }
        }

        protected void ShowTransientMessage(
            TextLabel label,
            string message,
            ref ThreadingTimer clearTimer,
            int delayMs = 5000)
        {
            if (label == null)
                return;

            string expectedMessage = message ?? string.Empty;
            label.Text = expectedMessage;
            DisposeTimer(ref clearTimer);

            clearTimer = new ThreadingTimer(_ =>
            {
                RunOnUiThread(() =>
                {
                    if (!string.Equals(label.Text, expectedMessage, StringComparison.Ordinal))
                        return;

                    label.Text = string.Empty;
                });
            }, null, Math.Max(0, delayMs), Timeout.Infinite);
        }
    }
}