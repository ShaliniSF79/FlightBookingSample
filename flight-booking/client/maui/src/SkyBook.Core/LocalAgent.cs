using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SkyBook.Core;

public sealed record AzureOpenAiOptions(string Endpoint, string ApiKey, string Deployment, string ApiVersion = "2024-10-21");

/// <summary>Flat trip/traveller inputs (same keys the search form's action context uses).</summary>
public sealed class Trip
{
    public string TripType = "", From = "", To = "", DepartureDate = "", ReturnDate = "", CabinClass = "";
    public int Adults, Children;
    public string FirstName = "", LastName = "", Email = "", PassportNumber = "", Nationality = "";

    public static Trip FromJson(JsonObject? o)
    {
        var t = new Trip();
        if (o is null) return t;
        t.TripType = NormTripType(S(o["tripType"]));
        t.From = S(o["from"]); t.To = S(o["to"]);
        t.DepartureDate = S(o["departureDate"]); t.ReturnDate = S(o["returnDate"]);
        t.CabinClass = NormCabin(S(o["cabinClass"]));
        t.Adults = I(o["adults"]); t.Children = I(o["children"]);
        t.FirstName = S(o["firstName"]); t.LastName = S(o["lastName"]); t.Email = S(o["email"]);
        t.PassportNumber = S(o["passportNumber"]); t.Nationality = S(o["nationality"]);
        return t;
    }

    /// <summary>Copy values from <paramref name="other"/> where it has something (later inputs win).</summary>
    public void Overlay(Trip other)
    {
        if (other.TripType != "") TripType = other.TripType;
        if (other.From != "") From = other.From;
        if (other.To != "") To = other.To;
        if (other.DepartureDate != "") DepartureDate = other.DepartureDate;
        if (other.ReturnDate != "") ReturnDate = other.ReturnDate;
        if (other.CabinClass != "") CabinClass = other.CabinClass;
        if (other.Adults > 0) Adults = other.Adults;
        if (other.Children > 0) Children = other.Children;
        if (other.FirstName != "") FirstName = other.FirstName;
        if (other.LastName != "") LastName = other.LastName;
        if (other.Email != "") Email = other.Email;
        if (other.PassportNumber != "") PassportNumber = other.PassportNumber;
        if (other.Nationality != "") Nationality = other.Nationality;
    }

    public List<string> MissingForSearch()
    {
        var m = new List<string>();
        if (From == "") m.Add("origin (From)");
        if (To == "") m.Add("destination (To)");
        if (DepartureDate == "") m.Add("departure date");
        if (TripType == "") m.Add("trip type");
        if (TripType == "roundTrip" && ReturnDate == "") m.Add("return date");
        if (CabinClass == "") m.Add("cabin class");
        if (Adults < 1) m.Add("number of adults");
        return m;
    }

    public List<string> MissingForBooking()
    {
        var m = MissingForSearch();
        if (FirstName == "") m.Add("traveller first name");
        if (LastName == "") m.Add("traveller last name");
        if (Email == "") m.Add("contact email");
        if (PassportNumber == "") m.Add("passport number");
        if (Nationality == "") m.Add("nationality");
        return m;
    }

    public string TripTypeLabel => TripType switch { "oneWay" => "One Way", "multiCity" => "Multi City", "roundTrip" => "Round Trip", _ => "Round Trip" };
    public string CabinLabel => CabinClass switch { "premiumEconomy" => "Premium Economy", "business" => "Business", "first" => "First Class", _ => "Economy" };

    public static string NormTripType(string s) { s = s.ToLowerInvariant(); return s.Contains("one") ? "oneWay" : s.Contains("multi") ? "multiCity" : s.Contains("round") ? "roundTrip" : ""; }
    public static string NormCabin(string s) { s = s.ToLowerInvariant(); return s.Contains("premium") ? "premiumEconomy" : s.Contains("business") ? "business" : s.Contains("first") ? "first" : s.Contains("econ") ? "economy" : ""; }

    internal static string S(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.ToJsonString().Trim('"')).Trim() : "";
    internal static int I(JsonNode? n) => int.TryParse(S(n), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
}

/// <summary>
/// The Python agent re-implemented inside the app, so the sample runs with MAUI + Syncfusion only.
/// It applies the same stage gate as <c>system_prompt.md</c> (search -> results -> confirmation) and renders the
/// bundled <c>designs_maui</c> surfaces. Azure OpenAI (optional) is used only to understand free-text requests.
/// </summary>
public sealed class LocalAgent(Func<string, Task<string>> loadDesign, HttpClient http, AzureOpenAiOptions? ai = null) : IAgentClient
{
    private const string HistoryMarker = "\n\n## Conversation history";
    private const string ContextMarker = "Context from the user's earlier inputs";
    private readonly Random _rng = new();

    public async Task<AgentReply> SendAsync(string text, string? contextId, CancellationToken ct = default)
    {
        var ctxId = contextId ?? $"local-{Guid.NewGuid():N}"[..14];
        var h = text.IndexOf(HistoryMarker, StringComparison.Ordinal);
        var request = (h >= 0 ? text[..h] : text).Trim();

        var (messages, say) = TryParseAction(request) is { } action
            ? await HandleActionAsync(action)
            : await HandlePromptAsync(request, text, ct);

        return new AgentReply(messages, say, ctxId);
    }

    // ------------------------------------------------------------------ surface actions (button clicks)

    private static JsonObject? TryParseAction(string request)
    {
        var i = request.IndexOf("{\"name\"", StringComparison.Ordinal);
        if (i < 0) return null;
        try { return JsonNode.Parse(request[i..]) is JsonObject o && o["name"] is JsonValue && o["context"] is JsonObject ? o : null; }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private async Task<(JsonArray?, string)> HandleActionAsync(JsonObject action)
    {
        var name = Trip.S(action["name"]);
        var ctx = (JsonObject)action["context"]!;
        var trip = Trip.FromJson(ctx);

        if (name == "searchFlights")
        {
            var missing = trip.MissingForSearch();
            return missing.Count > 0
                ? (await SearchPageAsync(trip, MissingMessage("To search for flights", missing)), MissingMessage("To search for flights", missing))
                : await ResultsAsync(trip, $"Found flights from {trip.From} to {trip.To} on {trip.DepartureDate}. Tap a row, then press Book now.");
        }

        if (Regex.IsMatch(name, "select|book|proceed", RegexOptions.IgnoreCase))
        {
            var chosen = ctx["selectedFlight"] as JsonObject;
            if (chosen is null)
            {
                const string pick = "Please tap the flight you want in the table first, then press Book now.";
                return trip.MissingForSearch().Count > 0 ? (await SearchPageAsync(trip, pick), pick) : await ResultsAsync(trip, pick);
            }
            var missing = trip.MissingForBooking();
            if (missing.Count > 0)
            {
                var msg = MissingMessage("Before I can confirm your booking", missing) + " Please complete the form and search again.";
                return (await SearchPageAsync(trip, msg), msg);
            }
            return await ConfirmationAsync(trip, chosen);
        }

        return (await SearchPageAsync(trip, "Here is the flight search form."), "Here is the flight search form.");
    }

    private static string MissingMessage(string lead, List<string> missing) =>
        $"{lead} I still need: {string.Join(", ", missing)}.";

    // ------------------------------------------------------------------ free text / stage starters

    private async Task<(JsonArray?, string)> HandlePromptAsync(string request, string fullText, CancellationToken ct)
    {
        var lower = request.ToLowerInvariant();
        var trip = Trip.FromJson(ExtractEarlierInputs(request));

        // Stage starters (the welcome cards) open their page directly, as in the React sample.
        if (lower.StartsWith("show me the flight search form"))
            return (await SearchPageAsync(trip, "Here is the flight search form. Fill in your trip details and press Search Flights."),
                    "Here is the flight search form. Fill in your trip details and press Search Flights.");
        if (lower.StartsWith("show me the flight results page"))
            return await ResultsAsync(trip.MissingForSearch().Count == 0 ? trip : SampleTrip(),
                "Here are the available flights. Tap a row, then press Book now.");
        if (lower.Contains("booking and confirmation page") || lower.StartsWith("show the booking confirmation page"))
            return await ConfirmationAsync(trip.MissingForBooking().Count == 0 ? trip : SampleTrip(withTraveller: true), SampleFlight(),
                "Here is the booking confirmation page.");

        // Offline: "fill details on your own" prefills the form with a sample trip, like the React demo.
        if (ai is null && Regex.IsMatch(lower, @"on your own|yourself|any details|sample details|fill (all|the|in)"))
        {
            var filled = SampleFill(trip);
            const string said = "I have prefilled the search form with sample travel details. Review them and press Search Flights.";
            return (await SearchPageAsync(filled, said), said);
        }

        if (ai is null)
        {
            const string noAi = "I can open the Search, Results and Confirmation pages, but to understand free-text requests I need Azure OpenAI configured " +
                                "(azure-openai.local.json or AZURE_OPENAI_* environment variables). Pick a step from the home page, or fill the form directly.";
            return (await SearchPageAsync(trip, noAi), noAi);
        }

        // Free text: let the model extract (or, when asked, make up) the form values. The stage gate stays in code.
        var parsed = await AskModelAsync(fullText, ct);
        var intent = Trip.S(parsed?["intent"]);
        var say = Trip.S(parsed?["explanation"]);
        var fromModel = Trip.FromJson(parsed?["fields"] as JsonObject);
        trip.Overlay(fromModel);

        if (intent == "chat" || parsed is null)
            return (null, say.Length > 0 ? say : "How can I help with your trip?");
        if (say.Length == 0) say = "I've filled in what I could. Review the form and press Search Flights.";
        return (await SearchPageAsync(trip, say), say);
    }

    private static JsonObject? ExtractEarlierInputs(string request)
    {
        var i = request.IndexOf(ContextMarker, StringComparison.Ordinal);
        if (i < 0) return null;
        var j = request.IndexOf('{', i);
        if (j < 0) return null;
        try { return JsonNode.Parse(request[j..]) as JsonObject; } catch (System.Text.Json.JsonException) { return null; }
    }

    private async Task<JsonObject?> AskModelAsync(string text, CancellationToken ct)
    {
        var system = """
            You are SkyBook, a flight-booking assistant. Today is @TODAY@.
            Read the user's request (and any earlier inputs / history) and reply with ONE JSON object:
            {"intent":"search_form"|"chat","explanation":"<one or two friendly sentences>","fields":{
              "tripType":"roundTrip|oneWay|multiCity","from":"City (IATA)","to":"City (IATA)",
              "departureDate":"YYYY-MM-DD","returnDate":"YYYY-MM-DD","cabinClass":"economy|premiumEconomy|business|first",
              "adults":1,"children":0,"firstName":"","lastName":"","email":"","passportNumber":"","nationality":""}}
            Rules: use intent "search_form" for anything about flights/booking, "chat" for small talk or questions.
            Put in "fields" ONLY what the user stated or what is in the earlier inputs; leave everything else out.
            EXCEPTION: if the user asks you to fill details "on your own"/"yourself"/"anything", invent plausible values for ALL fields.
            Never claim a booking is confirmed. In "explanation" name what the user still has to fill in.
            """.Replace("@TODAY@", DateTime.Now.ToString("yyyy-MM-dd"));
        var body = new JsonObject
        {
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = text }),
            ["temperature"] = 0,
            ["response_format"] = new JsonObject { ["type"] = "json_object" },
        };
        var url = $"{ai!.Endpoint.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(ai.Deployment)}/chat/completions?api-version={ai.ApiVersion}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        req.Headers.Add("api-key", ai.ApiKey);
        using var res = await http.SendAsync(req, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure OpenAI request failed ({(int)res.StatusCode}): {Truncate(payload, 200)}");
        var content = JsonNode.Parse(payload)?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        return content is null ? null : JsonNode.Parse(content) as JsonObject;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    // ------------------------------------------------------------------ page builders (bundled designs + data)

    private async Task<JsonArray> SearchPageAsync(Trip t, string say)
    {
        var d = await LoadAsync("stage1-flight-search.json");
        // The date pickers show a date even before the user touches them; store the same value so Search sees it.
        if (t.DepartureDate == "") t.DepartureDate = DateTime.Today.AddDays(14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (t.ReturnDate == "") t.ReturnDate = DateTime.Today.AddDays(21).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        SetData(d, "/", new JsonObject
        {
            ["search"] = new JsonObject
            {
                ["tripType"] = t.TripType == "" ? "roundTrip" : t.TripType,
                ["from"] = t.From, ["to"] = t.To,
                ["departureDate"] = t.DepartureDate, ["returnDate"] = t.ReturnDate,
                ["cabinClass"] = t.CabinClass == "" ? "economy" : t.CabinClass,
                ["passengers"] = new JsonObject { ["adults"] = Math.Max(1, t.Adults), ["children"] = t.Children },
            },
            ["passenger"] = new JsonObject
            {
                ["firstName"] = t.FirstName, ["lastName"] = t.LastName, ["email"] = t.Email,
                ["passportNumber"] = t.PassportNumber, ["nationality"] = t.Nationality,
            },
        });
        return Explain(d, say);
    }

    private async Task<(JsonArray, string)> ResultsAsync(Trip t, string say)
    {
        var d = await LoadAsync("stage2-flight-results.json");
        var ret = t.TripType == "roundTrip" && t.ReturnDate != "" ? $" • Return: {t.ReturnDate}" : "";
        SetData(d, "/stage1", new JsonObject
        {
            ["tripType"] = t.TripTypeLabel, ["from"] = t.From, ["to"] = t.To,
            ["departureDate"] = t.DepartureDate, ["returnDate"] = t.ReturnDate, ["cabinClass"] = t.CabinLabel,
            ["passengers"] = new JsonObject { ["adults"] = t.Adults, ["children"] = t.Children, ["infants"] = 0 },
            ["preferredAirline"] = "Any", ["nonstopOnly"] = false,
        });
        SetData(d, "/summaryText",
            $"{t.TripTypeLabel} • {t.From} → {t.To}\nDeparture: {t.DepartureDate}{ret}\nCabin: {t.CabinLabel} • Adults: {t.Adults} • Children: {t.Children} • Infants: 0\nPreferred Airline: Any • Non-stop Only: No");

        var (fromCode, toCode) = (Iata(t.From), Iata(t.To));
        foreach (var path in new[] { "/allFlights", "/filteredFlights" })
        {
            if (FindData(d, path) is not JsonArray flights) continue;
            foreach (var f in flights.OfType<JsonObject>())
            {
                f["departure"] = Regex.Replace(Trip.S(f["departure"]), "[A-Z]{3}$", fromCode);
                f["arrival"] = Regex.Replace(Trip.S(f["arrival"]), "[A-Z]{3}$", toCode);
            }
        }
        return (Explain(d, say), say);
    }

    private Task<(JsonArray, string)> ConfirmationAsync(Trip t, JsonObject chosen) =>
        ConfirmationAsync(t, chosen, $"Your booking is confirmed on {Trip.S(chosen["airline"])} {Trip.S(chosen["flight"])}. Your e-ticket has been issued.");

    private async Task<(JsonArray, string)> ConfirmationAsync(Trip t, JsonObject chosen, string say)
    {
        var d = await LoadAsync("stage3-booking-confirmation.json");
        var code = new string(Enumerable.Range(0, 6).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[_rng.Next(32)]).ToArray());
        var pax = $"{t.Adults} Adult{(t.Adults == 1 ? "" : "s")}{(t.Children > 0 ? $", {t.Children} Child{(t.Children == 1 ? "" : "ren")}" : "")}";
        var dates = t.TripType == "roundTrip" && t.ReturnDate != "" ? $"{t.DepartureDate} – {t.ReturnDate}" : t.DepartureDate;
        var arrow = t.TripType == "roundTrip" ? "↔" : "→";
        var (fromCode, toCode) = (Iata(t.From), Iata(t.To));

        SetText(d, "heroSub", $"Confirmation Code: {code} • {t.TripTypeLabel} • {t.From} {arrow} {t.To} • {pax} • {dates}");
        SetText(d, "outboundSub", $"{Trip.S(chosen["airline"])} • Confirmed • {Trip.S(chosen["flight"])}");
        SetText(d, "outboundText",
            $"{Trip.S(chosen["departure"])} → {Trip.S(chosen["arrival"])}\n{Trip.S(chosen["stops"])} • {Trip.S(chosen["duration"])}\nBaggage: {Trip.S(chosen["baggage"])}\nStatus: Ticketed");
        if (t.TripType == "roundTrip")
        {
            SetText(d, "returnSub", $"{Trip.S(chosen["airline"])} • Confirmed");
            SetText(d, "returnText", $"{toCode} → {fromCode} on {t.ReturnDate}\nStatus: Ticketed");
        }
        else
        {
            SetText(d, "returnSub", "One-way booking");
            SetText(d, "returnText", "No return flight on this booking.");
        }
        SetData(d, "/passengers", new JsonArray(new JsonObject
        {
            ["passenger"] = $"{t.FirstName} {t.LastName}".Trim(),
            ["email"] = t.Email, ["phone"] = "—",
            ["passport"] = $"{t.Nationality} / {t.PassportNumber}", ["seatMeal"] = "—",
        }));
        return (Explain(d, say), say);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<JsonArray> LoadAsync(string file) => JsonNode.Parse(await loadDesign(file))!.AsArray();

    private static JsonArray Explain(JsonArray d, string say)
    {
        d.Add(new JsonObject
        {
            ["version"] = "v0.9",
            ["updateDataModel"] = new JsonObject { ["surfaceId"] = "workspace", ["path"] = "/_explanation", ["value"] = say },
        });
        return d;
    }

    private static JsonObject? DataMsg(JsonArray d, string path) =>
        d.Select(m => m?["updateDataModel"] as JsonObject).FirstOrDefault(u => u is not null && Trip.S(u["path"]) == path);

    private static JsonNode? FindData(JsonArray d, string path) => DataMsg(d, path)?["value"];

    private static void SetData(JsonArray d, string path, JsonNode value)
    {
        if (DataMsg(d, path) is { } u) u["value"] = value;
        else d.Add(new JsonObject { ["version"] = "v0.9", ["updateDataModel"] = new JsonObject { ["surfaceId"] = "workspace", ["path"] = path, ["value"] = value } });
    }

    private static void SetText(JsonArray d, string componentId, string text)
    {
        foreach (var m in d)
            foreach (var c in (m?["updateComponents"]?["components"] as JsonArray ?? []).OfType<JsonObject>())
                if (Trip.S(c["id"]) == componentId) c["text"] = text;
    }

    public static string Iata(string place)
    {
        var m = Regex.Match(place, @"\(([A-Za-z]{3})\)");
        if (m.Success) return m.Groups[1].Value.ToUpperInvariant();
        var letters = new string(place.Where(char.IsLetter).ToArray());
        return letters.Length >= 3 ? letters[..3].ToUpperInvariant() : "---";
    }

    /// <summary>Keeps whatever the user already entered and fills every missing field with a plausible sample.</summary>
    private static Trip SampleFill(Trip t)
    {
        var s = SampleTrip(withTraveller: true);
        s.From = "San Francisco (SFO)"; s.To = "Tokyo (HND)";
        s.DepartureDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
        s.ReturnDate = DateTime.Today.AddDays(40).ToString("yyyy-MM-dd");
        s.Adults = 1; s.Children = 0; s.CabinClass = "business";
        s.FirstName = "Hiroshi"; s.LastName = "Tanaka"; s.Email = "h.tanaka@example.jp"; s.PassportNumber = "TK9988776"; s.Nationality = "IN";
        s.Overlay(t);
        return s;
    }

    private static Trip SampleTrip(bool withTraveller = false) => new()
    {
        TripType = "roundTrip", From = "New York (JFK)", To = "London (LHR)", DepartureDate = "2026-09-14", ReturnDate = "2026-09-23",
        CabinClass = "business", Adults = 2, Children = 1,
        FirstName = withTraveller ? "Alexandra" : "", LastName = withTraveller ? "Reed" : "",
        Email = withTraveller ? "alexandra.reed@enterpriseair.com" : "", PassportNumber = withTraveller ? "XK92PL781" : "", Nationality = withTraveller ? "US" : "",
    };

    private static JsonObject SampleFlight() => new()
    {
        ["airline"] = "SkyJet Airways", ["flight"] = "SJ 201", ["departure"] = "08:15 JFK", ["arrival"] = "20:05 LHR",
        ["duration"] = "6h 50m", ["stops"] = "Non-stop", ["baggage"] = "2 x 23kg",
    };
}
