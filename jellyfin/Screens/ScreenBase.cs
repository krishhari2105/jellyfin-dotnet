using System;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Constants;
using JellyfinTizen.Core;

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
            HideDebugOverlay();
        }

        protected View _debugOverlay;
        protected TextLabel _debugOverlayLabel;
        protected int _debugScrollOffset;
        protected bool _debugOverlayVisible;

        protected void CreateDebugOverlay()
        {
            if (_debugOverlay != null)
                return;

            _debugOverlay = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = new Color(0f, 0f, 0f, 0.7f),
                PositionUsesPivotPoint = true,
                ParentOrigin = Tizen.NUI.ParentOrigin.Center,
                PivotPoint = Tizen.NUI.PivotPoint.Center,
                // Z index is not directly assignable for View, rely on Add order or PositionZ if needed
                Opacity = 0.0f,
            };
            Add(_debugOverlay);

            _debugOverlayLabel = new TextLabel
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Text = "Debug Log",
                TextColor = Color.White,
                PointSize = 12.0f,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Begin,
                Padding = new Extents(10, 10, 10, 10)
            };
            _debugOverlay.Add(_debugOverlayLabel);
            TailscaleDebugLog.LogAdded += OnLogAdded;
        }

        protected void RefreshDebugOverlay(bool autoScrollToBottom = false)
        {
            if (!DebugSwitches.EnableVerboseDebugLogging || !DebugSwitches.EnablePlaybackDebugOverlay)
                return;

            if (_debugOverlay == null)
                CreateDebugOverlay();

            _debugOverlayLabel.Text = TailscaleDebugLog.GetRecentLines();

            if (_debugOverlayVisible && _debugOverlay.Opacity < 0.9f)
                _debugOverlay.Opacity = 1.0f;
        }

        protected bool TryScrollDebugOverlay(int direction)
        {
            return false; // Scrolling is not implemented in this simplified version
        }

        protected void ShowDebugOverlay()
        {
            if (!DebugSwitches.EnableVerboseDebugLogging || !DebugSwitches.EnablePlaybackDebugOverlay)
                return;

            if (_debugOverlay == null)
                CreateDebugOverlay();

            _debugOverlayVisible = true;
            _debugOverlay.Opacity = 1.0f;
            RefreshDebugOverlay(autoScrollToBottom: true);
        }

        public void ShowDebugOverlayPublic()
        {
            ShowDebugOverlay();
        }

        protected void HideDebugOverlay()
        {
            if (_debugOverlay == null)
                return;

            _debugOverlayVisible = false;
            _debugOverlay.Opacity = 0.0f;
            TailscaleDebugLog.LogAdded -= OnLogAdded;
        }

        private void OnLogAdded()
        {
            RunOnUiThread(() => RefreshDebugOverlay());
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

        protected void FireAndForget(Task task, string operationName = null)
        {
            if (task == null)
                return;

            task.ContinueWith(
                faultedTask =>
                {
                    var ex = faultedTask.Exception?.Flatten();
                    if (ex != null)
                    {
                        var name = operationName ?? task.GetType().Name;
                        TailscaleDebugLog.Add($"[FireAndForget:{name}] Unhandled exception: {ex.Message}");
                        foreach (var inner in ex.InnerExceptions)
                        {
                            TailscaleDebugLog.Add($"[FireAndForget:{name}]   -> {inner.GetType().Name}: {inner.Message}");
                            Tizen.Log.Warn("FireAndForget", $"{name}: {inner}");
                        }
                    }
                },
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
