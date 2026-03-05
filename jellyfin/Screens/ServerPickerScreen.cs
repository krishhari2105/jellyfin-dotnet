using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;

namespace JellyfinTizen.Screens
{
    public class ServerPickerScreen : ScreenBase, IKeyHandler
    {
        private const int CardWidth = 286;
        private const int CardBoxHeight = 164;
        private const int CardHeight = 282;
        private const int CardSpacing = 28;
        private const int CardsViewportHeight = 332;
        private const int ScrollEdgePadding = 64;
        private const int FallbackViewportWidth = 960;

        private sealed class ServerCard
        {
            public AppState.StoredServer Server;
            public View Root;
            public View Box;
            public TextLabel Initials;
            public TextLabel Name;
            public TextLabel Detail;
        }

        private readonly List<AppState.StoredServer> _servers;
        private readonly List<ServerCard> _cards = new();
        private readonly List<View> _actionButtons = new();
        private readonly string _initialErrorMessage;

        private View _cardsViewport;
        private View _cardsRow;
        private View _addButton;
        private View _removeButton;
        private TextLabel _errorLabel;
        private int _cardsContentWidth;
        private int _selectedCardIndex;
        private int _selectedActionIndex;
        private bool _cardsFocused;
        private bool _busy;

        public ServerPickerScreen(string initialErrorMessage = null)
        {
            _initialErrorMessage = initialErrorMessage;
            _servers = new List<AppState.StoredServer>(AppState.GetStoredServers());
            Initialize();
        }

        private void Initialize()
        {
            var root = MonochromeAuthFactory.CreateBackground();
            var panel = MonochromeAuthFactory.CreatePanel(width: 1280);
            panel.Add(MonochromeAuthFactory.CreateTitle("Choose Your Server"));
            panel.Add(MonochromeAuthFactory.CreateSubtitle(
                $"Select a server card to continue. Max saved servers: {AppState.MaxStoredServers}."
            ));

            _cardsViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = CardsViewportHeight,
                ClippingMode = ClippingModeType.ClipChildren
            };

            _cardsRow = new View
            {
                PositionY = 8,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(CardSpacing, 0)
                }
            };

            foreach (var server in _servers)
            {
                var card = CreateServerCard(server);
                _cards.Add(card);
                _cardsRow.Add(card.Root);
            }

            if (_cards.Count > 0)
            {
                _cardsContentWidth = (_cards.Count * CardWidth) + ((_cards.Count - 1) * CardSpacing);
                _cardsRow.WidthSpecification = _cardsContentWidth;
                _cardsViewport.Add(_cardsRow);
            }
            else
            {
                _cardsViewport.Add(new TextLabel("No saved servers yet")
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    PointSize = 24.0f,
                    TextColor = new Color(1f, 1f, 1f, 0.72f)
                });
            }

            panel.Add(_cardsViewport);

            var actionRow = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CellPadding = new Size2D(20, 0)
                }
            };

            _addButton = MonochromeAuthFactory.CreateButton("Add Server", out _, primary: true);
            _removeButton = MonochromeAuthFactory.CreateButton("Remove Selected", out _, primary: false);
            _actionButtons.Add(_addButton);
            _actionButtons.Add(_removeButton);
            actionRow.Add(_addButton);
            actionRow.Add(_removeButton);
            panel.Add(actionRow);

            _errorLabel = MonochromeAuthFactory.CreateErrorLabel();
            panel.Add(_errorLabel);

            root.Add(panel);
            Add(root);
        }

        public override void OnShow()
        {
            _busy = false;
            _selectedCardIndex = Math.Max(0, _servers.FindIndex(s => s.IsActive));
            if (_selectedCardIndex < 0)
                _selectedCardIndex = 0;
            _selectedActionIndex = 0;
            _cardsFocused = _cards.Count > 0;
            EnsureActionSelectionValid();
            UpdateVisualState();

            if (_cardsFocused)
            {
                FocusManager.Instance.SetCurrentFocusView(_cards[_selectedCardIndex].Root);
                EnsureFocusedCardVisible(centerWhenNoOverflow: true);
            }
            else
            {
                FocusManager.Instance.SetCurrentFocusView(_actionButtons[_selectedActionIndex]);
            }

            if (!string.IsNullOrWhiteSpace(_initialErrorMessage))
                ShowErrorMessage(_initialErrorMessage);
        }

        public void HandleKey(AppKey key)
        {
            if (_busy)
                return;

            switch (key)
            {
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    return;
                case AppKey.Left:
                    if (_cardsFocused)
                        MoveCardFocus(-1);
                    else
                        MoveActionFocus(-1);
                    return;
                case AppKey.Right:
                    if (_cardsFocused)
                        MoveCardFocus(1);
                    else
                        MoveActionFocus(1);
                    return;
                case AppKey.Up:
                    if (!_cardsFocused && _cards.Count > 0)
                    {
                        _cardsFocused = true;
                        FocusManager.Instance.SetCurrentFocusView(_cards[_selectedCardIndex].Root);
                        EnsureFocusedCardVisible();
                        UpdateVisualState();
                    }
                    return;
                case AppKey.Down:
                    if (_cardsFocused)
                    {
                        _cardsFocused = false;
                        EnsureActionSelectionValid();
                        FocusManager.Instance.SetCurrentFocusView(_actionButtons[_selectedActionIndex]);
                        UpdateVisualState();
                    }
                    return;
                case AppKey.Enter:
                    if (_cardsFocused)
                    {
                        if (_cards.Count > 0)
                            _ = SelectServerAsync(_cards[_selectedCardIndex].Server);
                    }
                    else
                    {
                        ActivateAction();
                    }
                    return;
            }
        }

        private ServerCard CreateServerCard(AppState.StoredServer server)
        {
            var root = new View
            {
                WidthSpecification = CardWidth,
                HeightSpecification = CardHeight,
                Focusable = true
            };

            var box = new View
            {
                WidthSpecification = CardWidth,
                HeightSpecification = CardBoxHeight,
                CornerRadius = 24.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 2.0f,
                BorderlineColor = MonochromeAuthFactory.PanelFallbackBorder,
                BackgroundColor = MonochromeAuthFactory.PanelFallbackColor
            };

            var initials = new TextLabel(BuildInitials(server?.DisplayName))
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 44.0f,
                TextColor = new Color(1f, 1f, 1f, 0.92f)
            };
            box.Add(initials);

            var name = new TextLabel(server?.DisplayName ?? "Server")
            {
                WidthSpecification = CardWidth,
                HeightSpecification = 52,
                PositionY = CardBoxHeight + 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                PointSize = 23.0f,
                TextColor = new Color(1f, 1f, 1f, 0.92f),
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            var detail = new TextLabel(BuildCardDetail(server))
            {
                WidthSpecification = CardWidth,
                HeightSpecification = 58,
                PositionY = CardBoxHeight + 70,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                PointSize = 18.0f,
                TextColor = new Color(1f, 1f, 1f, 0.70f),
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            root.Add(box);
            root.Add(name);
            root.Add(detail);

            return new ServerCard
            {
                Server = server,
                Root = root,
                Box = box,
                Initials = initials,
                Name = name,
                Detail = detail
            };
        }

        private void MoveCardFocus(int delta)
        {
            if (_cards.Count == 0)
                return;

            var next = Math.Clamp(_selectedCardIndex + delta, 0, _cards.Count - 1);
            if (next == _selectedCardIndex)
                return;

            _selectedCardIndex = next;
            FocusManager.Instance.SetCurrentFocusView(_cards[_selectedCardIndex].Root);
            EnsureFocusedCardVisible();
            UpdateVisualState();
        }

        private void MoveActionFocus(int delta)
        {
            if (_actionButtons.Count == 0)
                return;

            var min = 0;
            var max = _actionButtons.Count - 1;
            var next = Math.Clamp(_selectedActionIndex + delta, min, max);
            if (next == _selectedActionIndex)
                return;

            _selectedActionIndex = next;
            EnsureActionSelectionValid();
            FocusManager.Instance.SetCurrentFocusView(_actionButtons[_selectedActionIndex]);
            UpdateVisualState();
        }

        private void EnsureActionSelectionValid()
        {
            if (_cards.Count == 0 && _selectedActionIndex == 1)
                _selectedActionIndex = 0;
        }

        private void UpdateVisualState()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                ApplyCardVisualState(
                    _cards[i],
                    selected: i == _selectedCardIndex,
                    focusOnCards: _cardsFocused
                );
            }

            var addFocused = !_cardsFocused && _selectedActionIndex == 0;
            var removeFocused = !_cardsFocused && _selectedActionIndex == 1;
            MonochromeAuthFactory.SetButtonFocusState(_addButton, primary: true, focused: addFocused);
            MonochromeAuthFactory.SetButtonFocusState(_removeButton, primary: false, focused: removeFocused);
            _removeButton.Opacity = _cards.Count > 0 ? 1.0f : 0.45f;
        }

        private static void ApplyCardVisualState(ServerCard card, bool selected, bool focusOnCards)
        {
            if (card == null)
                return;

            if (!selected)
            {
                card.Root.Scale = Vector3.One;
                card.Root.PositionZ = 0;
                card.Box.BackgroundColor = MonochromeAuthFactory.PanelFallbackColor;
                card.Box.BorderlineColor = MonochromeAuthFactory.PanelFallbackBorder;
                card.Box.BorderlineWidth = 2.0f;
                card.Initials.TextColor = new Color(1f, 1f, 1f, 0.92f);
                card.Name.TextColor = new Color(1f, 1f, 1f, 0.84f);
                card.Detail.TextColor = new Color(1f, 1f, 1f, 0.64f);
                return;
            }

            if (focusOnCards)
            {
                card.Root.Scale = new Vector3(1.05f, 1.05f, 1f);
                card.Root.PositionZ = 18;
                card.Box.BackgroundColor = new Color(1f, 1f, 1f, 1f);
                card.Box.BorderlineColor = new Color(1f, 1f, 1f, 1f);
                card.Box.BorderlineWidth = 0.0f;
                card.Initials.TextColor = new Color(0f, 0f, 0f, 1f);
                card.Name.TextColor = new Color(1f, 1f, 1f, 0.98f);
                card.Detail.TextColor = new Color(1f, 1f, 1f, 0.86f);
                return;
            }

            card.Root.Scale = Vector3.One;
            card.Root.PositionZ = 8;
            card.Box.BackgroundColor = new Color(1f, 1f, 1f, 0.18f);
            card.Box.BorderlineColor = new Color(1f, 1f, 1f, 0.72f);
            card.Box.BorderlineWidth = 2.0f;
            card.Initials.TextColor = new Color(1f, 1f, 1f, 0.98f);
            card.Name.TextColor = new Color(1f, 1f, 1f, 0.92f);
            card.Detail.TextColor = new Color(1f, 1f, 1f, 0.76f);
        }

        private void EnsureFocusedCardVisible(bool centerWhenNoOverflow = false)
        {
            if (_cards.Count == 0 || _cardsViewport == null || _cardsRow == null)
                return;

            float viewportWidth = _cardsViewport.SizeWidth > 0
                ? _cardsViewport.SizeWidth
                : FallbackViewportWidth;
            if (viewportWidth <= 0)
                return;

            if (_cardsContentWidth <= viewportWidth)
            {
                if (centerWhenNoOverflow)
                    _cardsRow.PositionX = (viewportWidth - _cardsContentWidth) / 2;
                return;
            }

            float targetX = _cardsRow.PositionX;
            float itemLeft = _selectedCardIndex * (CardWidth + CardSpacing);
            float itemRight = itemLeft + CardWidth;
            float visibleLeft = -_cardsRow.PositionX;
            float visibleRight = visibleLeft + viewportWidth;

            if (itemRight > visibleRight - ScrollEdgePadding)
                targetX -= itemRight - (visibleRight - ScrollEdgePadding);
            else if (itemLeft < visibleLeft + ScrollEdgePadding)
                targetX += (visibleLeft + ScrollEdgePadding) - itemLeft;

            float minX = viewportWidth - _cardsContentWidth;
            if (targetX < minX)
                targetX = minX;
            if (targetX > 0)
                targetX = 0;

            _cardsRow.PositionX = targetX;
        }

        private void ActivateAction()
        {
            if (_selectedActionIndex == 0)
            {
                AddServer();
                return;
            }

            if (_selectedActionIndex == 1)
            {
                RemoveSelectedServer();
                return;
            }
        }

        private void AddServer()
        {
            if (!AppState.CanAddServer())
            {
                ShowErrorMessage($"Saved server limit reached ({AppState.MaxStoredServers}).");
                return;
            }

            NavigationService.NavigateWithLoading(
                () => new ServerSetupScreen(),
                "Loading server setup..."
            );
        }

        private void RemoveSelectedServer()
        {
            if (_cards.Count == 0)
            {
                ShowErrorMessage("No saved server selected.");
                return;
            }

            var server = _cards[_selectedCardIndex].Server;
            if (server == null || string.IsNullOrWhiteSpace(server.Url))
            {
                ShowErrorMessage("No saved server selected.");
                return;
            }

            if (!AppState.RemoveServer(server.Url))
            {
                ShowErrorMessage("Unable to remove server.");
                return;
            }

            NavigationService.Navigate(
                new ServerPickerScreen("Server removed."),
                addToStack: false
            );
        }

        private async Task SelectServerAsync(AppState.StoredServer server)
        {
            if (server == null || string.IsNullOrWhiteSpace(server.Url))
            {
                ShowErrorMessage("Invalid server selection.");
                return;
            }

            _busy = true;

            RunOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new LoadingScreen("Connecting to server..."),
                    addToStack: false
                );
            });

            try
            {
                if (!AppState.ActivateServer(server.Url, includeSession: true))
                {
                    NavigateToPickerWithError("Saved server was not found.");
                    return;
                }

                var hasTokenSession = !string.IsNullOrWhiteSpace(AppState.AccessToken) &&
                                      !string.IsNullOrWhiteSpace(AppState.UserId);
                if (hasTokenSession && await TryResumeSavedSessionAsync())
                {
                    NavigateToHome();
                    return;
                }

                var users = await WithTimeout(AppState.Jellyfin.GetPublicUsersAsync(), 12000);
                NavigateToUserSelect(users);
            }
            catch
            {
                NavigateToPickerWithError("Failed to connect. Please try again.");
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task<bool> TryResumeSavedSessionAsync()
        {
            try
            {
                var me = await WithTimeout(AppState.Jellyfin.GetCurrentUserAsync(), 9000);
                var userId = string.IsNullOrWhiteSpace(AppState.UserId)
                    ? me.userId
                    : AppState.UserId;
                var username = string.IsNullOrWhiteSpace(AppState.Username)
                    ? me.username
                    : AppState.Username;

                if (string.IsNullOrWhiteSpace(userId))
                    return false;

                AppState.SaveSession(
                    AppState.ServerUrl,
                    AppState.AccessToken,
                    userId,
                    username ?? string.Empty
                );
                AppState.Jellyfin.SetUserId(userId);
                return true;
            }
            catch (Exception ex) when (IsUnauthorized(ex))
            {
                AppState.ClearSession(clearServer: false);
                return false;
            }
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task)
                throw new TimeoutException("Server connection request timed out.");

            return await task;
        }

        private static bool IsUnauthorized(Exception ex)
        {
            if (ex is HttpRequestException httpEx)
            {
                if (httpEx.StatusCode == HttpStatusCode.Unauthorized ||
                    httpEx.StatusCode == HttpStatusCode.Forbidden)
                {
                    return true;
                }
            }

            var message = ex?.Message ?? string.Empty;
            return message.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void NavigateToHome()
        {
            RunOnUiThread(() =>
            {
                NavigationService.ClearStack();
                NavigationService.Navigate(
                    new HomeLoadingScreen(),
                    addToStack: false
                );
            });
        }

        private void NavigateToUserSelect(List<JellyfinUser> users)
        {
            RunOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new UserSelectScreen(users),
                    addToStack: false
                );
            });
        }

        private void NavigateToPickerWithError(string message)
        {
            RunOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new ServerPickerScreen(message),
                    addToStack: false
                );
            });
        }

        private static string BuildCardDetail(AppState.StoredServer server)
        {
            if (server == null)
                return string.Empty;

            var parts = new List<string>();

            if (server.IsActive)
                parts.Add("Current");

            if (server.HasSavedSession)
            {
                parts.Add(string.IsNullOrWhiteSpace(server.Username)
                    ? "Saved Login"
                    : server.Username);
            }
            else
            {
                parts.Add("Sign In Required");
            }

            return string.Join(" | ", parts);
        }

        private static string BuildInitials(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "JF";

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return "JF";
            if (words.Length == 1)
            {
                var one = words[0].Trim();
                return one.Length >= 2
                    ? one.Substring(0, 2).ToUpperInvariant()
                    : one.Substring(0, 1).ToUpperInvariant();
            }

            return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
        }

        private void ShowErrorMessage(string message)
        {
            _errorLabel.Text = message ?? string.Empty;

            var timer = new System.Timers.Timer(5000);
            timer.Elapsed += (_, _) =>
            {
                RunOnUiThread(() => _errorLabel.Text = string.Empty);
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }
    }
}
