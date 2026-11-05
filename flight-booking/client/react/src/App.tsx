import { JSX, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { syncfusionCatalog, SyncfusionA2UIProvider } from '@syncfusion/ej2-react-a2ui';
import { TextAreaComponent, ChangeEventArgs, InputEventArgs } from '@syncfusion/ej2-react-inputs';
import { AIAssistViewComponent, PromptRequestEventArgs } from '@syncfusion/ej2-react-interactive-chat';
import { ButtonComponent } from '@syncfusion/ej2-react-buttons';
import { MessageProcessor } from '@a2ui/web_core/v0_9';
import type { SurfaceModel } from '@a2ui/web_core/v0_9';
import type { ReactComponentImplementation } from '@a2ui/react/v0_9';
import './App.css';

interface Stage { id: 'search' | 'results' | 'booking'; step: number; icon: string; cta: string; title: string; tagline: string; copy: string; highlights: string[]; starterPrompt: string; }


/* ---------- Inline SVG icons (Lucide-style, stroke = currentColor) ---------- */
type IconName = 'search' | 'plane' | 'ticket' | 'bot' | 'wave' | 'list' | 'check';
const ICON_PATHS: Record<IconName, JSX.Element> = {
  list: (<><path d="M8 6h13M8 12h13M8 18h13" /><path d="M3.5 6h.01M3.5 12h.01M3.5 18h.01" /></>),
  check: (<><circle cx="12" cy="12" r="9" /><path d="m8 12.5 2.8 2.8L16 9.5" /></>),
  search: (<><circle cx="11" cy="11" r="7" /><path d="m21 21-4.3-4.3" /><path d="M8.5 11h5" /></>),
  plane: (<path d="M17.8 19.2 16 11l3.5-3.5C21 6 21.5 4 21 3c-1-.5-3 0-4.5 1.5L13 8 4.8 6.2c-.5-.1-.9.1-1.1.5l-.3.5c-.2.5-.1 1 .3 1.3L9 12l-2 3H4l-1 1 3 2 2 3 1-1v-3l3-2 3.5 5.3c.3.4.8.5 1.3.3l.5-.2c.4-.3.6-.7.5-1.2z" />),
  ticket: (<><path d="M3 9a2 2 0 0 0 0 6v3a1 1 0 0 0 1 1h16a1 1 0 0 0 1-1v-3a2 2 0 0 1 0-6V6a1 1 0 0 0-1-1H4a1 1 0 0 0-1 1z" /><path d="M14 5v2M14 11v2M14 17v2" /></>),
  bot: (<><path d="M12 3c.6 4.6 2.4 6.4 7 7-4.6.6-6.4 2.4-7 7-.6-4.6-2.4-6.4-7-7 4.6-.6 6.4-2.4 7-7z" /><path d="M19 15c.2 1.6.9 2.3 2.5 2.5-1.6.2-2.3.9-2.5 2.5-.2-1.6-.9-2.3-2.5-2.5 1.6-.2 2.3-.9 2.5-2.5z" /></>),
  wave: (<><path d="M12 3 4 6v6c0 4.5 3.2 7.8 8 9 4.8-1.2 8-4.5 8-9V6z" /><path d="m9 12 2 2 4-4" /></>),
};
function Icon({ name, size = 22 }: { name: IconName; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
      {ICON_PATHS[name]}
    </svg>
  );
}
const STAGE_ICON: Record<'search' | 'results' | 'booking', IconName> = { search: 'search', results: 'list', booking: 'check' };
const AI_SUGGESTIONS = ['Find the cheapest flight from New York to London', 'Reschedule my trip to next Friday', 'Upgrade my cabin to Business', 'Book the selected itinerary'];
const SAMPLES: Record<'search' | 'results' | 'booking', string[]> = {
  search: ['New York → London', 'Economy / Business', 'Mar 12 – 18'],
  results: ['24 matching flights found', 'Lowest fare from $549'],
  booking: ['Passenger details verified', 'Payment ready'],
};
const STAGES: Stage[] = [
  { id: 'search', step: 1, icon: '🔎', cta: 'Search Flights', title: 'Search Flights', tagline: 'Find the best route', copy: 'Trip type, dates, cabin & travellers.', highlights: ['Round trip / One way / Multi-city', 'Premium cabin filters'], starterPrompt: 'Show me the flight search form.' },
  { id: 'results', step: 2, icon: '🛫', cta: 'View Results', title: 'Flight Results', tagline: 'Compare and choose', copy: 'Compare available flights by airline, departure time and pricing.', highlights: ['Sortable fare grid', 'Select the best flight in seconds.'], starterPrompt: 'Show me the flight results page.' },
  { id: 'booking', step: 3, icon: '🔒', cta: 'Review Booking', title: 'Booking & Confirmation', tagline: 'Confirm & get ticket', copy: 'Review trip & confirm.', highlights: ['Review traveler details and trip summary before booking.', 'Confirmation message'], starterPrompt: 'Show me the booking and Confirmation page.' },
];
const AGENT_URL = (import.meta.env.VITE_AGENT_URL as string | undefined) ?? 'http://localhost:10006';

function summarizeAction(action: any): string {
  if (!action || typeof action !== 'object') return 'Submitted your selection.';
  const name: string = action.name ?? action.action ?? '';
  const ctx = action.context ?? {};
  const from = ctx.from ?? ctx.origin;
  const to = ctx.to ?? ctx.destination;
  const date = ctx.departureDate ?? ctx.date;
  const pax = ctx.adults != null ? `${ctx.adults} adult${Number(ctx.adults) === 1 ? '' : 's'}` + (ctx.children ? `, ${ctx.children} child${Number(ctx.children) === 1 ? '' : 'ren'}` : '') : '';
  const flight = ctx.flightNumber ?? ctx.flight ?? ctx.airline ?? ctx.flightId;
  const price = ctx.price ?? ctx.fare;
  switch (name) {
    case 'selectFlight':
      return `Book${flight ? ` ${flight}` : ' this flight'}${from && to ? ` (${from} → ${to})` : ''}${price ? ` at ${price}` : ''}.`;
    case 'searchFlights':
      return `Search flights${from && to ? ` from ${from} to ${to}` : ''}${date ? ` on ${date}` : ''}${pax ? ` for ${pax}` : ''}.`;
    default:
      return name === 'select' ? `Book this flight${from && to ? ` from ${from} to ${to}` : ''}${date ? ` on ${date}` : ''}${pax ? ` for ${pax}` : ''}.` : `${String(name || 'request').replace(/([A-Z])/g, ' $1').trim().replace(/^./, (c) => c.toUpperCase())}.`;
  }
}

/** Removes children that reference components the agent never defined (they render as "[Loading x...]"). */
function sanitizeEnvelope(messages: any[]): any[] {
  if (!Array.isArray(messages)) return messages;
  const defined = new Map<string, Set<string>>();
  for (const m of messages) {
    const uc = m?.updateComponents;
    if (!uc) continue;
    const set = defined.get(uc.surfaceId) ?? new Set<string>();
    for (const c of uc.components ?? []) if (c?.id) set.add(c.id);
    defined.set(uc.surfaceId, set);
  }
  const cleaned = messages.map((m) => {
    const uc = m?.updateComponents;
    if (!uc) return m;
    const ids = defined.get(uc.surfaceId)!;
    const components = (uc.components ?? []).map((c: any) =>
      Array.isArray(c?.children) ? { ...c, children: c.children.filter((id: any) => typeof id !== 'string' || ids.has(id)) } : c);
    return { ...m, updateComponents: { ...uc, components } };
  });
  // Drop container components that became empty (e.g. rows whose labels were never defined).
  return cleaned.map((m) => {
    const uc = m?.updateComponents;
    if (!uc) return m;
    const emptyIds = new Set<string>(uc.components.filter((c: any) => Array.isArray(c?.children) && c.children.length === 0 && c.id !== 'root' && ['Row', 'Column'].includes(c.component)).map((c: any) => c.id));
    if (!emptyIds.size) return m;
    const components = uc.components.filter((c: any) => !emptyIds.has(c.id)).map((c: any) =>
      Array.isArray(c?.children) ? { ...c, children: c.children.filter((id: any) => !emptyIds.has(id)) } : c);
    return { ...m, updateComponents: { ...uc, components } };
  });
}

/** Reads the agent's reply text from the `/_explanation` data-model update. */
function extractExplanation(messages: any[]): string {
  if (!Array.isArray(messages)) return '';
  for (let i = messages.length - 1; i >= 0; i--) {
    const u = messages[i]?.updateDataModel;
    if (u && u.path === '/_explanation' && typeof u.value === 'string' && u.value.trim()) return u.value.trim();
  }
  return '';
}

function newId(prefix: string): string { return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`; }

/* ---------- AI AssistView templates (module-level so their identity stays stable across renders) ---------- */
const fmtTime = (ts: number) => new Date(ts).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
// Timestamps keyed by prompt index ("p0" = prompt, "r0" = response), stamped on first render.
const msgTimes: Record<string, number> = {};
const stamp = (key: string) => (msgTimes[key] ??= Date.now());
const assistRef: { current: AIAssistViewComponent | null } = { current: null };
const uiStore = {
  state: { busy: false, welcome: true },
  subs: new Set<() => void>(),
  set(patch: Partial<{ busy: boolean; welcome: boolean }>) { this.state = { ...this.state, ...patch }; this.subs.forEach((fn) => fn()); },
  subscribe: (fn: () => void) => { uiStore.subs.add(fn); return () => { uiStore.subs.delete(fn); }; },
  get: () => uiStore.state,
};
const useUi = () => useSyncExternalStore(uiStore.subscribe, uiStore.get);
// Handlers owned by <App>, reachable from the AssistView templates.
const uiActions: { openStage: (s: Stage) => void } = { openStage: () => {} };

const footerTemplate = () => <ChatFooter />;
// AssistView hands responses over as markdown-rendered HTML; show them as plain text.
const toPlainText = (html: string) => new DOMParser().parseFromString(html ?? '', 'text/html').body.textContent?.trim() ?? '';
const bannerTemplate = () => <WelcomeBanner />;
const promptItemTemplate = (props: { prompt: string; index: number }) => (
  <div className="chat-bubble chat-bubble--user">
    <div className="chat-msg"><div className="chat-text">{props.prompt}</div><div className="chat-time">{fmtTime(stamp(`p${props.index}`))}</div></div>
  </div>
);
const responseItemTemplate = (props: { response: string; index: number }) => (
  <div className="chat-bubble chat-bubble--bot">
    <div className="chat-avatar" aria-hidden="true"><Icon name="bot" size={16} /></div>
    <div className="chat-msg"><div className="chat-text">{toPlainText(props.response)}</div><div className="chat-time">{fmtTime(stamp(`r${props.index}`))}</div></div>
  </div>
);
const responseAnimationTemplate = () => (
  <div className="chat-bubble chat-bubble--bot">
    <div className="chat-avatar" aria-hidden="true"><Icon name="bot" size={16} /></div>
    <div className="chat-text typing"><span /><span /><span /></div>
  </div>
);

function WelcomeBanner() {
  const { welcome } = useUi();
  if (!welcome) return (<div className="chat-empty">Hi! Pick a stage from the home page, or just tell me what you need.</div>);
  return (
    <div className="chat-welcome">
      <div className="hero-badge"><span className="hero-dot" /> Enterprise AI Travel Assistant</div>
              <h1 className="hero-title">Plan and book business travel<span className="hero-accent"> in minutes.</span></h1>
              <p className="hero-sub">Start with a step below or simply ask SkyBook to help plan and book your trip.</p>
              <ol className="journey" aria-label="Booking journey">
                {STAGES.map((s) => (
                  <li key={s.id} className="journey-item"><span className="journey-dot">{s.step}</span><span className="journey-label">{s.id === 'booking' ? 'Confirmation' : s.id === 'results' ? 'Results' : 'Search'}</span></li>
                ))}
              </ol>
              <div className="stage-grid">
                {STAGES.map((s) => (
                  <button key={s.id} className={`stage-card stage-card--${s.id}`} type="button" onClick={() => uiActions.openStage(s)}>
                    <div className="stage-icon"><Icon name={STAGE_ICON[s.id]} size={24} /></div>
                    <div className="stage-title">{s.title}</div>
                    <div className="stage-step">Step {s.step}</div>
                    <div className="stage-copy">{s.copy}</div>
                    <div className="stage-samples">{SAMPLES[s.id].map((t) => (<span key={t} className="sample-tag">{t}</span>))}</div>
                    <ul className="stage-highlights">{s.highlights.map((h) => (<li key={h}><span className="stage-bullet">✓</span> {h}</li>))}</ul>
                    <span className="stage-arrow" aria-hidden="true">{s.cta} →</span>
                  </button>
                ))}
              </div>
    </div>
  );
}

function ChatFooter() {
  const { busy, welcome } = useUi();
  const [value, setValue] = useState('');
  const wrapRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    const ta = wrapRef.current?.querySelector('textarea');
    if (!ta) return;
    ta.style.height = 'auto';
    ta.style.height = `${Math.min(Math.max(ta.scrollHeight, 44), 140)}px`;
  }, [value, welcome]);
  const send = () => { if (!value.trim() || busy) return; assistRef.current?.executePrompt(value); setValue(''); };
  // Enter sends; Shift+Enter inserts a new line.
  const onKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (e.key !== 'Enter' || e.shiftKey || e.nativeEvent.isComposing) return;
    e.preventDefault();
    send();
  };
  const input = (
    <>
      <div className="ta-wrap" ref={wrapRef} onKeyDown={onKeyDown}>
        <TextAreaComponent value={value} input={(e: InputEventArgs) => setValue(String(e.value ?? ''))} change={(e: ChangeEventArgs) => setValue(String(e.value ?? ''))} placeholder={welcome ? 'Try: "Find me a flight from New York to London next Friday"  (Shift+Enter for a new line)' : 'Message SkyBook\u2026'} floatLabelType="Never" rows={1} resizeMode="None" cssClass={welcome ? 'welcome-input' : 'chat-input'} />
      </div>
      <ButtonComponent isPrimary content={welcome ? 'Ask SkyBook' : 'Send'} disabled={!value.trim() || busy} type="submit" />
    </>
  );
  if (welcome) return (
    <div className="welcome-footer">
      <div className="ai-panel">
        <div className="ai-panel-head"><span className="ai-spark"><Icon name="bot" size={18} /></span><span className="ai-panel-title">Ask SkyBook to…</span></div>
        <form className="welcome-cta" onSubmit={(e) => { e.preventDefault(); send(); }}>{input}</form>
      </div>
    </div>
  );
  return <form className="chat-input-row" onSubmit={(e) => { e.preventDefault(); send(); }}>{input}</form>;
}

export default function App() {
  const [surface, setSurface] = useState<SurfaceModel<ReactComponentImplementation> | null>(null);
  const [messageCount, setMessageCount] = useState(0);
  const [chatKey, setChatKey] = useState(0);
  const [isSending, setIsSending] = useState(false);
  const [view, setView] = useState<'welcome' | 'chat'>('welcome');
  const [activeStage, setActiveStage] = useState<Stage | null>(null);
  const [mobileTab, setMobileTab] = useState<'chat' | 'view'>('chat');
  const silentPayloadRef = useRef<string | null>(null);
  const tripRef = useRef<Record<string, any>>({});
  const userPromptsRef = useRef<string[]>([]);
  const contextIdRef = useRef<string | null>(null);
  const processor = useMemo(() => new MessageProcessor<ReactComponentImplementation>([syncfusionCatalog], (action) => {
    const name: string = action?.name ?? '';
    // Ignore UI noise events (row/dropdown selection) – they are not commands and make the agent re-render the search form.
    if (!name || name === 'select') return;
    // AssistView ignores prompts while a response is pending.
    if (uiStore.state.busy) return;
    const own = action?.context && typeof action.context === 'object' ? action.context : {};
    const sel = own.selectedRow && typeof own.selectedRow === 'object' ? own.selectedRow : null;
    const { results: _r, selectedRow: _s, ...ownLite } = own;
    // Remember what the user entered so the booking step uses the real inputs.
    if (name === 'searchFlights') tripRef.current = { ...tripRef.current, ...ownLite };
    const isBook = /select|book|proceed/i.test(name);
    const merged = isBook
      ? { name, context: { ...tripRef.current, ...ownLite, ...(sel ? { selectedFlight: sel } : {}) } }
      : { ...action, context: { ...ownLite } };
    const summary = summarizeAction(merged);
    const text = isBook
      ? `The user clicked "Book now". Show the booking confirmation page (stage 3) now, NOT the search form. Use exactly these user details and chosen flight, do not invent any: ${JSON.stringify(merged)}`
      : JSON.stringify(merged);
    // The summary is shown as the user's chat prompt; the agent receives the detailed payload.
    silentPayloadRef.current = text;
    assistRef.current?.executePrompt(summary);
  }), [],);

  const addBotMessage = (text: string) => { assistRef.current?.addPromptResponse(text); };

  async function sendToAgent(text: string, silent: boolean) {
    if (!text.trim()) return;
    setIsSending(true);
    uiStore.set({ busy: true });
    try {
      // For free-text prompts, attach the collected user inputs so the agent doesn't invent details.
      const hasInputs = Object.keys(tripRef.current).length > 0;
      // Only the explicit stage-3 starter jumps to confirmation; other free text follows the agent's guided flow.
      const bookIntent = !silent && text === STAGES[2].starterPrompt ? 'Show the booking confirmation page (stage 3). ' : '';
      const base = silent ? text : `${bookIntent}${!hasInputs ? text : `${text}\n\nContext from the user's earlier inputs (use these, do not invent passenger or trip details): ${JSON.stringify(tripRef.current)}`}`;
      // The current request is already the last entry; send up to 8 earlier ones as conversation history.
      const earlier = userPromptsRef.current.slice(-9, -1);
      const outgoing = earlier.length ? `${base}\n\n## Conversation history (earlier user requests, oldest first)\n${earlier.map((t) => `User: ${t}`).join('\n')}` : base;
      const message: Record<string, any> = { kind: 'message', messageId: newId('msg'), role: 'user', parts: [{ text: outgoing }] };
      if (contextIdRef.current) message.contextId = contextIdRef.current;
      const res = await fetch(AGENT_URL, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ jsonrpc: '2.0', id: newId('req'), method: 'message/send', params: { message } }) });
      const envelope = await res.json();
      if (typeof envelope?.result?.contextId === 'string') contextIdRef.current = envelope.result.contextId;
      const parts: any[] = (envelope?.result?.artifacts ?? []).flatMap((a: any) => a?.parts ?? []);
      const a2uiMessages = parts.map((p) => p?.data?.a2uiEnvelope).find(Boolean);
      const agentText = [envelope?.result?.status?.message?.parts, ...(parts.length ? [parts] : [])]
        .flatMap((x: any) => x ?? []).map((p: any) => p?.text).filter((t: any) => typeof t === 'string' && t.trim()).join(' ').trim();
      if (a2uiMessages) {
        try {
          // Always render on a fresh surface: drop existing ones when the agent creates a new surface.
          if (Array.isArray(a2uiMessages) && a2uiMessages.some((m: any) => m?.createSurface)) {
            for (const id of Array.from(processor.model.surfacesMap.keys())) processor.model.deleteSurface(id);
          }
          processor.processMessages(sanitizeEnvelope(a2uiMessages));
        } catch (err: any) {
          console.error('[A2UI] failed to render surface', err, a2uiMessages);
          addBotMessage(`I received the results but couldn't render them: ${err?.message ?? err}`);
          return;
        }
        const explanation = extractExplanation(a2uiMessages);
        const reply = explanation || agentText;
        addBotMessage(reply || 'Updated the booking view.');
      } else {
        console.warn('[A2UI] no a2uiEnvelope in response', envelope);
        addBotMessage("I couldn't get a valid result from the booking service. Please try again.");
      }
    } catch (e: any) {
      console.error('[A2UI] fetch error', e); addBotMessage(`Something went wrong: ${e?.message ?? e}`);
    } finally { setIsSending(false); uiStore.set({ busy: false }); }
  }

  // Fired by AIAssistView whenever a prompt is submitted (typed, or executed programmatically for stage / surface actions).
  const onPromptRequest = (args: PromptRequestEventArgs) => {
    const text = String(args.prompt ?? '');
    userPromptsRef.current.push(text);
    setMessageCount((n) => n + 1);
    setView('chat');
    const payload = silentPayloadRef.current;
    silentPayloadRef.current = null;
    void sendToAgent(payload ?? text, payload !== null);
  };

  useEffect(() => {
    const created = processor.onSurfaceCreated(setSurface);
    const removed = processor.onSurfaceDeleted((id) => { setSurface((prev) => (prev && prev.id === id ? null : prev)); });
    return () => { created.unsubscribe(); removed.unsubscribe(); };
  }, [processor]);

  useEffect(() => { uiStore.set({ welcome: view === 'welcome' }); }, [view]);

  const resetSurfaces = () => { for (const id of Array.from(processor.model.surfacesMap.keys())) { processor.model.deleteSurface(id); } };
  const resetConversation = () => {
    contextIdRef.current = null; userPromptsRef.current = []; tripRef.current = {}; silentPayloadRef.current = null;
    Object.keys(msgTimes).forEach((k) => delete msgTimes[k]);
    setMessageCount(0); setChatKey((k) => k + 1);
  };
  const openStage = (stage: Stage) => { setActiveStage(stage); sendPrompt(stage.starterPrompt); };
  const sendPrompt = (text: string) => { if (text.trim()) assistRef.current?.executePrompt(text); };
  uiActions.openStage = openStage;
  const backHome = () => { resetSurfaces(); setSurface(null); resetConversation(); setActiveStage(null); setView('welcome'); setMobileTab('chat'); };
  const currentStep = surface ? Math.max(1, Math.min(3, ((): number => {
    const id = String(surface.id).toLowerCase();
    // Match stage patterns: "stage3", "confirmation", "booking", "confirm"
    if (/stage[_-]?3|book|confirm/.test(id)) return 3;
    // Match stage patterns: "stage2", "results", "flight.*results"
    if (/stage[_-]?2|result/.test(id)) return 2;
    // Default to stage 1 (search)
    return 1;
  })())) : 0;
  return (
    <div className={`app-shell ${view === 'chat' ? 'is-chat' : 'is-welcome'}`}>
      <header className="app-header">
        <div className="brand"><div className="brand-mark" aria-hidden="true"><Icon name="plane" size={20} /></div><div className="brand-text"><div className="brand-name">SkyBook</div><div className="brand-sub">Enterprise Flight Booking</div></div></div>
        <nav className="app-nav">{view === 'chat' && (<button className="nav-cta" onClick={backHome} type="button"> ← Home</button>)}</nav>
      </header>
      {view === 'chat' && (
        <div className="mobile-tabs" role="tablist">
          <button type="button" role="tab" aria-selected={mobileTab === 'chat'} className={mobileTab === 'chat' ? 'is-active' : ''} onClick={() => setMobileTab('chat')}>Chat</button>
          <button type="button" role="tab" aria-selected={mobileTab === 'view'} className={mobileTab === 'view' ? 'is-active' : ''} onClick={() => setMobileTab('view')}>Booking view</button>
        </div>
      )}
      <main className={`app-body tab-${mobileTab}`}>
        <section className="chat-pane" aria-label="Flight booking assistant">
          <div className={`chat-room ${view === 'welcome' ? 'chat-room--welcome' : ''}`}>
            {view === 'chat' && (
              <>
                <div className="chat-room-header">
                  <div className="ch-top">
                    <div className="bot-avatar" aria-hidden="true"><Icon name="bot" size={22} /><span className="ch-presence" /></div>
                    <div className="bot-meta">
                      <div className="bot-name">SkyBook Assistant <span className="ch-verified" title="Verified enterprise assistant"><Icon name="wave" size={14} /></span></div>
                      <div className="bot-status">Online</div>
                    </div>
                    <button type="button" className="ch-clear" onClick={resetConversation} disabled={messageCount === 0} title="Clear conversation">Clear</button>
                  </div>
                  {(currentStep > 0 || activeStage) && (
                    <div className="ch-context">
                      <span className="ch-context-label">Current step</span>
                      {currentStep > 0 ? (
                        <span className="chat-stage-pill" title={`Step ${currentStep} of 3`}><Icon name={STAGE_ICON[STAGES[currentStep - 1]?.id]} size={14} /> Step {currentStep} · {STAGES[currentStep - 1]?.title}</span>
                      ) : (
                        <span className="chat-stage-pill" title={`Step ${activeStage?.step || 1} of 3`}><Icon name={STAGE_ICON[(activeStage)?.id]} size={14} /> Step {activeStage?.step || 1} · {activeStage?.title}</span>
                      )}
                    </div>
                  )}
                </div>
              </>
            )}
                <AIAssistViewComponent
                  key={chatKey}
                  id="skybook-assist"
                  ref={assistRef}
                  cssClass="skybook-assist"
                  showHeader={false}
                  enableScrollToBottom
                  promptToolbarSettings={{ items: [] }}
                  responseToolbarSettings={{ items: [] }}
                  bannerTemplate={bannerTemplate}
                  promptItemTemplate={promptItemTemplate}
                  responseItemTemplate={responseItemTemplate}
                  responseAnimationTemplate={responseAnimationTemplate}
                  footerTemplate={footerTemplate}
                  promptRequest={onPromptRequest}
                />
          </div>
        </section>

        <section className="surface-pane" aria-label="Booking result">
          {view === 'chat' && (
            <ol className="stepper" aria-label="Booking progress">
              {STAGES.map((s) => (
                <li key={s.id} className={`step ${currentStep === s.step ? 'is-current' : ''} ${currentStep > s.step ? 'is-done' : ''}`}>
                  <span className="step-dot">{currentStep > s.step ? '✓' : s.step}</span>
                  <span className="step-label">{s.id === 'booking' ? 'Confirmation' : s.title.replace('Flights', '').replace('Flight ', '').trim()}</span>
                </li>
              ))}
            </ol>
          )}
          {isSending && (<div className="surface-loading" aria-hidden="true"><span /></div>)}
          {surface ? (<div className="surface-scroll"><SyncfusionA2UIProvider key={surface.id} surface={surface} /></div>) : (
            <div className="surface-placeholder">
              <div className="surface-placeholder-inner">
                <div className="surface-icon"><Icon name="plane" size={44} /></div>
                <h3>Your booking workspace</h3>
                <p>Pick a step or ask SkyBook. Live flight search, results and confirmation will appear here in real time.</p>

                <div className="ph-trip" aria-hidden="true">
                  <div className="ph-trip-top"><span className="ph-chip">Sample trip</span><span className="ph-dates">Mar 12 – 18 · 1 traveller · Economy</span></div>
                  <div className="ph-route">
                    <div className="ph-city"><b>JFK</b><span>New York</span></div>
                    <div className="ph-line"><span className="ph-plane"><Icon name="plane" size={16} /></span><em>7h 10m · Non-stop</em></div>
                    <div className="ph-city"><b>LHR</b><span>London</span></div>
                  </div>
                  <div className="ph-trip-foot"><span>24 flights found</span><strong>from $549</strong></div>
                </div>

                <div className="ph-steps">
                  {STAGES.map((s) => (
                    <button key={s.id} type="button" className={`ph-step ph-step--${s.id}`} onClick={() => openStage(s)} disabled={isSending}>
                      <span className="ph-step-icon"><Icon name={STAGE_ICON[s.id]} size={18} /></span>
                      <span className="ph-step-text"><b>Step {s.step} · {s.title}</b><small>{s.tagline}</small></span>
                      <span className="ph-step-go" aria-hidden="true">→</span>
                    </button>
                  ))}
                </div>
              </div>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
