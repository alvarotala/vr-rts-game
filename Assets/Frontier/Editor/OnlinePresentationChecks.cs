using System;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class OnlinePresentationChecks
    {
        public static void RunRegressionChecks()
        {
            Run();
            MultiplayerValidation.RunSessionChecks();
            ServerLifecycleChecks.Run();
            Debug.Log("FRONTIER_ONLINE_REGRESSION passed; presentation, session adapters and dedicated server lifecycle");
        }

        [MenuItem("Frontier/Multiplayer/Check online presentation timing")]
        public static void Run()
        {
            int checks = 0;
            const double frame = 1d / 72;
            var regular = new OnlinePresentation();
            var view = View(0); regular.Receive(view, 0);
            float previousAnimation = 0, previousPosition = 0;
            int nextTick = 2;
            for (int i = 1; i <= 360; i++)
            {
                double now = i * frame;
                if (now + .000001 >= nextTick / 20d)
                {
                    view = View(nextTick); regular.Receive(view, now); nextTick += 2;
                }
                regular.Advance(now);
                float position = regular.Position(view.entities[0]).x;
                if (i > 24)
                {
                    Check(regular.AnimationTick - previousAnimation > .20f, "10 Hz snapshots must animate on every 72 Hz frame, including the second 50 ms.");
                    Check(position - previousPosition > .20f && position - previousPosition < .32f, "Steady movement must advance continuously at the sampled speed.");
                }
                Check(position <= view.tick + .001f, "Positions never extrapolate beyond an observed sample.");
                previousAnimation = regular.AnimationTick; previousPosition = position;
            }

            // Irregular delivery and one dropped frame: receipt must not restart
            // the trajectory or move it backward. All sample positions lie x=tick.
            var jitter = new OnlinePresentation(); view = View(0); jitter.Receive(view, 0);
            double[] arrivals = { .118, .193, .322, .391, .529, .731, .794, .922, 1.018 };
            int[] ticks = { 2, 4, 6, 8, 10, 14, 16, 18, 20 };
            int arrival = 0; previousPosition = previousAnimation = 0;
            for (int i = 1; i <= 78; i++)
            {
                double now = i * frame; jitter.Advance(now);
                float before = jitter.Position(view.entities[0]).x;
                while (arrival < arrivals.Length && now >= arrivals[arrival])
                {
                    view = View(ticks[arrival++]); jitter.Receive(view, now);
                    Check(Math.Abs(jitter.Position(view.entities[0]).x - before) < .001f, "Packet arrival must not restart or jump the position interpolation.");
                }
                float position = jitter.Position(view.entities[0]).x;
                Check(position >= previousPosition - .001f && position - previousPosition <= frame * 20 * 1.101 + .001, "Jitter recovery stays continuous with bounded catch-up speed.");
                Check(position <= view.tick + .001f, "Jitter cannot extrapolate movement.");
                Check(jitter.AnimationTick >= previousAnimation, "A late snapshot cannot rewind animation.");
                previousPosition = position; previousAnimation = jitter.AnimationTick;
            }
            jitter.Advance(2);
            Check(Math.Abs(jitter.AnimationTick - (view.tick + OnlinePresentation.BufferTicks)) < .001f, "Animation stops after the 150 ms allowance when snapshots stall.");
            Check(Math.Abs(jitter.Position(view.entities[0]).x - view.tick) < .001f, "A stalled position stops exactly at the last observation.");
            float stalled = jitter.AnimationTick; jitter.Advance(4);
            Check(jitter.AnimationTick == stalled, "A longer outage cannot keep work animations running.");
            view = View(80); jitter.Receive(view, 4);
            Check(jitter.Position(view.entities[0]).x == 80, "A long gap discards old trajectories and resynchronizes at the observation.");

            var visibility = new OnlinePresentation(); visibility.Receive(View(0), 0);
            visibility.Receive(View(2), .1); visibility.Advance(.2);
            var remembered = View(4); remembered.entities[0].remembered = true;
            visibility.Receive(remembered, .2);
            Check(visibility.Position(remembered.entities[0]).x == 4, "Remembered entities use their latest observation directly.");
            var appeared = View(6, 60); visibility.Receive(appeared, .3);
            Check(visibility.Position(appeared.entities[0]).x == 60, "Reappearance snaps directly to the newly observed position.");
            var following = View(8, 62); visibility.Receive(following, .4);
            Check(visibility.Position(following.entities[0]).x == 60, "A new appearance stays at its first observation while the buffer catches up.");
            visibility.Advance(.5);
            Check(Math.Abs(visibility.Position(following.entities[0]).x - 61) < .001f, "Movement resumes only along observations after reappearance.");
            visibility.Receive(new PlayerView { tick = 10 }, .5);
            appeared = View(12, 120); visibility.Receive(appeared, .6);
            Check(visibility.Position(appeared.entities[0]).x == 120, "An omitted entity also starts a fresh trajectory when visible again.");
            appeared.entities[0].position = new Point(999, 0);
            Check(visibility.Position(appeared.entities[0]).x == 120, "Buffered positions are copied rather than aliasing mutable views.");

            var pause = new OnlinePresentation(); pause.Receive(View(0), 0);
            pause.Receive(View(2), .1); pause.Advance(.12);
            float animation = pause.AnimationTick, positionTick = pause.PositionTick;
            pause.Advance(1, true);
            Check(pause.AnimationTick == animation && pause.PositionTick == positionTick, "Countdown pauses both cosmetic clocks.");
            pause.Advance(1.01);
            Check(Math.Abs(pause.AnimationTick - animation - .2f) < .001f, "Resume excludes paused wall time.");
            animation = pause.AnimationTick; pause.Advance(1.01);
            Check(pause.AnimationTick == animation, "Multiple visual consumers in one frame share one clock step.");
            var terminal = View(4); terminal.winner = 0; pause.Receive(terminal, 1.1); pause.Advance(5);
            Check(pause.AnimationTick == 24 && pause.Position(terminal.entities[0]).x == 4, "Final effects can complete without extrapolating units.");
            pause.Reset(); pause.Receive(View(0, 50), 10);
            Check(pause.AnimationTick == 0 && pause.Position(View(0).entities[0]).x == 50, "A new match resets timing and trajectories.");
            pause.Receive(View(0, 50), 10.1); pause.Advance(11);
            Check(pause.AnimationTick == OnlinePresentation.BufferTicks, "Duplicate countdown ticks cannot renew the stall allowance indefinitely.");
            Debug.Log($"FRONTIER_ONLINE_PRESENTATION passed: {checks} checks; synthetic 10 Hz / 72 FPS, jitter, loss, stalls, visibility, pause and reset; no device validation");

            void Check(bool condition, string message)
            { if (!condition) throw new Exception(message); checks++; }
        }

        private static PlayerView View(int tick, float? x = null) => new PlayerView
        {
            tick = tick,
            entities = new[] { new VisibleEntity { id = 1, position = new Point(x ?? tick, 0) } }
        };
    }
}
