import {
  LOCAL_ALIASES,
  SECTION_ALIASES,
  STOP_WORDS,
  aliasesFor
} from './knowledge-base.js';

const WEIGHTS = Object.freeze({
  skills: 25,
  keywords: 15,
  experience: 20,
  responsibilities: 15,
  education: 5,
  ats: 10,
  achievements: 10
});

const SECTION_BY_HEADING = new Map();
for (const [section, headings] of Object.entries(SECTION_ALIASES)) {
  for (const heading of headings) SECTION_BY_HEADING.set(normalizeHeading(heading), section);
}

const WEAK_LEADS = /^(?:worked on|helped with|assisted with|responsible for|duties included|participated in|involved in|tasked with|handled)\b/i;
const ACTION_LEADS = /^(?:built|created|designed|developed|delivered|implemented|improved|increased|reduced|launched|led|managed|automated|analyzed|optimized|owned|migrated|mentored|wrote|generated|coordinated|negotiated|achieved|resolved|supported|administered|maintained)\b/i;
const METRIC_PATTERN = /(?:\b\d+(?:\.\d+)?\s*(?:%|(?:percent|x|hours?|days?|weeks?|months?|users?|customers?|projects?|people|employees?)\b)|[$€£]\s?\d[\d,.]*|\b(?:increased|reduced|grew|saved|cut|improved|exceeded)\b[^.\n]{0,50}\b\d+(?:\.\d+)?)/i;
const DATE_RANGE_PATTERN = /((?:jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?\s+)?(?:19|20)\d{2})\s*(?:-|–|—|to)\s*((?:present|current|now)|(?:(?:jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?\s+)?(?:19|20)\d{2}))/gi;

/** Convert user-provided text into a stable, matching-friendly representation. */
export function normalizeText(value) {
  if (value === null || value === undefined) return '';
  return String(value)
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[’‘`]/g, "'")
    .replace(/&/g, ' and ')
    .replace(/[\u2010-\u2015]/g, '-')
    .replace(/[^a-z0-9+#.]+/g, ' ')
    .replace(/\.(?![a-z0-9])|(?<![a-z0-9])\./g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/** Tokenize without external NLP dependencies; technical forms such as C++ survive. */
export function tokenize(value) {
  const text = normalizeText(value);
  return text.match(/[a-z0-9]+(?:\.[a-z0-9]+|[+#])*/g) ?? [];
}

function normalizeHeading(value) {
  return normalizeText(value).replace(/[:|]+/g, '').trim();
}

function occurrences(text, phrase) {
  const haystack = ` ${normalizeText(text)} `;
  const needle = ` ${normalizeText(phrase)} `;
  if (needle.trim() === '') return [];
  const found = [];
  let from = 0;
  while (true) {
    const index = haystack.indexOf(needle, from);
    if (index < 0) return found;
    found.push(Math.max(0, index - 1));
    from = index + needle.length - 1;
  }
}

function knownTermDetails(value) {
  const clauses = String(value ?? '').split(/[\n;!?]|\.(?=\s|$)/).map(normalizeText).filter(Boolean);
  const details = [];
  for (const canonical of Object.keys(LOCAL_ALIASES)) {
    const candidates = aliasesFor(canonical).sort((a, b) => normalizeText(b).length - normalizeText(a).length);
    const hits = [];
    for (const [clauseIndex, clause] of clauses.entries()) {
      const seen = new Set();
      for (const alias of candidates) {
        for (const index of occurrences(clause, alias)) {
          if (seen.has(index)) continue;
          seen.add(index);
          const context = clause.slice(Math.max(0, index - 85), index);
          // Negated claims are not evidence of a skill at all.
          if (/\b(?:no|without|not|never|lack|lacking)\b[^,]{0,55}$/.test(context)) continue;
          const weak = /\b(?:familiar with|exposure to|basic knowledge|beginner|learning|coursework|studying|interested in)\b/.test(context);
          hits.push({ alias, index, clauseIndex, weak });
        }
      }
    }
    if (!hits.length) continue;
    details.push({
      canonical,
      alias: hits[0].alias,
      occurrences: hits.length,
      confidence: hits.every(hit => hit.weak) ? 'partial' : 'matched'
    });
  }
  return details;
}

/**
 * Extract unique, normalized keywords. Known local aliases become canonical names;
 * other meaningful words are retained once, so repetition cannot inflate a score.
 */
export function extractKeywords(value, options = {}) {
  const text = normalizeText(value);
  if (!text) return [];
  const includeGeneral = options.includeGeneral !== false;
  const result = [];
  const seen = new Set();
  const details = knownTermDetails(value);
  for (const detail of details.sort((a, b) => a.canonical.localeCompare(b.canonical))) {
    if (!seen.has(detail.canonical)) {
      seen.add(detail.canonical);
      result.push(detail.canonical);
    }
  }
  if (!includeGeneral) return result;
  // Remove aliases before collecting general words, so AWS and "Amazon Web
  // Services" have identical keyword weight rather than counting four times.
  let remainder = ` ${text} `;
  const aliases = details.flatMap(detail => aliasesFor(detail.canonical))
    .map(normalizeText).sort((a, b) => b.length - a.length);
  for (const alias of aliases) {
    const needle = ` ${alias} `;
    while (remainder.includes(needle)) remainder = remainder.replace(needle, ' ');
  }
  for (const token of tokenize(remainder)) {
    if (token.length < 3 || STOP_WORDS.has(token) || /^\d+$/.test(token)) continue;
    if (!seen.has(token)) {
      seen.add(token);
      result.push(token);
    }
  }
  return result;
}

function splitSections(value) {
  const text = String(value ?? '');
  const sections = {};
  let current = 'general';
  sections[current] = [];
  for (const line of text.split(/\r?\n/)) {
    const heading = SECTION_BY_HEADING.get(normalizeHeading(line.replace(/[:]+$/, '')));
    if (heading) {
      current = heading;
      if (!sections[current]) sections[current] = [];
    } else if (line.trim()) {
      sections[current].push(line.trim());
    }
  }
  return Object.fromEntries(Object.entries(sections).map(([key, lines]) => [key, lines.join('\n')]));
}

function linesOf(value) {
  return String(value ?? '').split(/\r?\n/).map(line => line.trim()).filter(Boolean);
}

function extractBullets(value) {
  return linesOf(value)
    .filter(line => /^(?:[-*•▪◦]|\d+[.)])\s+/.test(line))
    .map(line => line.replace(/^(?:[-*•▪◦]|\d+[.)])\s+/, '').trim())
    .filter(Boolean);
}

function weakBulletDetails(bullets) {
  return bullets.flatMap(text => {
    const reasons = [];
    if (WEAK_LEADS.test(text)) reasons.push('vague lead-in');
    if (text.length < 40) reasons.push('too brief to show context or outcome');
    if (!ACTION_LEADS.test(text) && !METRIC_PATTERN.test(text)) reasons.push('no clear action or result signal');
    return reasons.length ? [{ text, reasons }] : [];
  });
}

function achievementBullets(bullets) {
  return bullets.filter(text => METRIC_PATTERN.test(text) || /\b(?:award|awarded|promoted|won|recognition|exceeded|achieved)\b/i.test(text));
}

function parseDateYear(value) {
  const match = String(value).match(/(?:19|20)\d{2}/);
  return match ? Number(match[0]) : null;
}

function calculateExperienceYears(text) {
  const explicit = [...String(text).matchAll(/\b(\d+(?:\.\d+)?)\+?\s+years?\b/gi)].map(match => Number(match[1]));
  let total = explicit.length ? Math.max(...explicit) : 0;
  for (const match of String(text).matchAll(DATE_RANGE_PATTERN)) {
    const start = parseDateYear(match[1]);
    const end = /present|current|now/i.test(match[2]) ? new Date().getFullYear() : parseDateYear(match[2]);
    if (start && end && end >= start && end - start <= 60) total = Math.max(total, end - start);
  }
  return Math.round(total * 10) / 10;
}

function extractDateEntries(text) {
  return [...String(text).matchAll(DATE_RANGE_PATTERN)].map(match => ({
    range: match[0].trim(),
    start: match[1].trim(),
    end: match[2].trim()
  }));
}

function detectContact(text) {
  return {
    emails: String(text).match(/[\w.+-]+@[\w.-]+\.[a-z]{2,}/gi) ?? [],
    phones: String(text).match(/(?:\+?\d[\d ().-]{7,}\d)/g) ?? [],
    urls: String(text).match(/(?:https?:\/\/|www\.)\S+/gi) ?? []
  };
}

function detectAtsIssues(text, sections, contact) {
  const issues = [];
  if (!contact.emails.length) issues.push({ code: 'missing-email', message: 'No email address was detected.' });
  if (!contact.phones.length) issues.push({ code: 'missing-phone', message: 'No phone number was detected.' });
  if (!sections.experience) issues.push({ code: 'missing-experience-section', message: 'No conventional experience section was detected.' });
  if (!sections.skills) issues.push({ code: 'missing-skills-section', message: 'No conventional skills section was detected.' });
  if (!sections.education) issues.push({ code: 'missing-education-section', message: 'No conventional education section was detected.' });
  if (/\|\s*\w+\s*\|/.test(text)) issues.push({ code: 'possible-table-layout', message: 'Table-like separators may be difficult for some ATS parsers.' });
  return issues;
}

function educationTerms(text) {
  return [...new Set((String(text).match(/\b(?:ph\.?d\.?|doctorate|master(?:'s|s)?|mba|bachelor(?:'s|s)?|associate(?:'s|s)?|degree|certification|certificate)\b/gi) ?? []).map(normalizeText))];
}

function requiredSkillGroups(text, skills) {
  const required = [];
  const preferred = [];
  for (const skill of skills) {
    const index = normalizeText(text).indexOf(` ${normalizeText(skill)} `);
    const context = normalizeText(text).slice(Math.max(0, index - 100), index + 140);
    if (/\b(?:preferred|nice to have|bonus|plus)\b/.test(context)) preferred.push(skill);
    else if (/\b(?:required|must|need|minimum|qualifications?)\b/.test(context)) required.push(skill);
  }
  return { required: [...new Set(required)], preferred: [...new Set(preferred)] };
}

/** Parse a plain-text resume without relying on a file format or remote service. */
export function parseResumeText(value) {
  const text = String(value ?? '');
  const sections = splitSections(text);
  const details = knownTermDetails(text);
  const bullets = extractBullets(text);
  const contact = detectContact(text);
  const weakBullets = weakBulletDetails(bullets);
  return {
    type: 'resume',
    text,
    normalizedText: normalizeText(text),
    sections,
    contact,
    skills: details.map(detail => detail.canonical),
    skillDetails: details,
    keywords: extractKeywords(text),
    bulletPoints: bullets,
    weakBullets,
    achievements: achievementBullets(bullets),
    experienceEntries: extractDateEntries(sections.experience ?? text),
    experienceYears: calculateExperienceYears(sections.experience ?? text),
    education: educationTerms(sections.education ?? text),
    atsIssues: detectAtsIssues(text, sections, contact)
  };
}

/** Parse a job description into the same local vocabulary plus role requirements. */
export function parseJobDescriptionText(value) {
  const text = String(value ?? '');
  const sections = splitSections(text);
  const details = knownTermDetails(text);
  const skills = details.map(detail => detail.canonical);
  const bullets = extractBullets(text);
  const responsibilities = bullets.length
    ? bullets
    : linesOf(text).filter(line => /\b(?:responsible|will|you will|duties|develop|build|manage|lead|maintain|create|collaborate)\b/i.test(line));
  const groups = requiredSkillGroups(text, skills);
  const yearsRequired = [...text.matchAll(/\b(?:at least|minimum of|over|more than)?\s*(\d+(?:\.\d+)?)\+?\s+years?\b/gi)]
    .map(match => Number(match[1]))
    .reduce((max, valueFound) => Math.max(max, valueFound), 0);
  return {
    type: 'job-description',
    text,
    normalizedText: normalizeText(text),
    sections,
    skills: [...new Set(skills)],
    skillDetails: details,
    requiredSkills: groups.required.length ? groups.required : [...new Set(skills)],
    preferredSkills: groups.preferred,
    keywords: extractKeywords(text),
    responsibilities,
    education: educationTerms(text),
    yearsRequired,
    atsIssues: []
  };
}

function asResume(value) {
  if (value && typeof value === 'object' && value.type === 'resume' && Array.isArray(value.skills)) return value;
  return parseResumeText(value?.text ?? value?.resumeText ?? value ?? '');
}

function asJob(value) {
  if (value && typeof value === 'object' && value.type === 'job-description' && Array.isArray(value.skills)) return value;
  return parseJobDescriptionText(value?.text ?? value?.jobDescription ?? value ?? '');
}

function scoreSkills(resume, job) {
  const candidate = new Set(resume.skills ?? []);
  const partialContext = new Set((resume.skillDetails ?? []).filter(detail => detail.confidence === 'partial').map(detail => detail.canonical));
  const required = [...new Set(job.skills ?? [])];
  const matched = [];
  const partial = [];
  const missing = [];
  for (const skill of required) {
    // An adjacent technology is useful context, but it is not evidence of the
    // required skill. Only an explicitly mentioned skill can be matched or
    // partial (for example, "familiar with Python").
    if (candidate.has(skill) && !partialContext.has(skill)) matched.push(skill);
    else if (candidate.has(skill)) partial.push(skill);
    else missing.push(skill);
  }
  const points = matched.length + partial.length * 0.5;
  return {
    matched,
    partial,
    missing,
    score: required.length ? Math.round((points / required.length) * 100) : 100
  };
}

function overlapScore(items, candidateKeywords) {
  if (!items.length) return 100;
  let covered = 0;
  for (const item of items) {
    const keywords = extractKeywords(item, { includeGeneral: false });
    if (!keywords.length) {
      if (candidateKeywords.has(normalizeText(item))) covered += 1;
    } else if (keywords.some(keyword => candidateKeywords.has(keyword))) covered += 1;
  }
  return Math.round((covered / items.length) * 100);
}

function scoreExperience(resume, job) {
  const actual = Number(resume.experienceYears) || 0;
  const required = Number(job.yearsRequired) || 0;
  if (!required) return actual > 0 ? 100 : 50;
  return Math.min(100, Math.round((actual / required) * 100));
}

function scoreEducation(resume, job) {
  const required = job.education ?? [];
  if (!required.length) return 100;
  const candidate = resume.education ?? [];
  if (!candidate.length) return 0;
  if (required.some(term => candidate.some(found => found.includes(term) || term.includes(found)))) return 100;
  return 40;
}

function scoreAts(resume) {
  const issues = resume.atsIssues ?? [];
  const penalty = issues.reduce((total, issue) => total + (issue.code === 'missing-email' || issue.code === 'missing-phone' ? 15 : 10), 0);
  return Math.max(0, 100 - penalty);
}

/** Score a resume against a job description using fixed, explainable weights. */
export function analyzeResume(resumeInput, jobInput) {
  const resume = asResume(resumeInput);
  const job = asJob(jobInput);
  const skillMatches = scoreSkills(resume, job);
  const candidateKeywords = new Set(resume.keywords ?? []);
  const keywordScore = overlapScore(job.keywords ?? [], candidateKeywords);
  const responsibilitiesScore = overlapScore(job.responsibilities ?? [], candidateKeywords);
  const scores = {
    skills: skillMatches.score,
    keywords: keywordScore,
    experience: scoreExperience(resume, job),
    responsibilities: responsibilitiesScore,
    education: scoreEducation(resume, job),
    ats: scoreAts(resume),
    achievements: resume.achievements?.length ? Math.min(100, 50 + resume.achievements.length * 20) : 0
  };
  // An entirely empty pair is not a well-aligned resume; return a neutral,
  // explicit zero instead of allowing no requirements to look like a pass.
  if (!normalizeText(resume.text) && !normalizeText(job.text)) {
    for (const key of Object.keys(scores)) scores[key] = 0;
  }
  const overall = Math.round(Object.entries(WEIGHTS).reduce((total, [key, weight]) => total + scores[key] * weight, 0) / 100);
  const analysis = {
    overallScore: overall,
    score: overall,
    weights: { ...WEIGHTS },
    scores,
    weightedScores: Object.fromEntries(Object.entries(WEIGHTS).map(([key, weight]) => [key, Math.round(scores[key] * weight) / 100])),
    resume,
    job,
    skillMatches,
    matchedSkills: skillMatches.matched,
    partialSkills: skillMatches.partial,
    missingSkills: skillMatches.missing,
    weakBullets: resume.weakBullets ?? [],
    atsIssues: resume.atsIssues ?? []
  };
  analysis.recommendations = buildRecommendations(analysis);
  return analysis;
}

/** Generate truthful, actionable next steps; none of these advise inventing experience. */
export function buildRecommendations(analysis = {}) {
  const recommendations = [];
  const matches = analysis.skillMatches ?? {
    missing: analysis.missingSkills ?? [],
    partial: analysis.partialSkills ?? [],
    matched: analysis.matchedSkills ?? []
  };
  if (matches.missing?.length) {
    recommendations.push(`Address missing skills selectively: ${matches.missing.slice(0, 5).join(', ')}. Add a skill only when you genuinely have it; otherwise do not claim it.`);
  }
  if (matches.partial?.length) {
    recommendations.push(`Clarify evidence for partial skills: ${matches.partial.slice(0, 5).join(', ')}. Describe the real scope of your experience rather than overstating proficiency.`);
  }
  if ((analysis.weakBullets ?? []).length) {
    recommendations.push('Rewrite weak bullets with a truthful action, context, and outcome. Add measurable results only when you can verify them; do not invent metrics.');
  }
  if ((analysis.atsIssues ?? []).length) {
    const codes = new Set(analysis.atsIssues.map(issue => issue.code));
    if (codes.has('missing-email') || codes.has('missing-phone')) recommendations.push('Add accurate contact details in plain text so recruiters and ATS tools can identify them.');
    if (codes.has('missing-experience-section') || codes.has('missing-skills-section') || codes.has('missing-education-section')) recommendations.push('Use clear conventional headings such as Experience, Skills, and Education; keep the content factual.');
    if (codes.has('possible-table-layout')) recommendations.push('Replace table-like layouts with simple headings and bullets where possible for more reliable ATS parsing.');
  }
  if ((analysis.scores?.keywords ?? 100) < 70) recommendations.push('Mirror relevant terminology from the job description when it accurately describes your background; do not keyword-stuff or add unsupported claims.');
  if ((analysis.scores?.achievements ?? 0) < 50) recommendations.push('Where truthful, make outcomes visible in achievement bullets (scale, time, quality, or impact) rather than listing duties alone.');
  if (!recommendations.length) recommendations.push('The resume is well aligned on the detected criteria. Keep every claim accurate and tailor only wording that truthfully reflects your experience.');
  return [...new Set(recommendations)];
}

export { WEIGHTS };
