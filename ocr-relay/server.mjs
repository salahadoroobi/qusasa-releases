import { createServer } from 'node:http';
import { pathToFileURL } from 'node:url';

const maxBytes = 1_000_000;
const pngSignature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);

export function makeRelay({ key, dailyLimit = 100, fetchOcr = fetch }) {
    if (!key || !Number.isSafeInteger(dailyLimit) || dailyLimit < 1) {
        throw new Error('Set OCR_SPACE_API_KEY and a positive OCR_DAILY_LIMIT.');
    }

    const recent = new Map();
    let minute = 0;
    let day = 0;
    let usedToday = 0;

    return createServer({ requestTimeout: 15_000, headersTimeout: 10_000 }, async (req, res) => {
        const reply = (status, value) => {
            res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
            res.end(JSON.stringify(value));
        };

        if (req.method !== 'POST' || req.url !== '/v1/scan') return reply(404, { error: 'Not found' });
        if (req.headers['content-type'] !== 'image/png') return reply(415, { error: 'Expected PNG' });
        const length = Number(req.headers['content-length']);
        if (!Number.isInteger(length) || length < 33) return reply(411, { error: 'Image length required' });
        if (length > maxBytes) return reply(413, { error: 'Image too large' });

        const now = Date.now();
        const thisMinute = Math.floor(now / 60_000);
        if (minute !== thisMinute) { minute = thisMinute; recent.clear(); }
        const thisDay = Math.floor(now / 86_400_000);
        if (day !== thisDay) { day = thisDay; usedToday = 0; }
        // The front proxy should also limit the actual client IP. Forwarding headers are untrusted here.
        const ip = req.socket.remoteAddress;
        const count = recent.get(ip) ?? 0;
        if (count >= 10 || usedToday >= dailyLimit) return reply(429, { error: 'Scan limit reached' });
        recent.set(ip, count + 1);
        usedToday++;

        try {
            const chunks = [];
            let received = 0;
            for await (const chunk of req) {
                received += chunk.length;
                if (received > maxBytes) return reply(413, { error: 'Image too large' });
                chunks.push(chunk);
            }
            const image = Buffer.concat(chunks);
            if (!image.subarray(0, 8).equals(pngSignature) || image.toString('ascii', 12, 16) !== 'IHDR') {
                return reply(400, { error: 'Invalid PNG' });
            }
            const width = image.readUInt32BE(16);
            const height = image.readUInt32BE(20);
            if (!width || !height || width * height > 16_000_000) return reply(413, { error: 'Image dimensions too large' });
            const form = new FormData();
            form.set('file', new Blob([image], { type: 'image/png' }), 'clip.png');
            form.set('OCREngine', '2');
            form.set('language', 'auto');
            form.set('scale', 'true');
            const upstream = await fetchOcr('https://api.ocr.space/parse/image', {
                method: 'POST', body: form, headers: { apikey: key }, signal: AbortSignal.timeout(12_000)
            });
            if (!upstream.ok) return reply(503, { error: 'OCR unavailable' });
            const result = await upstream.json();
            if (result.IsErroredOnProcessing || !Array.isArray(result.ParsedResults)) {
                return reply(503, { error: 'OCR unavailable' });
            }
            const text = result.ParsedResults.map(page => typeof page.ParsedText === 'string' ? page.ParsedText : '')
                .join('\n').slice(0, 500_000);
            return reply(200, { text });
        } catch {
            if (!res.destroyed && !res.headersSent) reply(503, { error: 'OCR unavailable' });
        }
    });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    const relay = makeRelay({
        key: process.env.OCR_SPACE_API_KEY,
        dailyLimit: Number(process.env.OCR_DAILY_LIMIT ?? 100)
    });
    relay.listen(Number(process.env.PORT ?? 8080), process.env.BIND_ADDRESS ?? '127.0.0.1');
}
