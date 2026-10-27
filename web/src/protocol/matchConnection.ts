import { Kind, MaxCommandBytes, MaxHelloBytes, Signal, envelope, open } from './codec';
import {
  type CommandAck, type GameCommand, type MatchResult, type MatchWelcome, type PlayerView,
  encodeCommand, normalizeAck, normalizeReport, normalizeView, normalizeWelcome,
} from './types';
import { FrameAssembler, MaxResultBytes, MaxResultCompressed, MaxSnapshotBytes, MaxSnapshotCompressed, decodeJson, inflate } from './wire';

export interface Compatibility { protocol: number; rules: number; contentHash: string }

export interface MatchConnectionHandlers {
  admitted?: () => void;
  started?: (welcome: MatchWelcome) => void;
  view?: (view: PlayerView) => void;
  ack?: (ack: CommandAck) => void;
  finished?: (result: MatchResult) => void;
  status?: (message: string) => void;
  closed?: (reason: string) => void;
}

type SocketFactory = (url: string) => WebSocket;

// Browser side of the dedicated server's websocket transport. Same lifecycle as
// NgoMatchClient: hello → admitted → ready → welcome (countdown) → snapshots/acks →
// result (acknowledged once). Authority always stays on the server.
export class MatchConnection {
  private socket: WebSocket | null = null;
  private readonly snapshots = new FrameAssembler(MaxSnapshotCompressed, MaxSnapshotBytes);
  private readonly results = new FrameAssembler(MaxResultCompressed, MaxResultBytes);
  private welcome: MatchWelcome | null = null;
  private buffered: PlayerView | null = null;
  private startsAt = 0;
  private ended = false;
  private closedReported = false;
  private lastStateAt = 0;
  private watchdog: ReturnType<typeof setInterval> | null = null;
  private connectedAt = 0;
  admitted = false;
  readonly encoder = new TextEncoder();

  constructor(
    readonly url: string,
    private readonly compatibility: Compatibility,
    private readonly faction: string,
    private readonly handlers: MatchConnectionHandlers,
    private readonly ticket: { matchId: string; joinTicket: string } | null = null,
    private readonly createSocket: SocketFactory = (u) => new WebSocket(u),
    private readonly now: () => number = () => performance.now() / 1000,
  ) {}

  get matchWelcome() { return this.welcome; }
  get isOpen() { return this.socket?.readyState === 1 && !this.closedReported; }
  get countdownRemaining(): number {
    return this.welcome === null || this.ended ? 0 : Math.max(0, this.startsAt - this.now());
  }

  connect(): void {
    if (this.socket) throw new Error('Already connected.');
    const hello = this.encoder.encode(JSON.stringify({
      protocol: this.compatibility.protocol, rules: this.compatibility.rules, contentHash: this.compatibility.contentHash,
      faction: this.faction, matchId: this.ticket?.matchId ?? '', joinTicket: this.ticket?.joinTicket ?? '',
    }));
    if (hello.length > MaxHelloBytes) throw new Error('Connection request is too large.');
    let socket: WebSocket;
    try { socket = this.createSocket(this.url); }
    catch { this.fail('Could not open a connection to that address.'); return; }
    socket.binaryType = 'arraybuffer';
    this.socket = socket;
    this.connectedAt = this.lastStateAt = this.now();
    socket.onopen = () => socket.send(envelope(Kind.Hello, hello));
    socket.onmessage = (event) => {
      if (!(event.data instanceof ArrayBuffer)) return;
      this.dispatch(new Uint8Array(event.data));
    };
    socket.onclose = (event) => this.fail(event.reason || (this.admitted ? 'Connection to the server was lost.' : 'Could not connect. Check the server address and port.'));
    socket.onerror = () => { /* onclose follows with the reason */ };
    // Same deadlines as NgoMatchClient: connect within 20 s; a running match must keep sending state.
    this.watchdog = setInterval(() => {
      const now = this.now();
      if (this.ended || this.closedReported) return;
      if (!this.admitted && now - this.connectedAt > 20) this.close('Connection timed out.');
      else if (this.welcome && now - this.lastStateAt > 15) this.close('The server stopped sending match state.');
    }, 1000);
  }

  send(command: GameCommand): boolean {
    if (!this.isOpen || !this.welcome || this.countdownRemaining > 0 || this.ended || command.sequence <= 0) return false;
    const bytes = this.encoder.encode(encodeCommand(command));
    if (bytes.length > MaxCommandBytes) return false;
    this.socket!.send(envelope(Kind.Command, bytes));
    return true;
  }

  close(reason = 'You left the match.'): void {
    const socket = this.socket;
    if (socket && socket.readyState <= 1) {
      try { socket.close(1000, 'Client left.'); } catch { /* already closing */ }
    }
    this.fail(reason);
  }

  private fail(reason: string): void {
    if (this.closedReported) return;
    this.closedReported = true;
    if (this.watchdog) clearInterval(this.watchdog);
    this.watchdog = null;
    if (this.socket) { this.socket.onmessage = null; this.socket.onclose = null; }
    this.handlers.closed?.(reason);
  }

  private dispatch(frame: Uint8Array): void {
    const message = open(frame);
    if (!message) { this.close('The server sent an invalid message.'); return; }
    try {
      switch (message.kind) {
        case Kind.Admitted:
          if (this.admitted) return;
          this.admitted = true;
          this.socket!.send(envelope(Kind.Ready, Signal));
          this.handlers.admitted?.();
          break;
        case Kind.Welcome: {
          const welcome = normalizeWelcome(decodeJson(message.payload));
          if (this.welcome || welcome.protocol !== this.compatibility.protocol || welcome.player < 0 || welcome.player > 1)
            throw new Error('Invalid match welcome.');
          this.welcome = welcome; this.lastStateAt = this.now();
          this.startsAt = this.now() + Math.max(0, Math.min(10, welcome.countdownSeconds));
          this.handlers.started?.(welcome);
          if (this.buffered) { const initial = this.buffered; this.buffered = null; this.publish(initial); }
          break;
        }
        case Kind.Snapshot: {
          if (this.ended) return;
          const assembled = this.snapshots.add(message.payload);
          if (!assembled) return;
          const view = normalizeView(decodeJson(inflate(assembled.compressed, assembled.rawLength, MaxSnapshotBytes)));
          if (this.welcome) this.publish(view); else this.buffered = view;
          break;
        }
        case Kind.Ack:
          this.handlers.ack?.(normalizeAck(decodeJson(message.payload)));
          break;
        case Kind.Result: {
          if (this.ended) return;
          this.lastStateAt = this.now();
          const assembled = this.results.add(message.payload);
          if (!assembled) return;
          const envelopeJson = decodeJson(inflate(assembled.compressed, assembled.rawLength, MaxResultBytes)) as Record<string, unknown>;
          const view = normalizeView(envelopeJson.view);
          // Availability comes from the explicit flag, never from the report's shape.
          const report = envelopeJson.reportUnavailable === true ? null : normalizeReport(envelopeJson.report);
          if (view.winner < 0 || view.winner > 2 || (report && (report.winner !== view.winner || report.tick !== view.tick)))
            throw new Error('Incomplete final result.');
          this.publish(view);
          this.ended = true;
          this.socket!.send(envelope(Kind.ResultAck, Signal));
          this.handlers.finished?.({ view, report });
          break;
        }
        case Kind.Status: {
          const status = decodeJson(message.payload) as { message?: unknown };
          if (typeof status.message === 'string') this.handlers.status?.(status.message);
          break;
        }
        default:
          throw new Error('Unknown server message.');
      }
    } catch (error) {
      this.close('Invalid server message: ' + (error instanceof Error ? error.message : String(error)));
    }
  }

  private publish(view: PlayerView): void {
    if (!this.welcome || view.player !== this.welcome.player) throw new Error('Invalid player state.');
    this.lastStateAt = this.now();
    this.handlers.view?.(view);
  }
}
