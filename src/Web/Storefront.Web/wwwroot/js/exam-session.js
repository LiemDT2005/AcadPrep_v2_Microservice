/**
 * Monolith exam workspace presentation, backed by microservice session data.
 * Checkpoints and submission are proxied by Razor Pages through the API gateway.
 */
(function () {
    const config = window.examSessionConfig || {};
    const mode = config.mode || 'full';
    const questions = config.questions || [];
    if (!questions.length) return;
    const answers = { ...(config.answers || {}) };
    const audioProgress = { ...(config.audioProgress || {}) };
    const apiMode = mode === 'practice' ? 'practice' : 'full-test';
    const storageKey = `acadprep:${apiMode}:${config.sessionId}:${config.expiresAt || config.examId}`;
    try {
        const pending = JSON.parse(sessionStorage.getItem(storageKey) || 'null');
        if (pending) Object.assign(answers, pending.answers);
    } catch { /* Storage can be disabled by browser settings. */ }
    let saving = Promise.resolve(true);
    let saveTimer;
    let submitting = false;
    const audio = new Audio();
    audio.preload = 'metadata';
    let audioKey = null;
    let audioStart = 0;
    let audioEnd = null;
    let audioPosition = 0;
    let changingAudio = false;
    let autoplayNext = false;
    let currentIndex = 0;
    let expired = false;
    let leaving = false;
    const expiresAt = config.expiresAt ? Date.parse(config.expiresAt) : null;
    const timerEl = document.getElementById('exam-timer');
    const paletteEl = document.getElementById('question-palette');
    const optionsEl = document.getElementById('options-container');
    const questionTextEl = document.getElementById('question-text');
    const questionLabelEl = document.getElementById('question-label');
    const questionImageWrap = document.getElementById('question-image-wrap');
    const questionImageEl = document.getElementById('question-image');
    const partLabelEl = document.getElementById('current-part-label');
    const answeredCountEl = document.getElementById('answered-count');
    const answeredCountBadge = document.getElementById('answered-count-badge');
    const currentQNumEl = document.getElementById('current-q-num');
    const totalQNumEl = document.getElementById('total-q-num');
    const totalQNumNav = document.getElementById('total-q-num-nav');
    const sidebarEl = document.getElementById('question-sidebar');
    const sidebarBackdrop = document.getElementById('sidebar-backdrop');
    function getGroupIdentity(q) {
        if (!q) return null;
        if ((q.part === 3 || q.part === 4 || q.part === 7) && q.questionGroupId != null) {
            return { key: `g-${q.questionGroupId}`, part: q.part };
        }
        if (q.part === 6 && q.passageId != null) {
            return { key: `p-${q.passageId}`, part: q.part };
        }
        return null;
    }

    function sameGroup(a, b) {
        const ia = getGroupIdentity(a);
        const ib = getGroupIdentity(b);
        return !!(ia && ib && ia.key === ib.key && ia.part === ib.part);
    }

    /** Bounds of the unit containing `idx` (inclusive). */
    function getUnitRange(idx) {
        const q = questions[idx];
        if (!q) return { start: idx, end: idx, isGroup: false, key: `q-${idx}` };

        const identity = getGroupIdentity(q);
        if (identity) {
            let start = idx;
            while (start > 0 && sameGroup(questions[start - 1], q)) start -= 1;
            let end = idx;
            while (end < questions.length - 1 && sameGroup(questions[end + 1], q)) end += 1;
            return { start, end, isGroup: true, key: identity.key };
        }

        return { start: idx, end: idx, isGroup: false, key: `q-${q.id}` };
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function renderPassagesHtml(passages) {
        return (passages || []).map((p, i) => {
            const blocks = [];
            if (p.imageUrl) {
                blocks.push(`<img src="${escapeHtml(p.imageUrl)}" alt="Passage ${i + 1}" class="max-h-96 rounded-lg mx-auto mb-3" />`);
            }
            if (p.content) {
                blocks.push(`<div class="text-body-md whitespace-pre-wrap">${escapeHtml(p.content)}</div>`);
            }
            if (!blocks.length) return '';
            const sep = i > 0 ? 'pt-4 mt-4 border-t border-outline-variant/20' : '';
            return `<div class="passage-block ${sep}">${blocks.join('')}</div>`;
        }).join('');
    }


    function isReadingGroupPart(part) { return part === 6 || part === 7; }
    function getCurrentUnit() { return getUnitRange(currentIndex); }
    function snapToUnitStart(index) { return getUnitRange(index).start; }
    function canNavigateTo(index) {
        if (index < 0 || index >= questions.length) return false;
        if (mode !== 'full') return true;
        const current = questions[currentIndex];
        const target = questions[index];
        if (sameGroup(current, target) || index === currentIndex) return true;
        if (current.part <= 4 && current.audioUrl && !audioProgress[getUnitRange(currentIndex).key]?.completed) return false;
        return !(target.part <= 4 && target.audioUrl && audioProgress[getUnitRange(index).key]?.completed);
    }
    function goToIndex(index) {
        if (!canNavigateTo(index)) return;
        currentIndex = snapToUnitStart(index);
        renderQuestion();
        closeSidebar();
    }
    function updateNavButtons() {
        const unit = getCurrentUnit();
        const prev = document.getElementById('btn-prev');
        const next = document.getElementById('btn-next');
        if (prev) {
            prev.disabled = unit.start === 0;
            prev.classList.toggle('opacity-40', prev.disabled);
        }
        if (next) {
            next.disabled = unit.end === questions.length - 1;
            next.classList.toggle('opacity-40', next.disabled);
        }
    }
    function selectAnswer(questionId, letter) {
        if (expired || submitting) return;
        answers[questionId] = letter;
        scheduleSave();
        updateAnsweredCount();
        // Update only option styles, so passage and question scroll positions stay put.
        const block = optionsEl.querySelector(`[data-question-id="${questionId}"]`);
        block?.querySelectorAll('[data-answer]').forEach(button => {
            const selected = button.dataset.answer === letter;
            button.setAttribute('aria-pressed', String(selected));
            button.classList.toggle('border-primary', selected);
            button.classList.toggle('bg-primary/5', selected);
            button.classList.toggle('ring-2', selected);
            button.classList.toggle('ring-primary/20', selected);
            button.classList.toggle('border-outline-variant/40', !selected);
            button.classList.toggle('hover:border-primary/50', !selected);
        });
    }
    function showModal(id) { document.getElementById(id)?.classList.remove('hidden'); }
    function hideModal(id) { document.getElementById(id)?.classList.add('hidden'); }
    function updateTimer() {
        if (expiresAt === null) return;
        const remaining = Math.max(0, Math.ceil((expiresAt - Date.now()) / 1000));
        const h = Math.floor(remaining / 3600);
        const m = Math.floor((remaining % 3600) / 60);
        const s = remaining % 60;
        const units = h > 0 ? [h, m, s] : [m, s];
        if (timerEl) {
            timerEl.textContent = units.map(n => String(n).padStart(2, '0')).join(':');
            timerEl.classList.toggle('exam-timer-warning', remaining <= 300);
        }
        if (remaining === 0 && !expired) {
            expired = true;
            document.getElementById('section-intro')?.classList.add('hidden');
            document.getElementById('exam-workspace')?.classList.remove('hidden');
            renderQuestion();
            audio.pause();
            submitSession();
            if (document.getElementById('modal-timeout')) showModal('modal-timeout');
            else {
                const notice = document.createElement('p');
                notice.className = 'text-error text-body-sm font-bold';
                notice.setAttribute('role', 'alert');
                notice.textContent = "Time's up! Answering has stopped.";
                document.getElementById('exam-workspace').prepend(notice);
            }
        }
        if (!expired) window.setTimeout(updateTimer, 1000);
    }
    function renderOptionButtons(q, container, answerLocked) {
        (q.options || []).forEach(opt => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.dataset.answer = opt.letter;
            const selected = answers[q.id] === opt.letter;
            btn.disabled = answerLocked;
            btn.setAttribute('aria-pressed', String(selected));
            btn.setAttribute('aria-label', `Question ${q.questionNumber}, option ${opt.letter}`);
            btn.className = 'w-full text-left p-3 rounded-xl border-2 transition-all flex items-start gap-3 ' +
                (selected
                    ? 'border-primary bg-primary/5 ring-2 ring-primary/20'
                    : 'border-outline-variant/40 hover:border-primary/50') +
                (answerLocked ? ' opacity-60 cursor-not-allowed' : '');
            btn.innerHTML = `<span class="shrink-0 w-8 h-8 rounded-full bg-surface-container flex items-center justify-center font-bold">${escapeHtml(opt.letter)}</span><span class="pt-1">${escapeHtml(opt.text)}</span>`;
            if (!answerLocked) {
                btn.addEventListener('click', () => selectAnswer(q.id, opt.letter));
            }
            container.appendChild(btn);
        });
    }

    function renderQuestion() {
        const unit = getCurrentUnit();
        currentIndex = unit.start;
        const head = questions[unit.start];
        if (!head) return;

        const nums = [];
        for (let i = unit.start; i <= unit.end; i++) nums.push(questions[i].questionNumber);

        // 1) Update ALL visible UI before touching audio
        if (questionLabelEl) {
            questionLabelEl.textContent = unit.isGroup
                ? `Questions ${nums[0]}–${nums[nums.length - 1]}`
                : `Question ${head.questionNumber}`;
        }
        if (currentQNumEl) currentQNumEl.textContent = String(head.questionNumber);
        if (partLabelEl) partLabelEl.textContent = `Part ${head.part}`;

        const passages = head.passages || [];
        const isReadingSet = unit.isGroup && isReadingGroupPart(head.part) && passages.length > 0;
        const stimulusImage = !isReadingSet ? (head.groupImageUrl || head.imageUrl) : null;

        if (questionImageWrap && questionImageEl) {
            if (stimulusImage) {
                questionImageWrap.classList.remove('hidden');
                // Force reload so consecutive Part 1 images always refresh
                questionImageEl.src = '';
                questionImageEl.src = stimulusImage;
            } else {
                questionImageWrap.classList.add('hidden');
                questionImageEl.removeAttribute('src');
            }
        }

        if (questionTextEl) {
            if (isReadingSet) {
                questionTextEl.innerHTML = renderPassagesHtml(passages);
            } else if (unit.isGroup) {
                // Part 3/4: stimulus is audio + optional group image only
                questionTextEl.textContent = '';
            } else {
                questionTextEl.textContent = head.questionText || '';
            }
        }

        if (optionsEl) {
            optionsEl.innerHTML = '';
            for (let i = unit.start; i <= unit.end; i++) {
                const q = questions[i];
                const answerLocked = expired;

                const block = document.createElement('div');
                block.className = 'space-y-2' + (i > unit.start ? ' pt-4 border-t border-outline-variant/20' : '');
                block.dataset.questionId = String(q.id);

                if (unit.isGroup) {
                    const qHead = document.createElement('div');
                    qHead.className = 'text-label-sm font-bold text-on-surface mb-2';
                    qHead.textContent = `${q.questionNumber}. ${q.questionText || ''}`.trim();
                    block.appendChild(qHead);
                }

                const optsWrap = document.createElement('div');
                optsWrap.className = 'space-y-2';
                renderOptionButtons(q, optsWrap, answerLocked);
                block.appendChild(optsWrap);
                optionsEl.appendChild(block);
            }
        }

        configureAudio(head, unit);
        updatePaletteHighlight();
        updateNavButtons();

    }

    function buildPalette() {
        if (!paletteEl) return;
        paletteEl.innerHTML = '';

        const byPart = new Map();
        questions.forEach((q, idx) => {
            if (!byPart.has(q.part)) byPart.set(q.part, []);
            byPart.get(q.part).push({ q, idx });
        });

        const displayUnit = getCurrentUnit();
        

        [...byPart.keys()].sort((a, b) => a - b).forEach(part => {
            const group = document.createElement('div');
            group.className = 'space-y-2';

            const heading = document.createElement('div');
            heading.className = 'text-label-sm font-bold text-on-surface-variant uppercase tracking-wide px-1';
            heading.textContent = `Part ${part}`;
            group.appendChild(heading);

            const grid = document.createElement('div');
            grid.className = 'flex flex-wrap gap-1.5';

            byPart.get(part).forEach(({ q, idx }) => {
                const cell = document.createElement('button');
                cell.type = 'button';
                cell.dataset.qIdx = String(idx);
                cell.textContent = String(q.questionNumber);

                const answered = !!answers[q.id];
                const inDisplayUnit = idx >= displayUnit.start && idx <= displayUnit.end;
                const isHeardLocked = false;
                const navigable = canNavigateTo(idx);

                let cls = 'q-cell rounded-full border text-[11px] font-medium flex items-center justify-center transition-all ';
                if (inDisplayUnit) {
                    cls += 'bg-primary text-on-primary border-primary ring-2 ring-primary/30 ';
                } else if (isHeardLocked) {
                    cls += 'bg-outline-variant/40 text-on-surface-variant/50 border-transparent cursor-not-allowed ';
                } else if (answered) {
                    cls += 'bg-tertiary/20 text-tertiary border-tertiary/30 font-bold ';
                } else {
                    cls += 'bg-[#9e9e9e] text-white border-transparent ';
                }

                if (!navigable && !inDisplayUnit) cls += 'opacity-50 cursor-not-allowed ';
                else if (navigable) cls += 'hover:scale-105 ';

                cell.className = cls;
                cell.disabled = !navigable && !inDisplayUnit;
                cell.title = `Question ${q.questionNumber}`;
                cell.setAttribute('aria-label', `Go to question ${q.questionNumber}`);
                if (inDisplayUnit) cell.setAttribute('aria-current', 'true');
                cell.addEventListener('click', () => goToIndex(snapToUnitStart(idx)));
                grid.appendChild(cell);
            });

            group.appendChild(grid);
            paletteEl.appendChild(group);
        });
    }

    function updatePaletteHighlight() {
        buildPalette();
    }

    function updateAnsweredCount() {
        const count = Object.keys(answers).length;
        if (answeredCountEl) answeredCountEl.textContent = String(count);
        if (answeredCountBadge) answeredCountBadge.textContent = String(count);
        buildPalette();
    }

    function openSidebar() {
        sidebarEl?.classList.add('is-open');
        sidebarEl?.classList.remove('-translate-x-full');
        sidebarBackdrop?.classList.remove('hidden');
    }
    function closeSidebar() {
        sidebarEl?.classList.remove('is-open');
        sidebarEl?.classList.add('-translate-x-full');
        sidebarBackdrop?.classList.add('hidden');
    }
    function toggleSidebar() {
        if (!sidebarEl) return;
        if (sidebarEl.classList.contains('is-open')) closeSidebar();
        else openSidebar();
    }


    const statusEl = document.getElementById('save-status');
    function status(message, error = false) {
        if (!statusEl) return;
        statusEl.textContent = message;
        statusEl.classList.toggle('text-error', error);
    }
    function rememberPending() {
        try { sessionStorage.setItem(storageKey, JSON.stringify({ answers })); } catch { }
    }
    function checkpointBody() {
        return { answers: { ...answers }, audioProgress: { ...audioProgress } };
    }
    async function request(handler, body) {
        const response = await fetch(`/Exams/Take/${config.sessionId}?mode=${apiMode}&handler=${handler}`, {
            method: 'POST', headers: { 'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value },
            body: JSON.stringify(body), keepalive: true
        });
        const result = await response.json();
        if (!response.ok) throw new Error(result.error || 'Unable to save. Retry when connected.');
        return result;
    }
    function saveCheckpoint() {
        if (expired || submitting) return saving;
        const body = checkpointBody();
        status('Saving…');
        saving = saving.then(async () => {
            try {
                await request('Checkpoint', body);
                if (JSON.stringify(body.answers) === JSON.stringify(answers)) {
                    try { sessionStorage.removeItem(storageKey); } catch { }
                }
                status('Saved');
                return true;
            } catch (error) { rememberPending(); status(error.message, true); return false; }
        });
        return saving;
    }
    function scheduleSave() {
        rememberPending();
        status('Saving…');
        clearTimeout(saveTimer);
        saveTimer = setTimeout(saveCheckpoint, 300);
    }
    async function submitSession() {
        if (submitting) return;
        clearTimeout(saveTimer);
        // Finish all queued checkpoints before grading.
        if (!expired) await saveCheckpoint(); else await saving;
        submitting = true;
        audio.pause();
        status('Submitting…');
        try {
            const result = await request('Submit', checkpointBody());
            leaving = true;
            try { sessionStorage.removeItem(storageKey); } catch { }
            window.location.assign(result.redirectUrl);
        } catch (error) {
            submitting = false;
            hideModal('modal-timeout');
            status(error.message + ' Press Submit to retry.', true);
        }
    }
    function audioStamp(seconds) {
        return `${Math.floor(seconds / 60).toString().padStart(2, '0')}:${Math.floor(seconds % 60).toString().padStart(2, '0')}`;
    }
    function saveAudio(completed = false) {
        if (!audioKey) return;
        audioProgress[audioKey] = { position: Math.max(audioStart, audio.currentTime || audioStart),
            completed: completed || !!audioProgress[audioKey]?.completed };
    }
    function configureAudio(head, unit) {
        const section = document.getElementById('audio-section');
        section?.classList.toggle('hidden', !head.audioUrl || expired);
        if (audioKey === unit.key) return;
        changingAudio = true;
        saveAudio();
        audio.pause();
        audioKey = unit.key;
        audioStart = head.audioStartSecond ?? 0;
        audioEnd = head.audioEndSecond ?? null;
        audioPosition = mode === 'full' ? Math.max(audioStart, audioProgress[audioKey]?.position || audioStart) : audioStart;
        if (!head.audioUrl || expired) { changingAudio = false; return; }
        audio.src = head.audioUrl;
        audio.onloadedmetadata = () => {
            audio.currentTime = Math.min(audioPosition, Number.isFinite(audio.duration) ? audio.duration : audioPosition);
            if (audioEnd === null && Number.isFinite(audio.duration)) audioEnd = audio.duration;
            changingAudio = false;
            if (autoplayNext) { autoplayNext = false; playAudio(); }
        };
        const btn = document.getElementById('audio-play-btn');
        if (btn) { btn.disabled = false; btn.textContent = mode === 'full' ? '▶' : '▶ / Ⅱ'; }
        document.getElementById('audio-locked-row')?.classList.toggle('hidden', mode !== 'full');
        const oldButton = document.getElementById('full-audio-start');
        if (oldButton) oldButton.remove();
        if (mode === 'full') {
            const play = document.createElement('button');
            play.id = 'full-audio-start'; play.type = 'button'; play.className = 'px-4 py-2 rounded-lg bg-primary text-on-primary';
            play.textContent = audioProgress[audioKey]?.completed ? 'Audio completed' : 'Start / resume audio';
            play.disabled = !!audioProgress[audioKey]?.completed;
            play.addEventListener('click', playAudio);
            section?.appendChild(play);
        }
    }
    function playAudio() {
        if (expired || submitting || !audio.src || (mode === 'full' && audioProgress[audioKey]?.completed)) return;
        if (mode === 'practice' && !audio.paused) { audio.pause(); return; }
        if (audioEnd !== null && audio.currentTime >= audioEnd) audio.currentTime = audioStart;
        audio.play().catch(() => status('Audio could not play. Check the media URL and press play again.', true));
    }
    function finishAudio() {
        if (!audioKey || audioProgress[audioKey]?.completed && mode === 'full') return;
        saveAudio(true);
        changingAudio = true;
        audio.pause();
        changingAudio = false;
        scheduleSave();
        if (mode === 'full') {
            autoplayNext = true;
            const next = getCurrentUnit().end + 1;
            if (next < questions.length) goToIndex(next);
        }
    }
    audio.addEventListener('ended', finishAudio);
    audio.addEventListener('timeupdate', () => {
        if (changingAudio) return;
        if (audioEnd !== null && audio.currentTime >= audioEnd) { finishAudio(); return; }
        audioPosition = audio.currentTime;
        saveAudio();
        const duration = (audioEnd ?? audio.duration) - audioStart;
        const elapsed = Math.max(0, audio.currentTime - audioStart);
        const fill = document.getElementById('audio-progress-fill');
        if (fill && duration > 0) fill.style.width = `${Math.min(100, elapsed / duration * 100)}%`;
        const current = document.getElementById('audio-time-current') || document.getElementById('audio-time');
        if (current) current.textContent = audioStamp(elapsed);
        const end = document.getElementById('audio-time-duration');
        if (end && Number.isFinite(duration)) end.textContent = audioStamp(duration);
    });
    audio.addEventListener('seeking', () => {
        if (mode === 'full' && !changingAudio && Math.abs(audio.currentTime - audioPosition) > 1)
            audio.currentTime = audioPosition;
    });
    audio.addEventListener('error', () => status('Audio unavailable. Check your connection or contact the content moderator.', true));
    document.getElementById('audio-play-btn')?.addEventListener('click', playAudio);
    document.getElementById('audio-progress-track')?.addEventListener('click', event => {
        if (mode !== 'practice' || audioEnd === null) return;
        const bounds = event.currentTarget.getBoundingClientRect();
        audio.currentTime = audioStart + Math.max(0, Math.min(1, (event.clientX - bounds.left) / bounds.width)) * (audioEnd - audioStart);
    });
    document.getElementById('btn-submit')?.addEventListener('click', () => showModal('modal-submit'));
    document.querySelectorAll('[data-close-submit]').forEach(b => b.addEventListener('click', () => hideModal('modal-submit')));
    document.querySelector('[data-confirm-submit]')?.addEventListener('click', submitSession);
    window.addEventListener('online', () => { if (expired) submitSession(); else saveCheckpoint(); });
    window.addEventListener('pagehide', () => { if (!expired && !submitting) saveCheckpoint(); });
    window.setInterval(() => { if (!expired && !submitting) { saveAudio(); saveCheckpoint(); } }, 5000);

    document.getElementById('btn-sidebar-toggle')?.addEventListener('click', toggleSidebar);
    document.getElementById('btn-sidebar-close')?.addEventListener('click', closeSidebar);
    sidebarBackdrop?.addEventListener('click', closeSidebar);
    document.addEventListener('keydown', event => { if (event.key === 'Escape') closeSidebar(); });
    document.getElementById('btn-next')?.addEventListener('click', () => goToIndex(getCurrentUnit().end + 1));
    document.getElementById('btn-prev')?.addEventListener('click', () => goToIndex(getCurrentUnit().start - 1));
    document.getElementById('btn-exit')?.addEventListener('click', () => showModal('modal-leave'));
    document.querySelectorAll('[data-close-leave]').forEach(button => button.addEventListener('click', () => hideModal('modal-leave')));
    document.querySelector('[data-confirm-leave]')?.addEventListener('click', async () => {
        if (!expired && !await saveCheckpoint()) return;
        leaving = true;
        audio.pause();
        window.location.assign(`/Exams/Detail/${config.examId}`);
    });
    document.getElementById('btn-section-start')?.addEventListener('click', () => {
        hideModal('section-intro');
        document.getElementById('exam-workspace')?.classList.remove('hidden');
        if (questions[currentIndex].audioUrl && !expired) playAudio();
    });
    document.querySelector('[data-close-timeout]')?.addEventListener('click', () => hideModal('modal-timeout'));
    window.addEventListener('beforeunload', event => {
        if (!leaving && Object.keys(answers).length > 0) {
            event.preventDefault();
            event.returnValue = '';
        }
    });
    if (totalQNumEl) totalQNumEl.textContent = String(questions.length);
    if (totalQNumNav) totalQNumNav.textContent = String(questions.length);
    renderQuestion();
    updateAnsweredCount();
    if (mode === 'full') {
        const description = document.getElementById('section-intro-desc');
        if (description) description.textContent = `${questions.length} questions · Parts ${[...new Set(questions.map(q => q.part))].join(', ')}`;
        const rules = document.getElementById('section-intro-rules');
        if (rules) {
            for (const text of ['Answers are saved automatically. Check the save status before leaving.', 'The server deadline continues while the page is closed.', 'Full-test audio plays once per question or group. Practice allows replay and seeking.']) {
                const item = document.createElement('li');
                item.textContent = text;
                rules.appendChild(item);
            }
        }
        showModal('section-intro');
        document.getElementById('exam-workspace')?.classList.add('hidden');
    }
    updateTimer();
})();
