const origin = "https://cycles-play-b366b760.azurewebsites.net";
const edgeShellPaths = new Set(["/", "/index.html", "/privacy.html", "/site.css"]);
const edgeMediaPath = /^\/(?:assets|media)\/.*\.(?:avif|gif|jpe?g|mp4|png|svg|webm|webp)$/i;
const legacyPromoPath = "/media/cycles-promo-30s.mp4";
const canonicalPromoPath = "/media/cycles-promo.mp4";

function isLegacyPromoRequest(request) {
  if (request.method !== "GET" && request.method !== "HEAD") {
    return false;
  }

  return new URL(request.url).pathname === legacyPromoPath;
}

export function isEdgeAssetRequest(request) {
  if (request.method !== "GET" && request.method !== "HEAD") {
    return false;
  }

  const { pathname } = new URL(request.url);
  return edgeShellPaths.has(pathname) || edgeMediaPath.test(pathname);
}

export async function handleRequest(request, env, fetchOrigin = fetch) {
  if (isLegacyPromoRequest(request)) {
    return new Response(null, {
      status: 308,
      headers: {
        location: canonicalPromoPath,
        "cache-control": "public, max-age=86400"
      }
    });
  }

  if (isEdgeAssetRequest(request)) {
    return env.ASSETS.fetch(request);
  }

  const incomingUrl = new URL(request.url);
  const originUrl = new URL(incomingUrl.pathname + incomingUrl.search, origin);
  const headers = new Headers(request.headers);
  headers.delete("x-cycles-proxy-secret");
  if (env.ORIGIN_AUTH_TOKEN) {
    headers.set("x-cycles-proxy-secret", env.ORIGIN_AUTH_TOKEN);
  }
  headers.set("x-forwarded-host", incomingUrl.host);
  headers.set("x-forwarded-proto", "https");
  headers.set("x-cycles-canonical-host", incomingUrl.host);
  headers.set("x-cycles-canonical-proto", "https");

  const originRequest = new Request(originUrl, {
    method: request.method,
    headers,
    body: request.method === "GET" || request.method === "HEAD" ? undefined : request.body,
    redirect: "manual"
  });
  const originResponse = await fetchOrigin(originRequest);
  const responseHeaders = new Headers(originResponse.headers);
  const location = responseHeaders.get("location");

  if (location?.startsWith(origin)) {
    responseHeaders.set("location", location.replace(origin, incomingUrl.origin));
  }

  return new Response(originResponse.body, {
    status: originResponse.status,
    statusText: originResponse.statusText,
    headers: responseHeaders
  });
}

export default {
  async fetch(request, env) {
    return handleRequest(request, env);
  }
};
