import assert from "node:assert/strict";
import test from "node:test";

import { handleRequest, isEdgeAssetRequest } from "./worker.js";

test("public shell and media GET requests are edge assets", () => {
  for (const path of [
    "/",
    "/index.html",
    "/site.css?v=20260716-1",
    "/media/cycles-promo.mp4",
    "/media/navigation-backgrounds/command.png",
    "/assets/galaxy/galaxy-overview.png"
  ]) {
    assert.equal(isEdgeAssetRequest(new Request(`https://cycles.example${path}`)), true, path);
  }
});

test("application code, documentation, APIs and mutating media requests stay on the origin path", () => {
  for (const [method, path] of [
    ["GET", "/app.html"],
    ["GET", "/app.js"],
    ["GET", "/styles.css"],
    ["GET", "/media/PROMO-PRODUCTION.md"],
    ["GET", "/auth/login"],
    ["GET", "/galaxy"],
    ["POST", "/media/cycles-promo.mp4"]
  ]) {
    assert.equal(
      isEdgeAssetRequest(new Request(`https://cycles.example${path}`, { method })),
      false,
      `${method} ${path}`
    );
  }
});

test("edge assets are served by the static binding without contacting Azure", async () => {
  let assetRequest;
  const env = {
    ASSETS: {
      async fetch(request) {
        assetRequest = request;
        return new Response("edge", { headers: { "x-cycles-source": "edge" } });
      }
    }
  };

  const response = await handleRequest(
    new Request("https://cycles.example/media/cycles-promo.mp4"),
    env,
    () => assert.fail("Azure origin should not be called for an edge asset.")
  );

  assert.equal(new URL(assetRequest.url).hostname, "cycles.example");
  assert.equal(response.headers.get("x-cycles-source"), "edge");
});

test("duration-based promo GET and HEAD requests redirect permanently to the canonical asset", async () => {
  for (const method of ["GET", "HEAD"]) {
    const response = await handleRequest(
      new Request("https://cycles.example/media/cycles-promo-30s.mp4?v=20260717-1", { method }),
      { ASSETS: { fetch: () => assert.fail("Legacy promo requests must not use the asset binding.") } },
      () => assert.fail("Legacy promo requests must not contact Azure.")
    );

    assert.equal(response.status, 308, method);
    assert.equal(response.headers.get("location"), "/media/cycles-promo.mp4", method);
    assert.equal(response.headers.get("cache-control"), "public, max-age=86400", method);
  }
});

test("protected requests preserve the Azure proxy and custom-domain redirect rewrite", async () => {
  let originRequest;
  const response = await handleRequest(
    new Request("https://cycles.example/app.html", { headers: { cookie: "session=value" } }),
    { ASSETS: { fetch: () => assert.fail("Protected requests must not use the asset binding.") } },
    async request => {
      originRequest = request;
      return new Response(null, {
        status: 303,
        headers: { location: "https://cycles-play-b366b760.azurewebsites.net/playground-access" }
      });
    }
  );

  assert.equal(new URL(originRequest.url).hostname, "cycles-play-b366b760.azurewebsites.net");
  assert.equal(originRequest.headers.get("x-forwarded-host"), "cycles.example");
  assert.equal(originRequest.headers.get("x-forwarded-proto"), "https");
  assert.equal(originRequest.headers.get("cookie"), "session=value");
  assert.equal(response.headers.get("location"), "https://cycles.example/playground-access");
});
