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
  window.dodgeLineBrowserControlsVersion = '2026.11.10.2';
  // Capture before Unity/other page handlers can stop bubbling. Test the canvas
  // itself too, so this stays valid if a fullscreen host changes its ancestors.
  // Never cancel mouse/pointer down/up: Unity needs them for right-click deselect.
  window.addEventListener('contextmenu', event => {
    const path = event.composedPath();
    if (path.includes(canvas) || path.includes(game)) event.preventDefault();
  }, { capture: true, passive: false });
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
