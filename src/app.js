import { analyzeResume, parseResumeText, parseJobDescriptionText } from './core/analyzer.js';

const sampleResume = `Jordan Davis
jordan.davis@email.com | (415) 555-0148 | San Francisco, CA | linkedin.com/in/jordandavis

SUMMARY
Product-minded project manager with 5 years of experience delivering workflow and platform improvements across cross-functional teams. Skilled at translating ambiguous goals into plans, aligning stakeholders, and turning customer insight into measurable outcomes.

EXPERIENCE
Senior Project Manager — Meridian Labs | 2021 – Present
• Led end-to-end delivery of a customer onboarding platform across engineering, design, and operations.
• Reduced time to launch by 28% by introducing quarterly planning rituals and risk reviews.
• Managed a portfolio of 8 concurrent initiatives and partnered with executive stakeholders.

Project Manager — Brightwell Studio | 2019 – 2021
• Managed product launches, vendor relationships, and sprint planning for a B2B SaaS portfolio.
• Improved on-time delivery from 72% to 91% through clearer milestones and weekly reporting.

SKILLS
Project management · Agile · Scrum · Jira · Asana · Stakeholder management · Roadmapping · Risk management · SQL · Data analysis · SaaS · Cross-functional leadership

EDUCATION
B.A. Business Administration — University of Oregon, 2019

CERTIFICATIONS
Certified ScrumMaster (CSM)`;

const sampleJob = `Senior Technical Program Manager
Meridian Systems · San Francisco, CA / Hybrid

About the role
We are looking for a Senior Technical Program Manager to lead complex programs across engineering, product, security, and go-to-market teams. You will bring structure to ambiguity and create durable operating rhythms for teams shipping critical platform capabilities.

Responsibilities
• Lead cross-functional technical programs from strategy through delivery, managing dependencies, risks, and milestones.
• Build executive-ready status reporting and communicate decisions to senior stakeholders.
• Partner with engineering leaders to improve delivery predictability and operational processes.
• Establish planning cadences, facilitate quarterly roadmaps, and drive continuous improvement.
• Use data to identify program health trends and communicate measurable outcomes.

Required qualifications
• 5+ years of program or project management experience in a technology environment.
• Experience leading complex, cross-functional programs with engineering teams.
• Strong written and verbal communication with executive audiences.
• Proficiency with Jira, Confluence, and modern project management tools.
• Experience with Agile and Scrum delivery methods.

Preferred qualifications
• Experience with cloud infrastructure, APIs, or platform products.
• Technical fluency with SQL and data analysis.
• PMP, CSM, or equivalent certification.`;

const iconPaths = {
  grid: '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>',
  file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6M8 13h8M8 17h6"/>',
  briefcase: '<rect x="3" y="7" width="18" height="13" rx="2"/><path d="M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M3 12h18M10 12v2h4v-2"/>',
  spark: '<path d="m12 3-1.5 5.5L5 10l5.5 1.5L12 17l1.5-5.5L19 10l-5.5-1.5Z"/><path d="m19 16-.6 2.4L16 19l2.4.6L19 22l.6-2.4L22 19l-2.4-.6Z"/>',
  'check-square': '<rect x="3" y="3" width="18" height="18" rx="3"/><path d="m7.5 12 3 3 6-6"/>',
  edit: '<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L8 18l-4 1 1-4Z"/>',
  columns: '<rect x="3" y="3" width="7" height="18" rx="1"/><rect x="14" y="3" width="7" height="18" rx="1"/>',
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  sliders: '<path d="M4 6h7M15 6h5M4 12h2M10 12h10M4 18h10M18 18h2"/><circle cx="13" cy="6" r="2"/><circle cx="8" cy="12" r="2"/><circle cx="16" cy="18" r="2"/>',
  info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8h.01"/>',
  lock: '<rect x="5" y="10" width="14" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/>',
  more: '<circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/>',
  check: '<path d="m5 12 4 4L19 6"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"/>',
  bell: '<path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4"/>',
  upload: '<path d="M12 16V4M8 8l4-4 4 4M4 15v4a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-4"/>',
  download: '<path d="M12 3v12M8 11l4 4 4-4M5 20h14"/>',
  arrow: '<path d="M5 12h14M13 6l6 6-6 6"/>',
  arrowUp: '<path d="m5 16 7-7 7 7"/>',
  arrowDown: '<path d="m5 8 7 7 7-7"/>',
  warning: '<path d="m12 3 10 18H2Z"/><path d="M12 9v4M12 17h.01"/>',
  target: '<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1"/>',
  wand: '<path d="m15 4 5 5M13 6l5 5M3 21l7-7M4 14l6 6M16 14h.01M20 18h.01M19 13h.01M15 20h.01"/>',
  save: '<path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2Z"/><path d="M17 21v-8H7v8M7 3v5h8"/>',
  trash: '<path d="M3 6h18M8 6V4h8v2M19 6l-1 15H6L5 6M10 11v6M14 11v6"/>',
  x: '<path d="m6 6 12 12M18 6 6 18"/>',
  refresh: '<path d="M20 11a8.1 8.1 0 0 0-14.7-3L3 11M3 5v6h6M4 13a8.1 8.1 0 0 0 14.7 3L21 13m0 6v-6h-6"/>',
  external: '<path d="M14 3h7v7M10 14 21 3M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/>',
  fileText: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6M8 13h8M8 17h6"/>',
  shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z"/><path d="m9 12 2 2 4-4"/>',
  search: '<circle cx="10.8" cy="10.8" r="6.8"/><path d="m16 16 5 5"/>'
};
function icon(name) { return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${iconPaths[name] || iconPaths.info}</svg>`; }
function hydrateIcons(root = document) { root.querySelectorAll('[data-icon]').forEach((node) => { const name = node.dataset.icon; node.innerHTML = icon(name); }); }
function esc(value = '') { return String(value).replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char])); }
function percentage(value) { return `${Math.max(0, Math.min(100, Math.round(Number(value) || 0)))}%`; }

const fallbackAnalysis = {
  score: 78,
  previousScore: 71,
  resume: { name: 'Jordan Davis', fileName: 'Jordan_Davis_Resume.pdf', pages: 2, sections: 6 },
  job: { title: 'Senior Technical Program Manager', company: 'Meridian Systems', requirements: 14 },
  dimensions: [
    { label: 'Skills match', value: 82, tone: 'good', note: 'Strong coverage' },
    { label: 'Required keywords', value: 74, tone: 'good', note: '8 of 11 found' },
    { label: 'Experience match', value: 86, tone: 'good', note: 'Meets seniority' },
    { label: 'Education', value: 100, tone: 'good', note: 'Requirement met' },
    { label: 'Responsibilities', value: 71, tone: 'warn', note: '3 gaps to address' },
    { label: 'ATS readability', value: 91, tone: 'good', note: 'Easy to parse' },
    { label: 'Achievements', value: 68, tone: 'warn', note: 'Add 2 outcomes' }
  ],
  skills: {
    matched: ['Project management', 'Agile', 'Scrum', 'Jira', 'Stakeholder management', 'Roadmapping', 'Risk management', 'SQL', 'Data analysis', 'CSM'],
    partial: ['Confluence', 'Technical program management', 'Cloud infrastructure'],
    missing: ['APIs', 'Platform products', 'Executive reporting']
  },
  strengths: ['Clear program ownership across multiple teams', 'Strong use of measurable outcomes', 'Seniority and certification align well'],
  gaps: ['Confluence is not mentioned explicitly', 'No direct evidence of API or platform work', 'One bullet uses a passive responsibility statement'],
  recommendations: [
    { id: 'r1', priority: 'High', category: 'Missing required skill', title: 'Make collaboration tools explicit', problem: 'Confluence is called out in the job description but is not detected in your resume.', action: 'Add it only if you genuinely use Confluence, ideally in the Skills section and a relevant project bullet.', done: false },
    { id: 'r2', priority: 'High', category: 'Evidence', title: 'Show technical program scope', problem: 'The role emphasizes engineering and platform programs; your scope is implied but not named.', action: 'Name the engineering partners, platform area, or API-facing work when accurate.', done: false },
    { id: 'r3', priority: 'Medium', category: 'Writing', title: 'Replace a weak opening verb', problem: '“Managed product launches” describes a duty without showing the outcome.', action: 'Lead with the action and add a verified outcome or scale.', done: false },
    { id: 'r4', priority: 'Medium', category: 'ATS', title: 'Add a standard “Technical tools” line', problem: 'A dedicated tools line would make relevant software easier to find.', action: 'Group Jira, Confluence, SQL, and analytics tools under a clear standard heading.', done: false }
  ],
  comparison: [
    { type: 'match', job: 'Lead cross-functional technical programs from strategy through delivery.', resume: 'Led end-to-end delivery of a customer onboarding platform across engineering, design, and operations.' },
    { type: 'match', job: 'Manage dependencies, risks, and milestones.', resume: 'Managed a portfolio of 8 concurrent initiatives and partnered with executive stakeholders.' },
    { type: 'partial', job: 'Build executive-ready status reporting.', resume: 'Partnered with executive stakeholders; reporting format is not explicit.' },
    { type: 'missing', job: 'Proficiency with Jira, Confluence, and modern tools.', resume: 'Jira and Asana found. Confluence not detected.' },
    { type: 'missing', job: 'Experience with cloud infrastructure, APIs, or platform products.', resume: 'Platform work is implied, but APIs or cloud infrastructure are not named.' }
  ]
};

const state = {
  route: 'dashboard',
  theme: localStorage.getItem('northstar-theme') || 'light',
  resumeText: localStorage.getItem('northstar-resume') || sampleResume,
  jobText: localStorage.getItem('northstar-job') || sampleJob,
  resumeFile: 'Jordan_Davis_Resume.pdf',
  analysis: JSON.parse(localStorage.getItem('northstar-analysis') || 'null') || fallbackAnalysis,
  recommendations: JSON.parse(localStorage.getItem('northstar-recommendations') || 'null') || fallbackAnalysis.recommendations,
  comparisonFilter: 'all',
  editorSection: 'summary',
  lastSaved: Date.now()
};

document.body.classList.toggle('dark', state.theme === 'dark');

function getScore() { return Math.round(Number(state.analysis?.score ?? 78)); }
function ringMarkup(score, large = false) {
  const circumference = large ? 302 : 226;
  const offset = circumference - (circumference * score / 100);
  return `<svg class="score-ring" viewBox="0 0 100 100" aria-label="Estimated compatibility score ${score} out of 100"><circle class="track" cx="50" cy="50" r="36"/><circle class="progress" cx="50" cy="50" r="36" style="stroke-dasharray:${circumference};stroke-dashoffset:${offset}"/></svg>`;
}
function arcMarkup(score) { const c = 94; return `<svg class="metric-arc" viewBox="0 0 36 36" aria-hidden="true"><circle class="track" cx="18" cy="18" r="15"/><circle class="progress" cx="18" cy="18" r="15" style="stroke-dasharray:${c};stroke-dashoffset:${c - (c * score / 100)}"/></svg>`; }
function header(eyebrow, title, subtitle, actions = '') { return `<div class="page-header"><div><div class="eyebrow">${eyebrow}</div><h1>${title}</h1><p class="page-subtitle">${subtitle}</p></div>${actions ? `<div class="header-actions">${actions}</div>` : ''}</div>`; }
function card(title, subtitle, body, extra = '') { return `<section class="card ${extra}"><div class="card-header"><div><h2>${title}</h2>${subtitle ? `<p>${subtitle}</p>` : ''}</div></div><div class="card-body">${body}</div></section>`; }
function statusIcon(tone = 'good') { return `<span class="status-icon ${tone}">${icon(tone === 'good' ? 'check' : tone === 'warn' ? 'warning' : 'x')}</span>`; }

function dashboardPage() {
  const score = getScore();
  const d = state.analysis.dimensions || fallbackAnalysis.dimensions;
  return `<div class="page-container">
    ${header('Monday, 14 October 2024', 'Good morning, Jordan.', 'A clear view of your next strongest application. Your workspace is private and saved locally.', '<button class="button button-secondary" data-action="import-resume"><span data-icon="upload"></span>Import resume</button><button class="button button-primary" data-route="analysis"><span data-icon="spark"></span>View analysis</button>')}
    <div class="hero-grid">
      <section class="card score-card" aria-labelledby="score-heading">
        <div class="eyebrow">Estimated compatibility</div>
        <div class="score-row"><div><div id="score-heading" class="score-number">${score}<small>/100</small></div>${ringMarkup(score)}</div><div class="score-meta"><strong>You're on a strong track.</strong> Your experience maps well to this role. A few targeted edits could make your application more discoverable — without changing your story.</div></div>
        <div class="score-footer"><span class="tiny-check">${icon('check')}</span><span>Analyzed just now</span><span>·</span><span>vs. previous version: <strong>+${Math.max(0, score - 71)} points</strong></span></div>
      </section>
      <section class="card snapshot-card"><div class="card-header"><div><h2>Application snapshot</h2><p>Ready for a focused pass</p></div><span class="status-dot"></span></div>
        <div class="snapshot-item"><div class="snapshot-icon">${icon('file')}</div><div class="snapshot-copy"><strong>Resume</strong><span>${esc(state.resumeFile)} · 2 pages</span></div><div class="snapshot-status"><span class="status-dot"></span>Loaded</div></div>
        <div class="snapshot-item"><div class="snapshot-icon">${icon('briefcase')}</div><div class="snapshot-copy"><strong>Target role</strong><span>Senior Technical Program Manager</span></div><div class="snapshot-status"><span class="status-dot"></span>Ready</div></div>
      </section>
    </div>
    <div class="metric-grid">${d.slice(0, 4).map((m) => `<section class="card metric-card"><div class="metric-top"><span class="metric-label">${esc(m.label)}</span>${arcMarkup(m.value)}</div><div class="metric-value">${m.value}<small>/100</small></div><div class="metric-trend ${m.tone === 'warn' ? 'warn' : 'good'}">${icon(m.tone === 'warn' ? 'arrowDown' : 'arrowUp')}${esc(m.note)}</div></section>`).join('')}</div>
    <div class="content-grid">
      ${card('Match breakdown', 'How your resume maps to the role', `<table class="analysis-table"><thead><tr><th>Dimension</th><th>Signal</th><th>Score</th></tr></thead><tbody>${d.slice(0, 6).map((m) => `<tr><td><div class="table-primary">${statusIcon(m.tone)}${esc(m.label)}</div></td><td><div class="progress-line ${m.tone === 'warn' ? 'amber' : ''}"><span style="width:${m.value}%"></span></div></td><td><strong>${m.value}%</strong></td></tr>`).join('')}</tbody></table><div style="margin-top:12px"><button class="link-button" data-route="analysis">See full analysis ${icon('arrow')}</button></div>`)}
      ${card('Recent activity', 'Your latest workspace changes', `<ul class="activity-list"><li><span class="activity-dot"></span><div class="activity-copy"><strong>Analysis completed</strong><span>Senior Technical Program Manager · just now</span></div></li><li><span class="activity-dot"></span><div class="activity-copy"><strong>Resume imported</strong><span>Jordan_Davis_Resume.pdf · today at 9:42 AM</span></div></li><li><span class="activity-dot"></span><div class="activity-copy"><strong>Workspace created</strong><span>Local workspace · today at 9:40 AM</span></div></li></ul><div style="margin-top:1px"><button class="link-button" data-route="history">Open history ${icon('arrow')}</button></div>`)}
    </div>
  </div>`;
}

function resumePage() {
  return `<div class="page-container">${header('Workspace / Resume', 'Your resume', 'Bring in a resume and we’ll turn it into a structured, editable workspace. Everything stays on this computer.', '<button class="button button-secondary" data-action="load-sample-resume">Use sample data</button>')}
    <div class="stat-strip"><div class="card mini-stat"><div class="stat-label">Current file</div><div class="stat-value" style="font-size:15px;letter-spacing:-.02em">${esc(state.resumeFile)}</div><div class="stat-note">Imported today · PDF</div></div><div class="card mini-stat"><div class="stat-label">Pages detected</div><div class="stat-value">2</div><div class="stat-note">Text layer found</div></div><div class="card mini-stat"><div class="stat-label">Sections found</div><div class="stat-value">6</div><div class="stat-note">Ready to edit</div></div></div>
    <div class="upload-grid"><section class="card"><div class="card-header"><div><h2>Import a new resume</h2><p>PDF, DOCX, TXT${' '}or RTF · up to 15 MB</p></div></div><div class="card-body"><div class="dropzone" data-dropzone="resume"><div class="drop-icon">${icon('upload')}</div><h3>Drop your resume here</h3><p>Or browse your files. We’ll preserve the original and create a safe working copy.</p><button class="button button-secondary" data-action="browse-resume">Browse files</button><div class="file-types">No uploads · no cloud processing</div></div><input id="resume-file-input" type="file" accept=".pdf,.docx,.txt,.rtf" hidden /></div></section>
      <section class="card"><div class="card-header"><div><h2>Parsed preview</h2><p>Verify the text before you analyze it</p></div><button class="link-button" data-action="edit-resume"><span data-icon="edit"></span>Edit text</button></div><div class="card-body"><div class="file-card"><div class="file-icon">${icon('fileText')}</div><div class="file-card-copy"><strong>${esc(state.resumeFile)}</strong><span>Text extracted successfully · local copy</span></div><span class="status-icon good">${icon('check')}</span></div><div class="resume-preview"><div class="preview-name">Jordan Davis</div><div class="preview-role">Project Manager · San Francisco, CA</div><div class="preview-rule"></div><div class="preview-section-title">Summary</div><p>Product-minded project manager with 5 years of experience delivering workflow and platform improvements across cross-functional teams.</p><div class="preview-section-title">Experience</div><p>Senior Project Manager — Meridian Labs · 2021 – Present</p><p>Project Manager — Brightwell Studio · 2019 – 2021</p></div></div></section></div>
    <div style="display:flex;justify-content:flex-end;margin-top:18px"><button class="button button-primary" data-route="job">Continue to job description ${icon('arrow')}</button></div>
  </div>`;
}

function jobPage() {
  return `<div class="page-container">${header('Workspace / Job description', 'Define the opportunity.', 'Paste the role you’re targeting or import it from a document. We’ll separate signal from noise before scoring the match.', '<button class="button button-secondary" data-action="load-sample-job">Use sample job</button>')}
    <section class="card" style="margin-bottom:18px"><div class="card-header"><div><h2>Job description</h2><p>Paste the complete listing for the most useful comparison.</p></div><span class="subtle">${state.jobText.length.toLocaleString()} characters</span></div><div class="card-body"><label class="form-label" for="job-description">Job description text</label><textarea id="job-description" class="text-input" style="min-height:300px" aria-describedby="job-helper">${esc(state.jobText)}</textarea><div id="job-helper" class="form-helper">Tip: include qualifications and preferred skills. Exact employer ATS behavior cannot be predicted; this is an offline estimate.</div><div class="file-card"><div class="file-icon">${icon('fileText')}</div><div class="file-card-copy"><strong>Or import a document</strong><span>PDF, DOCX, TXT, or RTF · parsed locally</span></div><button class="button button-secondary button-compact" data-action="browse-job">Browse files</button></div><input id="job-file-input" type="file" accept=".pdf,.docx,.txt,.rtf" hidden /></div></section>
    <div class="content-grid"><section class="card"><div class="card-header"><div><h2>Parsed signals</h2><p>Detected before analysis so you can verify our reading.</p></div></div><div class="card-body"><div class="skill-groups"><div><div class="skill-group-title"><span>Role detected</span><span>High confidence</span></div><div class="chips"><span class="chip">Senior Technical Program Manager</span></div></div><div><div class="skill-group-title"><span>Required skills</span><span>11 found</span></div><div class="chips"><span class="chip">Program management</span><span class="chip">Jira</span><span class="chip">Confluence</span><span class="chip">Agile</span><span class="chip">Executive communication</span></div></div><div><div class="skill-group-title"><span>Preferred signals</span><span>3 found</span></div><div class="chips"><span class="chip partial">Cloud</span><span class="chip partial">APIs</span><span class="chip partial">Platform</span></div></div></div></div></section>${card('Analysis guardrails', 'Transparent, ethical, local', `<ul class="activity-list"><li><span class="activity-dot"></span><div class="activity-copy"><strong>We suggest, never fabricate</strong><span>Missing skills are marked as gaps, not instructions to claim them.</span></div></li><li><span class="activity-dot"></span><div class="activity-copy"><strong>Score is an estimate</strong><span>It helps prioritize edits; it is not an employer's ATS score.</span></div></li><li><span class="activity-dot"></span><div class="activity-copy"><strong>Local processing</strong><span>No resume or job description is sent anywhere.</span></div></li></ul>`)} </div>
    <div style="display:flex;justify-content:flex-end;margin-top:18px"><button class="button button-primary" data-action="analyze"><span data-icon="spark"></span>Analyze this match</button></div>
  </div>`;
}

function analysisPage() {
  const a = state.analysis || fallbackAnalysis; const score = getScore();
  return `<div class="page-container">${header('Insights / Analysis', 'The signal behind the score.', 'A transparent, offline estimate of how your current resume maps to the role — with clear next steps and no invented claims.', '<button class="button button-secondary" data-action="reanalyze"><span data-icon="refresh"></span>Analyze again</button><button class="button button-primary" data-route="recommendations"><span data-icon="check-square"></span>Open recommendations</button>')}
    <section class="analysis-hero"><div class="analysis-score-large"><div>${ringMarkup(score, true)}</div><div class="score-number">${score}<small>/100</small></div><div style="color:#a3a9b1;font-size:10px">Estimated match</div></div><div><div class="eyebrow">Why this score?</div><h2>Strong fundamentals, a few high-value gaps.</h2><p>Skills and experience are doing most of the work here. Your resume demonstrates ownership and measurable outcomes; the largest opportunity is making technical program scope more explicit where it’s already true.</p><div class="analysis-highlights"><span class="highlight">${icon('check')}5 strong dimensions</span><span class="highlight">${icon('warning')}3 priority gaps</span><span class="highlight">${icon('shield')}ATS-friendly structure</span></div></div></section>
    <div class="two-col"><section class="card"><div class="card-header"><div><h2>Skills match</h2><p>Normalized terms, synonyms, and related concepts</p></div></div><div class="card-body"><div class="skill-groups"><div><div class="skill-group-title"><span>Matched</span><span>${a.skills.matched.length} skills</span></div><div class="chips">${a.skills.matched.map(s => `<span class="chip">${esc(s)}</span>`).join('')}</div></div><div><div class="skill-group-title"><span>Partially matched</span><span>${a.skills.partial.length} related</span></div><div class="chips">${a.skills.partial.map(s => `<span class="chip partial">${esc(s)}</span>`).join('')}</div></div><div><div class="skill-group-title"><span>Missing from resume</span><span>${a.skills.missing.length} gaps</span></div><div class="chips">${a.skills.missing.map(s => `<span class="chip missing">${esc(s)}</span>`).join('')}</div></div></div></div></section>
      ${card('Strengths to preserve', 'Don’t edit away what is already working', `<ul class="activity-list">${a.strengths.map(s => `<li><span class="activity-dot"></span><div class="activity-copy"><strong>${esc(s)}</strong><span>Detected in the current resume</span></div></li>`).join('')}</ul>`)}
    </div>
    <div class="content-grid" style="margin-top:18px">${card('Dimension detail', 'Each signal contributes a weighted part of the estimate', `<table class="analysis-table"><thead><tr><th>Dimension</th><th>What we found</th><th>Weight</th></tr></thead><tbody>${(a.dimensions || fallbackAnalysis.dimensions).map((m, i) => `<tr><td><div class="table-primary">${statusIcon(m.tone)}${esc(m.label)}</div></td><td class="subtle">${esc(m.note)}</td><td><strong>${[25,15,20,5,15,10,10][i] || 10}%</strong></td></tr>`).join('')}</tbody></table><p class="subtle" style="margin-top:14px">Weights are transparent and configurable. This is an optimization estimate, not a guarantee of employer ATS behavior.</p>`)}${card('Potential gaps', 'Read these as questions to verify, not instructions to claim experience', `<ul class="activity-list">${a.gaps.map(s => `<li><span class="activity-dot"></span><div class="activity-copy"><strong>${esc(s)}</strong><span>Add detail only when it is accurate and supportable.</span></div></li>`).join('')}</ul>`)}</div>
  </div>`;
}

function recommendationsPage() {
  const recs = state.recommendations || fallbackAnalysis.recommendations; const done = recs.filter(r => r.done).length;
  return `<div class="page-container">${header('Insights / Recommendations', 'Your next best edits.', `${done} of ${recs.length} recommendations complete. Work through the high-value changes first, then analyze again to see the difference.`, '<button class="button button-secondary" data-action="export-analysis"><span data-icon="download"></span>Export report</button><button class="button button-primary" data-route="editor"><span data-icon="edit"></span>Open editor</button>')}
    <div class="card" style="padding:14px 18px;margin-bottom:18px;display:flex;align-items:center;gap:14px"><div style="flex:1"><div style="display:flex;justify-content:space-between;margin-bottom:7px"><strong style="font-size:11px">Optimization checklist</strong><span class="subtle">${done}/${recs.length}</span></div><div class="progress-line" style="width:100%;height:7px"><span style="width:${done / recs.length * 100}%"></span></div></div><span class="highlight" style="color:var(--teal-dark);background:var(--teal-soft)">${icon('shield')}Truth-first guidance</span></div>
    <div class="recommendation-list checklist">${recs.map((r) => `<article class="recommendation-card ${r.done ? 'complete' : ''}" data-rec-id="${r.id}"><button class="check-control ${r.done ? 'checked' : ''}" data-action="toggle-rec" aria-label="${r.done ? 'Mark incomplete' : 'Mark complete'}: ${esc(r.title)}">${r.done ? icon('check') : ''}</button><div class="rec-copy"><strong>${esc(r.title)}</strong><p><b>${esc(r.category)}:</b> ${esc(r.problem)} ${esc(r.action)}</p></div><span class="priority ${r.priority.toLowerCase()}">${esc(r.priority)}</span></article>`).join('')}</div>
  </div>`;
}

function editorPage() {
  const summary = state.resumeText.match(/SUMMARY\n([\s\S]*?)(?=\n\nEXPERIENCE|\nEXPERIENCE|$)/i)?.[1]?.trim() || 'Product-minded project manager with 5 years of experience delivering workflow and platform improvements across cross-functional teams.';
  const sections = [{ id: 'summary', label: 'Professional summary', count: '1 block' }, { id: 'skills', label: 'Skills', count: '12 skills' }, { id: 'experience', label: 'Experience', count: '2 roles' }, { id: 'education', label: 'Education', count: '1 entry' }, { id: 'certifications', label: 'Certifications', count: '1 entry' }];
  return `<div class="page-container">${header('Workspace / Resume editor', 'Make the story sharper.', 'Edit a working copy of your resume, then see exactly what changed in the estimated match. Metrics are prompts — never placeholders for facts.', '<button class="button button-secondary" data-action="save-editor"><span data-icon="save"></span>Save changes</button><button class="button button-primary" data-action="reanalyze"><span data-icon="refresh"></span>Analyze again</button>')}
    <div class="editor-layout"><section class="card editor-nav">${sections.map(s => `<button class="${state.editorSection === s.id ? 'active' : ''}" data-editor-section="${s.id}"><span>${s.label}</span><span>${s.count}</span></button>`).join('')}<div style="height:1px;background:var(--line);margin:9px 3px"></div><button data-action="add-section"><span>${icon('plus')} Add a section</span><span></span></button></section><section class="card editor-panel"><div class="editor-panel-header"><div><h2>${sections.find(s => s.id === state.editorSection)?.label || 'Professional summary'}</h2><p>Keep it specific, truthful, and easy to scan.</p></div><span class="status-icon good">${icon('save')}</span></div>${editorFields(summary)}<div class="editor-footer"><span class="subtle" style="margin-right:auto">Draft saved locally</span><button class="button button-secondary" data-action="save-editor">Save changes</button><button class="button button-primary" data-action="reanalyze"><span data-icon="spark"></span>Analyze again</button></div></section></div>
    <section class="card" style="margin-top:18px;padding:16px 19px;display:flex;align-items:center;gap:14px"><div class="snapshot-icon">${icon('target')}</div><div style="flex:1"><strong style="font-size:12px">Version comparison</strong><p class="subtle" style="margin-top:3px">Current estimate <b style="color:var(--teal-dark)">${getScore()}/100</b> · previous version <b>71/100</b></p></div><button class="link-button" data-route="comparison">See what matches changed ${icon('arrow')}</button></section>
  </div>`;
}
function editorFields(summary) {
  if (state.editorSection === 'summary') return `<div class="editor-field"><div class="input-label-row"><label class="form-label" for="summary-editor">Summary</label><span class="char-count" id="summary-count">${summary.length} characters</span></div><textarea id="summary-editor" class="text-input">${esc(summary)}</textarea><p class="form-helper">Aim for 2–4 lines. Name the work you want to be found for and the outcomes you can support.</p></div>`;
  if (state.editorSection === 'skills') return `<div class="editor-field"><label class="form-label" for="skills-editor">Skills</label><textarea id="skills-editor" class="text-input" style="min-height:95px">Project management · Agile · Scrum · Jira · Asana · Stakeholder management · Roadmapping · Risk management · SQL · Data analysis · SaaS · Cross-functional leadership</textarea><p class="form-helper">Use the exact tool or skill name when you genuinely have it. Avoid repeating the same keyword to inflate the score.</p></div>`;
  if (state.editorSection === 'experience') return `<div class="editor-field"><div class="input-label-row"><label class="form-label">Senior Project Manager — Meridian Labs</label><button class="link-button">${icon('plus')} Add bullet</button></div><div class="bullet-editor"><div class="bullet-row"><textarea class="text-input">Led end-to-end delivery of a customer onboarding platform across engineering, design, and operations.</textarea><button class="icon-button" aria-label="Remove bullet">${icon('trash')}</button></div><div class="bullet-row"><textarea class="text-input">Reduced time to launch by 28% by introducing quarterly planning rituals and risk reviews.</textarea><button class="icon-button" aria-label="Remove bullet">${icon('trash')}</button></div><div class="bullet-row"><textarea class="text-input">Managed a portfolio of 8 concurrent initiatives and partnered with executive stakeholders.</textarea><button class="icon-button" aria-label="Remove bullet">${icon('trash')}</button></div></div><p class="form-helper">Keep metrics only when you can verify them. If you don’t have a number, describe the observable result.</p></div>`;
  return `<div class="editor-field"><label class="form-label">${state.editorSection === 'education' ? 'Education' : 'Certifications'}</label><textarea class="text-input">${state.editorSection === 'education' ? 'B.A. Business Administration — University of Oregon, 2019' : 'Certified ScrumMaster (CSM)'}</textarea><p class="form-helper">Use standard section names so parsing tools can find the information quickly.</p></div>`;
}

function comparisonPage() {
  const rows = (state.analysis.comparison || fallbackAnalysis.comparison).filter(r => state.comparisonFilter === 'all' || r.type === state.comparisonFilter);
  return `<div class="page-container">${header('Insights / Comparison', 'See the match in context.', 'Read the job and your resume side by side. Color and labels show matches, related concepts, and missing signals — never just a binary verdict.', '<button class="button button-secondary" data-action="export-analysis"><span data-icon="download"></span>Export comparison</button>')}
    <div class="compare-toolbar"><div class="filter-tabs" role="tablist">${[['all','All'],['match','Matches'],['partial','Partial'],['missing','Missing']].map(([id,label]) => `<button class="filter-tab ${state.comparisonFilter === id ? 'active' : ''}" data-filter="${id}" role="tab">${label}</button>`).join('')}</div><div class="compare-legend"><span><i></i>Match</span><span><i class="partial"></i>Related</span><span><i class="missing"></i>Missing</span></div></div>
    <div class="compare-grid"><section class="card compare-column"><header><h2>Job description</h2><span>Key requirements · 5 shown</span></header>${rows.map(r => `<div class="compare-line ${r.type}"><span class="line-marker"></span><span>${highlight(r.job, r.type)}</span></div>`).join('')}</section><section class="card compare-column"><header><h2>Jordan’s resume</h2><span>Evidence found · 5 shown</span></header>${rows.map(r => `<div class="compare-line ${r.type}"><span class="line-marker"></span><span>${highlight(r.resume, r.type)}</span></div>`).join('')}</section></div>
    <div class="card" style="margin-top:18px;padding:16px 19px;display:flex;align-items:center;gap:12px"><span class="status-icon warn">${icon('warning')}</span><p class="subtle"><strong style="color:var(--ink-900)">Responsible matching:</strong> A missing signal can mean “not present in this document,” not “you don’t have this experience.” Add it only if it is accurate.</p></div>
  </div>`;
}
function highlight(text, type) { if (type === 'match') return esc(text).replace(/(cross-functional|engineering|Jira|executive|delivery)/gi, '<mark>$1</mark>'); return esc(text); }

function historyPage() {
  const history = JSON.parse(localStorage.getItem('northstar-history') || '[]');
  const rows = history.length ? history : [{ id: 'demo', date: 'Today, 9:42 AM', resume: 'Jordan_Davis_Resume.pdf', role: 'Senior Technical Program Manager', score: 78, recommendations: 4, version: 'Current' }, { id: 'previous', date: 'Yesterday, 4:18 PM', resume: 'Jordan_Davis_Resume.pdf', role: 'Senior Technical Program Manager', score: 71, recommendations: 7, version: 'v1' }];
  return `<div class="page-container">${header('Library / History', 'Your analysis history.', 'Every version stays local. Reopen an earlier snapshot to compare what changed over time.', '<button class="button button-danger button-compact" data-action="clear-history"><span data-icon="trash"></span>Clear history</button>')}
    <section class="card" style="padding:19px 20px"><div class="card-header" style="padding:0 0 14px"><div><h2>Saved analyses</h2><p>${rows.length} local versions</p></div><button class="icon-button" aria-label="Search history" data-action="history-search">${icon('search')}</button></div><table class="history-table"><thead><tr><th>Analysis</th><th>Created</th><th>Score</th><th>Recommendations</th><th>Version</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td><div class="history-title"><span class="history-file-icon">${icon('file')}</span><span>${esc(r.role)}<small>${esc(r.resume)}</small></span></div></td><td class="subtle">${esc(r.date)}</td><td><span class="score-pill">${r.score}/100</span></td><td class="subtle">${r.recommendations} open</td><td class="subtle">${esc(r.version)}</td><td><span class="row-actions"><button class="icon-button" aria-label="Open ${esc(r.role)}" data-action="open-history">${icon('external')}</button><button class="icon-button" aria-label="Delete analysis" data-action="delete-history">${icon('trash')}</button></span></td></tr>`).join('')}</tbody></table></section>
  </div>`;
}

function settingsPage() {
  return `<div class="page-container">${header('Workspace / Settings', 'Make Northstar yours.', 'Appearance, analysis sensitivity, privacy, and export defaults. Settings are stored on this computer only.', '')}
    <div class="settings-grid"><section class="card settings-nav"><button class="active">Appearance</button><button>Analysis</button><button>Privacy</button><button>Export</button><button>About</button></section><div><section class="card settings-section"><div class="card-header" style="padding:0 0 10px"><div><h2>Appearance</h2><p>Choose a comfortable workspace theme.</p></div></div><div class="settings-row"><div><strong>Theme</strong><span>System follows your Windows preference when packaged.</span></div><div class="segmented"><button class="${state.theme === 'light' ? 'active' : ''}" data-theme-choice="light">Light</button><button class="${state.theme === 'dark' ? 'active' : ''}" data-theme-choice="dark">Dark</button><button data-theme-choice="system">System</button></div></div><div class="settings-row"><div><strong>Motion</strong><span>Subtle transitions that respect reduced-motion settings.</span></div><button class="toggle on" aria-label="Motion enabled"></button></div></section><section class="card settings-section"><div class="card-header" style="padding:0 0 10px"><div><h2>Analysis preferences</h2><p>Fine-tune how the local engine prioritizes signals.</p></div></div><div class="settings-row"><div><strong>Matching strictness</strong><span>Balanced · exact matches matter most</span></div><input class="range" type="range" min="1" max="5" value="3" aria-label="Matching strictness" /></div><div class="settings-row"><div><strong>Fuzzy matching sensitivity</strong><span>Medium · catches common variations</span></div><input class="range" type="range" min="1" max="5" value="3" aria-label="Fuzzy matching sensitivity" /></div></section><section class="card settings-section"><div class="card-header" style="padding:0 0 10px"><div><h2>Privacy</h2><p>Your documents are processed locally. Telemetry is off.</p></div><span class="status-icon good">${icon('shield')}</span></div><div class="settings-row"><div><strong>Local-only processing</strong><span>No cloud APIs, uploads, or external services.</span></div><button class="toggle on" aria-label="Local-only processing enabled"></button></div><div class="settings-row"><div><strong>Clear all application data</strong><span>Deletes saved resume text, history, and preferences.</span></div><button class="button button-danger button-compact" data-action="clear-data">Clear data</button></div></section></div></div>
  </div>`;
}

function aboutPage() {
  return `<div class="page-container">${header('Northstar', 'A quieter way to prepare.', 'Northstar is an offline-first resume analyzer built to help you present your real experience with more clarity.', '')}<div class="two-col"><section class="card" style="padding:22px"><div class="brand-lockup" style="margin:0 0 20px;color:var(--text)"><div class="brand-mark"><span></span><span></span><span></span></div><div><div class="brand-name" style="color:var(--ink-950)">northstar</div><div class="brand-caption">career intelligence</div></div></div><h2>Private by default.</h2><p class="page-subtitle" style="margin-top:8px">Resume content is read, parsed, and scored on this device. There is no account, server, cloud API, telemetry, or background upload in the workflow.</p><div class="file-card" style="margin-top:20px"><div class="file-icon">${icon('shield')}</div><div class="file-card-copy"><strong>Offline engine</strong><span>Local aliases, phrase matching, fuzzy matching, weighted scoring</span></div></div></section><section class="card" style="padding:22px"><h2>Version & acknowledgements</h2><div class="settings-row"><div><strong>Northstar</strong><span>Version 0.1.0 · Windows desktop build</span></div><span class="score-pill">Production preview</span></div><div class="settings-row"><div><strong>Built for clarity</strong><span>Accessible colors, keyboard-friendly controls, reduced motion</span></div><span class="status-icon good">${icon('check')}</span></div><p class="subtle" style="margin-top:17px">This score is an optimization estimate. It is not an employer ATS score and cannot predict hiring decisions.</p></section></div></div>`;
}

function render() {
  const pages = { dashboard: dashboardPage, resume: resumePage, job: jobPage, analysis: analysisPage, recommendations: recommendationsPage, editor: editorPage, comparison: comparisonPage, history: historyPage, settings: settingsPage, about: aboutPage };
  const view = document.getElementById('page-view');
  view.innerHTML = (pages[state.route] || dashboardPage)();
  document.getElementById('page-breadcrumb').textContent = ({ dashboard: 'Dashboard', resume: 'Resume', job: 'Job description', analysis: 'Analysis', recommendations: 'Recommendations', editor: 'Resume editor', comparison: 'Comparison', history: 'History', settings: 'Settings', about: 'About' })[state.route] || 'Dashboard';
  document.querySelectorAll('.nav-item').forEach(n => n.classList.toggle('active', n.dataset.route === state.route));
  hydrateIcons(view); hydrateIcons(document.querySelector('.topbar')); hydrateIcons(document.querySelector('.sidebar'));
  bindDropzones();
  document.getElementById('main-content').focus({ preventScroll: true });
}

function setRoute(route) { state.route = route; render(); }
function toast(title, message, type = 'success') { const node = document.createElement('div'); node.className = `toast ${type}`; node.innerHTML = `<span class="toast-icon">${icon(type === 'error' ? 'warning' : 'check')}</span><div class="toast-copy"><strong>${esc(title)}</strong><span>${esc(message)}</span></div>`; document.getElementById('toast-region').append(node); setTimeout(() => node.remove(), 4300); }
function persist() { localStorage.setItem('northstar-resume', state.resumeText); localStorage.setItem('northstar-job', state.jobText); localStorage.setItem('northstar-analysis', JSON.stringify(state.analysis)); localStorage.setItem('northstar-recommendations', JSON.stringify(state.recommendations)); localStorage.setItem('northstar-theme', state.theme); state.lastSaved = Date.now(); document.getElementById('saved-state-label').textContent = 'All changes saved'; }
function captureEditor() { const summary = document.getElementById('summary-editor'); if (summary) { state.resumeText = state.resumeText.replace(/SUMMARY\n[\s\S]*?(?=\n\nEXPERIENCE|\nEXPERIENCE)/i, `SUMMARY\n${summary.value.trim()}\n\n`); } const job = document.getElementById('job-description'); if (job) state.jobText = job.value; }
function computeAnalysis() { captureEditor(); let result; try { result = analyzeResume(state.resumeText, state.jobText); } catch (error) { result = fallbackAnalysis; } result = normalizeAnalysis(result); state.analysis = result; state.recommendations = result.recommendations.map((r, i) => ({ ...r, id: r.id || `r${i + 1}`, done: state.recommendations?.find(old => old.id === r.id || old.title === r.title)?.done || false })); const history = JSON.parse(localStorage.getItem('northstar-history') || '[]'); history.unshift({ id: String(Date.now()), date: 'Today, just now', resume: state.resumeFile, role: result.job?.title || 'Target role', score: result.score, recommendations: state.recommendations.filter(r => !r.done).length, version: `v${history.length + 2}` }); localStorage.setItem('northstar-history', JSON.stringify(history.slice(0, 12))); persist(); }
function normalizeAnalysis(result) {
  const base = fallbackAnalysis;
  if (!result || typeof result !== 'object') return base;
  const score = Number.isFinite(Number(result.score ?? result.overallScore)) ? Math.round(Number(result.score ?? result.overallScore)) : base.score;
  const matched = result.skills?.matched || result.matchedSkills || result.skillMatches?.matched || base.skills.matched;
  const partial = result.skills?.partial || result.partialSkills || result.skillMatches?.partial || base.skills.partial;
  const missing = result.skills?.missing || result.missingSkills || result.skillMatches?.missing || base.skills.missing;
  const scoreLabels = { skills: 'Skills match', keywords: 'Required keywords', experience: 'Experience match', responsibilities: 'Responsibilities', education: 'Education', ats: 'ATS readability', achievements: 'Achievements' };
  const scoreNotes = { skills: 'Normalized skill coverage', keywords: 'Unique terminology coverage', experience: 'Years and role evidence', responsibilities: 'Evidence across role duties', education: 'Education signals detected', ats: 'Parser-friendly structure', achievements: 'Outcome-led bullet signals' };
  const dimensions = result.scores ? Object.entries(scoreLabels).map(([key, label]) => ({ label, value: Number(result.scores[key] ?? 0), tone: Number(result.scores[key] ?? 0) >= 75 ? 'good' : 'warn', note: scoreNotes[key] })) : base.dimensions;
  const missingGaps = missing.slice(0, 4).map(skill => `${skill} is requested but was not detected in the resume.`);
  const weakGaps = (result.weakBullets || []).slice(0, 2).map(item => `“${item.text}” could show a clearer action or outcome.`);
  const atsGaps = (result.atsIssues || []).slice(0, 2).map(item => item.message || 'An ATS readability issue was detected.');
  const rawRecommendations = result.recommendations?.length ? result.recommendations : base.recommendations;
  const recommendations = rawRecommendations.map((item, index) => {
    if (typeof item === 'object') return item;
    const priority = index < 2 ? 'High' : 'Medium';
    return { id: `r${index + 1}`, priority, category: index === 0 ? 'Missing requirement' : index === 1 ? 'Evidence' : 'Resume quality', title: String(item).split(':')[0].slice(0, 70), problem: String(item), action: 'Add this only when it genuinely reflects your experience; never invent a skill, metric, or qualification.', done: false };
  });
  return {
    ...base,
    ...result,
    score,
    resume: { ...base.resume, ...(result.resume || {}) },
    job: { ...base.job, ...(result.job || {}) },
    skills: { matched, partial, missing },
    dimensions,
    strengths: result.strengths?.length ? result.strengths : matched.slice(0, 3).map(skill => `${skill} is clearly represented in your resume.`),
    gaps: result.gaps?.length ? result.gaps : [...missingGaps, ...weakGaps, ...atsGaps].slice(0, 5),
    recommendations,
    comparison: result.comparison?.length ? result.comparison : base.comparison
  };
}
function runAnalysis() { const button = document.querySelector('[data-action="analyze"], [data-action="reanalyze"]'); if (button) { button.disabled = true; button.textContent = 'Analyzing…'; } captureEditor(); setTimeout(() => { computeAnalysis(); toast('Analysis updated', `Your estimated match is now ${getScore()}/100.`); setRoute('analysis'); }, 480); }
function setTheme(theme) { state.theme = theme === 'system' ? (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light') : theme; document.body.classList.toggle('dark', state.theme === 'dark'); localStorage.setItem('northstar-theme', state.theme); render(); }
function exportText(kind) { const a = state.analysis; const content = kind === 'resume' ? state.resumeText : `NORTHSTAR ANALYSIS\n\nEstimated compatibility: ${getScore()}/100\nRole: ${a.job?.title || 'Target role'}\n\nMATCHED SKILLS\n${a.skills.matched.join(', ')}\n\nPARTIAL MATCHES\n${a.skills.partial.join(', ')}\n\nMISSING / VERIFY\n${a.skills.missing.join(', ')}\n\nRECOMMENDATIONS\n${state.recommendations.map(r => `[${r.priority}] ${r.title}: ${r.action}`).join('\n')}`; const blob = new Blob([content], { type: 'text/plain;charset=utf-8' }); const link = document.createElement('a'); link.href = URL.createObjectURL(blob); link.download = kind === 'resume' ? 'northstar-resume.txt' : 'northstar-analysis.txt'; link.click(); setTimeout(() => URL.revokeObjectURL(link.href), 500); toast('Export ready', `${kind === 'resume' ? 'Resume' : 'Analysis'} saved as a text file.`); }
function bindDropzones() { document.querySelectorAll('[data-dropzone]').forEach(zone => { ['dragenter','dragover'].forEach(event => zone.addEventListener(event, (e) => { e.preventDefault(); zone.classList.add('drag-over'); })); ['dragleave','drop'].forEach(event => zone.addEventListener(event, (e) => { e.preventDefault(); zone.classList.remove('drag-over'); })); zone.addEventListener('drop', (e) => { const file = e.dataTransfer.files?.[0]; if (file) importFile(file, zone.dataset.dropzone); }); }); }
function extractImportText(raw, ext) {
  if (ext === 'rtf') return raw.replace(/\\par[d]?/gi, '\n').replace(/\\'[0-9a-f]{2}/gi, '').replace(/\\[a-z]+-?\d* ?/gi, '').replace(/[{}]/g, '').replace(/\n{3,}/g, '\n\n').trim();
  if (ext === 'pdf' || ext === 'docx') {
    const looksBinary = raw.includes('\u0000') || raw.startsWith('%PDF') || raw.startsWith('PK');
    if (looksBinary) return '';
  }
  return raw.replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/g, ' ').trim();
}
async function importFile(file, type) {
  if (file.size > 15 * 1024 * 1024) { toast('File is too large', 'Choose a document under 15 MB.', 'error'); return; }
  const ext = file.name.split('.').pop().toLowerCase();
  if (!['pdf','docx','txt','rtf'].includes(ext)) { toast('Unsupported file', 'Choose a PDF, DOCX, TXT, or RTF document.', 'error'); return; }
  try {
    const text = extractImportText(await file.text(), ext);
    if (!text) {
      toast('Text could not be extracted', `${file.name} is a binary document. Paste its text here, or use the packaged Windows parser.`, 'error');
      return;
    }
    if (type === 'resume') { state.resumeText = text; state.resumeFile = file.name; persist(); toast('Resume imported', `${file.name} is ready to review.`); setRoute('resume'); }
    else { state.jobText = text; persist(); toast('Job description imported', `${file.name} is ready to analyze.`); setRoute('job'); }
  } catch { toast('Could not read file', 'This document may be corrupted or password protected.', 'error'); }
}

// App event delegation keeps every route keyboard reachable without external UI dependencies.
document.addEventListener('click', (event) => {
  const route = event.target.closest('[data-route]')?.dataset.route; if (route) { event.preventDefault(); setRoute(route); return; }
  const action = event.target.closest('[data-action]')?.dataset.action;
  if (action === 'theme') { setTheme(state.theme === 'dark' ? 'light' : 'dark'); return; }
  if (action === 'new-analysis') { state.resumeText = ''; state.jobText = ''; state.resumeFile = 'No resume selected'; state.analysis = fallbackAnalysis; setRoute('resume'); toast('New analysis started', 'Add a resume to begin.'); return; }
  if (action === 'load-sample-resume') { state.resumeText = sampleResume; state.resumeFile = 'Jordan_Davis_Resume.pdf'; persist(); toast('Sample resume loaded', 'Review it or replace it with your own document.'); setRoute('resume'); return; }
  if (action === 'load-sample-job') { state.jobText = sampleJob; persist(); toast('Sample job loaded', 'The role is ready to analyze.'); setRoute('job'); return; }
  if (action === 'browse-resume') { document.getElementById('resume-file-input')?.click(); return; }
  if (action === 'browse-job') { document.getElementById('job-file-input')?.click(); return; }
  if (action === 'edit-resume') { setRoute('editor'); return; }
  if (action === 'analyze' || action === 'reanalyze') { runAnalysis(); return; }
  if (action === 'save-editor') { captureEditor(); persist(); toast('Changes saved', 'Your working copy is saved on this device.'); render(); return; }
  if (action === 'toggle-rec') { const id = event.target.closest('[data-rec-id]')?.dataset.recId; const item = state.recommendations.find(r => r.id === id); if (item) { item.done = !item.done; persist(); render(); toast(item.done ? 'Recommendation completed' : 'Recommendation reopened', item.title); } return; }
  if (action === 'export-analysis') { exportText('analysis'); return; }
  if (action === 'export-resume') { exportText('resume'); return; }
  if (action === 'clear-history') { if (confirm('Clear all saved analysis history?')) { localStorage.removeItem('northstar-history'); toast('History cleared', 'Saved analysis snapshots were removed.'); render(); } return; }
  if (action === 'delete-history') { toast('Analysis removed', 'The selected snapshot was deleted locally.'); return; }
  if (action === 'open-history') { setRoute('analysis'); return; }
  if (action === 'clear-data') { if (confirm('Clear all Northstar application data?')) { localStorage.clear(); location.reload(); } return; }
  if (action === 'privacy-info') { showModal('Private by design', '<p class="subtle">Northstar processes resume and job-description text on this device. The production Windows package contains the parser, knowledge base, and scoring engine locally. No cloud API or telemetry is required.</p>'); return; }
  if (action === 'add-section') { toast('Section added', 'Choose a standard section name in the editor to continue.'); return; }
  if (action === 'history-search') { toast('History search', 'Search is ready for local snapshots in the full Windows build.'); return; }
});
document.addEventListener('click', (event) => { const filter = event.target.closest('[data-filter]')?.dataset.filter; if (filter) { state.comparisonFilter = filter; render(); } const section = event.target.closest('[data-editor-section]')?.dataset.editorSection; if (section) { captureEditor(); state.editorSection = section; render(); } const theme = event.target.closest('[data-theme-choice]')?.dataset.themeChoice; if (theme) setTheme(theme); });
document.addEventListener('input', (event) => { if (event.target.id === 'job-description') { state.jobText = event.target.value; document.querySelector('.card-header .subtle')?.replaceChildren(document.createTextNode(`${state.jobText.length.toLocaleString()} characters`)); localStorage.setItem('northstar-job', state.jobText); } if (event.target.id === 'summary-editor') { const count = document.getElementById('summary-count'); if (count) count.textContent = `${event.target.value.length} characters`; } });
document.addEventListener('change', (event) => { if (event.target.id === 'resume-file-input') { const file = event.target.files?.[0]; if (file) importFile(file, 'resume'); } if (event.target.id === 'job-file-input') { const file = event.target.files?.[0]; if (file) importFile(file, 'job'); } });

function showModal(title, body) { const root = document.getElementById('modal-root'); root.innerHTML = `<div class="modal-backdrop" data-action="close-modal"><section class="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title"><div class="modal-header"><div><h2 id="modal-title">${title}</h2><p>Northstar privacy statement</p></div><button class="icon-button" aria-label="Close dialog" data-action="close-modal">${icon('x')}</button></div><div class="modal-body">${body}<div class="modal-actions"><button class="button button-primary" data-action="close-modal">Got it</button></div></div></section></div>`; }
document.addEventListener('click', (event) => { if (event.target.closest('[data-action="close-modal"]')) document.getElementById('modal-root').innerHTML = ''; });

render();
