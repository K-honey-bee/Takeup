using System;
using UnityEngine;

namespace TakeUp.Prototype.Editor
{
    public static class RhythmValidation
    {
        public static void Validate()
        {
            var notes = new[] { BeatDirection.Left, BeatDirection.Right, BeatDirection.Left };
            var judge = new RhythmJudge(notes, 1, 120, 0.15);
            Check(judge.Press(BeatDirection.Left, 1), "first hit");
            Check(judge.Press(BeatDirection.Right, 1.64), "late hit within window");
            Check(judge.Press(BeatDirection.Left, 1.86) && judge.Complete, "early hit within window");
            Check(!judge.Press(BeatDirection.Left, 2), "completed pattern rejects input");
            judge = new RhythmJudge(notes, 1, 120, 0.15);
            Check(!judge.Press(BeatDirection.Left, 0.8) && judge.Failed, "early input fails");
            judge = new RhythmJudge(notes, 1, 120, 0.15);
            Check(!judge.Press(BeatDirection.Right, 1) && judge.Failed, "wrong direction fails");
            judge = new RhythmJudge(notes, 1, 120, 0.15); judge.Advance(1.151);
            Check(judge.Failed, "missed note times out");
            judge = new RhythmJudge(notes, 1, 120, 0.15); judge.Press(BeatDirection.Left, 1);
            Check(!judge.Press(BeatDirection.Left, 1.01) && judge.Failed, "mashing fails");
            bool rejected = false;
            try { new RhythmJudge(Array.Empty<BeatDirection>(), 1, 120, 0.15); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "empty pattern rejected");
            Debug.Log("PASS: rhythm timing windows, exact sequence, early/wrong/missed/mashing rejection.");
        }
        static void Check(bool condition, string label) { if (!condition) throw new Exception("Rhythm test failed: " + label); }
    }
}
