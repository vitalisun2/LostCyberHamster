const assert = require("assert");
const fs = require("fs");
const os = require("os");
const path = require("path");

const {
  cleanupOldDeviceLogs,
  isPathInsideDirectory,
  removeEntryInsideRoot,
  runRetentionAfterSuccessfulUpload
} = require("./server");

const retentionAgeMs = 72 * 60 * 60 * 1000;
const nowMs = Date.parse("2026-07-04T12:00:00.000Z");
const oldMs = nowMs - retentionAgeMs - 1000;
const freshMs = nowMs - 60 * 60 * 1000;
const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), "lch-retention-"));

try {
  const logRoot = path.join(tempRoot, "android");
  const outsideRoot = path.join(tempRoot, "outside");
  fs.mkdirSync(logRoot, { recursive: true });
  fs.mkdirSync(outsideRoot, { recursive: true });

  const oldUpload = makeUpload(logRoot, "old-upload", oldMs);
  const freshUpload = makeUpload(logRoot, "fresh-upload", freshMs);
  const activeUpload = makeUpload(logRoot, "active-upload", oldMs);
  const oldFile = writeFileWithMtime(path.join(logRoot, "old-device-log.txt"), "old", oldMs);
  const protectedFile = writeFileWithMtime(path.join(logRoot, "_requests.log"), "access", oldMs);
  const outsideFile = writeFileWithMtime(path.join(outsideRoot, "outside-device-log.txt"), "outside", oldMs);

  const result = cleanupOldDeviceLogs({
    outputRoot: logRoot,
    nowMs,
    maxAgeMs: retentionAgeMs,
    activePath: activeUpload
  });

  assert.strictEqual(result.ok, true);
  assert.strictEqual(result.deleted, 2);
  assert.strictEqual(result.errors.length, 0);
  assert.strictEqual(fs.existsSync(oldUpload), false);
  assert.strictEqual(fs.existsSync(oldFile), false);
  assert.strictEqual(fs.existsSync(freshUpload), true);
  assert.strictEqual(fs.existsSync(activeUpload), true);
  assert.strictEqual(fs.existsSync(protectedFile), true);
  assert.strictEqual(fs.existsSync(outsideFile), true);
  assert.strictEqual(isPathInsideDirectory(path.join(logRoot, "child"), logRoot), true);
  assert.strictEqual(isPathInsideDirectory(outsideFile, logRoot), false);
  assert.throws(() => removeEntryInsideRoot(outsideFile, logRoot), /outside root/);
  const throttledRetention = {
    enabled: true,
    lastRunAtMs: Date.now(),
    throttleMs: 60 * 60 * 1000
  };
  assert.strictEqual(runRetentionAfterSuccessfulUpload(throttledRetention, activeUpload), null);

  console.log(
    JSON.stringify({
      ok: true,
      deleted: result.deleted,
      freedBytes: result.freedBytes,
      skippedActive: result.skippedActive,
      skippedFresh: result.skippedFresh,
      skippedProtected: result.skippedProtected
    })
  );
} finally {
  fs.rmSync(tempRoot, { recursive: true, force: true });
}

function makeUpload(root, name, mtimeMs) {
  const directory = path.join(root, name);
  fs.mkdirSync(directory, { recursive: true });
  writeFileWithMtime(path.join(directory, "metadata.json"), "{}", mtimeMs);
  writeFileWithMtime(path.join(directory, "diagnostic_log.txt"), "log", mtimeMs);
  writeFileWithMtime(path.join(directory, "package.json"), "{}", mtimeMs);
  setMtime(directory, mtimeMs);
  return directory;
}

function writeFileWithMtime(filePath, contents, mtimeMs) {
  fs.writeFileSync(filePath, contents, "utf8");
  setMtime(filePath, mtimeMs);
  return filePath;
}

function setMtime(targetPath, mtimeMs) {
  const date = new Date(mtimeMs);
  fs.utimesSync(targetPath, date, date);
}
