// Sets the deploy settings of the two Railway services of an environment: how each one is built, how it starts and how it is
// checked. They are service settings of the environment, not files in the repository: Railway ignored the `deploy` section of
// a railway.json uploaded with `railway up` (the worker started the API), so this script is how they are kept.
//
// It is safe to run again: it compares with what the environment has and only writes when something differs.
//
// Usage (needs a Railway login: `npx @railway/cli@5.69.0 login`):
//   node backend/scripts/configure-railway.mjs --project <project-id> --environment develop
//   node backend/scripts/configure-railway.mjs --project <project-id> --environment develop --dry-run
//
// Nothing secret is read or printed: variables are not touched here.
import { execSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { parseArgs } from 'node:util';

/** Version of the Railway CLI, the same one the deploy workflow uses. */
const RAILWAY_CLI_VERSION = '5.69.0';

/** Name of the service that runs the API. */
const API_SERVICE = 'photostudio-api';

/** Name of the service that runs the worker. */
const WORKER_SERVICE = 'photostudio-worker';

/**
 * Settings of each service, in the shape of the environment configuration of Railway.
 * - Both are built with the Dockerfile of the backend folder, which they receive as the root of the upload.
 * - The API is only considered healthy once it can reach MongoDB. Railway restarts it when it crashes (ON_FAILURE, the default of
 *   Railway, so it is not set: a value equal to the default is never stored and would look like a change on every run) up to 5 times.
 * - The worker is the same image started with another command. It is a cron job: it runs a maintenance pass every 15 minutes
 *   (UTC) and exits, so it is never restarted.
 */
const SETTINGS = {
  [API_SERVICE]: {
    build: { builder: 'DOCKERFILE', dockerfilePath: 'Dockerfile' },
    deploy: { healthcheckPath: '/health/ready', healthcheckTimeout: 60, restartPolicyMaxRetries: 5 },
  },
  [WORKER_SERVICE]: {
    build: { builder: 'DOCKERFILE', dockerfilePath: 'Dockerfile' },
    deploy: { startCommand: 'dotnet worker/PhotoStudio.Worker.dll', cronSchedule: '*/15 * * * *', restartPolicyType: 'NEVER' },
  },
};

/**
 * Runs the Railway CLI and returns what it printed.
 * @param {string[]} args Arguments for the CLI.
 * @param {{ cwd?: string, input?: string }} [options] Working directory and standard input.
 * @returns {string} Standard output of the command.
 */
function railway(args, options = {}) {
  // A shell is needed on Windows, where npx is a .cmd file. Every argument is quoted here, and the project and the environment
  // are validated before they get this far, so none of them can run a command.
  const quote = (argument) => (/^[A-Za-z0-9_@.:=/-]+$/.test(argument) ? argument : `"${argument.replace(/"/g, '\\"')}"`);
  return execSync(['npx', '--yes', `@railway/cli@${RAILWAY_CLI_VERSION}`, ...args].map(quote).join(' '), {
    encoding: 'utf8',
    stdio: ['pipe', 'pipe', 'pipe'],
    ...options,
  });
}

/**
 * Reads and validates the command line.
 * @returns {{ project: string, environment: string, dryRun: boolean }} The options.
 */
function readOptions() {
  const { values } = parseArgs({
    options: { project: { type: 'string' }, environment: { type: 'string' }, 'dry-run': { type: 'boolean', default: false } },
  });

  if (!values.project || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(values.project)) {
    throw new Error('--project must be the id of the Railway project (a GUID: Project settings > General).');
  }

  if (!values.environment || !/^[a-z0-9-]+$/.test(values.environment)) {
    throw new Error('--environment must be the name of the environment, for example develop.');
  }

  return { project: values.project, environment: values.environment, dryRun: values['dry-run'] };
}

/**
 * Finds the ids of the two services in the environment.
 * @param {string} project Project id.
 * @param {string} environment Environment name.
 * @param {string} workDirectory Empty directory the CLI can link, so the repository is not linked.
 * @returns {Record<string, string>} Service id by service name.
 */
function findServices(project, environment, workDirectory) {
  railway(['link', '--project', project, '--environment', environment], { cwd: workDirectory });
  const status = JSON.parse(railway(['status', '--json'], { cwd: workDirectory }));
  const environments = (status.environments?.edges ?? []).map((edge) => edge.node);
  const instances = (environments.find((candidate) => candidate.name === environment)?.serviceInstances?.edges ?? []).map((edge) => edge.node);
  const ids = Object.fromEntries(instances.map((instance) => [instance.serviceName, instance.serviceId]));

  for (const name of [API_SERVICE, WORKER_SERVICE]) {
    if (!ids[name]) {
      throw new Error(`The environment "${environment}" has no service named "${name}". Create it first (docs/Deployment.md section 3.3); the name must match exactly.`);
    }
  }

  return ids;
}

/**
 * Tells whether everything in `wanted` is already present, with the same values, in `current`. Extra settings in `current` are
 * fine: only what this script manages is compared.
 * @param {unknown} current What the environment has now.
 * @param {unknown} wanted What the script wants it to have.
 * @returns {boolean} True when nothing needs to change.
 */
function alreadyApplied(current, wanted) {
  if (wanted !== null && typeof wanted === 'object') {
    return current !== null && typeof current === 'object' && Object.entries(wanted).every(([key, value]) => alreadyApplied(current[key], value));
  }

  return current === wanted;
}

const options = readOptions();
const workDirectory = mkdtempSync(join(tmpdir(), 'railway-configure-'));

try {
  const ids = findServices(options.project, options.environment, workDirectory);
  const patch = { services: Object.fromEntries(Object.entries(SETTINGS).map(([name, settings]) => [ids[name], settings])) };

  // `environment edit` with a JSON patch reports "committed" even when nothing changed, so the comparison is made here.
  const current = JSON.parse(railway(['environment', 'config', '--environment', options.environment, '--json'], { cwd: workDirectory }));

  if (options.dryRun) {
    console.log(`Dry run: this is what would be applied to "${options.environment}" (service ids replaced by names):`);
    console.log(JSON.stringify(SETTINGS, null, 2));
    console.log(alreadyApplied(current, patch) ? 'It is already applied: a real run would change nothing.' : 'It differs from what the environment has now.');
  } else if (alreadyApplied(current, patch)) {
    console.log(`Already up to date in "${options.environment}": nothing to change.`);
  } else {
    const output = railway(
      ['environment', 'edit', '--project', options.project, '--environment', options.environment, '--json', '--message', 'configure-railway-script'],
      { cwd: workDirectory, input: JSON.stringify(patch) },
    );
    const result = JSON.parse(output.trim().split('\n').at(-1));
    console.log(result.committed ? `Applied to "${options.environment}".` : `Nothing was applied: ${result.message}.`);
  }
} finally {
  rmSync(workDirectory, { recursive: true, force: true });
}
