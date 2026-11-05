using System.Text.Json.Nodes;
using Microsoft.Maui.Controls.Shapes;
using SkyBook.Core;
using Syncfusion.A2UI.Core.Schema;
using Syncfusion.A2UI.Core.State;
using Syncfusion.Maui.A2UI.Hosting;

namespace SkyBook.Maui;

/// <summary>
/// Port of <c>App.tsx</c>. Non-visual logic lives in <c>SkyBook.Core</c>; this page owns the views, the
/// <see cref="SurfaceHost"/> (React: <c>MessageProcessor</c>) and the welcome / chat view state.
/// </summary>
public partial class MainPage : ContentPage
{
    private enum View { Welcome, Chat }
    private enum Tab { Chat, Booking }

    private readonly SurfaceHost _host;
    private readonly AgentClient _agent;
    private readonly BookingSession _session;
    private readonly ChatViewModel _chat = new();
    private readonly List<Border> _stageCards = [];
    private readonly List<(Border Chip, Label Dot, Label Label)> _stepper = [];
    private readonly List<Button> _placeholderButtons = [];

    private View _view = View.Welcome;
    private Tab _tab = Tab.Chat;
    private bool _busy;
    private bool _compact;
    private Stage? _activeStage;
    private string? _silentPayload; // React: silentPayloadRef
    private SurfaceModel? _surface;

    public MainPage(SurfaceHost host, AgentClient agent, BookingSession session)
    {
        InitializeComponent();
        _host = host; _agent = agent; _session = session;

        Assist.BindingContext = _chat;
        _chat.PromptSubmitted += text => _ = SubmitPromptAsync(text, silentPayload: null, alreadyShown: true);

        SurfaceView.Host = _host;
        _host.SurfaceCreated += (_, s) => MainThread.BeginInvokeOnMainThread(() => SetSurface(s));
        _host.SurfaceUpdated += (_, s) => MainThread.BeginInvokeOnMainThread(() => { if (_surface?.Id == s.Id) UpdateStep(); });
        _host.SurfaceDeleted += (_, id) => MainThread.BeginInvokeOnMainThread(() => { if (_surface?.Id == id) SetSurface(null); });
        _host.ActionDispatched += (_, a) => MainThread.BeginInvokeOnMainThread(() => OnSurfaceAction(a));

        BuildJourney();
        BuildStageCards();
        BuildStepper();
        BuildPlaceholderSteps();
        ApplyState();
        SizeChanged += (_, _) => ApplyLayout();
    }

    // ------------------------------------------------------------------ view building

    private void BuildJourney()
    {
        foreach (var s in Stage.All)
            JourneyRow.Add(new HorizontalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Border
                    {
                        WidthRequest = 22, HeightRequest = 22, StrokeThickness = 0, BackgroundColor = Color.FromArgb("#2563eb"),
                        StrokeShape = new RoundRectangle { CornerRadius = 11 },
                        Content = new Label { Text = s.Step.ToString(), FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
                    },
                    new Label { Text = s.ShortLabel, FontSize = 12, TextColor = Color.FromArgb("#475569"), VerticalOptions = LayoutOptions.Center },
                },
            });
    }

    private void BuildStageCards()
    {
        foreach (var s in Stage.All)
        {
            var card = new StageCardView(s, OpenStage) { Margin = new Thickness(8) };
            _stageCards.Add(card);
            StageGrid.Add(card);
        }
    }

    private void BuildStepper()
    {
        foreach (var s in Stage.All)
        {
            var dot = new Label { Text = s.Step.ToString(), FontSize = 12, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, WidthRequest = 26, HeightRequest = 26 };
            var label = new Label { Text = s.StepperLabel, FontSize = 12, VerticalOptions = LayoutOptions.Center };
            var host = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 13 }, Content = dot };
            Stepper.Add(new HorizontalStackLayout { Spacing = 6, Children = { host, label } });
            _stepper.Add((host, dot, label));
        }
    }

    private void BuildPlaceholderSteps()
    {
        foreach (var s in Stage.All)
        {
            var b = new Button
            {
                Text = $"Step {s.Step} · {s.Title}   →", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0f172a"),
                BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#e2e8f0"), BorderWidth = 1, CornerRadius = 12,
                HorizontalOptions = LayoutOptions.Fill, Padding = new Thickness(14, 11),
                AutomationId = $"placeholder-{s.Id.ToString().ToLowerInvariant()}",
            };
            SemanticProperties.SetDescription(b, $"{s.Title}: {s.Tagline}");
            b.Clicked += (_, _) => OpenStage(s);
            _placeholderButtons.Add(b);
            PlaceholderSteps.Add(b);
        }
    }

    // ------------------------------------------------------------------ state → UI

    private void ApplyState()
    {
        var welcome = _view == View.Welcome;
        WelcomeScroll.IsVisible = welcome;
        Assist.IsVisible = !welcome;
        ChatHeader.IsVisible = !welcome;
        HomeButton.IsVisible = !welcome;
        Stepper.IsVisible = !welcome;
        SurfacePane.IsVisible = !welcome && (!_compact || _tab == Tab.Booking);
        ChatPane.IsVisible = welcome || !_compact || _tab == Tab.Chat;
        MobileTabs.IsVisible = !welcome && _compact;

        ChatColumn.Width = welcome || _compact ? GridLength.Star : new GridLength(420);
        SurfaceColumn.Width = welcome || _compact ? new GridLength(0) : GridLength.Star;
        ChatPane.MinimumWidthRequest = welcome || _compact ? 0 : 420;

        foreach (var b in _placeholderButtons) b.IsEnabled = !_busy;
        WelcomeSendButton.IsEnabled = !_busy && !string.IsNullOrWhiteSpace(WelcomeEditor.Text);
        ClearButton.IsEnabled = _chat.AssistItems.Count > 0;
        SurfaceLoading.IsVisible = SurfaceLoading.IsRunning = _busy;
        Assist.Margin = new Thickness(0);
        WelcomeScroll.Padding = _compact ? new Thickness(16) : new Thickness(24);

        var active = _tab == Tab.Chat;
        ChatTabButton.BackgroundColor = active ? Color.FromArgb("#2563eb") : Colors.White;
        ChatTabButton.TextColor = active ? Colors.White : Color.FromArgb("#64748b");
        ViewTabButton.BackgroundColor = !active ? Color.FromArgb("#2563eb") : Colors.White;
        ViewTabButton.TextColor = !active ? Colors.White : Color.FromArgb("#64748b");

        UpdateStep();
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        var compact = Width > 0 && Width < AppConfig.CompactWidth;
        if (compact != _compact) { _compact = compact; ApplyState(); return; }

        // React: .is-welcome .stage-grid is 3 columns on wide screens, 1 on narrow ones.
        var available = Math.Max(280, Math.Min(1100, Width - 48 - 48));
        var cardWidth = _compact ? available - 16 : (available - 6 * 8) / 3;
        foreach (var c in _stageCards) c.WidthRequest = Math.Max(240, cardWidth);
        Body.Padding = _compact ? new Thickness(12) : new Thickness(24, 20, 24, 24);
    }

    private int CurrentStep() =>
        _surface is null ? 0 : Stage.InferStep(_surface.Id,
            _surface.ComponentsModel.Keys);

    private void UpdateStep()
    {
        var step = CurrentStep();
        for (var i = 0; i < _stepper.Count; i++)
        {
            var (chip, dot, label) = _stepper[i];
            var n = i + 1;
            var current = step == n; var done = step > n;
            var bg = current ? Color.FromArgb("#2563eb") : done ? Color.FromArgb("#14a37f") : Color.FromArgb("#e2e8f0");
            chip.BackgroundColor = bg;
            dot.Text = done ? "✓" : n.ToString();
            dot.TextColor = current || done ? Colors.White : Color.FromArgb("#64748b");
            label.TextColor = current ? Color.FromArgb("#0f172a") : Color.FromArgb("#64748b");
            label.FontAttributes = current ? FontAttributes.Bold : FontAttributes.None;
            label.IsVisible = !_compact || current; // React: .step-label hidden on narrow screens unless current
        }

        var shown = step > 0 ? Stage.All[step - 1] : _activeStage;
        StagePillRow.IsVisible = shown is not null;
        if (shown is not null) StagePillLabel.Text = $"{shown.Icon}  Step {shown.Step} · {shown.Title}";
    }

    private void SetSurface(SurfaceModel? surface)
    {
        _surface = surface;
        SurfaceView.Surface = surface;
        SurfaceScroll.IsVisible = surface is not null;
        Placeholder.IsVisible = surface is null;
        UpdateStep();
        if (surface is not null && _compact) { _tab = Tab.Booking; ApplyState(); }
    }

    // ------------------------------------------------------------------ user interaction

    private void OnHomeClicked(object? s, EventArgs e) => BackHome();
    private void OnChatTab(object? s, EventArgs e) { _tab = Tab.Chat; ApplyState(); }
    private void OnViewTab(object? s, EventArgs e) { _tab = Tab.Booking; ApplyState(); }
    private void OnClearClicked(object? s, EventArgs e) => ResetConversation();
    private void OnWelcomeTextChanged(object? s, TextChangedEventArgs e) => ApplyState();

    private void OnWelcomeSend(object? s, EventArgs e)
    {
        var text = WelcomeEditor.Text?.Trim();
        if (string.IsNullOrEmpty(text) || _busy) return;
        WelcomeEditor.Text = "";
        _ = SubmitPromptAsync(text, null, alreadyShown: false);
    }

    private void OpenStage(Stage stage)
    {
        if (_busy) return;
        _activeStage = stage;
        _ = SubmitPromptAsync(stage.StarterPrompt, null, alreadyShown: false);
    }

    private void ResetSurfaces()
    {
        if (_host.Surfaces.Count == 0) return;
        var del = new JsonArray(_host.Surfaces.Keys.Select(id =>
            (JsonNode)new JsonObject { ["version"] = "v0.9", ["deleteSurface"] = new JsonObject { ["surfaceId"] = id } }).ToArray());
        _host.ProcessJson(del.ToJsonString());
    }

    private void ResetConversation()
    {
        _session.Reset();
        _silentPayload = null;
        _chat.Clear();
        ApplyState();
    }

    private void BackHome()
    {
        ResetSurfaces();
        SetSurface(null);
        ResetConversation();
        _activeStage = null;
        _view = View.Welcome;
        _tab = Tab.Chat;
        ApplyState();
    }

    /// <summary>Port of the MessageProcessor action callback in App.tsx.</summary>
    private void OnSurfaceAction(A2uiClientAction action)
    {
        var surface = _host.Surfaces.GetValueOrDefault(action.SurfaceId);
        var ctx = ContextResolver.Resolve(action.Context, path => surface?.DataModel.Get(path));
        var prompt = _session.BuildActionPrompt(action.Name, ctx, _busy);
        if (prompt is null) return;
        // The summary is shown as the user's chat prompt; the agent receives the detailed payload.
        _ = SubmitPromptAsync(prompt.ChatText, prompt.AgentPayload, alreadyShown: false);
    }

    // ------------------------------------------------------------------ agent round-trip

    /// <summary>Port of <c>onPromptRequest</c> + <c>sendToAgent</c> (App.tsx).</summary>
    private async Task SubmitPromptAsync(string text, string? silentPayload, bool alreadyShown)
    {
        if (_busy) return; // AssistView ignores prompts while a response is pending
        _silentPayload = silentPayload;
        if (!alreadyShown) _chat.AddUserMessage(text);
        _session.RecordUserPrompt(text);
        _view = View.Chat;
        _tab = Tab.Chat;
        var payload = _silentPayload; _silentPayload = null;
        await SendToAgentAsync(payload ?? text, silent: payload is not null);
    }

    private async Task SendToAgentAsync(string text, bool silent)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _busy = true;
        _chat.ShowTyping();
        ApplyState();
        try
        {
            var outgoing = _session.BuildOutgoingText(text, silent);
            var reply = await _agent.SendAsync(outgoing, _session.ContextId);
            if (reply.ContextId is not null) _session.ContextId = reply.ContextId;

            if (reply.A2uiMessages is not { } messages)
            {
                System.Diagnostics.Debug.WriteLine("[A2UI] no a2uiEnvelope in response");
                _chat.AddBotMessage("I couldn't get a valid result from the booking service. Please try again.");
                return;
            }

            try
            {
                // Always render on a fresh surface: drop existing ones when the agent creates a new surface.
                if (EnvelopeSanitizer.HasCreateSurface(messages)) ResetSurfaces();
                _host.ProcessJson(EnvelopeSanitizer.Sanitize(messages).ToJsonString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[A2UI] failed to render surface: {ex}");
                _chat.AddBotMessage($"I received the results but couldn't render them: {ex.Message}");
                return;
            }

            var explanation = EnvelopeSanitizer.ExtractExplanation(messages);
            var answer = explanation.Length > 0 ? explanation : reply.AgentText;
            _chat.AddBotMessage(answer.Length > 0 ? answer : "Updated the booking view.");
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[A2UI] agent unreachable: {ex}");
            await ShowBundledStageAsync(text);
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[A2UI] agent timeout: {ex}");
            await ShowBundledStageAsync(text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[A2UI] fetch error: {ex}");
            _chat.AddBotMessage($"Something went wrong: {ex.Message}");
        }
        finally
        {
            _chat.HideTyping();
            _busy = false;
            ApplyState();
        }
    }

    /// <summary>
    /// Agent unreachable: open the matching stage page from the bundled design (same JSON the agent serves),
    /// so Search / Results / Confirmation still open. Free-text requests that need the AI get a hint instead.
    /// </summary>
    private async Task ShowBundledStageAsync(string text)
    {
        var lower = text.ToLowerInvariant();
        string? file = null; string say = "";
        if (lower.Contains("selectflightandproceed") || lower.Contains("booking and confirmation") || lower.Contains("confirmation page"))
        { file = "stage3-booking-confirmation.json"; say = "Here is the booking confirmation page."; }
        else if (lower.Contains("searchflights") || lower.Contains("flight results"))
        { file = "stage2-flight-results.json"; say = "Here are the flight results. Tap a flight row, then press Book now."; }
        else if (lower.Contains("flight search form"))
        { file = "stage1-flight-search.json"; say = "Here is the flight search form. Fill in your trip and press Search Flights."; }

        if (file is null)
        {
            _chat.AddBotMessage($"I can't reach the SkyBook agent at {AppConfig.AgentUrl}, so I can't answer free-text requests yet. " +
                                "Start the agent (or harness/mock_agent.py) and try again, or pick a step from the workspace.");
            return;
        }

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("designs/" + file);
            using var reader = new StreamReader(stream);
            var messages = System.Text.Json.Nodes.JsonNode.Parse(await reader.ReadToEndAsync())!.AsArray();
            ResetSurfaces();
            _host.ProcessJson(EnvelopeSanitizer.Sanitize(messages).ToJsonString());
            _chat.AddBotMessage(say + " (offline preview - start the agent for live AI replies)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[A2UI] bundled stage failed: {ex}");
            _chat.AddBotMessage($"I couldn't open that page: {ex.Message}");
        }
    }
}
