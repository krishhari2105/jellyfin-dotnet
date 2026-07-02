using System;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using System.Linq;

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

            int screenWidth = Window.Default.Size.Width;
            int overlayWidth = Math.Clamp(screenWidth - 120, 760, 1320);
            const int overlayHeight = 280;

            _debugOverlay = new View
            {
                PositionX = 40,
                PositionY = Window.Default.Size.Height - overlayHeight - 40,
                WidthSpecification = overlayWidth,
                HeightSpecification = overlayHeight,
                BackgroundColor = new Color(0f, 0f, 0f, 0.85f),
                CornerRadius = 10.0f,
                ClippingMode = ClippingModeType.ClipChildren
            };

            _debugOverlayLabel = new TextLabel(string.Empty)
            {
                PositionX = 16,
                PositionY = 12,
                WidthSpecification = overlayWidth - 32,
                HeightSpecification = overlayHeight - 24,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                PointSize = 20f,
                TextColor = new Color(0.92f, 0.98f, 1f, 0.96f),
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Top,
                EnableMarkup = false
            };

            _debugOverlay.Add(_debugOverlayLabel);
            Add(_debugOverlay);
            _debugOverlay.Hide();
        }

        protected void RefreshDebugOverlay(bool autoScrollToBottom = false)
        {
            if (!_debugOverlayVisible || _debugOverlayLabel == null)
                return;

            var allLogs = Core.TailscaleDebugLog.GetAllLines();
            int visibleLines = 12;
            int maxOffset = Math.Max(0, allLogs.Length - visibleLines);

            if (autoScrollToBottom)
            {
                _debugScrollOffset = maxOffset;
            }
            else
            {
                _debugScrollOffset = Math.Clamp(_debugScrollOffset, 0, maxOffset);
            }
            
            string visibleText = string.Join("\n", allLogs.Skip(_debugScrollOffset).Take(visibleLines));
            _debugOverlayLabel.Text = visibleText;
            _debugOverlay.RaiseToTop();
        }

        protected bool TryScrollDebugOverlay(int direction)
        {
            if (!_debugOverlayVisible || _debugOverlay == null)
                return false;
            
            int oldOffset = _debugScrollOffset;
            _debugScrollOffset += direction > 0 ? 3 : -3;
            RefreshDebugOverlay();
            return _debugScrollOffset != oldOffset;
        }

        protected void ShowDebugOverlay()
        {
            CreateDebugOverlay();
            _debugOverlayVisible = true;
            _debugOverlay?.Show();
            
            // Always unsubscribe first to avoid duplicate handlers on repeated OnShow calls
            Core.TailscaleDebugLog.LogAdded -= OnLogAdded;
            Core.TailscaleDebugLog.LogAdded += OnLogAdded;
            
            RefreshDebugOverlay(autoScrollToBottom: true);
        }

        public void ShowDebugOverlayPublic()
        {
            ShowDebugOverlay();
        }

        protected void HideDebugOverlay()
        {
            if (_debugOverlayVisible)
            {
                _debugOverlayVisible = false;
                _debugOverlay?.Hide();
                Core.TailscaleDebugLog.LogAdded -= OnLogAdded;
            }
        }

        private void OnLogAdded()
        {
            RunOnUiThread(() =>
            {
                RefreshDebugOverlay(autoScrollToBottom: true);
            });
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
