using System.Runtime.CompilerServices;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.UI
{
    public static class MediaCardFocus
    {
        private const string CardFrameName = "CardFrame";
        private const string CardContentName = "CardContent";

        private const float FocusBorderlineWidth = 2.0f;
        private const float UnfocusedBorderlineWidth = 0.0f;

        // Focus changes happen on every D-pad press. Resolving the frame through
        // View.Children allocates a managed list plus a wrapper per child, so the
        // resolved frame is memoised against the card instead.
        private static readonly ConditionalWeakTable<View, View> _frameCache = new();

        // Borderline assignments rebuild the frame's background visual in DALi, so
        // the last applied focus state is tracked to skip redundant writes.
        private static readonly ConditionalWeakTable<View, FocusState> _frameFocusState = new();

        private sealed class FocusState
        {
            public bool Applied;
            public bool HasValue;
        }

        // NUI Vector3 is a native-backed handle, so the two scale values used by every
        // focus transition are allocated once instead of per key press.
        private static readonly Vector3 ScaleOne = new Vector3(1f, 1f, 1f);
        private static readonly Vector3 FocusScaleVector =
            new Vector3(UiTheme.MediaCardFocusScale, UiTheme.MediaCardFocusScale, 1f);

        /// <summary>Shared unit scale used to reset an unfocused card.</summary>
        public static Vector3 UnfocusedScale => ScaleOne;

        /// <summary>Shared scale for a focused card at <see cref="UiTheme.MediaCardFocusScale"/>.</summary>
        public static Vector3 FocusedScale => FocusScaleVector;

        public static View GetCardFrame(View card)
        {
            if (card == null)
                return null;

            if (_frameCache.TryGetValue(card, out var cached))
                return cached;

            var frame = FindChildByName(card, CardFrameName);
            if (frame != null)
                _frameCache.Add(card, frame);

            return frame;
        }

        public static View GetCardContent(View card)
        {
            var frame = GetCardFrame(card);
            if (frame == null)
                return null;

            return FindChildByName(frame, CardContentName);
        }

        /// <summary>
        /// Resolves a direct child by name without materialising the whole
        /// <see cref="View.Children"/> collection.
        /// </summary>
        private static View FindChildByName(View parent, string name)
        {
            if (parent == null)
                return null;

            uint childCount = parent.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                var child = parent.GetChildAt(i);
                if (child != null && child.Name == name)
                    return child;
            }

            return null;
        }

        public static void ApplyFrameFocus(View frame, Color lineColor)
        {
            SetFrameFocus(frame, focused: true, lineColor);
        }

        public static void ClearFrameFocus(View frame)
        {
            SetFrameFocus(frame, focused: false, lineColor: null);
        }

        /// <summary>
        /// Applies focus borderline styling, skipping the DALi visual rebuild when the
        /// frame is already in the requested state. Corner radius and background colour
        /// are established once at card construction and are deliberately not rewritten
        /// here.
        /// </summary>
        private static void SetFrameFocus(View frame, bool focused, Color lineColor)
        {
            if (frame == null)
                return;

            if (!_frameFocusState.TryGetValue(frame, out var state))
            {
                state = new FocusState();
                _frameFocusState.Add(frame, state);
            }

            if (state.HasValue && state.Applied == focused)
                return;

            if (focused)
            {
                frame.BorderlineColor = lineColor ?? UiTheme.MediaCardFocusBorder;
                frame.BorderlineWidth = FocusBorderlineWidth;
            }
            else
            {
                frame.BorderlineWidth = UnfocusedBorderlineWidth;
            }

            state.Applied = focused;
            state.HasValue = true;
        }

        public static void ApplyCardFocus(View card, bool focused, float scale)
        {
            var frame = GetCardFrame(card);
            var scaleTarget = frame ?? card;
            if (!focused)
                scaleTarget.Scale = ScaleOne;
            else if (scale == UiTheme.MediaCardFocusScale)
                scaleTarget.Scale = FocusScaleVector;
            else
                scaleTarget.Scale = new Vector3(scale, scale, 1f);
            if (frame != null)
            {
                frame.PositionZ = focused ? 20 : 0;
                card.PositionZ = 0;
            }
            else
            {
                card.PositionZ = focused ? 20 : 0;
            }

            if (focused)
                ApplyFrameFocus(frame, UiTheme.MediaCardFocusBorder);
            else
                ClearFrameFocus(frame);
        }
    }
}
