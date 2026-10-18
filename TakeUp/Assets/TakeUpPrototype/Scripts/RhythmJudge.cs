using System;

namespace TakeUp.Prototype
{
    // Pure timing logic: audio DSP time is supplied by the game; no frame-rate dependence.
    public sealed class RhythmJudge
    {
        public int Hits { get; private set; }
        public bool Failed { get; private set; }
        public bool Complete => !Failed && Hits == notes.Length;
        public int Count => notes.Length;
        public BeatDirection Note(int index) => notes[index];
        public double TimeFor(int index) => firstBeat + interval * index;
        readonly BeatDirection[] notes;
        readonly double firstBeat, interval, window;
        public RhythmJudge(BeatDirection[] sequence, double first, double bpm, double tolerance)
        {
            if (sequence == null || sequence.Length == 0) throw new ArgumentException("리듬 패턴이 비어 있습니다.");
            notes = (BeatDirection[])sequence.Clone();
            firstBeat = Math.Max(0.5, first);
            interval = 60 / Math.Max(40, bpm);
            window = Math.Min(Math.Max(0.01, tolerance), interval * 0.45);
        }
        public bool Press(BeatDirection direction, double time)
        {
            if (Failed || Complete) return false;
            if (direction != notes[Hits] || Math.Abs(time - TimeFor(Hits)) > window) { Failed = true; return false; }
            Hits++; return true;
        }
        public void Advance(double time)
        {
            if (!Failed && !Complete && time > TimeFor(Hits) + window) Failed = true;
        }
    }
}
