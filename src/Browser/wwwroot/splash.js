const screen = document.getElementById('boot-screen');
const message = document.getElementById('boot-message');
const progress = document.getElementById('boot-progress');
const retry = document.getElementById('boot-retry');
const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;

retry.addEventListener('click', () => location.reload());

let introDone = Promise.resolve();

if (!reducedMotion && globalThis.gsap && globalThis.SplitText) {
  gsap.registerPlugin(SplitText);
  const split = SplitText.create('.boot-title', {
    type: 'words,chars',
    mask: 'chars',
    charsClass: 'char',
    smartWrap: true
  });

  gsap.set('.boot-title', { visibility: 'visible' });
  introDone = new Promise(resolve => {
    gsap.timeline({ onComplete: resolve })
      .from('.boot-mark', { scale: 0.78, autoAlpha: 0, duration: 0.65, ease: 'power3.out' })
      .from('.boot-name', { y: 12, autoAlpha: 0, duration: 0.55, ease: 'power3.out' }, 0.18)
      .fromTo(split.chars,
        { yPercent: 110, autoAlpha: 0 },
        { yPercent: 0, autoAlpha: 1, duration: 0.8, stagger: 0.055, ease: 'power4.out' },
        0.34)
      .from('.boot-message', { y: 10, autoAlpha: 0, duration: 0.55, ease: 'power3.out' }, 0.8)
      .from('.boot-progress-track', { scaleX: 0.6, autoAlpha: 0, duration: 0.5, ease: 'power3.out' }, 0.9);
  });

  gsap.to(progress, { scaleX: 0.72, duration: 1.3, ease: 'power2.out' });
} else {
  document.querySelector('.boot-title').style.visibility = 'visible';
  progress.style.transform = 'scaleX(0.72)';
}

let settled = false;

export async function waitForIntro() {
  await introDone;
  message.textContent = '正在启动编辑器';
  if (!reducedMotion) {
    progress.classList.add('is-loading');
    await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  }
}

export function studioReady() {
  if (settled) return;
  settled = true;

  introDone.then(() => {
    if (reducedMotion || !globalThis.gsap) {
      screen.remove();
      return;
    }

    const currentTransform = getComputedStyle(progress).transform;
    progress.classList.remove('is-loading');
    progress.style.transform = currentTransform;
    message.textContent = '准备就绪';
    gsap.timeline({ onComplete: () => screen.remove() })
      .to(progress, { scaleX: 1, duration: 0.5, ease: 'power3.out' })
      .to(screen, { autoAlpha: 0, duration: 0.5, ease: 'power2.inOut' }, '+=0.12');
  });
}

export function studioStillLoading() {
  if (settled) return;
  message.textContent = '加载时间较长，正在继续准备编辑器…';
  retry.hidden = false;
}

export function studioFailed(error) {
  if (settled) return;
  settled = true;
  console.error('CardStudio could not start', error);
  screen.classList.add('is-error');
  message.textContent = '启动失败，请重试';
  retry.hidden = false;
  if (globalThis.gsap) gsap.killTweensOf(progress);
}
