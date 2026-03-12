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
        public virtual void OnHide() { }

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
