// Moves the bookings (and the calendar) of one tenant id to a user, for example after the user registered and the data
// they had before was stored under an older photographer id. It is idempotent: running it twice moves nothing the second time.
//
// Usage (mongosh connected to the right database; fromId is optional and defaults to the id the development data used):
//   docker exec -i photostudio-mongo mongosh photostudio_dev --eval "const username='tu.usuario'; $(cat backend/scripts/migrate-tenant.js)"
//   const username = "tu.usuario"; const fromId = "0197a000-0000-7000-8000-000000000001"; load("migrate-tenant.js")
const legacyId = UUID(typeof fromId === 'undefined' ? '0197a000-0000-7000-8000-000000000001' : fromId);
const user = db.users.findOne({ username: username.trim().toLowerCase() });
if (!user) { throw new Error('There is no user "' + username + '". Register it in the app first.'); }

const moved = db.bookings.updateMany({ photographerId: legacyId }, { $set: { photographerId: user._id } });

// The calendar document is keyed by the photographer id, so it is copied to the new id and the old one is removed.
const calendar = db.photographer_calendars.findOne({ _id: legacyId });
let calendarMoved = false;
if (calendar) {
  const existing = db.photographer_calendars.findOne({ _id: user._id });
  calendar._id = user._id;
  if (existing) {
    // The user already has a calendar (they created bookings after registering): merge the old entries into it.
    db.photographer_calendars.updateOne({ _id: user._id }, { $push: { entries: { $each: calendar.entries } } });
  } else {
    db.photographer_calendars.insertOne(calendar);
  }
  db.photographer_calendars.deleteOne({ _id: legacyId });
  calendarMoved = true;
}

print(JSON.stringify({ username: user.username, bookingsMoved: moved.modifiedCount, calendarMoved }));
