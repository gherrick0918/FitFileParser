# samples/

This directory holds sanitized FIT activity files used for manual testing and
for understanding how different devices and activities produce different data.

## Planned fixtures

| File                    | Description                                              |
|-------------------------|----------------------------------------------------------|
| `outdoor-run.fit`       | GPS outdoor run with heart rate and cadence              |
| `treadmill-run.fit`     | Treadmill session (no GPS, possible elapsed/timer delta) |
| `run-with-power.fit`    | Outdoor run with a running power meter                   |
| `interval-run.fit`      | Structured interval session with many laps               |
| `missing-heart-rate.fit`| Activity recorded without a heart-rate sensor            |
| `corrupt-file.fit`      | Deliberately corrupted file for error-handling tests     |

## Privacy note

FIT files can contain timestamps, GPS coordinates, and health-related metrics
such as heart rate and power output. Before committing any sample files,
ensure they have been fully sanitized to remove personally identifiable
information and precise location data.

## Obtaining sanitized samples

1. Export an activity from Garmin Connect or your device.
2. Use a tool such as [FIT File Tools](https://www.fitfiletools.com/) or the
   Garmin FIT SDK to strip GPS coordinates and adjust timestamps.
3. Verify the sanitized file parses correctly before committing it.
