const fs = require("fs");
const http = require("http");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..", "..");
const configPath = path.join(__dirname, "device-log-collector.config.json");
const config = JSON.parse(fs.readFileSync(configPath, "utf8"));

const args = parseArgs(process.argv.slice(2));
const host = args.host || config.host || "0.0.0.0";
const port = Number(args.port || config.port || 8765);
const outputRoot = path.resolve(repoRoot, args.outputRoot || config.outputRoot || "DeviceLogs/android");
const maxBodyBytes = Number(config.maxBodyBytes || 10 * 1024 * 1024);
const accessLogPath = path.join(outputRoot, "_requests.log");
const probeLogPath = path.join(outputRoot, "_probes.log");

fs.mkdirSync(outputRoot, { recursive: true });

const server = http.createServer((request, response) => {
  if (request.method === "GET" && request.url === "/health") {
    logRequest(request, 200, { result: "health" });
    sendJson(response, 200, { ok: true, outputRoot });
    return;
  }

  if (request.method !== "POST" || request.url !== "/upload") {
    if (request.method === "POST" && request.url === "/probe") {
      handleProbe(request, response);
      return;
    }

    logRequest(request, 404, { result: "not_found" });
    sendJson(response, 404, { ok: false, error: "not_found" });
    return;
  }

  if (config.sharedToken && request.headers["x-lch-device-log-token"] !== config.sharedToken) {
    logRequest(request, 403, { result: "forbidden" });
    sendJson(response, 403, { ok: false, error: "forbidden" });
    return;
  }

  readBody(request, maxBodyBytes)
    .then((body) => {
      const result = savePayload(body);
      logRequest(request, 200, { result: "saved", id: result.id });
      return result;
    })
    .then((result) => sendJson(response, 200, result))
    .catch((error) => {
      console.error(`[collector] upload failed: ${error.stack || error.message}`);
      logRequest(request, 400, { result: "upload_failed", error: error.message });
      sendJson(response, 400, { ok: false, error: error.message });
    });
});

server.listen(port, host, () => {
  console.log(`[collector] listening on http://${host}:${port}`);
  console.log(`[collector] outputRoot=${outputRoot}`);
});

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

function savePayload(body) {
  const payload = JSON.parse(body);
  const metadata = payload.metadata || {};
  const createdAt = sanitizeTimestamp(metadata.createdAtUtc || new Date().toISOString());
  const device = sanitizeName(metadata.deviceModel || "unknown_device");
  const reason = sanitizeName(metadata.reason || "manual");
  const session = sanitizeName((metadata.sessionId || "session").slice(0, 12));
  const directory = path.join(outputRoot, `${createdAt}_${device}_${reason}_${session}`);

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

function handleProbe(request, response) {
  if (config.sharedToken && request.headers["x-lch-device-log-token"] !== config.sharedToken) {
    logRequest(request, 403, { result: "probe_forbidden" });
    sendJson(response, 403, { ok: false, error: "forbidden" });
    return;
  }

  readBody(request, maxBodyBytes)
    .then((body) => {
      const entry = {
        receivedAtUtc: new Date().toISOString(),
        remoteAddress: request.socket && request.socket.remoteAddress,
        userAgent: request.headers["user-agent"] || "",
        contentType: request.headers["content-type"] || "",
        body
      };
      fs.appendFileSync(probeLogPath, `${JSON.stringify(entry)}\n`, "utf8");
      logRequest(request, 200, { result: "probe_saved" });
      sendJson(response, 200, { ok: true });
    })
    .catch((error) => {
      logRequest(request, 400, { result: "probe_failed", error: error.message });
      sendJson(response, 400, { ok: false, error: error.message });
    });
}

function logRequest(request, statusCode, details) {
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

  fs.appendFileSync(accessLogPath, `${JSON.stringify(entry)}\n`, "utf8");
  console.log(`[collector] ${entry.remoteAddress} ${entry.method} ${entry.url} ${statusCode}`);
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
