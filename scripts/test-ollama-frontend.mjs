import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const html = fs.readFileSync(new URL('../frontend-main/index.html', import.meta.url), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
// Compile the entire dashboard, then evaluate with a minimal DOM and no network.
new vm.Script(script);
const elements = new Map();
function element(id) {
    if (!elements.has(id)) elements.set(id, { value: id === 'ollamaStatus' ? 'all' : '', textContent: '', innerHTML: '' });
    return elements.get(id);
}
const context = vm.createContext({
    console, Intl, Date, Set, Map, URL, URLSearchParams,
    window: { addEventListener() {} },
    document: { getElementById: element, addEventListener() {}, querySelectorAll: () => [] },
    fetch: () => Promise.reject(new Error('Network disabled in test')),
    setTimeout() {}, setInterval() {},
});
// Suppress only the initial dashboard load; exercise the production tab functions.
vm.runInContext(script.replace(/\bloadDashboard\(\);/g, ''), context);
const connections = Array.from({ length: 30 }, (_, i) => ({
    Id: String(i), Timestamp: '2026-11-12T00:00:00Z', Peer: `203.0.113.1:${1000 + i}`,
    Method: 'POST', Path: '/api/chat', Status: i === 0 ? 429 : 200,
    Body: '<img src=x onerror=alert(1)>', Response: 'Hello!',
}));
context.fixture = { RetentionLimit: 1000, Connections: connections };
vm.runInContext('setupOllama(fixture)', context);
assert.match(element('ollamaCount').textContent, /30 matching.*1 \/ 2/);
assert.equal((element('ollamaConnections').innerHTML.match(/<article /g) || []).length, 25);
assert.ok(!element('ollamaConnections').innerHTML.includes('<img'));
assert.match(element('ollamaConnections').innerHTML, /&lt;img/);
assert.match(element('ollamaConnections').innerHTML, /ATTACKER INPUT/);
assert.match(element('ollamaConnections').innerHTML, /HONEYPOT RESPONSE/);
assert.match(element('ollamaOverviewStats').innerHTML, /API Connections/);
assert.match(element('ollamaOverviewStats').innerHTML, /Completion Replies/);
assert.match(element('ollamaOverviewStats').innerHTML, />30</);
assert.match(element('ollamaOverviewFreshness').textContent, /not lifetime totals/);
element('ollamaNext').onclick();
assert.match(element('ollamaCount').textContent, /2 \/ 2/);
element('ollamaStatus').value = 'throttled';
element('ollamaStatus').oninput();
assert.match(element('ollamaCount').textContent, /1 matching/);
element('ollamaQuery').value = 'not-present';
element('ollamaQuery').oninput();
assert.match(element('ollamaConnections').innerHTML, /No API connections match/);
vm.runInContext('setupOllama(null)', context);
assert.match(element('ollamaFreshness').textContent, /not been published/);
assert.match(element('ollamaOverviewStats').innerHTML, /—/);
assert.match(element('ollamaOverviewFreshness').textContent, /not been published/);
context.chat = {
    Id: 'chat', Timestamp: '2026-11-12T00:00:00Z', Peer: '203.0.113.2:4567',
    Method: 'POST', Path: '/api/chat', Status: 200, Response: 'Hello <script>alert(1)</script>',
    Body: JSON.stringify({ model: '<img src=x>', stream: true, messages: [
        { role: 'system', content: 'System instruction' },
        { role: 'user', content: [{ type: 'text', text: 'User question' }, { type: 'image_url', image_url: { url: 'https://example.invalid/tracking.png' } }] },
    ] }),
};
const chatHtml = vm.runInContext('renderOllamaExchange(chat)', context);
assert.match(chatHtml, /System instruction/);
assert.match(chatHtml, /User question/);
assert.match(chatHtml, /\[image_url\]/);
assert.ok(!chatHtml.includes('<script>'));
assert.ok(!chatHtml.includes('<img'));
assert.ok(!chatHtml.includes('src="https://example.invalid'));
assert.match(chatHtml, /&lt;script/);
assert.match(chatHtml, /Requested model/);
context.noBody = { Id: 'get', Method: 'GET', Path: '/api/tags', Status: 200 };
assert.match(vm.runInContext('renderOllamaExchange(noBody)', context), /No request body expected/);
context.noBody = { Id: 'limit', Status: 429 };
assert.match(vm.runInContext('renderOllamaExchange(noBody)', context), /throttled before/);
context.noBody = { Id: 'error', Method: 'POST', Status: 408, Error: 'timeout_or_shutdown' };
assert.match(vm.runInContext('renderOllamaExchange(noBody)', context), /connection failed or ended/);
context.truncated = { Id: 'partial', Method: 'POST', Path: '/api/chat', Status: 400, Body: '{"messages":[', BodyTruncated: true };
assert.match(vm.runInContext('renderOllamaExchange(truncated)', context), /truncated at 4,096/);
assert.match(vm.runInContext('renderOllamaExchange(truncated)', context), /raw request/);
context.usage = { TotalPromptTokens: 6088755, TotalCompletionTokens: 67215 };
vm.runInContext('renderPerformance(usage, {}, [], [])', context);
assert.equal(element('totalTokensUsed').textContent, '6,155,970');
assert.match(element('totalTokensBreakdown').textContent, /6,088,755 prompt \+ 67,215 completion/);
context.usage = {};
vm.runInContext('renderPerformance(usage, {}, [], [])', context);
assert.equal(element('totalTokensUsed').textContent, '—');
context.usage = { TotalPromptTokens: 0, TotalCompletionTokens: 0 };
vm.runInContext('renderPerformance(usage, {}, [], [])', context);
assert.equal(element('totalTokensUsed').textContent, '0');
context.usage = { TotalPromptTokens: -1, TotalCompletionTokens: 10 };
vm.runInContext('renderPerformance(usage, {}, [], [])', context);
assert.equal(element('totalTokensUsed').textContent, '—');
element('ollamaQuery').value = '';
element('ollamaStatus').value = 'all';
context.groupFixture = { RetentionLimit: 1000, Connections: [
    ...Array.from({ length: 60 }, (_, i) => ({ Id: `empty-${i}`, Timestamp: '2026-11-12T00:00:00Z',
        Peer: `203.0.113.3:${i + 1000}`, Method: 'GET', Path: '/api/tags', Status: 200,
        Body: i % 3 === 0 ? null : i % 3 === 1 ? '' : ' \n ' })),
    ...connections.slice(1).map(row => ({ ...row, Body: '{"prompt":"kept separate"}' })),
] };
vm.runInContext('setupOllama(groupFixture)', context);
assert.match(element('ollamaCount').textContent, /89 matching connections.*29 with bodies.*60 bodyless grouped.*1 \/ 2/);
assert.equal((element('ollamaConnections').innerHTML.match(/ollama-bodyless-group/g) || []).length, 1);
assert.equal((element('ollamaConnections').innerHTML.match(/<article /g) || []).length, 25);
assert.ok(!element('ollamaConnections').innerHTML.includes('API CONNECTION // empty-'));
assert.match(element('ollamaConnections').innerHTML, /60 bodyless connections/);
assert.match(element('ollamaOverviewStats').innerHTML, />89</);
element('ollamaQuery').value = '/api/tags';
element('ollamaQuery').oninput();
assert.match(element('ollamaCount').textContent, /60 matching connections.*0 with bodies.*60 bodyless grouped/);
assert.equal((element('ollamaConnections').innerHTML.match(/<article /g) || []).length, 0);
assert.ok(!element('ollamaConnections').innerHTML.includes('No API connections match'));
context.groupFixture.Connections = [{ Id: 'hostile', Method: 'GET', Path: '<img src=x>', Status: 429, Body: null }];
element('ollamaQuery').value = '';
vm.runInContext('setupOllama(groupFixture)', context);
assert.ok(!element('ollamaConnections').innerHTML.includes('<img'));
assert.match(element('ollamaConnections').innerHTML, /&lt;img/);
element('ollamaStatus').value = 'success';
element('ollamaStatus').oninput();
assert.match(element('ollamaConnections').innerHTML, /No API connections match/);
console.log('Frontend: bodyless grouping, body-only pagination, filtered aggregates, total-token metric, overview statistics, paired cards, escaping and optional-data checks passed.');
