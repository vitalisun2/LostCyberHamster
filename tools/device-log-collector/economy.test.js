const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const os = require("node:os");
const crypto = require("node:crypto");
const http = require("node:http");
const { createCollectorContext, createHttpServer, cleanupOldDeviceLogs } = require("./server");

test("HTTP archive ACK, replay, malformed packet, and diagnostic retention", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lch-economy-"));
  const context = createCollectorContext({ sharedToken: "test", retention: { enabled: false } },
    { outputRoot: path.join(root, "diagnostics"), economyRoot: path.join(root, "archive") });
  const server = createHttpServer(context);
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const data = JSON.stringify({ schema_version: 1, event_id: "s:1", profile_id: "p", session_id: "s" }) + "\n";
    const id = crypto.createHash("sha256").update(data).digest("hex");
    const url = `http://127.0.0.1:${server.address().port}/upload`;
    const post = body => new Promise((resolve, reject) => {
      const request = http.request(url, { method: "POST", headers: { "Content-Type": "application/json", "X-LCH-Device-Log-Token": "test" } }, response => {
        let result = "";
        response.on("data", chunk => result += chunk);
        response.on("end", () => resolve({ status: response.statusCode, json: () => JSON.parse(result) }));
      });
      request.on("error", reject);
      request.end(JSON.stringify(body));
    });
    for (let i = 0; i < 2; i++) {
      const response = await post({ economyJsonl: data, economyBatchId: id });
      assert.equal(response.status, 200);
      assert.equal((await response.json()).economyBatchId, id);
    }
    assert.equal(fs.readdirSync(context.economyRoot).filter(x => x.endsWith(".jsonl")).length, 1);
    const archive = path.join(context.economyRoot, id + ".jsonl");
    const old = new Date(Date.now() - 100 * 3600000);
    fs.utimesSync(archive, old, old);
    const diagnostic = path.join(context.outputRoot, "old.txt");
    fs.writeFileSync(diagnostic, "old");
    fs.utimesSync(diagnostic, old, old);
    const cleanup = cleanupOldDeviceLogs({ outputRoot: context.outputRoot });
    assert.equal(cleanup.deleted, 1);
    assert.equal(fs.readFileSync(archive, "utf8"), data);
    assert.equal((await post({ economyJsonl: data, economyBatchId: "bad" })).status, 400);
    assert.equal((await post({ economyJsonl: data.slice(0, -1), economyBatchId: id })).status, 400);
  } finally {
    await new Promise(resolve => server.close(resolve));
    // mkdtemp owns this exact root; do not touch an arbitrary configured archive.
    assert.ok(path.basename(root).startsWith("lch-economy-"));
    fs.rmSync(root, { recursive: true, force: true });
  }
});
