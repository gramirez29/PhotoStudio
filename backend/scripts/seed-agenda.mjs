#!/usr/bin/env node
// Seeds a LOCAL development account with bookings spread over the next three months, to try the agenda of the app.
//
// What it does, in order:
//   1. Logs in as the test user; if it does not exist, registers it (and marks its email as verified directly in the local MongoDB, because
//      in Development the verification code is only printed in the log of the API).
//   2. Creates bookings through the real API (so every rule of the backend applies) on different days, at different hours and with
//      different durations: some tentative, some confirmed (contract signed on paper + deposit paid in cash), some cancelled.
//   3. Includes the cases that are hard to see by hand: three bookings on one day, a session that goes through midnight, a 4-hour session,
//      and two cancelled bookings whose time was taken again by another booking (they overlap on the screen when "show all" is on).
//   4. Asks the calendar endpoint for the next 62 days and prints what the agenda will show.
//
// Usage (from the repository root; the API must be running and reachable, by default at http://localhost:8080):
//   node backend/scripts/seed-agenda.mjs
//   node backend/scripts/seed-agenda.mjs --username agenda --email agenda@example.com --password "Agenda-12345"
//   node backend/scripts/seed-agenda.mjs --reset        (first removes the bookings of the test user, to start again)
//
// Options: --api-url, --username, --email, --password, --container (MongoDB container, default photostudio-mongo),
//          --database (default photostudio_dev), --reset.
//
// Limits you should know about:
//   - The API refuses sessions in the past, so there are no completed or client-absent bookings in the seed.
//   - Tentative bookings expire by themselves 48 hours after they are created (the policy of the booking), like in real life.
//   - Running it again without --reset adds more cancelled bookings (a cancelled booking frees its time, so it can be created again);
//     use --reset to start from nothing.
//   - It refuses to run against the real sites of the project: it is for the local API only.

import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import process from 'node:process';

/** Hosts of the real environments, which this script must never touch. */
const PROTECTED_HOST_SUFFIX = 'photosensestudio.com';

/** Names of the clients of the test bookings, used in order. */
const CLIENTS = [
  'María Fernanda Solano', 'José Pablo Mora', 'Daniela Rojas', 'Luis Diego Vargas', 'Camila Jiménez', 'Andrés Castro',
  'Valeria Araya', 'Esteban Quesada', 'Paola Chaves', 'Marco Alvarado', 'Sofía Brenes', 'Kevin Salazar',
  'Natalia Vega', 'Mauricio Cordero', 'Fabiola Núñez', 'Gerardo Zúñiga', 'Ximena Barrantes', 'Ricardo Picado',
  'Alejandra Monge', 'Sebastián Herrera', 'Karla Sandí', 'Óscar Ulate', 'Mónica Esquivel', 'Rafael Porras',
  'Jimena Trejos', 'Bryan Calderón', 'Lucía Segura', 'Adrián Mata', 'Priscilla Obando', 'Tomás Villalobos',
  'Gabriela Ureña', 'Felipe Carvajal', 'Silvia Arguedas',
];

/** Sessions the photographer sells, with a price in colones each, used in order. */
const SERVICES = [
  { name: 'Retrato Familiar', price: 65000 },
  { name: 'Retrato Individual', price: 35000 },
  { name: 'Pre-Boda', price: 110000 },
  { name: 'Compromiso', price: 65000 },
  { name: 'Producto', price: 45000 },
  { name: 'Retrato Exterior', price: 65000 },
];

/**
 * The bookings to create. `day` counts days from today, `hour` is the start by the clock of this machine (`null` means "a couple of
 * hours from now", for today), `hours` is the duration. A `cancelled` booking is created and cancelled at once, which frees its time, so
 * the booking listed after it can take the same time.
 */
const PLAN = [
  { day: 0, hour: null, hours: 2, kind: 'confirmed' },
  { day: 1, hour: 9, hours: 2, kind: 'confirmed' },
  { day: 1, hour: 14, hours: 1, kind: 'tentative' },
  { day: 1, hour: 16, hours: 2, kind: 'tentative' },
  { day: 2, hour: 10, hours: 3, kind: 'confirmed' },
  { day: 3, hour: 22, hours: 3, kind: 'tentative' }, // goes through midnight
  { day: 5, hour: 8, hours: 1, kind: 'tentative' },
  { day: 5, hour: 11, hours: 2, kind: 'confirmed' },
  { day: 6, hour: 15, hours: 2, kind: 'cancelled' },
  { day: 8, hour: 9, hours: 4, kind: 'confirmed' }, // the longest
  { day: 9, hour: 13, hours: 2, kind: 'tentative' },
  { day: 10, hour: 10, hours: 2, kind: 'cancelled' }, // its time is taken again below
  { day: 10, hour: 11, hours: 2, kind: 'tentative' }, // overlaps the cancelled one
  { day: 12, hour: 10, hours: 2, kind: 'tentative' },
  { day: 12, hour: 14, hours: 2, kind: 'confirmed' },
  { day: 15, hour: 9, hours: 1, kind: 'tentative' },
  { day: 15, hour: 10, hours: 2, kind: 'confirmed' },
  { day: 16, hour: 16, hours: 2, kind: 'cancelled' },
  { day: 19, hour: 8, hours: 2, kind: 'tentative' },
  { day: 21, hour: 15, hours: 2, kind: 'confirmed' },
  { day: 24, hour: 10, hours: 2, kind: 'tentative' },
  { day: 26, hour: 14, hours: 2, kind: 'cancelled' }, // its time is taken again below
  { day: 26, hour: 15, hours: 2, kind: 'confirmed' }, // overlaps the cancelled one
  { day: 28, hour: 9, hours: 2, kind: 'confirmed' },
  { day: 28, hour: 12, hours: 2, kind: 'tentative' },
  { day: 33, hour: 14, hours: 3, kind: 'tentative' },
  { day: 38, hour: 10, hours: 2, kind: 'confirmed' },
  { day: 41, hour: 16, hours: 1, kind: 'cancelled' },
  { day: 47, hour: 9, hours: 2, kind: 'tentative' },
  { day: 52, hour: 13, hours: 2, kind: 'confirmed' },
  { day: 58, hour: 10, hours: 2, kind: 'tentative' },
  { day: 64, hour: 15, hours: 2, kind: 'tentative' },
  { day: 71, hour: 9, hours: 2, kind: 'confirmed' },
  { day: 80, hour: 11, hours: 2, kind: 'tentative' },
  { day: 88, hour: 14, hours: 2, kind: 'cancelled' },
];

/**
 * Reads `--name value` pairs and `--flag` switches from the command line.
 * @param {string[]} argv Arguments after the script name.
 * @returns {Record<string, string | boolean>} What was given, by name without the dashes.
 */
function parseArgs(argv) {
  const values = {};
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (!argument.startsWith('--')) {
      continue;
    }

    const name = argument.slice(2);
    const next = argv[index + 1];
    if (next === undefined || next.startsWith('--')) {
      values[name] = true;
    } else {
      values[name] = next;
      index += 1;
    }
  }

  return values;
}

/**
 * Stops the script with a clear message.
 * @param {string} message What went wrong and what to do.
 * @returns {never} Never returns.
 */
function fail(message) {
  console.error(`\nERROR: ${message}`);
  process.exit(1);
}

/**
 * Calls the API and reads its JSON answer.
 * @param {string} apiUrl Base address of the API.
 * @param {string} method HTTP method.
 * @param {string} path Path with its query string.
 * @param {unknown} [body] Object to send as JSON.
 * @param {string} [token] Access token.
 * @returns {Promise<{ status: number, body: any }>} The status and the parsed body (null when there is none).
 */
async function call(apiUrl, method, path, body, token) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (token !== undefined) {
    headers.Authorization = `Bearer ${token}`;
  }

  let response;
  try {
    response = await fetch(`${apiUrl}${path}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  } catch (error) {
    fail(`The API at ${apiUrl} did not answer (${error.message}). Is it running? Start it with "dotnet run --project src/PhotoStudio.Api --launch-profile PhotoStudio.Api" from the backend folder.`);
  }

  const text = await response.text();
  return { status: response.status, body: text.length > 0 ? JSON.parse(text) : null };
}

/**
 * Runs a piece of JavaScript in the mongosh of the local MongoDB container.
 * @param {string} container Name of the container.
 * @param {string} database Name of the database.
 * @param {string} script Code to run; its printed output is returned.
 * @returns {string} What the script printed.
 */
function mongosh(container, database, script) {
  try {
    // NOSONAR: a local development tool; it needs the docker CLI of the developer, found in their PATH, and runs no user-provided command.
    return execFileSync('docker', ['exec', container, 'mongosh', database, '--quiet', '--eval', script], { encoding: 'utf8' }).trim();
  } catch (error) {
    return fail(`Could not run mongosh in the container "${container}" (${error.message.split('\n')[0]}). Is Docker running and the container up?`);
  }
}

/**
 * Builds an instant at an hour of the clock of this machine, some days from today.
 * @param {number} day Days from today.
 * @param {number} hour Hour of the day, from 0 to 23.
 * @returns {Date} The instant.
 */
function at(day, hour) {
  const today = new Date();
  return new Date(today.getFullYear(), today.getMonth(), today.getDate() + day, hour, 0, 0, 0);
}

/**
 * Signs in, registering the test user first when it does not exist.
 * @param {object} context Everything the steps share.
 * @returns {Promise<string>} The access token of a verified session.
 */
async function signIn(context) {
  const { apiUrl, username, email, password, container, database } = context;
  let login = await call(apiUrl, 'POST', '/api/auth/login', { username, password });

  if (login.status === 401) {
    const registered = await call(apiUrl, 'POST', '/api/auth/register', { username, email, password, name: 'Agenda de Prueba', phone: '+50670001234' });
    if (registered.status === 409) {
      fail(`The user "${username}" or the email "${email}" already exists with another password. Use --username/--email for a new test user, or the right --password.`);
    }

    if (registered.status !== 201) {
      fail(`Could not register "${username}": HTTP ${registered.status} ${JSON.stringify(registered.body)}`);
    }

    console.log(`Registered the test user "${username}".`);
    login = await call(apiUrl, 'POST', '/api/auth/login', { username, password });
  }

  if (login.status !== 200) {
    fail(`Could not sign in as "${username}": HTTP ${login.status} ${JSON.stringify(login.body)}`);
  }

  if (login.body.emailVerified === false) {
    // The code of the email only goes to the log of the API in Development; the script marks the email as verified in the database.
    mongosh(container, database, `db.users.updateOne({ username: ${JSON.stringify(username)}, emailVerifiedAt: null }, { $set: { emailVerifiedAt: new Date() } })`);
    login = await call(apiUrl, 'POST', '/api/auth/login', { username, password });
    if (login.body?.emailVerified !== true) {
      fail('The email of the test user could not be marked as verified.');
    }

    console.log('Marked the email of the test user as verified.');
  }

  return login.body.accessToken;
}

/**
 * Removes the bookings of the test user from the local database, so the seed can start from nothing.
 * @param {object} context Everything the steps share.
 * @returns {void}
 */
function resetBookings(context) {
  const { username, container, database } = context;
  const answer = mongosh(
    container,
    database,
    `const user = db.users.findOne({ username: ${JSON.stringify(username)} });
     if (!user) { print('no user'); } else {
       const removed = db.bookings.deleteMany({ photographerId: user._id }).deletedCount;
       db.photographer_calendars.deleteOne({ _id: user._id });
       db.notifications.deleteMany({ photographerId: user._id });
       print('removed ' + removed);
     }`,
  );
  console.log(`Reset: ${answer}`);
}

/**
 * Creates one booking of the plan through the API, and takes it to the state the plan says.
 * @param {object} context Everything the steps share.
 * @param {{ day: number, hour: number | null, hours: number, kind: string }} item What to create.
 * @param {number} index Position in the plan, to choose the client and the session.
 * @returns {Promise<string>} What happened: the state it ended in, or why it was skipped.
 */
async function createBooking(context, item, index) {
  const { apiUrl, token } = context;
  const service = SERVICES[index % SERVICES.length];
  const client = CLIENTS[index % CLIENTS.length];
  const now = new Date();
  const hour = item.hour ?? Math.min(now.getHours() + 2, 21);
  const start = at(item.day, hour);
  const end = new Date(start.getTime() + item.hours * 3_600_000);
  if (start.getTime() <= now.getTime()) {
    return 'skipped (it would be in the past)';
  }

  const created = await call(
    apiUrl,
    'POST',
    '/api/bookings',
    {
      clientName: client,
      clientPhone: `+506${70000000 + index * 137}`,
      packageName: service.name,
      packagePrice: service.price,
      currency: 'CRC',
      sessionStart: start.toISOString(),
      sessionEnd: end.toISOString(),
    },
    token,
  );
  if (created.status === 409) {
    return 'skipped (that time is already taken: the seed was already run?)';
  }

  if (created.status !== 201) {
    fail(`Could not create a booking: HTTP ${created.status} ${JSON.stringify(created.body)}`);
  }

  const id = created.body.id;
  if (item.kind === 'cancelled') {
    const cancelled = await call(apiUrl, 'POST', `/api/bookings/${id}/cancel`, {}, token);
    return cancelled.status === 200 ? 'cancelled' : fail(`Could not cancel a booking: HTTP ${cancelled.status} ${JSON.stringify(cancelled.body)}`);
  }

  if (item.kind === 'confirmed') {
    const signed = await call(apiUrl, 'POST', `/api/bookings/${id}/contract/in-person`, { signerName: client, templateVersion: 'v2', isPaperContract: true }, token);
    const paid = await call(
      apiUrl,
      'POST',
      `/api/bookings/${id}/payments/in-person`,
      { amount: created.body.depositRequired.amount, currency: 'CRC', method: 'Cash', idempotencyKey: randomUUID() },
      token,
    );
    if (signed.status !== 200 || paid.status !== 200) {
      fail(`Could not confirm a booking: contract HTTP ${signed.status}, payment HTTP ${paid.status} ${JSON.stringify(paid.body)}`);
    }

    return paid.body.status === 'Confirmed' ? 'confirmed' : `left as ${paid.body.status}`;
  }

  return 'tentative';
}

/**
 * Runs the seed.
 * @returns {Promise<void>} Resolves when it is done.
 */
async function main() {
  const args = parseArgs(process.argv.slice(2));
  const apiUrl = String(args['api-url'] ?? process.env.API_URL ?? 'http://localhost:8080').replace(/\/$/, '');
  const username = String(args.username ?? 'agenda').trim().toLowerCase();
  const context = {
    apiUrl,
    username,
    email: String(args.email ?? 'agenda@example.com').trim().toLowerCase(),
    password: String(args.password ?? 'Agenda-12345'),
    container: String(args.container ?? 'photostudio-mongo'),
    database: String(args.database ?? 'photostudio_dev'),
  };

  if (new URL(apiUrl).hostname.endsWith(PROTECTED_HOST_SUFFIX)) {
    fail(`Refusing to seed ${apiUrl}: it is a real environment. This script is for the local API only.`);
  }

  if (!/^[a-z0-9._-]{3,30}$/.test(username)) {
    fail('The username must have 3 to 30 letters, digits, dots, hyphens or underscores.');
  }

  console.log(`API: ${apiUrl}   user: ${username}\n`);
  if (args.reset === true) {
    resetBookings(context);
  }

  context.token = await signIn(context);

  const counts = {};
  for (const [index, item] of PLAN.entries()) {
    // NOSONAR: sequential on purpose; the order matters because a cancelled booking frees the time that the next one of the plan takes.
    const outcome = await createBooking(context, item, index);
    counts[outcome] = (counts[outcome] ?? 0) + 1;
    const when = at(item.day, item.hour ?? new Date().getHours() + 2).toLocaleString('es-CR', { dateStyle: 'medium', timeStyle: 'short' });
    console.log(`  ${when.padEnd(28)} ${item.hours} h  ${outcome}`);
  }

  console.log('\nResult:');
  for (const [outcome, count] of Object.entries(counts)) {
    console.log(`  ${String(count).padStart(3)}  ${outcome}`);
  }

  const from = at(0, 0);
  const to = at(62, 0);
  const range = `from=${encodeURIComponent(from.toISOString())}&to=${encodeURIComponent(to.toISOString())}`;
  const active = await call(apiUrl, 'GET', `/api/bookings/calendar?${range}`, undefined, context.token);
  const everything = await call(apiUrl, 'GET', `/api/bookings/calendar?${range}&includeEnded=true`, undefined, context.token);
  if (active.status !== 200 || everything.status !== 200) {
    fail(`The calendar endpoint answered HTTP ${active.status} / ${everything.status}. Does the API have the agenda (branch feat/agenda-calendar-main)?`);
  }

  console.log(`\nIn the next 62 days the agenda will show ${active.body.items.length} bookings, and ${everything.body.items.length} with "Mostrar todas las reservas".`);
  console.log(`\nSign in the app with  usuario: ${username}   contraseña: ${context.password}`);
}

await main();
