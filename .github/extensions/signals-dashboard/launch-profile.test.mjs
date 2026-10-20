import test from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
    buildDeskAgentArgv,
    isDeskProfile,
    isSafeQuotedWindowsCmdArg,
    isSafeWindowsCmdShim,
    isWindowsAppExecutionAlias,
    normalizeDeskProfile,
    parsePluginMcpNames,
    quoteWindowsCmdArgument,
    resolveDeskLaunchPreferences,
} from "./launch-profile.mjs";

test("normalizes supported profiles and defaults unknown values to repo", () => {
    assert.equal(isDeskProfile("repo"), true);
    assert.equal(isDeskProfile("CONNECTED"), true);
    assert.equal(isDeskProfile("other"), false);
    assert.equal(normalizeDeskProfile("CONNECTED"), "connected");
    assert.equal(normalizeDeskProfile("other"), "repo");
});

test("recognizes Windows App Execution Alias paths without trusting repository executables", () => {
    assert.equal(isWindowsAppExecutionAlias(
        "C:\\Users\\person\\AppData\\Local\\Microsoft\\WindowsApps\\wt.exe",
        "C:\\Users\\person\\AppData\\Local"), true);
    assert.equal(isWindowsAppExecutionAlias(
        "C:\\repo\\wt.exe",
        "C:\\Users\\person\\AppData\\Local"), false);
    assert.equal(isWindowsAppExecutionAlias(
        "C:\\Users\\person\\AppData\\Local\\Microsoft\\WindowsApps\\wt.cmd",
        "C:\\Users\\person\\AppData\\Local"), false);
});

test("quotes trusted cmd shim arguments and rejects percent-bearing paths", () => {
    assert.equal(
        quoteWindowsCmdArgument("C:\\Program Files\\Agency\\agency.cmd"),
        "\"C:\\Program Files\\Agency\\agency.cmd\"");
    assert.equal(quoteWindowsCmdArgument("--scope"), "\"--scope\"");
    assert.equal(isSafeWindowsCmdShim("C:\\Program Files\\Agency\\agency.cmd"), true);
    assert.equal(isSafeWindowsCmdShim("C:\\Users\\%USERNAME%\\agency.cmd"), false);
    // Quoted args may contain parentheses (common workshop folders).
    assert.equal(isSafeQuotedWindowsCmdArg("C:\\Work\\Project (1)"), true);
    assert.equal(isSafeQuotedWindowsCmdArg("C:\\Users\\%USERNAME%\\w"), false);
    assert.equal(isSafeQuotedWindowsCmdArg("C:\\Users\\!DELAY!\\w"), false);
});

test("executes a Windows cmd shim with safe quoting", {
    skip: process.platform !== "win32",
}, () => {
    const root = mkdtempSync(join(tmpdir(), "workshop-profile-"));
    const shimDir = join(root, "Shim Name");
    mkdirSync(shimDir);
    const shim = join(shimDir, "copilot.cmd");
    writeFileSync(shim, "@echo off\r\necho {\"plugins\":[]}\r\n");

    const cmd = join(process.env.SystemRoot, "System32", "cmd.exe");
    const commandLine = `"${[shim, "plugins", "list"]
        .map(quoteWindowsCmdArgument)
        .join(" ")}"`;
    const result = spawnSync(cmd, ["/d", "/s", "/c", commandLine], {
        encoding: "utf8",
        windowsHide: true,
        windowsVerbatimArguments: true,
    });

    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /\{"plugins":\[\]\}/);
});

test("extracts enabled plugin-scoped MCP names and rejects unsafe names", () => {
    const names = parsePluginMcpNames(JSON.stringify({
        plugins: [
            { kind: "mcp", name: "teams", scope: "plugin", enabled: true },
            { kind: "mcp", name: "repo-mcp", source: "plugin", enabled: true },
            { kind: "mcp", name: "teams", scope: "plugin", enabled: true },
            { kind: "mcp", name: "disabled", scope: "plugin", enabled: false },
            { kind: "mcp", name: "workspace", scope: "repository", enabled: true },
            { kind: "skill", name: "not-an-mcp", scope: "plugin", enabled: true },
            { kind: "mcp", name: "bad;name", scope: "plugin", enabled: true },
        ],
    }));

    assert.deepEqual(names, ["teams", "repo-mcp"]);
    assert.deepEqual(parsePluginMcpNames(`Agency startup\n${JSON.stringify({
        plugins: [{ kind: "mcp", name: "ado", scope: "plugin", enabled: true }],
    })}`), ["ado"]);
    assert.equal(parsePluginMcpNames("not json"), null);
    assert.equal(parsePluginMcpNames("{}"), null);
});

test("builds an Agency repo profile on top of the existing wrapper", () => {
    assert.deepEqual(buildDeskAgentArgv({
        deskName: "cost-desk",
        workshopDir: "C:\\workshop",
        useAgency: true,
        agencyCommand: "C:\\tools\\agency.exe",
        profile: "repo",
        pluginMcpNames: ["teams", "ado"],
    }).slice(0, -2), [
        "C:\\tools\\agency.exe", "copilot", "--no-default-mcps",
        "--disable-mcp-server", "teams",
        "--disable-mcp-server", "ado",
        "--add-dir", "C:\\workshop",
    ]);
});

test("builds a plain Copilot repo profile without Agency-only flags", () => {
    assert.deepEqual(buildDeskAgentArgv({
        deskName: "cost-desk",
        workshopDir: "/workshop",
        useAgency: false,
        copilotCommand: "/usr/local/bin/copilot",
        profile: "repo",
        pluginMcpNames: ["calendar"],
    }).slice(0, -2), [
        "/usr/local/bin/copilot", "--name", "cost-desk",
        "--disable-mcp-server", "calendar",
        "--add-dir", "/workshop",
    ]);
});

test("connected preserves tools while Agency discovery failure still removes defaults", () => {
    assert.deepEqual(buildDeskAgentArgv({
        deskName: "cost-desk",
        workshopDir: "/workshop",
        useAgency: true,
        profile: "connected",
        pluginMcpNames: ["teams"],
    }).slice(0, -2), [
        "agency", "copilot", "--add-dir", "/workshop",
    ]);

    assert.deepEqual(buildDeskAgentArgv({
        deskName: "cost-desk",
        workshopDir: "/workshop",
        useAgency: true,
        profile: "repo",
        pluginMcpNames: [],
        discoverySucceeded: false,
    }).slice(0, -2), [
        "agency", "copilot", "--no-default-mcps", "--add-dir", "/workshop",
    ]);
});

test("launches use independent session UUIDs and explicit preferences across both routes and profiles", () => {
    const ids = new Set();
    for (const useAgency of [false, true]) {
        for (const profile of ["repo", "connected"]) {
            const argv = buildDeskAgentArgv({
                deskName: "probe",
                workshopDir: "C:\\workshop",
                useAgency,
                profile,
                pluginMcpNames: ["teams"],
                launchPreferences: {
                    model: "example-model",
                    reasoningEffort: "max",
                    context: "long_context",
                },
            });
            const sessionIndex = argv.indexOf("--session-id");
            assert.notEqual(sessionIndex, -1);
            const id = argv[sessionIndex + 1];
            assert.match(id, /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i);
            assert.equal(ids.has(id), false);
            ids.add(id);
            assert.equal(argv[argv.indexOf("--model") + 1], "example-model");
            assert.equal(argv[argv.indexOf("--reasoning-effort") + 1], "max");
            assert.equal(argv[argv.indexOf("--context") + 1], "long_context");
            assert.equal(argv.includes("--no-default-mcps"), useAgency && profile === "repo");
            assert.equal(argv.includes("--disable-mcp-server"), profile === "repo");
            assert.equal(argv.includes("--resume"), false);
            assert.equal(argv.includes("--continue"), false);
        }
    }
});

test("explicit launch preferences reject invalid values instead of silently using defaults", () => {
    for (const [key, value] of [
        ["model", 'model" & echo unexpected'],
        ["reasoningEffort", "unlimited"],
        ["context", "1M"],
        ["model", null],
        ["context", ""],
        ["reasoningEffort", 5],
    ]) {
        assert.throws(() => buildDeskAgentArgv({
            deskName: "probe",
            workshopDir: "C:\\workshop",
            useAgency: false,
            launchPreferences: { [key]: value },
        }), /Invalid desk launch/);
    }
});

test("per-launch preferences override the panel selection without resetting other fields", () => {
    assert.deepEqual(resolveDeskLaunchPreferences({
        reasoningEffort: "low",
    }, {
        model: "example-model",
        reasoningEffort: "high",
        context: "long_context",
    }), {
        model: "example-model",
        reasoningEffort: "low",
        context: "long_context",
    });
    assert.deepEqual(resolveDeskLaunchPreferences(), {});
});
