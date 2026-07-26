namespace JellyfinTizen.Core
{
    public enum AppLifecycleState
    {
        NotStarted,
        Ready,
        Suspended,
        Resuming
    }

    public static class AppLifecycle
    {
        private static readonly object Gate = new();
        private static AppLifecycleState _state = AppLifecycleState.NotStarted;

        public static AppLifecycleState State
        {
            get
            {
                lock (Gate)
                    return _state;
            }
        }

        public static bool IsResuming => State == AppLifecycleState.Resuming;

        public static bool TryTransition(AppLifecycleState from, AppLifecycleState to)
        {
            lock (Gate)
            {
                if (_state != from)
                    return false;

                _state = to;
                return true;
            }
        }

        public static void TransitionToSuspended()
        {
            lock (Gate)
                _state = AppLifecycleState.Suspended;
        }
    }
}
