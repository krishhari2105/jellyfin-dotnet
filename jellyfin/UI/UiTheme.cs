using Tizen.NUI;

namespace JellyfinTizen.UI
{
    public static class UiTheme
    {
        public static readonly Color Background = new Color(7f / 255f, 11f / 255f, 20f / 255f, 1f);
        public static readonly Color Surface = new Color(17f / 255f, 24f / 255f, 38f / 255f, 0.94f);
        public static readonly Color SurfaceFocused = new Color(24f / 255f, 34f / 255f, 54f / 255f, 0.98f);
        public static readonly Color SurfaceMuted = new Color(26f / 255f, 34f / 255f, 49f / 255f, 0.96f);
        public static readonly Color SurfaceBorder = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color Accent = new Color(0f / 255f, 167f / 255f, 225f / 255f, 1f);
        public static readonly Color AccentFocused = new Color(30f / 255f, 176f / 255f, 222f / 255f, 1f);
        public static readonly Color AccentSoft = new Color(0f / 255f, 167f / 255f, 225f / 255f, 0.24f);
        public static readonly Color MediaCardFocusFill = new Color(1f, 1f, 1f, 0.16f);
        public static readonly Color MediaCardFocusBorder = new Color(1f, 1f, 1f, 0.92f);
        public static readonly Color TextPrimary = new Color(243f / 255f, 247f / 255f, 255f / 255f, 1f);
        public static readonly Color TextSecondary = new Color(183f / 255f, 195f / 255f, 216f / 255f, 1f);
        public static readonly Color Danger = new Color(255f / 255f, 90f / 255f, 100f / 255f, 1f);
        public static readonly Color DetailsBackdropDim = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color DetailsPosterSurface = new Color(17f / 255f, 24f / 255f, 38f / 255f, 1f);
        public static readonly Color DetailsOverviewText = new Color(183f / 255f, 195f / 255f, 216f / 255f, 1f);
        public static readonly Color DetailsActionButtonBase = new Color(17f / 255f, 24f / 255f, 38f / 255f, 0.78f);
        public static readonly Color DetailsActionButtonFocused = new Color(0f / 255f, 167f / 255f, 225f / 255f, 0.80f);
        public static readonly Color DetailsChipSurface = new Color(31f / 255f, 42f / 255f, 59f / 255f, 1.0f);
        public static readonly Color PlayerTrackBase = new Color(183f / 255f, 195f / 255f, 216f / 255f, 0.35f);
        public static readonly Color PlayerTrackFocused = new Color(44f / 255f, 194f / 255f, 240f / 255f, 0.34f);
        public static readonly Color PlayerTrackIdle = new Color(243f / 255f, 247f / 255f, 255f / 255f, 0.50f);
        public static readonly Color PlayerPreviewFill = new Color(243f / 255f, 247f / 255f, 255f / 255f, 0.68f);
        public static readonly Color PlayerTextMuted = new Color(183f / 255f, 195f / 255f, 216f / 255f, 0.72f);
        public static readonly Color PlayerPanel = new Color(17f / 255f, 24f / 255f, 38f / 255f, 0.96f);
        public static readonly Color PlayerPanelFocused = new Color(24f / 255f, 34f / 255f, 54f / 255f, 0.98f);
        public static readonly Color PlayerButtonBase = new Color(243f / 255f, 247f / 255f, 255f / 255f, 0.12f);
        public static readonly Color PlayerButtonFocused = new Color(44f / 255f, 194f / 255f, 240f / 255f, 0.34f);
        public static readonly Color PlayerSelectionFill = new Color(44f / 255f, 194f / 255f, 240f / 255f, 0.24f);
        public static readonly Color PlayerSubtitleOffsetBase = new Color(243f / 255f, 247f / 255f, 255f / 255f, 0.18f);
        public static readonly Color PlayerSubtitleOffsetTrack = new Color(183f / 255f, 195f / 255f, 216f / 255f, 0.55f);
        public static readonly Color PlayerSubtitleOffsetCenter = new Color(243f / 255f, 247f / 255f, 255f / 255f, 0.92f);
        public static readonly Color PlayerFeedbackBackdrop = new Color(4f / 255f, 8f / 255f, 16f / 255f, 0.46f);
        public static readonly Color PlayerSeekBackdrop = new Color(4f / 255f, 8f / 255f, 16f / 255f, 0.38f);
        public static readonly Color PlayerTopGradientStart = new Color(4f / 255f, 8f / 255f, 16f / 255f, 0.92f);
        public static readonly Color PlayerTopGradientEnd = new Color(4f / 255f, 8f / 255f, 16f / 255f, 0.0f);
        public static readonly Color PlayerBottomGradientStart = new Color(4f / 255f, 8f / 255f, 16f / 255f, 0.0f);
        public static readonly Color PlayerBottomGradientEnd = new Color(4f / 255f, 8f / 255f, 16f / 255f, 0.96f);
        public static readonly Color PlayerTimeText = new Color(243f / 255f, 247f / 255f, 255f / 255f, 0.94f);
        public static readonly Color PlayerSubtitleText = new Color(243f / 255f, 247f / 255f, 255f / 255f, 0.96f);

        public const int SidePadding = 76;
        public const int PanelWidth = 1120;
        public const int PanelTop = 132;
        public const int PanelVerticalPadding = 56;
        public const int PanelRadius = 28;
        public const int FieldHeight = 92;
        public const int ButtonHeight = 92;
        public const int ListItemHeight = 92;
        public const int MediaRowTitleHeight = 60;

        public const float DisplayTitle = 52f;
        public const float SectionTitle = 44f;
        public const float Body = 28f;
        public const float Caption = 23f;
        public const float MediaTopBarTitle = 40f;
        public const float MediaRowTitle = 34f;
        public const float MediaCardTitle = 26f;
        public const float MediaCardSubtitle = 20f;
        public const float SettingsPanelTitle = 26f;
        public const float SettingsPanelOption = 24f;

        // Media browser layout spacing
        public const int HomeTopBarHeight = 80;
        public const int HomeRowsTopGap = 28;
        public const int HomeSidePadding = 60;
        public const int HomeFocusBorder = 5;
        public const int HomeFocusPad = 24;

        public const int LibraryTopBarHeight = 90;
        public const int LibraryTopBarPadding = 60;
        public const int LibraryContentTopGap = 20;
        public const int LibrarySidePadding = 80;
        public const int LibraryCardSpacing = 30;
        public const int LibraryRowSpacing = 50;
        public const int LibraryFocusBorder = 5;
        public const int LibraryFocusPad = 20;
        public const int LibraryTopGlowPadBoost = 8;
        public const int PlayerOverlayWidth = 450;
        public const int PlayerOverlayHeight = 500;
        public const int PlayerOverlayHeaderHeight = 80;
        public const int PlayerOverlayListRowHeight = 60;
        public const float PlayerOverlayListRowRadius = 8.0f;
        public const ushort PlayerOverlayItemMarginHorizontal = 20;
        public const ushort PlayerOverlayItemMarginVertical = 5;
        public const ushort PlayerOverlayItemPaddingLeft = 20;
        public const int PlayerAudioScrollHeight = 420;
        public const int PlayerSubtitleOffsetButtonWidth = 280;
        public const int PlayerSubtitleOffsetButtonHeight = 45;
        public const float PlayerSubtitleOffsetButtonRadius = 22.5f;
        public const int PlayerSubtitleOffsetButtonX = 85;
        public const int PlayerSubtitleOffsetTrackContainerY = 40;
        public const int PlayerSubtitleOffsetTrackHeight = 36;
        public const int PlayerSubtitleOffsetLineHeight = 2;
        public const int PlayerSubtitleOffsetLineY = 17;
        public const float PlayerSubtitleOffsetLineRadius = 1.0f;
        public const int PlayerSubtitleOffsetCenterWidth = 2;
        public const int PlayerSubtitleOffsetCenterHeight = 16;
        public const int PlayerSubtitleOffsetCenterY = 10;
        public const int PlayerSubtitleOffsetThumbSize = 12;
        public const int PlayerSubtitleOffsetThumbY = 12;
        public const float PlayerSubtitleOffsetThumbRadius = 6.0f;
        public const int PlayerControlsRowHeight = 80;
        public const int PlayerControlButtonHeight = 60;
        public const float PlayerControlButtonRadius = 28.0f;
        public const int PlayerControlIconSize = 28;
        public const int PlayerClockBoxWidth = 180;
        public const int PlayerClockBoxHeight = 42;
        public const int PlayerProgressRowHeight = 50;
        public const int PlayerProgressRowY = 90;
        public const int PlayerEndsAtHeight = 36;
        public const float PlayerClockText = 26f;
        public const float PlayerTimeTextSize = 26f;
        public const float PlayerEndsAtText = 23f;
        public const float PlayerTopTitleText = 36f;
        public const float PlayerControlLabelText = 26f;
        public const float PlayerSeekFeedbackText = 24f;
        public const float PlayerSmartPopupTitle = 27f;
        public const float PlayerSmartPopupSubtitle = 21f;
        public const float PlayerOverlayHeader = 34f;
        public const float PlayerOverlayItem = 28f;
        public const float PlayerAudioItem = 26f;
        public const float PlayerOffsetLabel = 20f;
        public const float PlayerSubtitleTextSize = 48f;

        // Startup/loading
        public const float StartupLoadingText = 44f;
        public const float HomeLoadingText = 44f;
        public const int LoadingCardWidth = 520;
        public const int LoadingCardHeight = 240;
        public const int LoadingSpinnerSize = 92;
        public const float LoadingSpinnerRadius = 46f;
        public const float LoadingMessageText = 28f;

        // Legacy details fallback screen
        public const float LegacyDetailsTitle = 24f;
        public const float LegacyDetailsBody = 16f;
    }
}
