// Extension: signals-dashboard
// Live dashboard showing agent signals from workshop desks.
// Scans desks/*/.signals/ for JSON files, renders the latest signal per desk.
// Supports stashing desks (48hr hold) and restoring them.

import { createServer } from "node:http";
import { readdir, readFile, writeFile, stat } from "node:fs/promises";
import { join } from "node:path";
import { joinSession, createCanvas } from "@github/copilot-sdk/extension";

const servers = new Map();
const STASH_TTL_MS = 48 * 60 * 60 * 1000;

// --- Stash management ---

async function readStash(workshopDir) {
    const fp = join(workshopDir, ".desk-stash.json");
    try {
        const raw = await readFile(fp, "utf-8");
        const stash = JSON.parse(raw);
        const now = Date.now();
        const live = stash.filter(e => (now - new Date(e.stashedAt).getTime()) < STASH_TTL_MS);
        if (live.length !== stash.length) await writeStash(workshopDir, live);
        return live;
    } catch { return []; }
}

async function writeStash(workshopDir, entries) {
    const fp = join(workshopDir, ".desk-stash.json");
    await writeFile(fp, JSON.stringify(entries, null, 2), "utf-8");
}

async function stashDesk(workshopDir, deskName) {
    const stash = await readStash(workshopDir);
    if (stash.some(e => e.name === deskName)) return stash;
    stash.push({ name: deskName, stashedAt: new Date().toISOString() });
    await writeStash(workshopDir, stash);
    return stash;
}

async function restoreDesk(workshopDir, deskName) {
    let stash = await readStash(workshopDir);
    stash = stash.filter(e => e.name !== deskName);
    await writeStash(workshopDir, stash);
    return stash;
}

// --- Signal reading ---

async function scanSignals(workshopDir) {
    const results = [];
    for (const subdir of ["desks", "classroom"]) {
        const parent = join(workshopDir, subdir);
        let entries;
        try { entries = await readdir(parent, { withFileTypes: true }); }
        catch { continue; }

        for (const entry of entries) {
            if (!entry.isDirectory() || entry.name.startsWith(".")) continue;
            const sigDir = join(parent, entry.name, ".signals");
            let sigFiles;
            try { sigFiles = await readdir(sigDir); }
            catch {
                results.push({
                    deskName: entry.name, signalType: "none", agentName: entry.name,
                    confidence: 0, accuracy: 0, completeness: 0, intent: 0,
                    whatWorked: "", whatWasHard: "", skillGap: "",
                    escalationReason: null, escalationBlocked: null, recommendation: null,
                    emittedAt: null, signalCount: 0,
                    tokensIn: 0, tokensOut: 0, model: null,
                });
                continue;
            }

            const jsonFiles = sigFiles.filter(f => f.endsWith(".json"));
            if (jsonFiles.length === 0) {
                results.push({
                    deskName: entry.name, signalType: "none", agentName: entry.name,
                    confidence: 0, accuracy: 0, completeness: 0, intent: 0,
                    whatWorked: "", whatWasHard: "", skillGap: "",
                    escalationReason: null, escalationBlocked: null, recommendation: null,
                    emittedAt: null, signalCount: 0,
                    tokensIn: 0, tokensOut: 0, model: null,
                });
                continue;
            }

            let latest = null, latestTime = 0;
            for (const f of jsonFiles) {
                const fp = join(sigDir, f);
                try {
                    const s = await stat(fp);
                    if (s.mtimeMs > latestTime) { latestTime = s.mtimeMs; latest = fp; }
                } catch {}
            }
            if (!latest) continue;
            try {
                const raw = await readFile(latest, "utf-8");
                const sig = JSON.parse(raw);
                results.push({
                    deskName: entry.name,
                    signalType: sig.signal_type || "execution",
                    subtype: sig.subtype || sig.signal_type || "execution",
                    agentName: sig.agent_name || entry.name,
                    confidence: sig.self_assessment?.confidence || 0,
                    accuracy: sig.self_assessment?.accuracy || 0,
                    completeness: sig.self_assessment?.completeness || 0,
                    intent: sig.self_assessment?.intent || 0,
                    whatWorked: sig.patterns?.what_worked || "",
                    whatWasHard: sig.patterns?.what_was_hard || "",
                    skillGap: sig.patterns?.skill_gap || "",
                    escalationReason: sig.escalation?.reason || null,
                    escalationBlocked: sig.escalation?.blocked_on || null,
                    recommendation: sig.escalation?.recommendation || null,
                    emittedAt: new Date(latestTime).toISOString(),
                    signalCount: jsonFiles.length,
                    tokensIn: sig.usage?.tokens_in || 0,
                    tokensOut: sig.usage?.tokens_out || 0,
                    model: sig.usage?.model || null,
                });
            } catch {}
        }
    }
    return results;
}

// --- Sorting: escalations → recent signals → no signals ---

function signalSortKey(sig) {
    if (sig.signalType === "escalation") return 0;
    if (sig.signalType === "execution") return 1;
    if (sig.signalType === "partnership") return 1;
    return 2; // "none"
}

function sortSignals(signals) {
    return signals.sort((a, b) => {
        const ka = signalSortKey(a), kb = signalSortKey(b);
        if (ka !== kb) return ka - kb;
        if (a.emittedAt && b.emittedAt) return new Date(b.emittedAt) - new Date(a.emittedAt);
        if (a.emittedAt) return -1;
        if (b.emittedAt) return 1;
        return a.deskName.localeCompare(b.deskName);
    });
}

// --- HTML rendering ---

function esc(s) {
    return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
function truncate(s, len) {
    return s.length > len ? s.slice(0, len) + "…" : s;
}

function formatTokens(n) {
    if (!n) return null;
    if (n >= 1000000) return `${(n / 1000000).toFixed(1)}M`;
    if (n >= 1000) return `${(n / 1000).toFixed(1)}k`;
    return `${n}`;
}

function scoreBar(value, label, max = 5) {
    const pct = (value / max) * 100;
    const color = value >= 4 ? "#22c55e" : value >= 3 ? "#eab308" : value >= 1 ? "#ef4444" : "#262626";
    return `<div>
        <div style="display:flex;justify-content:space-between;margin-bottom:2px;">
            <span style="font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.04em;">${label}</span>
            <span style="font-size:10px;color:#94a3b8;">${value}/5</span>
        </div>
        <div style="height:4px;background:#1e293b;border-radius:2px;overflow:hidden;">
            <div style="width:${pct}%;height:100%;background:${color};border-radius:2px;transition:width .3s;"></div>
        </div>
    </div>`;
}

function timeSince(isoDate) {
    if (!isoDate) return "—";
    const diff = Date.now() - new Date(isoDate).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return "just now";
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}h ago`;
    return `${Math.floor(hrs / 24)}d ago`;
}

function timeRemaining(stashedAt) {
    const remaining = STASH_TTL_MS - (Date.now() - new Date(stashedAt).getTime());
    if (remaining <= 0) return "expiring";
    const hrs = Math.floor(remaining / 3600000);
    return hrs >= 1 ? `${hrs}h left` : `${Math.floor(remaining / 60000)}m left`;
}

function avgScore(signals) {
    const withSignals = signals.filter(s => s.signalType !== "none");
    if (withSignals.length === 0) return null;
    const avg = (field) => {
        const vals = withSignals.map(s => s[field]).filter(v => v > 0);
        return vals.length ? (vals.reduce((a, b) => a + b, 0) / vals.length).toFixed(1) : "—";
    };
    return { confidence: avg("confidence"), accuracy: avg("accuracy"), completeness: avg("completeness"), intent: avg("intent") };
}

function renderSummaryBar(activeSignals) {
    const escalations = activeSignals.filter(s => s.signalType === "escalation").length;
    const withSignals = activeSignals.filter(s => s.signalType !== "none").length;
    const awaiting = activeSignals.filter(s => s.signalType === "none").length;
    const totalTokens = activeSignals.reduce((sum, s) => sum + (s.tokensIn || 0) + (s.tokensOut || 0), 0);
    const avg = avgScore(activeSignals);

    const escBadge = escalations > 0
        ? `<span style="background:#7f1d1d;color:#fca5a5;padding:3px 10px;border-radius:12px;font-size:12px;font-weight:600;">⚠ ${escalations} escalation${escalations > 1 ? "s" : ""}</span>`
        : "";

    const tokenBadge = totalTokens > 0
        ? `<span style="font-size:11px;color:#475569;">🪙 ${formatTokens(totalTokens)}</span>`
        : "";

    const avgBlock = avg ? `
        <div style="display:flex;gap:12px;font-size:11px;color:#64748b;">
            <span>intent <b style="color:#94a3b8;">${avg.intent}</b></span>
            <span>conf <b style="color:#94a3b8;">${avg.confidence}</b></span>
            <span>acc <b style="color:#94a3b8;">${avg.accuracy}</b></span>
            <span>comp <b style="color:#94a3b8;">${avg.completeness}</b></span>
        </div>` : "";

    return `
    <div style="display:flex;justify-content:space-between;align-items:center;padding:10px 14px;
                background:#0f172a;border:1px solid #1e293b;border-radius:8px;margin-bottom:14px;">
        <div style="display:flex;align-items:center;gap:12px;">
            <span style="font-size:13px;color:#cbd5e1;"><b style="color:#f1f5f9;">${activeSignals.length}</b> desk${activeSignals.length !== 1 ? "s" : ""}</span>
            <span style="font-size:11px;color:#475569;">${withSignals} reporting · ${awaiting} awaiting</span>
            ${tokenBadge}
            ${escBadge}
        </div>
        ${avgBlock}
    </div>`;
}

function renderSignalCard(sig) {
    const isEscalation = sig.signalType === "escalation";
    const noSignal = sig.signalType === "none";
    const borderColor = isEscalation ? "#dc2626" : noSignal ? "#1e293b" : "#1e3a5f";
    const bgColor = isEscalation ? "#0f0604" : "#0f172a";

    const typeLabel = isEscalation
        ? `<span style="background:#7f1d1d;color:#fca5a5;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:600;">⚠ ${sig.subtype === "blocked" ? "BLOCKED" : "HANDS-UP"}</span>`
        : noSignal
        ? `<span style="background:#1e293b;color:#64748b;padding:2px 8px;border-radius:4px;font-size:11px;">📡 awaiting</span>`
        : sig.subtype === "done"
        ? `<span style="background:#052e16;color:#86efac;padding:2px 8px;border-radius:4px;font-size:11px;">✓ done</span>`
        : sig.subtype === "partnership"
        ? `<span style="background:#1e1b4b;color:#a5b4fc;padding:2px 8px;border-radius:4px;font-size:11px;">◇ partnership</span>`
        : `<span style="background:#0c2d48;color:#7dd3fc;padding:2px 8px;border-radius:4px;font-size:11px;">✓ checkpoint</span>`;

    const stashBtn = `<button onclick="stashDesk('${esc(sig.deskName)}')"
        style="background:none;border:1px solid #1e293b;color:#475569;padding:2px 8px;border-radius:4px;
               font-size:11px;cursor:pointer;transition:all .15s;"
        onmouseover="this.style.borderColor='#dc2626';this.style.color='#fca5a5'"
        onmouseout="this.style.borderColor='#1e293b';this.style.color='#475569'">stash</button>`;

    let escalationBlock = "";
    if (isEscalation && sig.escalationReason) {
        escalationBlock = `
        <div style="margin-top:10px;padding:8px 10px;background:#1c1917;border-left:3px solid #dc2626;border-radius:0 4px 4px 0;">
            <div style="font-size:11px;color:#fca5a5;font-weight:600;">Blocked on:</div>
            <div style="font-size:12px;color:#e2e8f0;margin-top:2px;">${esc(sig.escalationBlocked || sig.escalationReason)}</div>
            ${sig.recommendation ? `<div style="font-size:11px;color:#94a3b8;margin-top:4px;">→ ${esc(sig.recommendation)}</div>` : ""}
        </div>`;
    }

    const scoresBlock = noSignal ? `
        <div style="padding:12px 0;text-align:center;color:#334155;font-size:12px;">
            No signals yet — this desk is waiting for its first session.
        </div>` : `
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px 16px;margin-bottom:12px;">
            ${scoreBar(sig.intent, "intent")}
            ${scoreBar(sig.confidence, "confidence")}
            ${scoreBar(sig.accuracy, "accuracy")}
            ${scoreBar(sig.completeness, "completeness")}
        </div>`;

    const patternsBlock = (sig.whatWorked || sig.whatWasHard || sig.skillGap) ? `
        <div style="border-top:1px solid #1e293b;padding-top:8px;margin-top:4px;">
            ${sig.whatWorked ? `<div style="font-size:12px;margin-bottom:3px;line-height:1.4;"><span style="color:#22c55e;margin-right:4px;">✓</span><span style="color:#94a3b8;">${esc(truncate(sig.whatWorked, 160))}</span></div>` : ""}
            ${sig.whatWasHard ? `<div style="font-size:12px;margin-bottom:3px;line-height:1.4;"><span style="color:#eab308;margin-right:4px;">△</span><span style="color:#94a3b8;">${esc(truncate(sig.whatWasHard, 160))}</span></div>` : ""}
            ${sig.skillGap ? `<div style="font-size:12px;line-height:1.4;"><span style="color:#ef4444;margin-right:4px;">✗</span><span style="color:#94a3b8;">${esc(truncate(sig.skillGap, 160))}</span></div>` : ""}
        </div>` : "";

    return `
    <div style="background:${bgColor};border:1px solid ${borderColor};border-radius:8px;padding:14px;margin-bottom:8px;
                ${isEscalation ? "animation:pulse 2s ease-in-out infinite;" : ""}">
        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:10px;">
            <div style="display:flex;align-items:center;gap:8px;">
                <span style="font-size:15px;font-weight:600;color:#f1f5f9;">${esc(sig.deskName)}</span>
                ${typeLabel}
            </div>
            <div style="display:flex;align-items:center;gap:8px;">
                ${(sig.tokensIn || sig.tokensOut) ? `<span style="font-size:10px;color:#334155;background:#0f172a;border:1px solid #1e293b;padding:1px 6px;border-radius:3px;" title="in: ${sig.tokensIn} · out: ${sig.tokensOut}${sig.model ? ' · ' + esc(sig.model) : ''}">🪙 ${formatTokens(sig.tokensIn + sig.tokensOut)}</span>` : ""}
                <span style="font-size:11px;color:#475569;">${timeSince(sig.emittedAt)}${sig.signalCount ? ` · ${sig.signalCount}` : ""}</span>
                ${stashBtn}
            </div>
        </div>
        ${scoresBlock}
        ${patternsBlock}
        ${escalationBlock}
    </div>`;
}

function renderStashedCard(entry) {
    return `
    <div style="background:#080808;border:1px solid #1a1a1a;border-radius:6px;padding:8px 12px;margin-bottom:6px;
                display:flex;justify-content:space-between;align-items:center;">
        <div style="display:flex;align-items:center;gap:8px;">
            <span style="font-size:13px;color:#525252;">${esc(entry.name)}</span>
            <span style="font-size:10px;color:#3f3f46;background:#18181b;padding:1px 6px;border-radius:3px;">${timeRemaining(entry.stashedAt)}</span>
        </div>
        <button onclick="restoreDesk('${esc(entry.name)}')"
            style="background:none;border:1px solid #262626;color:#525252;padding:2px 8px;border-radius:4px;
                   font-size:11px;cursor:pointer;transition:all .15s;"
            onmouseover="this.style.borderColor='#22c55e';this.style.color='#86efac'"
            onmouseout="this.style.borderColor='#262626';this.style.color='#525252'">restore</button>
    </div>`;
}

function renderDashboard(signals, stashed) {
    const activeSignals = sortSignals(signals.filter(s => !stashed.some(e => e.name === s.deskName)));

    const cards = activeSignals.length > 0
        ? activeSignals.map(renderSignalCard).join("")
        : `<div style="text-align:center;padding:30px 20px;color:#475569;">
            <div style="font-size:28px;margin-bottom:10px;">🪨</div>
            <div style="font-size:14px;color:#94a3b8;margin-bottom:16px;">No active desks yet</div>
            <div style="text-align:left;background:#0f172a;border:1px solid #1e293b;border-radius:8px;padding:16px;max-width:360px;margin:0 auto;">
                <div style="font-size:12px;font-weight:600;color:#cbd5e1;margin-bottom:10px;">Get started</div>
                <div style="font-size:12px;color:#94a3b8;line-height:1.6;margin-bottom:8px;">
                    Ask the <b style="color:#7dd3fc;">Workshop TA</b> in chat:
                </div>
                <div style="background:#020617;border:1px solid #1e293b;border-radius:4px;padding:8px 10px;margin-bottom:12px;">
                    <code style="font-size:12px;color:#e2e8f0;background:none;padding:0;">"open a desk called scanning in ~/my-workshop"</code>
                </div>
                <div style="font-size:11px;color:#64748b;line-height:1.5;">
                    The TA uses the <b>desk-open</b> skill to create a desk with a journal. Once a desk emits signals, they'll appear here automatically.
                </div>
                <div style="border-top:1px solid #1e293b;margin-top:12px;padding-top:10px;font-size:11px;color:#475569;">
                    <div style="margin-bottom:4px;">💡 <b style="color:#64748b;">Quick commands to try:</b></div>
                    <div style="color:#64748b;line-height:1.8;">
                        • "open a desk for code review"<br/>
                        • "what's everyone working on?"<br/>
                        • "show me the signals"
                    </div>
                </div>
            </div>
           </div>`;

    const summaryBar = activeSignals.length > 0 ? renderSummaryBar(activeSignals) : "";

    const stashedSection = stashed.length > 0 ? `
        <div style="margin-top:20px;padding-top:12px;border-top:1px solid #1a1a1a;">
            <div style="font-size:11px;font-weight:600;color:#3f3f46;margin-bottom:8px;text-transform:uppercase;letter-spacing:.06em;">
                Stashed · ${stashed.length}
            </div>
            ${stashed.map(renderStashedCard).join("")}
        </div>` : "";

    return `<!doctype html>
<html>
<head>
    <meta charset="utf-8" />
    <title>Cairn · Signals</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
               background: #020617; color: #e2e8f0; padding: 16px; }
        code { background: #1e293b; padding: 1px 5px; border-radius: 3px; font-size: 12px; }
        @keyframes pulse {
            0%, 100% { border-color: #dc2626; }
            50% { border-color: #7f1d1d; }
        }
        #content { transition: opacity .15s; }
    </style>
</head>
<body>
    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px;">
        <h1 style="font-size:16px;font-weight:600;color:#f8fafc;">🪨 Cairn</h1>
        <span id="status" style="font-size:11px;color:#334155;">live</span>
    </div>
    <div id="content">
        ${summaryBar}
        ${cards}
        ${stashedSection}
    </div>

    <script>
        async function stashDesk(name) {
            await fetch('/api/stash/' + encodeURIComponent(name), { method: 'POST' });
            refresh();
        }
        async function restoreDesk(name) {
            await fetch('/api/restore/' + encodeURIComponent(name), { method: 'POST' });
            refresh();
        }
        async function refresh() {
            try {
                const res = await fetch('/');
                const html = await res.text();
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, 'text/html');
                const newContent = doc.getElementById('content');
                if (newContent) {
                    document.getElementById('content').innerHTML = newContent.innerHTML;
                }
            } catch {}
        }
        // Smooth auto-refresh every 5s (no full page reload)
        setInterval(refresh, 5000);
    </script>
</body>
</html>`;
}

// --- Server ---

async function startServer(instanceId, workshopDir) {
    const server = createServer(async (req, res) => {
        const url = new URL(req.url, `http://${req.headers.host}`);

        if (req.method === "POST" && url.pathname.startsWith("/api/stash/")) {
            const deskName = decodeURIComponent(url.pathname.split("/api/stash/")[1]);
            await stashDesk(workshopDir, deskName);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: true }));
            return;
        }
        if (req.method === "POST" && url.pathname.startsWith("/api/restore/")) {
            const deskName = decodeURIComponent(url.pathname.split("/api/restore/")[1]);
            await restoreDesk(workshopDir, deskName);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: true }));
            return;
        }

        const signals = await scanSignals(workshopDir);
        const stashed = await readStash(workshopDir);
        res.setHeader("Content-Type", "text/html; charset=utf-8");
        res.end(renderDashboard(signals, stashed));
    });
    await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
    const address = server.address();
    const port = typeof address === "object" && address ? address.port : 0;
    return { server, url: `http://127.0.0.1:${port}/` };
}

// --- Canvas registration ---

const session = await joinSession({
    canvases: [
        createCanvas({
            id: "signals-dashboard",
            displayName: "Workshop Signals",
            description: "Live dashboard showing agent signals from workshop desks. Pass workshopDir to point at your workshop root.",
            inputSchema: {
                type: "object",
                properties: {
                    workshopDir: { type: "string", description: "Absolute path to the workshop root (the folder containing desks/)" },
                },
                required: ["workshopDir"],
            },
            actions: [
                {
                    name: "refresh",
                    description: "Force-refresh the signals dashboard and return current signal data as JSON",
                    handler: async (ctx) => {
                        const entry = servers.get(ctx.instanceId);
                        if (!entry) return { error: "Dashboard not open" };
                        const signals = await scanSignals(entry.workshopDir);
                        const stashed = await readStash(entry.workshopDir);
                        return { signals, stashed, activeCount: signals.length - stashed.length };
                    },
                },
                {
                    name: "stash",
                    description: "Stash a desk (hides it for 48hrs, then it drops off). Use to pause a workstream.",
                    inputSchema: {
                        type: "object",
                        properties: { deskName: { type: "string", description: "Name of the desk to stash" } },
                        required: ["deskName"],
                    },
                    handler: async (ctx) => {
                        const entry = servers.get(ctx.instanceId);
                        if (!entry) return { error: "Dashboard not open" };
                        const stash = await stashDesk(entry.workshopDir, ctx.input.deskName);
                        return { ok: true, stashed: stash };
                    },
                },
                {
                    name: "restore",
                    description: "Restore a stashed desk back to active.",
                    inputSchema: {
                        type: "object",
                        properties: { deskName: { type: "string", description: "Name of the desk to restore" } },
                        required: ["deskName"],
                    },
                    handler: async (ctx) => {
                        const entry = servers.get(ctx.instanceId);
                        if (!entry) return { error: "Dashboard not open" };
                        const stash = await restoreDesk(entry.workshopDir, ctx.input.deskName);
                        return { ok: true, stashed: stash };
                    },
                },
            ],
            open: async (ctx) => {
                const workshopDir = ctx.input?.workshopDir || process.cwd();
                let entry = servers.get(ctx.instanceId);
                if (!entry) {
                    entry = await startServer(ctx.instanceId, workshopDir);
                    entry.workshopDir = workshopDir;
                    servers.set(ctx.instanceId, entry);
                }
                return { title: "🪨 Cairn · Signals", url: entry.url };
            },
            onClose: async (ctx) => {
                const entry = servers.get(ctx.instanceId);
                if (entry) {
                    servers.delete(ctx.instanceId);
                    await new Promise((resolve) => entry.server.close(() => resolve()));
                }
            },
        }),
    ],
});
