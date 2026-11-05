# SkyBook — .NET 10 MAUI client (Syncfusion.Maui.A2UI)

MAUI port of `../react`. Same agent, same A2A endpoint, same three-stage flow; booking screens are rendered by
`Syncfusion.Maui.A2UI` from A2UI v0.9 envelopes.

* Harness (start here): [`harness/INTENT.md`](harness/INTENT.md) · [`PLAN.md`](harness/PLAN.md) ·
  [`TASKS.md`](harness/TASKS.md) · [`ARCHITECTURE.md`](harness/ARCHITECTURE.md)
* Verify: `harness/verify.sh` (tests + design JSON + Android build)

## Running
1. Start the agent with the MAUI designs: set `FLIGHT_BOOKING_CLIENT=maui` (plus the usual `.env` — see `agent/README.md`), then `flight-booking`.
2. Install the MAUI workload (`dotnet workload install maui`) and run `src/SkyBook.Maui` for your platform.
   No API key handy? Run the offline stand-in instead: `python3 harness/mock_agent.py` (serves the bundled `designs_maui` on the same port).
   If neither is running the app shows "I can't reach the SkyBook agent at <url>" in the chat.
3. Agent URL defaults to `http://localhost:10006` (`http://10.0.2.2:10006` on Android emulator); override with `SKYBOOK_AGENT_URL`.
4. Syncfusion license: set `AppConfig.SyncfusionLicenseKey` (do not commit it).
