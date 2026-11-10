// Keep browser integration outside the simulation. Native builds do not load this file.
(() => {
  const canvas = document.getElementById('unity-canvas');
  const dialog = document.getElementById('quit-dialog');
  const cancel = () => {
    dialog.close();
    window.dodgeLineQuitState = 'playing';
    canvas.focus();
  };

  const game = document.getElementById('game');
  window.dodgeLineBrowserControlsVersion = '2026.11.11.1';
  const debugInput = new URLSearchParams(window.location.search).get('inputDebug') === '1';
  const inputEvents = [];
  let diagnostics;
  const report = () => JSON.stringify({
    version: window.dodgeLineBrowserControlsVersion,
    page: window.location.origin + window.location.pathname,
    browser: navigator.userAgent,
    fullscreen: document.fullscreenElement?.id ?? null,
    events: inputEvents
  }, null, 2);
  if (debugInput) {
    const panel = document.createElement('aside');
    panel.id = 'input-diagnostics';
    panel.innerHTML = '<strong>Right-click check</strong><p>Right-click inside the game, dismiss any browser menu, then copy this report.</p><textarea readonly aria-label="Input report"></textarea><button type="button">Copy report</button><span role="status"></span>';
    document.querySelector('main').appendChild(panel);
    diagnostics = panel.querySelector('textarea');
    diagnostics.value = report();
    panel.querySelector('button').addEventListener('click', async () => {
      diagnostics.value = report();
      try {
        await navigator.clipboard.writeText(diagnostics.value);
        panel.querySelector('[role=status]').textContent = 'Copied. Paste it into the chat.';
      } catch {
        diagnostics.focus(); diagnostics.select();
        panel.querySelector('[role=status]').textContent = 'Press Ctrl+C, then paste into the chat.';
      }
    });
  }
  const guardRightClick = event => {
    const path = event.composedPath();
    const inGame = path.includes(canvas) || path.includes(game);
    const shouldBlock = inGame && (event.type === 'contextmenu' || event.button === 2);
    const before = event.defaultPrevented;
    if (shouldBlock) {
      // preventDefault does not stop event delivery: Unity still receives the
      // right mouse down/up. Do not cancel pointerdown, which could suppress
      // compatibility mouse events, and do not use stopPropagation.
      if (event.type === 'mousedown' && path.includes(canvas)) canvas.focus({ preventScroll: true });
      event.preventDefault();
      if (event.type === 'contextmenu') event.returnValue = false;
    }
    if (debugInput && (event.type === 'contextmenu' || event.button === 2)) {
      inputEvents.push({
        type: event.type, target: event.target?.id || event.target?.nodeName,
        inGame, trusted: event.isTrusted, cancelable: event.cancelable,
        button: event.button, buttons: event.buttons,
        shift: event.shiftKey, ctrl: event.ctrlKey, alt: event.altKey, meta: event.metaKey,
        preventedBefore: before, preventedAfter: event.defaultPrevented,
        time: Math.round(performance.now())
      });
      if (inputEvents.length > 16) inputEvents.shift();
      diagnostics.value = report();
    }
  };
  // Capture before Unity/other page handlers can stop bubbling. Match the canvas
  // directly too, even if a fullscreen host changes its ancestors.
  for (const type of ['contextmenu', 'mousedown', 'mouseup', 'auxclick'])
    window.addEventListener(type, guardRightClick, { capture: true, passive: false });
  canvas.addEventListener('contextmenu', event => event.preventDefault(), { capture: true, passive: false });
  window.dodgeLineQuitState = 'playing';
  window.dodgeLineRequestQuit = () => {
    if (window.dodgeLineQuitState !== 'playing') return;
    window.dodgeLineQuitState = 'confirming';
    dialog.showModal();
    document.getElementById('quit-cancel').focus();
  };
  document.getElementById('quit-cancel').addEventListener('click', cancel);
  dialog.addEventListener('cancel', event => { event.preventDefault(); cancel(); });
  document.getElementById('quit-confirm').addEventListener('click', () => {
    if (window.dodgeLineQuitState !== 'confirming') return;
    window.dodgeLineQuitState = 'closing';
    dialog.close();
    // Must run directly from the Yes button's user gesture. Browsers can refuse it.
    window.close();
    setTimeout(async () => {
      // This branch runs only if the page survived the close request.
      if (document.fullscreenElement) {
        try { await document.exitFullscreen(); } catch { /* Still allow quitting. */ }
      }
      canvas.style.display = 'none';
      document.getElementById('fullscreen').disabled = true;
      document.getElementById('closed').hidden = false;
      document.getElementById('reopen').focus();
      try {
        if (window.dodgeLineInstance) await window.dodgeLineInstance.Quit();
      } catch (error) {
        console.error('Unable to release the Unity player:', error);
        document.querySelector('#closed p').textContent = 'Please close this tab to finish quitting the game.';
      }
      window.dodgeLineInstance = null;
      window.dodgeLineReady = false;
      window.dodgeLineQuitState = 'closed';
    }, 250);
  });
  document.getElementById('reopen').addEventListener('click', () => window.location.reload());
})();
