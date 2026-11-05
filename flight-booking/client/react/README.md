# SkyBook Flight Booking Client

A production-ready React application for AI-powered flight booking using the Syncfusion A2UI (AI-to-UI) framework.

## Overview

**SkyBook** is an enterprise flight booking assistant that uses AI to guide users through a 3-stage booking journey:

1. **Stage 1 - Search**: User specifies flight preferences (route, dates, cabin, passengers)
2. **Stage 2 - Results**: AI displays matching flights in a searchable grid
3. **Stage 3 - Confirmation**: User reviews and confirms booking details

The application communicates with an AI agent backend that generates dynamic Syncfusion UI components in real-time using the A2UI v0.9 specification.

---

## Prerequisites

### System Requirements
- **Node.js**: v18 or higher, Recommended Latest version
- **npm**: v8 or higher
- **OS**: Windows, macOS, or Linux

### Backend Requirements
- **Agent**: Running on `http://127.0.0.1:10006`
  - Uses OpenRouter provider with Gemini or GPT-4 models
  - Serves A2UI envelope messages over HTTP/JSON-RPC
  - See `flight-booking/agent/README.md` for setup

---

## Installation & Setup

> ⚠️ **Two terminals required** — the **agent** (Python) and the **client**
> (Vite/React) are independent processes and **must be started in two
> separate terminal windows**. The client only talks to the agent over
> HTTP, so leaving either one closed will break the app.

| Terminal | Process | Stays open for | URL |
| --- | --- | --- | --- |
| **Terminal 1** | Agent (`python -m flight_booking_agent.flight_booking_agent`) | Entire demo | `http://127.0.0.1:10006` |
| **Terminal 2** | Client (`npm run dev`)              | Entire demo | `http://localhost:5173`  |

Open them once at the start of the session and keep **both** running.

### 1. Navigate to the client directory
```bash
# from the repository root
cd flight-booking/client/react
```

### 2. Install Dependencies
```bash
npm install
```

This installs:
- **React 18.3.0** - UI framework
- **Syncfusion EJ2 React Components** - UI controls (DataGrid, DatePicker, etc.)
- **@a2ui/web_core & @a2ui/react** - A2UI message processor & renderer
- **Vite 5.4** - Development server & build tool

### 3. Configure Environment

Create or update `.env` file in the project root:

```env
# .env
VITE_AGENT_URL=http://127.0.0.1:10006/
```

**Configuration Options:**
| Variable | Default | Description |
|----------|---------|-------------|
| `VITE_AGENT_URL` | `http://127.0.0.1:10006/` | Agent backend HTTP endpoint |

### 4. Verify the Agent is Running

Before starting the client, ensure the agent backend is running in another terminal:

```bash
# Terminal 1 - start the agent (from the repo root)
cd flight-booking/agent
python -m pip install -e .
cd flight_booking_agent
cp .env.example .env       # then edit .env and add your API keys
python -m flight_booking_agent.flight_booking_agent

# You should see:
# INFO: Uvicorn running on http://127.0.0.1:10006 (Press CTRL+C to quit)
```

See [`../agent/README.md`](../agent/README.md) for full agent setup, troubleshooting, and configuration.

---

## Running the Application

> 💡 Make sure **Terminal 1 (the agent) is still running** before you
> start the client. If the client is up but the agent is closed, the
> UI will render but every action will fail with a network error.

### Development Mode

Open a **new terminal window** (Terminal 2 — the agent is in Terminal 1)
and start the Vite development server:

```bash
npm run dev
```

**Output:**
```
  VITE v5.4.0  ready in 125 ms

  ➜  Local:   http://localhost:5173/
  ➜  press h to show help
```

### Access the Application

Open your browser and navigate to:
```
http://localhost:5173
```

---

## Project Structure

```
flight-booking/client/react/
├── README.md            # This file
├── package.json         # Dependencies & scripts
├── .env                 # Vite env (VITE_AGENT_URL), git-ignored
├── index.html           # Vite entrypoint
├── tsconfig.json        # TypeScript config
├── vite.config.ts       # Vite config (port 5173, dedupe @a2ui/*)
├── public/
│   ├── favicon.ico
│   └── favicon.svg
└── src/
    ├── main.tsx         # React root + EJ2 Tailwind3 theme
    ├── App.tsx          # Main app — chat + A2UI surface + stepper
    └── App.css          # Layout & component styles
```

---

## Available Scripts

| Command | Description |
| --- | --- |
| `npm run dev` | Start the Vite dev server on `http://localhost:5173` |
| `npm run build` | Type-check (`tsc -b`) then build for production |
| `npm run preview` | Preview the production build locally |
| `npm run typecheck` | Run the TypeScript compiler in check-only mode |

---

## How the Client Talks to the Agent

1. The user types a message in the **AIAssistView** footer (or clicks a stage card).
2. `App.tsx` builds a JSON-RPC `message/send` envelope and POSTs it to `VITE_AGENT_URL`.
3. The agent returns an A2UI v0.9 envelope (`createSurface` + `updateComponents` + `updateDataModel`).
4. `@a2ui/web_core`'s `MessageProcessor` mounts the surface, and `@syncfusion/ej2-react-a2ui` renders the Syncfusion components in the right pane.
5. The `/_explanation` value from the final `updateDataModel` is shown as the bot reply in the chat.

---

## Troubleshooting

### ❌ Network error / agent not reachable

**Cause**: The agent isn't running, or `VITE_AGENT_URL` is wrong.

**Solution**:
1. Verify the agent is running: open `http://127.0.0.1:10006/docs` in a browser — you should see FastAPI docs.
2. Check `.env` has `VITE_AGENT_URL=http://127.0.0.1:10006/`.
3. Restart `npm run dev` after editing `.env` (Vite only reads env on startup).

### ❌ UI renders but actions do nothing

**Cause**: The agent is running but rejecting requests, or the renderer can't find a component.

**Solution**:
1. Open the browser dev console — Syncfusion A2UI renderer logs missing components with a `[Loading x...]` placeholder.
2. The client sanitizes the agent's envelope to drop any references the agent didn't define, so the page should still render cleanly.

### ❌ Port 5173 already in use

**Cause**: Another Vite app (or any process) is holding port 5173.

**Solution**:
```bash
# find and kill the conflicting process
lsof -ti:5173 | xargs kill -9    # macOS / Linux
# or stop the other Vite process and re-run `npm run dev`
```
