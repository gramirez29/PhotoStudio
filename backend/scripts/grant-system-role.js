// Grants (or removes) the system operator role of a user. Only a system operator may call POST /api/maintenance/run, which
// works on the data of every photographer. The role is never given by registration: it is set here, by whoever runs the
// database. The user gets it in the next access token (at most 15 minutes, or at the next sign-in).
//
// Usage (mongosh connected to the right database; revoke is optional and defaults to false):
//   docker exec -i photostudio-mongo mongosh photostudio_dev --eval "const username='tu.usuario'; $(cat backend/scripts/grant-system-role.js)"
//   const username = "tu.usuario"; const revoke = true; load("grant-system-role.js")
const normalized = username.trim().toLowerCase();
const grant = !(typeof revoke !== 'undefined' && revoke === true);
const result = db.users.updateOne({ username: normalized }, { $set: { isSystemOperator: grant } });
if (result.matchedCount === 0) { throw new Error('There is no user "' + username + '". Register it in the app first.'); }

print(JSON.stringify({ username: normalized, isSystemOperator: grant }));
