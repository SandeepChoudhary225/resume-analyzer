/**
 * Small, local vocabulary used by the offline analyzer.
 *
 * Keep this file deliberately data-only. New aliases can be added to a group
 * without changing analyzer logic, and no network lookup is ever performed.
 */
export const LOCAL_ALIASES = {
  javascript: ['javascript', 'js', 'ecmascript', 'es6', 'es2015'],
  typescript: ['typescript', 'ts'],
  python: ['python', 'py'],
  java: ['java'],
  'c++': ['c++', 'cpp'],
  'c#': ['c#', 'csharp', 'c sharp'],
  'node.js': ['node.js', 'nodejs', 'node js', 'node'],
  react: ['react', 'react.js', 'reactjs'],
  'react native': ['react native'],
  vue: ['vue', 'vue.js', 'vuejs'],
  angular: ['angular', 'angular.js', 'angularjs'],
  'next.js': ['next.js', 'nextjs', 'next js'],
  html: ['html', 'html5'],
  css: ['css', 'css3'],
  sql: ['sql', 'structured query language'],
  mysql: ['mysql'],
  postgresql: ['postgresql', 'postgres', 'psql'],
  mongodb: ['mongodb', 'mongo'],
  redis: ['redis'],
  graphql: ['graphql'],
  'rest api': ['rest api', 'restful api', 'rest'],
  aws: ['aws', 'amazon web services'],
  azure: ['azure', 'microsoft azure'],
  gcp: ['gcp', 'google cloud', 'google cloud platform'],
  docker: ['docker'],
  containerization: ['containerization', 'containers'],
  kubernetes: ['kubernetes', 'k8s'],
  terraform: ['terraform'],
  git: ['git'],
  'version control': ['version control', 'source control'],
  'ci/cd': ['ci/cd', 'ci cd', 'continuous integration', 'continuous delivery', 'continuous deployment'],
  linux: ['linux'],
  excel: ['excel', 'microsoft excel'],
  tableau: ['tableau'],
  'power bi': ['power bi', 'powerbi'],
  'machine learning': ['machine learning', 'ml'],
  'data analysis': ['data analysis', 'data analytics'],
  'data science': ['data science'],
  'natural language processing': ['natural language processing', 'nlp'],
  agile: ['agile'],
  scrum: ['scrum'],
  kanban: ['kanban'],
  'project management': ['project management'],
  'program management': ['program management'],
  jira: ['jira'],
  figma: ['figma'],
  'user experience': ['user experience', 'ux'],
  'user interface': ['user interface', 'ui'],
  leadership: ['leadership', 'team leadership'],
  communication: ['communication', 'written communication', 'verbal communication'],
  'cross-functional collaboration': ['cross-functional collaboration', 'cross functional collaboration', 'collaboration'],
  'problem solving': ['problem solving', 'problem-solving'],
  'critical thinking': ['critical thinking'],
  'attention to detail': ['attention to detail'],
  'customer service': ['customer service', 'client service'],
  sales: ['sales', 'selling'],
  marketing: ['marketing'],
  'content strategy': ['content strategy'],
  'a/b testing': ['a/b testing', 'ab testing', 'split testing'],
  'rest api design': ['rest api design'],
  'unit testing': ['unit testing', 'unit tests'],
  'automated testing': ['automated testing', 'test automation'],
  jest: ['jest'],
  cypress: ['cypress'],
  playwright: ['playwright']
};

/** Broad groups let the scorer identify a related (partial) skill locally. */
export const SKILL_CATEGORIES = {
  languages: ['javascript', 'typescript', 'python', 'java', 'c++', 'c#'],
  frontend: ['html', 'css', 'javascript', 'typescript', 'react', 'react native', 'vue', 'angular', 'next.js'],
  backend: ['node.js', 'java', 'python', 'sql', 'graphql', 'rest api', 'rest api design'],
  databases: ['sql', 'mysql', 'postgresql', 'mongodb', 'redis'],
  cloud: ['aws', 'azure', 'gcp', 'docker', 'containerization', 'kubernetes', 'terraform', 'linux'],
  data: ['sql', 'excel', 'tableau', 'power bi', 'machine learning', 'data analysis', 'data science', 'natural language processing'],
  delivery: ['git', 'version control', 'ci/cd', 'agile', 'scrum', 'kanban', 'jira', 'project management', 'program management'],
  quality: ['unit testing', 'automated testing', 'jest', 'cypress', 'playwright'],
  design: ['figma', 'user experience', 'user interface', 'a/b testing'],
  professional: ['leadership', 'communication', 'cross-functional collaboration', 'problem solving', 'critical thinking', 'attention to detail', 'customer service', 'sales', 'marketing', 'content strategy']
};

export const STOP_WORDS = new Set([
  'a', 'an', 'and', 'are', 'as', 'at', 'be', 'been', 'being', 'by', 'can', 'could',
  'for', 'from', 'has', 'have', 'having', 'in', 'into', 'is', 'it', 'its', 'of',
  'on', 'or', 'our', 'that', 'the', 'their', 'this', 'to', 'up', 'we', 'will',
  'with', 'you', 'your', 'years', 'year', 'work', 'working', 'role', 'team',
  'responsibilities', 'responsibility', 'experience', 'including', 'such', 'using',
  'required', 'requirement', 'requirements', 'preferred', 'minimum', 'must',
  'skills', 'skill', 'qualifications', 'qualification', 'candidate', 'candidates',
  'applicant', 'applicants', 'company', 'about', 'benefits', 'opportunity', 'job',
  'description', 'looking', 'seeking', 'strong', 'excellent', 'ability', 'knowledge'
]);

export const SECTION_ALIASES = {
  summary: ['summary', 'professional summary', 'profile', 'objective'],
  skills: ['skills', 'technical skills', 'core skills', 'competencies', 'technologies'],
  experience: ['experience', 'work experience', 'professional experience', 'employment history'],
  education: ['education', 'academic background', 'qualifications'],
  projects: ['projects', 'selected projects', 'portfolio'],
  certifications: ['certifications', 'certificates', 'licenses'],
  achievements: ['achievements', 'awards', 'accomplishments']
};

const aliasToCanonical = new Map();
for (const [canonical, aliases] of Object.entries(LOCAL_ALIASES)) {
  for (const alias of new Set([canonical, ...aliases])) aliasToCanonical.set(alias.toLowerCase(), canonical);
}

export function canonicalizeAlias(value) {
  if (typeof value !== 'string') return null;
  const key = value.trim().toLowerCase();
  return aliasToCanonical.get(key) ?? null;
}

export function aliasesFor(canonical) {
  return LOCAL_ALIASES[canonical] ? [...new Set([canonical, ...LOCAL_ALIASES[canonical]])] : [];
}

export function categoryFor(canonical) {
  return Object.entries(SKILL_CATEGORIES)
    .filter(([, terms]) => terms.includes(canonical))
    .map(([category]) => category);
}
