// Track selected parts for practice
// Automatically update summary when page loads — default all parts checked
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('input.part-checkbox').forEach(cb => {
        if (!cb.hasAttribute('data-initialized')) {
            cb.checked = true;
            cb.setAttribute('data-initialized', 'true');
        }
        syncPartCardStyle(parseInt(cb.getAttribute('data-part')));
    });
    updateSummary();
});

function syncPartCardStyle(partNumber) {
    const card = document.getElementById('part-card-' + partNumber);
    const partCheck = document.getElementById('part-check-' + partNumber);
    if (!card || !partCheck) return;
    if (partCheck.checked) {
        card.classList.add('border-primary', 'bg-primary/5');
        card.classList.remove('border-outline-variant/30');
    } else {
        card.classList.remove('border-primary', 'bg-primary/5');
        card.classList.add('border-outline-variant/30');
    }
}

function switchTab(tabName) {
    // Hide all tab contents
    document.getElementById('content-practice').classList.add('hidden');
    document.getElementById('content-fulltest').classList.add('hidden');
    document.getElementById('content-info').classList.add('hidden');
    
    // Remove active styles from all buttons
    const buttons = ['practice', 'fulltest', 'info'];
    buttons.forEach(btn => {
        const el = document.getElementById('tab-' + btn);
        if (el) {
            el.classList.remove('text-primary', 'font-bold', 'border-b-2', 'border-primary');
            el.classList.add('text-on-surface-variant');
        }
    });
    
    // Show selected content
    const activeContent = document.getElementById('content-' + tabName);
    if (activeContent) {
        activeContent.classList.remove('hidden');
    }
    
    // Add active styles to selected button
    const activeEl = document.getElementById('tab-' + tabName);
    if (activeEl) {
        activeEl.classList.add('text-primary', 'font-bold', 'border-b-2', 'border-primary');
        activeEl.classList.remove('text-on-surface-variant');
    }
}

// Handle Checkbox: Click card automatically toggles Part checkbox
function togglePartCheckbox(partNumber, isCheckboxClicked = false) {
    const partCheck = document.getElementById('part-check-' + partNumber);
    if (!partCheck) return;

    if (!isCheckboxClicked) {
        partCheck.checked = !partCheck.checked;
    }

    const tagChecks = document.querySelectorAll(`input.tag-checkbox[data-parent-part="${partNumber}"]`);
    tagChecks.forEach(cb => {
        cb.checked = partCheck.checked;
    });

    syncPartCardStyle(partNumber);
    updateSummary();
}

function syncParentPartCheckbox(partNumber) {
    const partCheck = document.getElementById('part-check-' + partNumber);
    if (!partCheck) return;

    const tagChecks = document.querySelectorAll(`input.tag-checkbox[data-parent-part="${partNumber}"]`);
    const anyTagChecked = Array.from(tagChecks).some(cb => cb.checked);

    if (anyTagChecked) {
        partCheck.checked = true;
    }

    syncPartCardStyle(partNumber);
    updateSummary();
}

function updateSummary() {
    let totalQuestions = 0;
    let estimatedMins = 0;
    let selectedPartsCount = 0;

    const partCheckboxes = document.querySelectorAll('input.part-checkbox');
    partCheckboxes.forEach(cb => {
        if (cb.checked) {
            selectedPartsCount++;
            const partNum = parseInt(cb.getAttribute('data-part'));
            const part = window.EXAM_CONFIG && window.EXAM_CONFIG.partsData ? window.EXAM_CONFIG.partsData[partNum] : null;
            if (part) {
                totalQuestions += part.questionCount;
                if (partNum <= 4) {
                    estimatedMins += Math.ceil((part.questionCount * 45) / 60);
                } else {
                    estimatedMins += part.questionCount; 
                }
            }
        }
    });

    const summaryText = selectedPartsCount === 0
        ? 'No parts selected — please choose at least 1 Part'
        : `Selected ${selectedPartsCount} ${(selectedPartsCount === 1 ? 'part' : 'parts')} | ${totalQuestions} questions | ~${estimatedMins} mins`;
    const summaryEl = document.getElementById('selection-summary');
    if (summaryEl) {
        summaryEl.innerText = summaryText;
    }
}

async function startPracticeSession() {
    const selectedPartNumbers = Array.from(document.querySelectorAll('input.part-checkbox:checked'))
        .map(cb => Number(cb.dataset.part));
    if (!selectedPartNumbers.length) {
        showStartError('Please select at least one Part.');
        return;
    }
    const selectedTags = Array.from(document.querySelectorAll('input.tag-checkbox:checked'))
        .filter(cb => selectedPartNumbers.includes(Number(cb.dataset.parentPart))).map(cb => cb.value);
    const time = document.getElementById('customTimeLimit').value;
    await startSession('StartPractice', {
        selectedPartNumbers,
        selectedTags: [...new Set(selectedTags)],
        timeLimitMinutes: time ? Number(time) : null
    });
}

async function startFullTestSession() {
    await startSession('StartFullTest', {});
}

function showStartError(message) {
    const error = document.getElementById('start-session-error');
    error.textContent = message;
    error.classList.remove('hidden');
    error.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

let startingSession = false;
async function startSession(handler, payload) {
    if (startingSession) return;
    startingSession = true;
    const buttons = document.querySelectorAll('button[onclick^="start"]');
    buttons.forEach(button => { button.disabled = true; button.setAttribute('aria-busy', 'true'); });
    document.getElementById('start-session-error').classList.add('hidden');
    try {
        const response = await fetch(`?handler=${handler}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify(payload)
        });
        const result = await response.json();
        if (!response.ok) throw new Error(result.error || 'Unable to start the session.');
        window.location.assign(result.redirectUrl);
    } catch (error) {
        showStartError(error.message || 'Unable to connect. Please try again.');
    } finally {
        startingSession = false;
        buttons.forEach(button => { button.disabled = false; button.removeAttribute('aria-busy'); });
    }
}
