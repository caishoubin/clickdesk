'use strict';

const navToggle = document.querySelector('.menu-toggle');
const navigation = document.querySelector('#navigation');
navToggle.addEventListener('click', () => {
  const expanded = navToggle.getAttribute('aria-expanded') !== 'true';
  navToggle.setAttribute('aria-expanded', String(expanded));
  navToggle.setAttribute('aria-label', expanded ? 'Close navigation' : 'Open navigation');
  navigation.classList.toggle('open', expanded);
});
navigation.querySelectorAll('a').forEach(link => link.addEventListener('click', () => {
  navToggle.setAttribute('aria-expanded', 'false');
  navToggle.setAttribute('aria-label', 'Open navigation');
  navigation.classList.remove('open');
}));

const pads = Array.from(document.querySelectorAll('[data-pad]'));
const recordButton = document.querySelector('#demo-record');
const playButton = document.querySelector('#demo-play');
const stopButton = document.querySelector('#demo-stop');
const speedInput = document.querySelector('#demo-speed');
const status = document.querySelector('#demo-status');
const caption = document.querySelector('#stage-caption');
const list = document.querySelector('#sequence-list');
const count = document.querySelector('#sequence-count');
const cursor = document.querySelector('#demo-cursor');
const progress = document.querySelector('#demo-progress');
const positions = { A: { x: 19, y: 35 }, B: { x: 54, y: 69 }, C: { x: 81, y: 28 } };
const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
let sequence = [{ pad: 'A', time: 200 }, { pad: 'B', time: 800 }, { pad: 'C', time: 1700 }];
let duration = 1800;
let mode = 'idle';
let recordStart = 0;
let frameId = null;
let activeIndex = -1;
let pulseTimer = null;

function setStatus(text, state = '') {
  status.className = `demo-status ${state}`;
  status.replaceChildren(document.createElement('i'), document.createTextNode(text));
}

function updateControls() {
  recordButton.disabled = mode !== 'idle';
  playButton.disabled = mode !== 'idle' || sequence.length === 0;
  stopButton.disabled = mode === 'idle';
  speedInput.disabled = mode !== 'idle';
}

function renderSequence() {
  list.replaceChildren();
  if (!sequence.length) {
    const row = document.createElement('li');
    const message = document.createElement('span');
    message.className = 'sequence-empty';
    message.textContent = 'Click A, B, or C to capture your own rhythm.';
    row.append(message);
    list.append(row);
  }
  sequence.forEach((event, index) => {
    const row = document.createElement('li');
    row.dataset.event = String(index);
    const gap = event.time - (sequence[index - 1]?.time ?? 0);
    const values = [String(index + 1).padStart(2, '0'), `Click ${event.pad}`, `${(gap / 1000).toFixed(2)} s`, index === 0 ? 'from start' : `after ${sequence[index - 1].pad}`];
    ['sequence-index', 'sequence-target', 'sequence-time', 'sequence-label'].forEach((className, column) => {
      const cell = document.createElement('span');
      cell.className = className;
      cell.textContent = values[column];
      row.append(cell);
    });
    list.append(row);
  });
  count.textContent = `${sequence.length} ${sequence.length === 1 ? 'click' : 'clicks'} · ${(duration / 1000).toFixed(1)} s`;
}

function clearHighlights() {
  pads.forEach(pad => pad.classList.remove('active'));
  list.querySelectorAll('li').forEach(row => row.classList.remove('active'));
}

function highlight(padId, index) {
  clearTimeout(pulseTimer);
  clearHighlights();
  pads.find(pad => pad.dataset.pad === padId)?.classList.add('active');
  const row = list.querySelector(`[data-event="${index}"]`);
  row?.classList.add('active');
  if (row) list.scrollTop = Math.max(0, row.offsetTop - list.offsetTop - list.clientHeight + row.offsetHeight);
  pulseTimer = setTimeout(() => pads.forEach(pad => pad.classList.remove('active')), 350);
}

function stopDemo(message) {
  if (frameId !== null) cancelAnimationFrame(frameId);
  frameId = null;
  if (mode === 'recording') {
    duration = Math.min(performance.now() - recordStart, 30000);
    renderSequence();
  }
  mode = 'idle';
  clearHighlights();
  clearTimeout(pulseTimer);
  updateControls();
  setStatus(message || 'Ready to replay');
  caption.textContent = sequence.length ? 'Your sequence is ready. Keep the rhythm.' : 'Select Record to start a new sequence.';
}

recordButton.addEventListener('click', () => {
  sequence = [];
  duration = 0;
  activeIndex = -1;
  progress.style.width = '0%';
  clearHighlights();
  recordStart = performance.now();
  mode = 'recording';
  setStatus('Recording', 'recording');
  caption.textContent = 'Click the pads in any order, then press Stop. Up to 12 clicks.';
  renderSequence();
  updateControls();
  function tick() {
    if (mode !== 'recording') return;
    duration = performance.now() - recordStart;
    count.textContent = `${sequence.length} ${sequence.length === 1 ? 'click' : 'clicks'} · ${(duration / 1000).toFixed(1)} s`;
    if (duration >= 30000) { stopDemo('Recording saved'); return; }
    frameId = requestAnimationFrame(tick);
  }
  frameId = requestAnimationFrame(tick);
});

pads.forEach(pad => pad.addEventListener('click', () => {
  if (mode === 'playing') return;
  if (mode === 'recording') {
    const now = performance.now() - recordStart;
    sequence.push({ pad: pad.dataset.pad, time: now });
    duration = now;
    renderSequence();
    highlight(pad.dataset.pad, sequence.length - 1);
    if (sequence.length >= 12) stopDemo('12 clicks captured');
  } else {
    highlight(pad.dataset.pad, -1);
    caption.textContent = 'Select Record to capture clicks, or Replay to try the example.';
  }
}));

playButton.addEventListener('click', () => {
  if (mode !== 'idle' || !sequence.length) return;
  mode = 'playing';
  activeIndex = -1;
  const speed = Number(speedInput.value);
  const started = performance.now();
  const total = Math.max(duration, sequence[sequence.length - 1].time + 300);
  progress.style.width = '0%';
  setStatus('Replaying', 'playing');
  caption.textContent = 'Same order. Same intervals. Your chosen speed.';
  updateControls();
  clearHighlights();
  function tick(now) {
    if (mode !== 'playing') return;
    const elapsed = (now - started) * speed;
    while (activeIndex + 1 < sequence.length && sequence[activeIndex + 1].time <= elapsed) {
      activeIndex++;
      highlight(sequence[activeIndex].pad, activeIndex);
      setStatus(`Replaying ${activeIndex + 1}/${sequence.length}`, 'playing');
    }
    const previous = activeIndex < 0 ? { time: 0, x: 12, y: 80 } : { time: sequence[activeIndex].time, ...positions[sequence[activeIndex].pad] };
    const next = sequence[activeIndex + 1];
    let position = previous;
    if (next) {
      const end = positions[next.pad];
      let ratio = Math.min(1, Math.max(0, (elapsed - previous.time) / Math.max(1, next.time - previous.time)));
      if (reduceMotion.matches) ratio = 0;
      else ratio = 1 - Math.pow(1 - ratio, 3);
      position = { x: previous.x + (end.x - previous.x) * ratio, y: previous.y + (end.y - previous.y) * ratio };
    }
    cursor.style.left = `${position.x}%`;
    cursor.style.top = `${position.y}%`;
    progress.style.width = `${Math.min(100, elapsed / total * 100)}%`;
    if (elapsed >= total) { stopDemo('Sequence complete'); return; }
    frameId = requestAnimationFrame(tick);
  }
  frameId = requestAnimationFrame(tick);
});

stopButton.addEventListener('click', () => stopDemo(mode === 'recording' ? 'Recording saved' : 'Playback stopped'));
document.addEventListener('keydown', event => {
  if (event.key === 'Escape' && mode !== 'idle') stopDemo('Demo stopped');
  if (event.key === 'Escape' && navToggle.getAttribute('aria-expanded') === 'true') {
    navToggle.click();
    navToggle.focus();
  }
});
document.addEventListener('visibilitychange', () => {
  if (document.hidden && mode !== 'idle') stopDemo('Demo paused');
});
