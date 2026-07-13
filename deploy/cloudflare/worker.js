const origin = "https://cycles-play-b366b760.azurewebsites.net";

export default {
  async fetch(request) {
    const incomingUrl = new URL(request.url);
    const originUrl = new URL(incomingUrl.pathname + incomingUrl.search, origin);
    const headers = new Headers(request.headers);
    headers.set("x-forwarded-host", incomingUrl.host);
    headers.set("x-forwarded-proto", "https");

    const originRequest = new Request(originUrl, {
      method: request.method,
      headers,
      body: request.method === "GET" || request.method === "HEAD" ? undefined : request.body,
      redirect: "manual"
    });
    const originResponse = await fetch(originRequest);
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
};
