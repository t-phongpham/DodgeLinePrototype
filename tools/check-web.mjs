// Headless desktop-browser smoke check of the actual built Unity player.
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { createServer } from 'node:net';
import { createHash } from 'node:crypto';
const option = (name, fallback) => { const i = process.argv.indexOf(name); return i >= 0 ? process.argv[i + 1] : fallback; };
const url = option('--url', 'http://127.0.0.1:8080/');
const output = path.resolve(option('--output', 'TestResults/WebBrowser'));
const browser = option('--browser', 'C:/Program Files/Google/Chrome/Application/chrome.exe');
if (!existsSync(browser)) throw new Error('Chrome executable not found; supply --browser.');
mkdirSync(output, { recursive: true });
const temporaryRoot = path.resolve(tmpdir());
const profile = mkdtempSync(path.join(temporaryRoot, 'dl-web-check-'));
const probe = createServer(); await new Promise(resolve => probe.listen(0, '127.0.0.1', resolve));
const port = probe.address().port; await new Promise(resolve => probe.close(resolve));
const chrome = spawn(browser, ['--headless=new', '--no-first-run', '--no-default-browser-check', '--disable-background-networking',
  '--use-angle=swiftshader', '--enable-unsafe-swiftshader', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, '--window-size=1366,900', 'about:blank'],
  { windowsHide: true, stdio: ['ignore', 'ignore', 'ignore'] });
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const report = { loaded: false, steps: [], exceptions: [], requests: [], overlayBlocksStageClick: false };
let socket, call, sequence = 0;
try {
  let tab;
  for (let attempt = 0; attempt < 100; attempt++) {
    try { tab = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' })).json(); if (tab.webSocketDebuggerUrl) break; } catch { }
    await sleep(100);
  }
  if (!tab?.webSocketDebuggerUrl) throw new Error('Could not connect to browser debugging endpoint.');
  socket = new WebSocket(tab.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }); });
  const pending = new Map();
  const createCall = connection => (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++sequence;
    const timer = setTimeout(() => { pending.delete(id); reject(new Error('Timed out: ' + method)); }, 30000);
    pending.set(id, { connection, resolve: result => { clearTimeout(timer); resolve(result); }, reject: error => { clearTimeout(timer); reject(error); } });
    connection.send(JSON.stringify({ id, method, params }));
  });
  socket.addEventListener('message', event => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) { const callback = pending.get(message.id); pending.delete(message.id); message.error ? callback.reject(new Error(JSON.stringify(message.error))) : callback.resolve(message.result); }
    if (message.method === 'Runtime.exceptionThrown') report.exceptions.push(message.params.exceptionDetails.exception?.description ?? message.params.exceptionDetails.text);
    if (message.method === 'Network.responseReceived' && /services\.api\.unity\.com/.test(message.params.response.url))
      report.requests.push({ url: message.params.response.url.split('?')[0], status: message.params.response.status });
  });
  socket.addEventListener('close', () => {
    for (const [id, callback] of pending) {
      if (callback.connection === socket) { pending.delete(id); callback.reject(new Error('Browser connection closed.')); }
    }
  });
  call = createCall(socket);
  const evaluate = async expression => (await call('Runtime.evaluate', { expression, returnByValue: true })).result.value;
  const screenshot = async name => { const image = Buffer.from((await call('Page.captureScreenshot', { format: 'png' })).data, 'base64'); writeFileSync(path.join(output, name + '.png'), image); return createHash('sha256').update(image).digest('hex'); };
  const energySnapshot = async name => {
    // Move the placement ghost/range away: its world-space ring can overlap HUD
    // pixels even when the numeric energy has not changed.
    await mouseMove(5, 5); await sleep(150);
    const rect = await evaluate('(()=>{const r=document.getElementById("unity-canvas").getBoundingClientRect();return {x:r.x,y:r.y,w:r.width,h:r.height}})()');
    const image = Buffer.from((await call('Page.captureScreenshot', { format: 'png', clip: {
      x: rect.x + 400 / 1280 * rect.w, y: rect.y + 10 / 720 * rect.h,
      width: 450 / 1280 * rect.w, height: 40 / 720 * rect.h, scale: 1
    } })).data, 'base64');
    writeFileSync(path.join(output, name + '.png'), image); return createHash('sha256').update(image).digest('hex');
  };
  const mouseMove = async (x, y) => call('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y });
  const clickElement = async id => {
    const rect = await evaluate(`(()=>{const r=document.getElementById(${JSON.stringify(id)}).getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}})()`);
    await call('Input.dispatchMouseEvent', { type: 'mousePressed', ...rect, button: 'left', clickCount: 1 });
    await call('Input.dispatchMouseEvent', { type: 'mouseReleased', ...rect, button: 'left', clickCount: 1 });
  };
  const click = async (x, y, button = 'left') => {
    const rect = await evaluate('(()=>{const r=document.getElementById("unity-canvas").getBoundingClientRect();return {x:r.x,y:r.y,w:r.width,h:r.height}})()');
    const point = { x: rect.x + x / 1280 * rect.w, y: rect.y + y / 720 * rect.h, button, clickCount: 1 };
    await mouseMove(point.x, point.y); await call('Input.dispatchMouseEvent', { type: 'mousePressed', ...point }); await sleep(100);
    await call('Input.dispatchMouseEvent', { type: 'mouseReleased', ...point }); await sleep(300);
  };
  const key = async letter => {
    const code = letter === ' ' ? 'Space' : letter === 'Escape' ? 'Escape' : 'Key' + letter.toUpperCase();
    const vk = letter === ' ' ? 32 : letter === 'Escape' ? 27 : letter.toUpperCase().charCodeAt(0);
    await call('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: letter, code, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk }); await sleep(100);
    await call('Input.dispatchKeyEvent', { type: 'keyUp', key: letter, code, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk }); await sleep(300);
  };
  await call('Runtime.enable'); await call('Page.enable'); await call('Network.enable');
  await call('Page.bringToFront'); await call('Emulation.setFocusEmulationEnabled', { enabled: true });
  await call('Page.navigate', { url });
  for (let attempt = 0; attempt < 180; attempt++) {
    const error = await evaluate('window.dodgeLineLoadError'); if (error) throw new Error(error);
    if (await evaluate('window.dodgeLineReady === true')) { report.loaded = true; break; }
    await sleep(500);
  }
  if (!report.loaded) throw new Error('Unity Web did not initialize within 90 seconds.');
  // The Unity startup promise resolves before its splash-screen fade finishes.
  await sleep(6000); await screenshot('01-menu'); report.steps.push('Unity initialized; main menu rendered after splash');
  // Exercise the actual Unity button -> IL2CPP -> .jslib -> HTML dialog bridge.
  await click(1000, 631);
  report.quitDialogOpened = await evaluate('document.getElementById("quit-dialog").open && window.dodgeLineQuitState === "confirming"');
  if (!report.quitDialogOpened) throw new Error('Unity Quit button did not open the browser confirmation dialog.');
  await screenshot('01a-quit-confirm');
  await clickElement('quit-cancel'); await sleep(300);
  report.quitCancelContinues = await evaluate('!document.getElementById("quit-dialog").open && window.dodgeLineReady && window.dodgeLineQuitState === "playing" && document.activeElement.id === "unity-canvas"');
  if (!report.quitCancelContinues) throw new Error('Cancel did not return focus to the running game.');
  await click(1000, 631); await key('Escape');
  report.quitEscapeContinues = await evaluate('!document.getElementById("quit-dialog").open && window.dodgeLineQuitState === "playing"');
  if (!report.quitEscapeContinues) throw new Error('Escape did not dismiss quit confirmation.');
  report.steps.push('Actual Unity Quit button opened HTML confirmation; No returned to the running menu');
  await click(635, 631); await sleep(12000); await mouseMove(5, 5);
  const before = await screenshot('02-leaderboard');
  await click(400, 205); await mouseMove(5, 5); await sleep(700);
  const after = await screenshot('03-overlay-input');
  report.overlayBlocksStageClick = before === after;
  if (!report.overlayBlocksStageClick) throw new Error('Clicking behind leaderboard changed the rendered scene; inspect overlay screenshots.');
  report.steps.push('Leaderboard opened; actual UGS HTTP responses recorded; underlying stage click blocked');
  await click(1145, 61); await click(400, 195); await sleep(1500); await screenshot('04-stage1');
  // Use actual IMGUI buttons, browser mouse and keyboard; no injected game state.
  await click(125, 128);
  const startingEnergy = await energySnapshot('04a-before-placement-energy');
  await click(220, 240); await screenshot('04b-unmarked-ground-rejected');
  report.unmarkedGroundDidNotSpend = startingEnergy === await energySnapshot('04c-rejected-placement-energy');
  if (!report.unmarkedGroundDidNotSpend) throw new Error('Clicking unmarked ground changed energy.');
  await click(590, 270); await screenshot('04d-pad-placement');
  report.padPlacementSpentEnergy = startingEnergy !== await energySnapshot('04e-paid-pad-energy');
  if (!report.padPlacementSpentEnergy) throw new Error('Clicking inside a valid pad did not spend energy.');
  await evaluate('window.dodgeLineContextEvents=[];document.addEventListener("contextmenu",e=>window.dodgeLineContextEvents.push({target:e.target.id,prevented:e.defaultPrevented}))');
  const paidEnergy = await energySnapshot('04f-before-deselect-energy');
  await click(580, 270, 'right');
  await click(940, 270); // Another empty, affordable gold pad: must inspect, never build.
  report.rightClickDeselects = paidEnergy === await energySnapshot('04g-after-deselect-energy');
  report.canvasContextMenuPrevented = await evaluate('window.dodgeLineContextEvents.some(e=>e.target === "unity-canvas" && e.prevented)');
  report.outsideContextMenuAllowed = await evaluate('document.querySelector("header h1").dispatchEvent(new MouseEvent("contextmenu", {bubbles:true,cancelable:true}))');
  if (!report.rightClickDeselects || !report.canvasContextMenuPrevented || !report.outsideContextMenuAllowed)
    throw new Error('Right-click deselection or scoped context-menu handling failed.');
  // Selecting an existing tower and then right-clicking must also remove its range ring.
  await click(580, 270); await mouseMove(5, 5); await screenshot('04h-selected-tower');
  await click(580, 270, 'right'); await mouseMove(5, 5); await screenshot('04i-deselected-tower');
  await key(' '); await sleep(1800); await screenshot('05-combat');
  await key('p'); await screenshot('06-pause');
  await key('r'); await screenshot('07-retry');
  report.steps.push('Entered stage1: unmarked ground rejected without spending; valid pad bought a tower; started wave, paused and retried using browser input');
  report.steps.push('Real canvas right-click prevented browser menu and cancelled building without spending on a second pad; outside context menu remained enabled');
  // Return through the real paused-game menu, then create normal-tab history so
  // Chrome must exercise its blocked-close path without stubbing window.close().
  await key('p'); await click(790, 455); await sleep(1000);
  await evaluate('history.pushState({}, "", "#quit-check-1");history.pushState({}, "", "#quit-check-2")');
  await click(1000, 631);
  if (!await evaluate('document.getElementById("quit-dialog").open')) throw new Error('Quit dialog not available after returning to menu.');
  await clickElement('quit-confirm');
  for (let i = 0; i < 100 && !await evaluate('window.dodgeLineQuitState === "closed"'); i++) await sleep(200);
  report.blockedCloseStopsPlayer = await evaluate('window.dodgeLineQuitState === "closed" && !window.dodgeLineReady && window.dodgeLineInstance === null && !document.getElementById("closed").hidden');
  if (!report.blockedCloseStopsPlayer) throw new Error('Blocked tab close did not stop Unity and show the closed screen.');
  await screenshot('08-closed-fallback');
  await clickElement('reopen');
  for (let i = 0; i < 180 && !await evaluate('window.dodgeLineReady === true'); i++) await sleep(500);
  if (!await evaluate('window.dodgeLineReady === true')) throw new Error('Play again did not reload Unity.');
  await sleep(6000); await screenshot('09-reopened-menu');
  report.reopenWorks = true;
  // A script-opened tab must really disappear after confirming Yes.
  await call('Runtime.evaluate', { expression: 'window.open(location.href, "dodge-line-quit-check")', userGesture: true });
  let popup;
  for (let i = 0; i < 50 && !popup; i++) {
    popup = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find(item => item.type === 'page' && item.id !== tab.id && item.url.startsWith(url));
    if (!popup) await sleep(200);
  }
  if (!popup) throw new Error('Script-opened quit test tab was not created.');
  const popupSocket = new WebSocket(popup.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { popupSocket.addEventListener('open', resolve, { once: true }); popupSocket.addEventListener('error', reject, { once: true }); });
  popupSocket.addEventListener('message', event => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) { const callback = pending.get(message.id); pending.delete(message.id); message.error ? callback.reject(new Error(JSON.stringify(message.error))) : callback.resolve(message.result); }
    if (message.method === 'Runtime.exceptionThrown') report.exceptions.push(message.params.exceptionDetails.exception?.description ?? message.params.exceptionDetails.text);
  });
  popupSocket.addEventListener('close', () => {
    for (const [id, callback] of pending) {
      if (callback.connection === popupSocket) { pending.delete(id); callback.reject(new Error('Game tab closed.')); }
    }
  });
  const popupCall = createCall(popupSocket);
  const popupEvaluate = async expression => (await popupCall('Runtime.evaluate', { expression, returnByValue: true })).result.value;
  await popupCall('Runtime.enable'); await popupCall('Page.enable');
  await popupCall('Page.bringToFront'); await popupCall('Emulation.setFocusEmulationEnabled', { enabled: true });
  for (let i = 0; i < 180 && !await popupEvaluate('window.dodgeLineReady === true'); i++) await sleep(500);
  if (!await popupEvaluate('window.dodgeLineReady === true')) throw new Error('Script-opened Unity tab did not initialize.');
  await sleep(6000);
  const quitPoint = await popupEvaluate('(()=>{const r=document.getElementById("unity-canvas").getBoundingClientRect();return {x:r.x+1000/1280*r.width,y:r.y+631/720*r.height}})()');
  await popupCall('Input.dispatchMouseEvent', { type:'mousePressed',...quitPoint,button:'left',clickCount:1 });
  await popupCall('Input.dispatchMouseEvent', { type:'mouseReleased',...quitPoint,button:'left',clickCount:1 }); await sleep(300);
  if (!await popupEvaluate('document.getElementById("quit-dialog").open')) throw new Error('Popup Unity button did not open confirmation.');
  const yesPoint = await popupEvaluate('(()=>{const r=document.getElementById("quit-confirm").getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}})()');
  await popupCall('Input.dispatchMouseEvent', { type:'mousePressed',...yesPoint,button:'left',clickCount:1 });
  // The target can vanish before CDP returns mouseReleased, which is the success path.
  const release = popupCall('Input.dispatchMouseEvent', { type:'mouseReleased',...yesPoint,button:'left',clickCount:1 }).catch(() => {});
  await Promise.race([release, sleep(500)]);
  for (let i = 0; i < 50; i++) {
    if (!(await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).some(item=>item.id === popup.id)) { report.scriptOpenedTabClosed = true; break; }
    await sleep(200);
  }
  popupSocket.close();
  if (!report.scriptOpenedTabClosed) throw new Error('Confirming Yes did not close the script-opened game tab.');
  report.steps.push('Browser refused close in a history-bearing tab: Unity stopped; Play again reloaded; a script-opened game tab actually closed after Yes');
  if (report.exceptions.length) throw new Error('Browser reported uncaught JavaScript/WASM exceptions.');
  writeFileSync(path.join(output, 'browser-check.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report, null, 2));
} catch (error) {
  report.failure = String(error); writeFileSync(path.join(output, 'browser-check.json'), JSON.stringify(report, null, 2)); throw error;
} finally {
  if (call && socket?.readyState === WebSocket.OPEN) { try { await Promise.race([call('Browser.close'), sleep(2000)]); } catch { } }
  socket?.close();
  for (let i = 0; i < 30 && chrome.exitCode === null; i++) await sleep(100);
  if (chrome.exitCode === null) chrome.kill();
  await sleep(500);
  if (path.dirname(profile) === temporaryRoot && path.basename(profile).startsWith('dl-web-check-')) {
    try { rmSync(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 }); } catch { /* Browser may still be releasing profile files. */ }
  }
}
