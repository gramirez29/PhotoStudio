import { readBookingId, readProjectId, toPushPlatform } from '../push';

describe('readProjectId', () => {
  it('reads the project the way "eas init" writes it', () => {
    expect(readProjectId({ eas: { projectId: '6560ba0b-780b-4028-8ae8-382869e88e34' } })).toBe('6560ba0b-780b-4028-8ae8-382869e88e34');
  });

  it('returns null for anything that has no project id', () => {
    expect(readProjectId(undefined)).toBeNull();
    expect(readProjectId(null)).toBeNull();
    expect(readProjectId('eas')).toBeNull();
    expect(readProjectId({})).toBeNull();
    expect(readProjectId({ eas: null })).toBeNull();
    expect(readProjectId({ eas: {} })).toBeNull();
    expect(readProjectId({ eas: { projectId: 123 } })).toBeNull();
    expect(readProjectId({ eas: { projectId: '' } })).toBeNull();
    expect(readProjectId({ eas: [] })).toBeNull();
  });
});

describe('readBookingId', () => {
  it('reads the booking the push is about', () => {
    expect(readBookingId({ bookingId: 'b1', notificationId: 'n1', type: 'PaymentProofSubmitted' })).toBe('b1');
  });

  it('opens nothing when the data is missing or has an unexpected shape', () => {
    expect(readBookingId(undefined)).toBeNull();
    expect(readBookingId(null)).toBeNull();
    expect(readBookingId('b1')).toBeNull();
    expect(readBookingId([])).toBeNull();
    expect(readBookingId({})).toBeNull();
    expect(readBookingId({ bookingId: 42 })).toBeNull();
    expect(readBookingId({ bookingId: '   ' })).toBeNull();
  });
});

describe('toPushPlatform', () => {
  it('maps the systems that receive push to the names the backend uses', () => {
    expect(toPushPlatform('android')).toBe('Android');
    expect(toPushPlatform('ios')).toBe('Ios');
  });

  it('returns null for the systems that do not', () => {
    expect(toPushPlatform('web')).toBeNull();
    expect(toPushPlatform('windows')).toBeNull();
    expect(toPushPlatform('')).toBeNull();
  });
});
