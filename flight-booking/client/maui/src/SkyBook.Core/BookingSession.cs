using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SkyBook.Core;

/// <summary>
/// Conversation state + prompt construction, ported from the refs, the MessageProcessor action callback and
/// <c>sendToAgent</c> in App.tsx. Wording sent to the agent is kept identical to React.
/// </summary>
public sealed class BookingSession
{
    private readonly List<string> _userPrompts = [];
    private JsonObject _trip = new();

    public string? ContextId { get; set; }
    public IReadOnlyList<string> UserPrompts => _userPrompts;
    public bool HasTripInputs => _trip.Count > 0;

    public void Reset() { ContextId = null; _userPrompts.Clear(); _trip = new(); }

    public void RecordUserPrompt(string text) => _userPrompts.Add(text);

    /// <summary>Result of turning a surface action into (chat bubble text, payload for the agent).</summary>
    public sealed record ActionPrompt(string ChatText, string AgentPayload);

    /// <summary>Port of the MessageProcessor action callback. Returns null when the action must be ignored.</summary>
    public ActionPrompt? BuildActionPrompt(string? name, JsonObject? context, bool busy)
    {
        // UI noise (row/dropdown selection) is not a command; the chat ignores prompts while a response is pending.
        if (string.IsNullOrEmpty(name) || name == "select" || busy) return null;

        var own = context ?? new JsonObject();
        var selected = own["selectedRow"] as JsonObject;
        var ownLite = (JsonObject)own.DeepClone();
        ownLite.Remove("results");
        ownLite.Remove("selectedRow");

        if (name == "searchFlights") Merge(_trip, ownLite);

        var isBook = Regex.IsMatch(name, "select|book|proceed", RegexOptions.IgnoreCase);
        var ctx = new JsonObject();
        if (isBook)
        {
            Merge(ctx, _trip); Merge(ctx, ownLite);
            if (selected is not null) ctx["selectedFlight"] = selected.DeepClone();
        }
        else Merge(ctx, ownLite);
        var merged = new JsonObject { ["name"] = name, ["context"] = ctx };

        var summary = ActionSummarizer.Summarize(merged);
        var json = merged.ToJsonString();
        var payload = isBook
            ? $"The user clicked \"Book now\". Show the booking confirmation page (stage 3) now, NOT the search form. Use exactly these user details and chosen flight, do not invent any: {json}"
            : json;
        return new ActionPrompt(summary, payload);
    }

    /// <summary>Port of the outgoing-text assembly in <c>sendToAgent</c>. Call after <see cref="RecordUserPrompt"/>.</summary>
    public string BuildOutgoingText(string text, bool silent)
    {
        var normalized = text.Trim();
        var lower = normalized.ToLowerInvariant();

        var searchIntent = !silent && (lower.Contains("search") || lower.Contains("find flights") || lower.Contains("fill details"));
        var resultsIntent = !silent && (lower.Contains("results") || lower.Contains("show flights") || lower.Contains("show me the flight results page"));
        var bookIntent = !silent && (normalized == Stage.All[2].StarterPrompt || lower.Contains("book now") || lower.Contains("confirm"));

        var prompt = normalized;
        if (searchIntent)
            prompt = $"Show me the flight search form. {normalized}\n\nFill all details on your own and find the flights.";
        else if (resultsIntent)
            prompt = $"Show me the flight results page. {normalized}";
        else if (bookIntent)
            prompt = $"Show the booking confirmation page (stage 3). {normalized}";

        var withInputs = HasTripInputs
            ? $"{prompt}\n\nContext from the user's earlier inputs (use these, do not invent passenger or trip details): {_trip.ToJsonString()}"
            : prompt;
        var basis = silent ? normalized : withInputs;

        // The current request is already the last entry; send up to 8 earlier ones, oldest first.
        var earlier = _userPrompts.Take(Math.Max(0, _userPrompts.Count - 1)).TakeLast(8).ToList();
        return earlier.Count == 0
            ? basis
            : $"{basis}\n\n## Conversation history (earlier user requests, oldest first)\n{string.Join("\n", earlier.Select(t => $"User: {t}"))}";
    }

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var kv in source) target[kv.Key] = kv.Value?.DeepClone();
    }
}
