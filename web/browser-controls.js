// Keep browser integration outside the simulation. Native builds do not load this file.
(() => {
  const canvas = document.getElementById('unity-canvas');
  const dialog = document.getElementById('quit-dialog');
  const cancel = () => {
    dialog.close();
    window.dodgeLineQuitState = 'playing';
    canvas.focus();
  };

  // Cancel only the browser menu: Unity still receives right-button down/up to deselect.
  document.getElementById('game').addEventListener('contextmenu', event => event.preventDefault());
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
