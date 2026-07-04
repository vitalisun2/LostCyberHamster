const fs = require("fs");
const http = require("http");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..", "..");
const configPath = path.join(__dirname, "device-log-collector.config.json");

const DEFAULT_RETENTION_MAX_AGE_HOURS = 72;
const DEFAULT_RETENTION_THROTTLE_MINUTES = 60;
const RETENTION_LOG_FILE = "_retention.log";
const RETENTION_STATE_FILE = "_retention_state.json";
const INTERNAL_ROOT_FILES = new Set([
  "_requests.log",
  "_probes.log",
  RETENTION_LOG_FILE,
  RETENTION_STATE_FILE
]);

if (require.main === module) {
  main();
}

function main() {
  const config = JSON.parse(fs.readFileSync(configPath, "utf8"));
  const args = parseArgs(process.argv.slice(2));
  const context = createCollectorContext(config, args);
  const server = createHttpServer(context);

  server.listen(context.port, context.host, () => {
    console.log(`[collector] listening on http://${context.host}:${context.port}`);
    console.log(`[collector] outputRoot=${context.outputRoot}`);
    console.log(
      `[collector] retention enabled=${context.retention.enabled} maxAgeHours=${context.retention.maxAgeHours} throttleMinutes=${context.retention.throttleMinutes}`
    );
  });
}

function createCollectorContext(config, args) {
  const host = args.host || config.host || "0.0.0.0";
  const port = Number(args.port || config.port || 8765);
  const configuredOutputRoot = path.resolve(repoRoot, args.outputRoot || config.outputRoot || "DeviceLogs/android");
  const maxBodyBytes = Number(config.maxBodyBytes || 10 * 1024 * 1024);
  const retention = createRetentionController(configuredOutputRoot, config.retention || {});
  const outputRoot = retention.outputRoot;

  return {
    config,
    host,
    port,
    outputRoot,
    maxBodyBytes,
    accessLogPath: path.join(outputRoot, "_requests.log"),
    probeLogPath: path.join(outputRoot, "_probes.log"),
    retention
  };
}

function createHttpServer(context) {
  return http.createServer((request, response) => {
    if (request.method === "GET" && request.url === "/health") {
      logRequest(request, 200, { result: "health" }, context);
      sendJson(response, 200, { ok: true, outputRoot: context.outputRoot });
      return;
    }

    if (request.method !== "POST" || request.url !== "/upload") {
      if (request.method === "POST" && request.url === "/probe") {
        handleProbe(request, response, context);
        return;
      }

      logRequest(request, 404, { result: "not_found" }, context);
      sendJson(response, 404, { ok: false, error: "not_found" });
      return;
    }

    if (context.config.sharedToken && request.headers["x-lch-device-log-token"] !== context.config.sharedToken) {
      logRequest(request, 403, { result: "forbidden" }, context);
      sendJson(response, 403, { ok: false, error: "forbidden" });
      return;
    }

    readBody(request, context.maxBodyBytes)
      .then((body) => {
        const result = savePayload(body, context);
        logRequest(request, 200, { result: "saved", id: result.id }, context);
        runRetentionAfterSuccessfulUpload(context.retention, result.savedPath);
        return result;
      })
      .then((result) => sendJson(response, 200, result))
      .catch((error) => {
        console.error(`[collector] upload failed: ${error.stack || error.message}`);
        logRequest(request, 400, { result: "upload_failed", error: error.message }, context);
        sendJson(response, 400, { ok: false, error: error.message });
      });
  });
}

function parseArgs(values) {
  const result = {};
  for (let index = 0; index < values.length - 1; index += 1) {
    if (values[index].startsWith("--")) {
      result[values[index].slice(2)] = values[index + 1];
      index += 1;
    }
  }

  return result;
}

function readBody(request, maxBytes) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;

    request.on("data", (chunk) => {
      size += chunk.length;
      if (size > maxBytes) {
        reject(new Error("payload_too_large"));
        request.destroy();
        return;
      }

      chunks.push(chunk);
    });

    request.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    request.on("error", reject);
  });
}

function savePayload(body, context) {
  const payload = JSON.parse(body);
  const metadata = payload.metadata || {};
  const createdAt = sanitizeTimestamp(metadata.createdAtUtc || new Date().toISOString());
  const device = sanitizeName(metadata.deviceModel || "unknown_device");
  const reason = sanitizeName(metadata.reason || "manual");
  const session = sanitizeName((metadata.sessionId || "session").slice(0, 12));
  const directory = path.join(context.outputRoot, `${createdAt}_${device}_${reason}_${session}`);

  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(path.join(directory, "metadata.json"), JSON.stringify(metadata, null, 2), "utf8");

  const logFileName = sanitizeName(payload.diagnosticLogFileName || "diagnostic_log.txt");
  const logBytes = payload.diagnosticLogBase64
    ? Buffer.from(payload.diagnosticLogBase64, "base64")
    : Buffer.alloc(0);
  fs.writeFileSync(path.join(directory, logFileName), logBytes);

  const packageSummary = {
    receivedAtUtc: new Date().toISOString(),
    diagnosticLogFileName: logFileName,
    diagnosticLogBytes: logBytes.length,
    diagnosticLogTruncated: Boolean(payload.diagnosticLogTruncated)
  };
  fs.writeFileSync(path.join(directory, "package.json"), JSON.stringify(packageSummary, null, 2), "utf8");

  console.log(`[collector] saved ${directory}`);
  return { ok: true, id: path.basename(directory), savedPath: directory };
}

function handleProbe(request, response, context) {
  if (context.config.sharedToken && request.headers["x-lch-device-log-token"] !== context.config.sharedToken) {
    logRequest(request, 403, { result: "probe_forbidden" }, context);
    sendJson(response, 403, { ok: false, error: "forbidden" });
    return;
  }

  readBody(request, context.maxBodyBytes)
    .then((body) => {
      const entry = {
        receivedAtUtc: new Date().toISOString(),
        remoteAddress: request.socket && request.socket.remoteAddress,
        userAgent: request.headers["user-agent"] || "",
        contentType: request.headers["content-type"] || "",
        body
      };
      fs.appendFileSync(context.probeLogPath, `${JSON.stringify(entry)}\n`, "utf8");
      logRequest(request, 200, { result: "probe_saved" }, context);
      sendJson(response, 200, { ok: true });
    })
    .catch((error) => {
      logRequest(request, 400, { result: "probe_failed", error: error.message }, context);
      sendJson(response, 400, { ok: false, error: error.message });
    });
}

function logRequest(request, statusCode, details, context) {
  const entry = {
    receivedAtUtc: new Date().toISOString(),
    remoteAddress: request.socket && request.socket.remoteAddress,
    remotePort: request.socket && request.socket.remotePort,
    method: request.method,
    url: request.url,
    statusCode,
    userAgent: request.headers["user-agent"] || "",
    contentLength: request.headers["content-length"] || "",
    details
  };

  fs.appendFileSync(context.accessLogPath, `${JSON.stringify(entry)}\n`, "utf8");
  console.log(`[collector] ${entry.remoteAddress} ${entry.method} ${entry.url} ${statusCode}`);
}

function createRetentionController(outputRoot, options = {}) {
  fs.mkdirSync(outputRoot, { recursive: true });
  const canonicalOutputRoot = fs.realpathSync.native(outputRoot);
  const maxAgeHours = readPositiveNumber(options.maxAgeHours, DEFAULT_RETENTION_MAX_AGE_HOURS);
  const throttleMinutes = readPositiveNumber(options.throttleMinutes, DEFAULT_RETENTION_THROTTLE_MINUTES);
  const statePath = path.join(canonicalOutputRoot, RETENTION_STATE_FILE);
  const lastRunAtMs = readRetentionLastRunAtMs(statePath);

  return {
    enabled: options.enabled !== false,
    outputRoot: canonicalOutputRoot,
    maxAgeHours,
    maxAgeMs: maxAgeHours * 60 * 60 * 1000,
    throttleMinutes,
    throttleMs: throttleMinutes * 60 * 1000,
    lastRunAtMs,
    statePath,
    logPath: path.join(canonicalOutputRoot, RETENTION_LOG_FILE)
  };
}

function runRetentionAfterSuccessfulUpload(retention, activePath) {
  if (!retention.enabled) {
    return null;
  }

  const nowMs = Date.now();
  if (nowMs - retention.lastRunAtMs < retention.throttleMs) {
    return null;
  }

  retention.lastRunAtMs = nowMs;
  let result;
  try {
    result = cleanupOldDeviceLogs({
      outputRoot: retention.outputRoot,
      nowMs,
      maxAgeMs: retention.maxAgeMs,
      activePath,
      protectedNames: INTERNAL_ROOT_FILES
    });
  } catch (error) {
    result = {
      ok: false,
      root: retention.outputRoot,
      cutoffUtc: new Date(nowMs - retention.maxAgeMs).toISOString(),
      scanned: 0,
      deleted: 0,
      freedBytes: 0,
      skippedActive: 0,
      skippedFresh: 0,
      skippedProtected: 0,
      skippedOutside: 0,
      errors: [{ path: retention.outputRoot, error: error.message }]
    };
  }

  writeRetentionState(retention, nowMs, result);
  logRetentionResult(retention, result);
  return result;
}

function cleanupOldDeviceLogs(options = {}) {
  const outputRoot = options.outputRoot;
  const nowMs = Number(options.nowMs === undefined ? Date.now() : options.nowMs);
  const maxAgeMs = Number(
    options.maxAgeMs === undefined
      ? DEFAULT_RETENTION_MAX_AGE_HOURS * 60 * 60 * 1000
      : options.maxAgeMs
  );
  const cutoffMs = nowMs - maxAgeMs;
  const protectedNames = new Set(options.protectedNames || INTERNAL_ROOT_FILES);

  if (!outputRoot) {
    throw new Error("outputRoot is required");
  }

  fs.mkdirSync(outputRoot, { recursive: true });
  const canonicalRoot = fs.realpathSync.native(outputRoot);
  const activePath = options.activePath && fs.existsSync(options.activePath)
    ? fs.realpathSync.native(options.activePath)
    : "";

  const result = {
    ok: true,
    root: canonicalRoot,
    cutoffUtc: new Date(cutoffMs).toISOString(),
    scanned: 0,
    deleted: 0,
    freedBytes: 0,
    skippedActive: 0,
    skippedFresh: 0,
    skippedProtected: 0,
    skippedOutside: 0,
    errors: []
  };

  const entries = fs.readdirSync(canonicalRoot, { withFileTypes: true });
  for (const entry of entries) {
    const entryPath = path.join(canonicalRoot, entry.name);
    if (!isPathInsideDirectory(entryPath, canonicalRoot)) {
      result.skippedOutside += 1;
      continue;
    }

    result.scanned += 1;

    if (protectedNames.has(entry.name)) {
      result.skippedProtected += 1;
      continue;
    }

    try {
      const stats = fs.lstatSync(entryPath);
      if (stats.isSymbolicLink()) {
        result.skippedOutside += 1;
        continue;
      }

      const canonicalEntryPath = fs.realpathSync.native(entryPath);
      if (!isPathInsideDirectory(canonicalEntryPath, canonicalRoot)) {
        result.skippedOutside += 1;
        continue;
      }

      if (
        activePath &&
        (isSamePath(canonicalEntryPath, activePath) || isPathInsideDirectory(activePath, canonicalEntryPath))
      ) {
        result.skippedActive += 1;
        continue;
      }

      if (stats.mtimeMs >= cutoffMs) {
        result.skippedFresh += 1;
        continue;
      }

      const freedBytes = getEntrySizeBytes(canonicalEntryPath, canonicalRoot);
      removeEntryInsideRoot(canonicalEntryPath, canonicalRoot);
      result.deleted += 1;
      result.freedBytes += freedBytes;
    } catch (error) {
      result.errors.push({ path: entryPath, error: error.message });
    }
  }

  result.ok = result.errors.length === 0;
  return result;
}

function getEntrySizeBytes(entryPath, rootPath) {
  if (!isPathInsideDirectory(entryPath, rootPath)) {
    throw new Error(`refusing to measure path outside root: ${entryPath}`);
  }

  const stats = fs.lstatSync(entryPath);
  if (!stats.isDirectory() || stats.isSymbolicLink()) {
    return stats.size;
  }

  let total = 0;
  for (const child of fs.readdirSync(entryPath)) {
    const childPath = path.join(entryPath, child);
    if (!isPathInsideDirectory(childPath, rootPath)) {
      throw new Error(`refusing to measure child outside root: ${childPath}`);
    }

    total += getEntrySizeBytes(childPath, rootPath);
  }

  return total;
}

function removeEntryInsideRoot(entryPath, rootPath) {
  if (!isPathInsideDirectory(entryPath, rootPath)) {
    throw new Error(`refusing to delete path outside root: ${entryPath}`);
  }

  fs.rmSync(entryPath, { recursive: true, force: false });
}

function readRetentionLastRunAtMs(statePath) {
  try {
    if (!fs.existsSync(statePath)) {
      return 0;
    }

    const state = JSON.parse(fs.readFileSync(statePath, "utf8"));
    const value = Date.parse(state.lastRunAtUtc || "");
    return Number.isFinite(value) ? value : 0;
  } catch (error) {
    console.warn(`[collector] retention state ignored: ${error.message}`);
    return 0;
  }
}

function writeRetentionState(retention, nowMs, result) {
  const state = {
    lastRunAtUtc: new Date(nowMs).toISOString(),
    maxAgeHours: retention.maxAgeHours,
    throttleMinutes: retention.throttleMinutes,
    lastResult: {
      ok: result.ok,
      deleted: result.deleted,
      freedBytes: result.freedBytes,
      errors: result.errors.length
    }
  };

  try {
    fs.writeFileSync(retention.statePath, JSON.stringify(state, null, 2), "utf8");
  } catch (error) {
    console.error(`[collector] retention state write failed: ${error.message}`);
  }
}

function logRetentionResult(retention, result) {
  const entry = {
    loggedAtUtc: new Date().toISOString(),
    ...result
  };
  const summary =
    `[collector] retention cleanup ok=${result.ok} scanned=${result.scanned} deleted=${result.deleted} ` +
    `freedBytes=${result.freedBytes} skippedFresh=${result.skippedFresh} skippedActive=${result.skippedActive} ` +
    `skippedProtected=${result.skippedProtected} skippedOutside=${result.skippedOutside} errors=${result.errors.length}`;

  try {
    fs.appendFileSync(retention.logPath, `${JSON.stringify(entry)}\n`, "utf8");
  } catch (error) {
    console.error(`[collector] retention log write failed: ${error.message}`);
  }

  if (result.ok) {
    console.log(summary);
  } else {
    console.error(summary);
  }
}

function readPositiveNumber(value, fallback) {
  const number = Number(value);
  return Number.isFinite(number) && number > 0 ? number : fallback;
}

function isPathInsideDirectory(candidatePath, rootPath) {
  const candidate = normalizePathForComparison(candidatePath);
  const root = normalizePathForComparison(rootPath);
  const relative = path.relative(root, candidate);
  return relative !== "" && !relative.startsWith("..") && !path.isAbsolute(relative);
}

function isSamePath(left, right) {
  return normalizePathForComparison(left) === normalizePathForComparison(right);
}

function normalizePathForComparison(value) {
  const resolved = path.resolve(value);
  return process.platform === "win32" ? resolved.toLowerCase() : resolved;
}

function sendJson(response, statusCode, data) {
  response.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(data));
}

function sanitizeTimestamp(value) {
  return value.replace(/[:.]/g, "-").replace(/[^\w-]/g, "_").slice(0, 32);
}

function sanitizeName(value) {
  return String(value).replace(/[^\w.-]+/g, "_").replace(/^_+|_+$/g, "").slice(0, 80) || "unknown";
}

module.exports = {
  cleanupOldDeviceLogs,
  createRetentionController,
  isPathInsideDirectory,
  removeEntryInsideRoot,
  runRetentionAfterSuccessfulUpload
};
