using Dynastream.Fit;
using FitDateTime = Dynastream.Fit.DateTime;
using SysDateTime = System.DateTime;

namespace FitFileParser.Tests.Helpers;

/// <summary>
/// Builds minimal in-memory FIT activity files suitable for unit tests.
/// </summary>
internal sealed class FitFileBuilder
{
    private Sport _sport = Sport.Running;
    private SubSport _subSport = SubSport.Generic;
    private SysDateTime _startTime = SysDateTime.UtcNow.AddHours(-1);
    private float _totalElapsedTime = 3600f;
    private float _totalDistance = 10_000f;
    private ushort _totalCalories = 600;
    private byte? _avgHr = 155;
    private byte? _maxHr = 185;
    private float? _avgSpeed = 2.78f;   // m/s ≈ 10 km/h
    private ushort? _avgPower = null;
    private float? _totalTrainingEffect = null;
    private readonly List<LapSpec> _laps = [];
    private bool _corruptCrc = false;
    private bool _truncate = false;

    public FitFileBuilder WithSport(Sport sport, SubSport sub = SubSport.Generic)
    {
        _sport = sport; _subSport = sub; return this;
    }
    public FitFileBuilder WithDuration(float seconds) { _totalElapsedTime = seconds; return this; }
    public FitFileBuilder WithDistance(float meters) { _totalDistance = meters; return this; }
    public FitFileBuilder WithCalories(ushort cal) { _totalCalories = cal; return this; }
    public FitFileBuilder WithNoHeartRate() { _avgHr = null; _maxHr = null; return this; }
    public FitFileBuilder WithAvgSpeed(float mps) { _avgSpeed = mps; return this; }
    public FitFileBuilder WithNoPower() { _avgPower = null; return this; }
    public FitFileBuilder WithPower(ushort watts) { _avgPower = watts; return this; }
    public FitFileBuilder WithTrainingEffect(float te) { _totalTrainingEffect = te; return this; }
    public FitFileBuilder WithCorruptCrc() { _corruptCrc = true; return this; }
    public FitFileBuilder WithTruncation() { _truncate = true; return this; }

    public FitFileBuilder AddLap(float durationSec, float distanceM,
                                 byte? avgHr = null, float? avgSpeedMps = null,
                                 ushort? avgPower = null)
    {
        _laps.Add(new LapSpec(durationSec, distanceM, avgHr, avgSpeedMps, avgPower));
        return this;
    }

    public Stream Build()
    {
        var ms = new MemoryStream();
        var enc = new Encode(ProtocolVersion.V10);
        enc.Open(ms);

        WriteFileId(enc);
        WriteSession(enc);
        WriteLaps(enc);
        WriteActivity(enc);

        enc.Close();
        ms.Position = 0;

        if (_corruptCrc)
        {
            var bytes = ms.ToArray();
            // Flip the last two bytes (CRC)
            bytes[^1] ^= 0xFF;
            bytes[^2] ^= 0xFF;
            return new MemoryStream(bytes);
        }

        if (_truncate)
        {
            var bytes = ms.ToArray();
            return new MemoryStream(bytes[..(bytes.Length / 2)]);
        }

        return ms;
    }

    private void WriteFileId(Encode enc)
    {
        var msg = new FileIdMesg();
        msg.SetType(Dynastream.Fit.File.Activity);
        msg.SetManufacturer(Manufacturer.Garmin);
        enc.Write(msg);
    }

    private void WriteSession(Encode enc)
    {
        var endTime = _startTime.AddSeconds(_totalElapsedTime);
        var msg = new SessionMesg();
        msg.SetTimestamp(new FitDateTime(endTime));
        msg.SetStartTime(new FitDateTime(_startTime));
        msg.SetSport(_sport);
        msg.SetSubSport(_subSport);
        msg.SetTotalElapsedTime(_totalElapsedTime);
        msg.SetTotalTimerTime(_totalElapsedTime);
        msg.SetTotalDistance(_totalDistance);
        msg.SetTotalCalories(_totalCalories);
        if (_avgHr.HasValue) msg.SetAvgHeartRate(_avgHr.Value);
        if (_maxHr.HasValue) msg.SetMaxHeartRate(_maxHr.Value);
        if (_avgSpeed.HasValue) msg.SetAvgSpeed(_avgSpeed.Value);
        if (_avgPower.HasValue) msg.SetAvgPower(_avgPower.Value);
        if (_totalTrainingEffect.HasValue) msg.SetTotalTrainingEffect(_totalTrainingEffect.Value);
        enc.Write(msg);
    }

    private void WriteLaps(Encode enc)
    {
        float lapStart = 0f;
        int num = _laps.Count > 0 ? _laps.Count : 1;

        for (int i = 0; i < num; i++)
        {
            LapSpec spec = _laps.Count > 0
                ? _laps[i]
                : new LapSpec(_totalElapsedTime, _totalDistance, _avgHr,
                              _avgSpeed, _avgPower);

            var lapStartTime = _startTime.AddSeconds(lapStart);
            var lapEndTime = lapStartTime.AddSeconds(spec.DurationSec);

            var msg = new LapMesg();
            msg.SetTimestamp(new FitDateTime(lapEndTime));
            msg.SetStartTime(new FitDateTime(lapStartTime));
            msg.SetTotalElapsedTime(spec.DurationSec);
            msg.SetTotalTimerTime(spec.DurationSec);
            msg.SetTotalDistance(spec.DistanceM);
            if (spec.AvgHr.HasValue) msg.SetAvgHeartRate(spec.AvgHr.Value);
            if (spec.AvgSpeedMps.HasValue) msg.SetAvgSpeed(spec.AvgSpeedMps.Value);
            if (spec.AvgPower.HasValue) msg.SetAvgPower(spec.AvgPower.Value);
            enc.Write(msg);

            lapStart += spec.DurationSec;
        }
    }

    private void WriteActivity(Encode enc)
    {
        var msg = new ActivityMesg();
        msg.SetTimestamp(new FitDateTime(_startTime.AddSeconds(_totalElapsedTime)));
        msg.SetTotalTimerTime(_totalElapsedTime);
        msg.SetNumSessions(1);
        msg.SetType(Dynastream.Fit.Activity.Manual);
        msg.SetEvent(Event.Activity);
        msg.SetEventType(EventType.Stop);
        enc.Write(msg);
    }

    private record LapSpec(float DurationSec, float DistanceM,
                           byte? AvgHr, float? AvgSpeedMps, ushort? AvgPower);
}
