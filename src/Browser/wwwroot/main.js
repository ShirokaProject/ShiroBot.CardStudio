import './assets.js';
import { waitForIntro, studioReady, studioFailed, studioStillLoading } from './splash.js';

const editorHost = document.getElementById('out');
let composing = false;
document.addEventListener('compositionstart', event => {
  if (editorHost.contains(event.target)) composing = true;
}, true);
document.addEventListener('compositionend', () => {
  composing = false;
}, true);
document.addEventListener('keydown', event => {
  if (editorHost.contains(event.target) &&
      (composing || event.isComposing || event.keyCode === 229)) {
    // Keep the browser's IME active, but do not forward its raw keys to Avalonia.
    event.stopPropagation();
  }
}, true);

const previewBusy = document.getElementById('preview-busy');
const previewWave = document.getElementById('preview-wave');
const wavePoints = [];
for (let index = 0; index <= 96; index++) {
  const progress = index / 96;
  const angle = (-105 + 255 * progress) * Math.PI / 180;
  const radius = 16 + 1.5 * Math.sin(progress * 10 * Math.PI);
  const x = 24 + radius * Math.cos(angle);
  const y = 24 + radius * Math.sin(angle);
  wavePoints.push(`${index ? 'L' : 'M'}${x.toFixed(2)},${y.toFixed(2)}`);
}
previewWave.setAttribute('d', wavePoints.join(' '));
Object.defineProperty(globalThis, 'cardStudioPreviewBusy', {
  configurable: true,
  get: () => !previewBusy.hidden,
  set: value => { previewBusy.hidden = !value; }
});

Object.defineProperty(globalThis, 'cardStudioPngDownload', {
  configurable: true,
  set: payload => {
    const { fileName, base64 } = JSON.parse(payload);
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    const url = URL.createObjectURL(new Blob([bytes], { type: 'image/png' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
  }
});


window.addEventListener("keydown", event => {
  if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "f" &&
      document.activeElement?.closest("#out")) event.preventDefault();
});

Object.defineProperty(globalThis, "cardStudioDarkTheme", {
  configurable: true,
  set: value => { document.documentElement.dataset.studioTheme = value ? "dark" : "light"; }
});

await waitForIntro();

const host = editorHost;
const showStudio = () => {
  observer.disconnect();
  clearTimeout(startupTimeout);
  requestAnimationFrame(() => requestAnimationFrame(studioReady));
};
const observer = new MutationObserver(() => {
  if (host.querySelector('canvas.avalonia-canvas')) showStudio();
});
observer.observe(host, { childList: true, subtree: true });
// Slow downloads are still valid startup attempts. Keep watching for the canvas.
const startupTimeout = setTimeout(studioStillLoading, 45_000);

try {
  const { dotnet } = await import('./_framework/dotnet.js');
  const runtime = await dotnet.withApplicationArgumentsFromQuery().create();
  await runtime.runMain(runtime.getConfig().mainAssemblyName, [globalThis.location.href]);
  if (host.querySelector('canvas.avalonia-canvas')) showStudio();
} catch (error) {
  observer.disconnect();
  clearTimeout(startupTimeout);
  studioFailed(error);
}
