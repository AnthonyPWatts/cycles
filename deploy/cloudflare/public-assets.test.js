import assert from "node:assert/strict";
import { promises as fs } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import {
  buildPublicAssets,
  collectPublicAssets,
  publicDirectoryRules,
  publicFiles,
  sourceRoot
} from "./build-public-assets.js";

const cloudflareDirectory = path.dirname(fileURLToPath(import.meta.url));

test("the source allowlist contains the public shell and representative artwork", async () => {
  const assets = await collectPublicAssets();

  for (const expected of [
    "index.html",
    "privacy.html",
    "site.css",
    "media/cycles-promo.mp4",
    "media/cycles-promo-poster.jpg",
    "assets/admirals/portraits/archive-violet-human-01.webp",
    "assets/galaxy/galaxy-overview.webp",
    "assets/icons/refresh.svg",
    "media/navigation-backgrounds/command.webp",
    "media/navigation-backgrounds/letterbox/history.webp",
    "media/promo/concept-cycle-legacy.png",
    "media/resource-backgrounds/industry.webp"
  ]) {
    assert.ok(assets.includes(expected), expected);
  }
});

test("the allowlist excludes executable dashboard and source-only files", async () => {
  const assets = await collectPublicAssets();

  for (const forbidden of [
    "app.html",
    "app.js",
    "styles.css",
    "assets/admirals/catalogue.json",
    "assets/admirals/README.md",
    "media/MUSIC-CREDITS.md",
    "media/PROMO-PRODUCTION.md"
  ]) {
    assert.equal(assets.includes(forbidden), false, forbidden);
  }

  assert.equal(publicFiles.some(file => file.startsWith("app.")), false);
  assert.equal(publicDirectoryRules.some(rule => rule.directory === "assets"), false);
  assert.equal(publicDirectoryRules.some(rule => rule.directory === "media"), false);
});

test("the generated bundle contains only allowlisted files", async t => {
  const temporaryRoot = await fs.mkdtemp(path.join(os.tmpdir(), "cycles-public-assets-"));
  t.after(() => fs.rm(temporaryRoot, { recursive: true, force: true }));

  const output = path.join(temporaryRoot, "bundle");
  const result = await buildPublicAssets({ source: sourceRoot, output });
  const generated = [];

  async function visit(currentDirectory) {
    for (const entry of await fs.readdir(currentDirectory, { withFileTypes: true })) {
      const absolutePath = path.join(currentDirectory, entry.name);
      if (entry.isDirectory()) {
        await visit(absolutePath);
      } else {
        generated.push(path.relative(output, absolutePath).split(path.sep).join("/"));
      }
    }
  }

  await visit(output);
  generated.sort((left, right) => left.localeCompare(right, "en"));

  assert.deepEqual(generated, result.assets);
  assert.ok(result.totalBytes > 0);
  await assert.rejects(fs.access(path.join(output, "app.html")));
  await assert.rejects(fs.access(path.join(output, "assets/admirals/catalogue.json")));
});

test("Wrangler builds the isolated bundle and uses asset-first routing", async () => {
  const configuration = await fs.readFile(
    path.join(cloudflareDirectory, "wrangler.toml"),
    "utf8");

  assert.match(configuration, /\[build\][\s\S]*command = "node build-public-assets\.js"/);
  assert.match(configuration, /directory = "\.\/\.public-assets"/);
  assert.match(configuration, /run_worker_first = false/);
  assert.doesNotMatch(configuration, /directory = "\.\.\/\.\.\/src\/Cycles\.Api\/wwwroot"/);
});
