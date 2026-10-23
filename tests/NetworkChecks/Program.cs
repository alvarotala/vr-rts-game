using Frontier.Networking;
using Frontier.Core;
using System.Text;
using System.Text.Json;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}
void Reject(Action action, string message)
{
    bool rejected = false;
    try { action(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, message);
}
FrameAssembler Assembler() => new(NetworkWire.MaxSnapshotCompressed, NetworkWire.MaxSnapshotBytes);
byte[] raw = new byte[15000]; new Random(42).NextBytes(raw);
byte[] packed = NetworkWire.Compress(raw, NetworkWire.MaxSnapshotBytes, NetworkWire.MaxSnapshotCompressed);
Check(packed.Length > NetworkWire.ChunkBytes * 3, "Fixture must span multiple packets.");
Check(NetworkWire.Decompress(packed, raw.Length, NetworkWire.MaxSnapshotBytes).SequenceEqual(raw), "Compression round-trip preserves state bytes.");
Reject(() => NetworkWire.Compress(raw, raw.Length - 1, NetworkWire.MaxSnapshotCompressed), "Outgoing raw limit is enforced.");
Reject(() => NetworkWire.Compress(raw, raw.Length, 100), "Outgoing compressed limit is enforced.");
Reject(() => NetworkWire.Decompress(packed, raw.Length - 1, NetworkWire.MaxSnapshotBytes), "A larger decoded payload cannot exceed its declared length.");
Reject(() => NetworkWire.Decompress(packed, raw.Length + 1, NetworkWire.MaxSnapshotBytes), "A truncated decoded payload cannot masquerade as complete.");
Reject(() => NetworkWire.Decompress(packed, int.MaxValue, NetworkWire.MaxSnapshotBytes), "Untrusted decoded length is bounded before allocation.");

int count = NetworkWire.ChunkCount(packed.Length);
byte[][] Chunks(long id) => Enumerable.Range(0, count).Select(i => NetworkWire.Chunk(id, packed, raw.Length, i)).ToArray();
var parts = Chunks(1);
var reassembly = Assembler(); byte[] completed = null;
foreach (int i in Enumerable.Range(0, count).Reverse())
{
    bool done = reassembly.Add(parts[i], out long id, out var data, out int length);
    Check(done == (i == 0), "Reordered frames publish only after every part arrives.");
    if (done) { Check(id == 1 && length == raw.Length, "Completed metadata belongs to the assembled frame."); completed = data; }
    Check(!reassembly.Add(parts[i], out _, out _, out _), "Duplicate chunks never publish twice or count as new parts.");
}
Check(completed.SequenceEqual(packed), "Reordered frame payload is exact.");
Check(NetworkWire.Decompress(completed, raw.Length, NetworkWire.MaxSnapshotBytes).SequenceEqual(raw), "Reordered frame decodes correctly.");

var losses = Assembler();
foreach (var part in parts.Skip(1)) Check(!losses.Add(part, out _, out _, out _), "A missing first packet never creates a partial state.");
var newer = Chunks(2);
Check(!losses.Add(newer[0], out _, out _, out _), "A new frame replaces incomplete older state.");
Check(!losses.Add(parts[0], out _, out _, out _), "Late old packets cannot overwrite newer state.");
bool newerDone = false;
foreach (var part in newer.Skip(1)) newerDone = losses.Add(part, out _, out _, out _);
Check(newerDone, "A complete later frame recovers after packet loss without a baseline.");
foreach (var part in parts) Check(!losses.Add(part, out _, out _, out _), "A previously skipped old frame cannot regress the client.");

byte[] Mutate(int offset, int value)
{
    byte[] packet = (byte[])parts[0].Clone();
    BitConverter.GetBytes(value).CopyTo(packet, offset); return packet;
}
foreach (var bad in new[] { Mutate(8, int.MaxValue), Mutate(8, -1), Mutate(12, int.MaxValue), Mutate(12, 0), Mutate(16, -1), Mutate(16, int.MaxValue), Mutate(20, 1), parts[0][..^1], new byte[24] })
    Check(!Assembler().Add(bad, out _, out _, out _), "Malformed metadata and payload lengths are rejected without publication.");

var progress = Assembler();
Check(!progress.Add(parts[0], out _, out _, out _, out bool advanced) && advanced, "A valid incomplete chunk records transfer progress.");
Check(!progress.Add(parts[0], out _, out _, out _, out advanced) && !advanced, "A duplicate cannot refresh the transfer timeout.");
Check(!progress.Add(Mutate(12, int.MaxValue), out _, out _, out _, out advanced) && !advanced, "Malformed metadata cannot refresh the transfer timeout.");
Check(!progress.Add(newer[0], out _, out _, out _, out advanced) && advanced, "A valid newer frame begins transfer progress.");
Check(!progress.Add(parts[1], out _, out _, out _, out advanced) && !advanced, "An obsolete chunk cannot refresh the transfer timeout.");
foreach (var part in newer.Skip(1))
{
    bool done = progress.Add(part, out _, out _, out _, out advanced);
    Check(advanced && done == ReferenceEquals(part, newer[^1]), "Every new chunk advances the transfer, including completion.");
}

byte[] finalView = Encoding.UTF8.GetBytes("{\"player\":1,\"tick\":37,\"winner\":1,\"entities\":[]}");
void CheckFinal(byte[] report, bool unavailable, string reason)
{
    var result = NetworkWire.PackResult(finalView, report, out int length, out bool reportUnavailable);
    using var json = JsonDocument.Parse(NetworkWire.Decompress(result, length, NetworkWire.MaxResultBytes));
    var envelope = json.RootElement;
    Check(reportUnavailable == unavailable && envelope.GetProperty("reportUnavailable").GetBoolean() == unavailable, reason);
    Check(envelope.GetProperty("view").GetProperty("player").GetInt32() == 1 && envelope.GetProperty("view").GetProperty("winner").GetInt32() == 1
        && envelope.GetProperty("view").GetProperty("tick").GetInt32() == 37, "Final delivery preserves the seat, winner and tick even without statistics.");
    Check(unavailable ? envelope.GetProperty("report").ValueKind == JsonValueKind.Null : envelope.GetProperty("report").GetProperty("winner").GetInt32() == 1,
        "The final envelope carries exactly one explicit report availability state.");
}
CheckFinal(Encoding.UTF8.GetBytes("{\"tick\":37,\"winner\":1}"), false, "A bounded report is delivered with the final view.");
CheckFinal(null, true, "A report rejected during serialization still delivers the outcome.");
CheckFinal(Encoding.UTF8.GetBytes("{\"padding\":\"" + new string('r', NetworkWire.MaxResultBytes - 50) + "\"}"), true,
    "Combined envelope overflow falls back to an unavailable report without losing the outcome.");
byte[] entropy = new byte[3 * 1024 * 1024]; new Random(73).NextBytes(entropy);
CheckFinal(Encoding.UTF8.GetBytes("{\"padding\":\"" + Convert.ToBase64String(entropy) + "\"}"), true,
    "Compressed report overflow also preserves a bounded outcome envelope.");
Reject(() => NetworkWire.PackResult(new byte[NetworkWire.MaxSnapshotBytes + 1], null, out _, out _), "An invalid oversized final view never masquerades as a report fallback.");

var fuzz = new Random(7);
var bounded = Assembler();
for (int i = 0; i < 2000; i++)
{
    byte[] packet = new byte[fuzz.Next(0, 1100)]; fuzz.NextBytes(packet);
    Check(!bounded.Add(packet, out _, out _, out _), "Random malformed datagrams never produce a valid frame.");
}
Console.WriteLine("FRONTIER_NETWORK_WIRE passed: " + checks + " checks");

GameCommand Valid() => new() { kind = CommandKind.Move, player = 1, sequence = 1, entities = new[] { 1 }, position = new Point(4, 5) };
Check(CommandBoundary.Validate(Valid()) == null, "Valid command envelope is allowed through to simulation validation.");
var cancel = Valid(); cancel.kind = CommandKind.CancelUnitOrder; cancel.unitOrderSequence = 73;
var cancellationCopy = JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize(cancel, new JsonSerializerOptions { IncludeFields = true }), new JsonSerializerOptions { IncludeFields = true });
Check(CommandBoundary.Validate(cancellationCopy) == null && cancellationCopy.kind == CommandKind.CancelUnitOrder && cancellationCopy.unitOrderSequence == 73, "Cancellation identity survives network JSON round-trip.");
var spoof = Valid(); spoof.player = 999; CommandBoundary.AssignAuthority(spoof, 0);
Check(spoof.player == 0, "Network authority comes from the assigned seat, not the payload.");
CommandBoundary.AssignAuthority(spoof, 1);
Check(spoof.player == 1, "Both server-assigned seats are supported.");
foreach (float invalid in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
{
    var command = Valid(); command.position.x = invalid;
    Check(CommandBoundary.Validate(command) != null, "Nonfinite x is rejected.");
    command = Valid(); command.position.y = invalid;
    Check(CommandBoundary.Validate(command) != null, "Nonfinite y is rejected.");
}
foreach (Action<GameCommand> damage in new Action<GameCommand>[] {
    c => c.sequence = 0, c => c.sequence = -1, c => c.kind = (CommandKind)999,
    c => c.replacePendingSequence = -1, c => c.queueOrderId = -1, c => c.unitOrderSequence = -1, c => c.entities = null,
    c => c.entities = new int[Simulation.UnitLimit + Simulation.BuildingLimit + 1], c => c.definition = new string('x', 65) })
{
    var command = Valid(); damage(command);
    Check(CommandBoundary.Validate(command) != null, "Invalid command envelope is rejected at the network boundary.");
}
var largest = Valid(); largest.entities = new int[Simulation.UnitLimit + Simulation.BuildingLimit];
Check(CommandBoundary.Validate(largest) == null, "Network envelope accepts maximum own population; game rules still validate selection.");
Check(CommandBoundary.Validate(null) != null, "Null command is rejected.");
var receipts = new CommandReceipts(2);
var accepted = new CommandAck { sequence = 1, accepted = true, tick = 25, message = "" };
receipts.Record(accepted);
Check(receipts.TryGet(1, out var repeated) && ReferenceEquals(repeated, accepted), "Duplicate order gets the original authoritative result without execution.");
receipts.Record(new CommandAck { sequence = 2, accepted = false, tick = 25, message = "No resources." });
Check(receipts.TryGet(2, out repeated) && !repeated.accepted && repeated.message == "No resources.", "Rejected commands are also idempotent.");
receipts.Record(new CommandAck { sequence = 4, accepted = true, tick = 26 });
Check(!receipts.TryGet(1, out _) && receipts.WasProcessed(1), "Eviction retires an old sequence permanently.");
Check(receipts.WasProcessed(3) && !receipts.WasProcessed(5), "Out-of-order old commands cannot bypass the high-water mark.");
bool duplicateRejected = false;
try { receipts.Record(new CommandAck { sequence = 4 }); } catch (InvalidOperationException) { duplicateRejected = true; }
Check(duplicateRejected, "A receipt cannot be replaced after processing.");
Console.WriteLine("FRONTIER_NETWORK_BOUNDARY passed: " + checks + " total checks");
