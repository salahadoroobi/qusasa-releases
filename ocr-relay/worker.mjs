const limit = 1_000_000;
const signature = [137, 80, 78, 71, 13, 10, 26, 10];
let minute = -1;
let day = -1;
let attempts = 0;
const clients = new Map();

function response(status, value) {
  return Response.json(value, { status, headers: { 'Cache-Control': 'no-store' } });
}

export default {
  async fetch(request, env) {
    const path = new URL(request.url).pathname;
    if (request.method === 'GET' && path === '/health') return response(200, { ready: Boolean(env.OCR_SPACE_API_KEY) });
    if (request.method !== 'POST' || path !== '/v1/scan') return response(404, { error: 'Not found' });
    if (request.headers.get('content-type') !== 'image/png') return response(415, { error: 'Expected PNG' });
    const length = Number(request.headers.get('content-length'));
    if (!Number.isInteger(length) || length < 33 || !request.body) return response(411, { error: 'Image length required' });
    if (length > limit) return response(413, { error: 'Image too large' });
    if (!env.OCR_SPACE_API_KEY) return response(503, { error: 'OCR unavailable' });

    const now = Date.now();
    const currentMinute = Math.floor(now / 60_000);
    const currentDay = Math.floor(now / 86_400_000);
    if (minute !== currentMinute) { minute = currentMinute; clients.clear(); }
    if (day !== currentDay) { day = currentDay; attempts = 0; }
    const ip = request.headers.get('CF-Connecting-IP') ?? 'unknown';
    const clientCount = clients.get(ip) ?? 0;
    if (attempts >= 80 || clientCount >= 5) return response(429, { error: 'Scan limit reached' });
    attempts++;
    clients.set(ip, clientCount + 1);

    try {
      const bytes = new Uint8Array(length);
      const reader = request.body.getReader();
      let offset = 0;
      for (;;) {
        const { value, done } = await reader.read();
        if (done) break;
        if (offset + value.length > length) {
          await reader.cancel();
          return response(413, { error: 'Image too large' });
        }
        bytes.set(value, offset);
        offset += value.length;
      }
      if (offset !== length || !signature.every((byte, index) => bytes[index] === byte) ||
          String.fromCharCode(...bytes.subarray(12, 16)) !== 'IHDR') return response(400, { error: 'Invalid PNG' });
      const view = new DataView(bytes.buffer);
      const width = view.getUint32(16);
      const height = view.getUint32(20);
      if (!width || !height || width * height > 16_000_000) return response(413, { error: 'Image dimensions too large' });
      const form = new FormData();
      form.set('file', new Blob([bytes], { type: 'image/png' }), 'clip.png');
      form.set('OCREngine', '2');
      form.set('language', 'auto');
      form.set('scale', 'true');
      const upstream = await fetch('https://api.ocr.space/parse/image', {
        method: 'POST', body: form, headers: { apikey: env.OCR_SPACE_API_KEY }, signal: AbortSignal.timeout(12_000)
      });
      if (!upstream.ok) return response(503, { error: 'OCR unavailable' });
      const result = await upstream.json();
      if (result.IsErroredOnProcessing || !Array.isArray(result.ParsedResults)) return response(503, { error: 'OCR unavailable' });
      const text = result.ParsedResults.map(page => typeof page.ParsedText === 'string' ? page.ParsedText : '')
        .join('\n').slice(0, 500_000);
      return response(200, { text });
    } catch {
      return response(503, { error: 'OCR unavailable' });
    }
  }
};
