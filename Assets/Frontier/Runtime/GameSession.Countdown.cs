using System;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        public const int StartCountdownSeconds = 3;
        public int CountdownNumber { get; private set; }
        public bool IsCountingDown => Running && !IsReplay && CountdownNumber > 0;
        public event Action<int> CountdownChanged;
        private float countdownRemaining;

        private void PrepareCountdown(float seconds)
        {
            countdownRemaining = Mathf.Clamp(seconds, 0, StartCountdownSeconds);
            CountdownNumber = Mathf.CeilToInt(countdownRemaining);
        }

        private void AdvanceCountdown(float seconds)
        {
            if (!IsCountingDown) return;
            // Offline interruptions retain the current number. Online uses the
            // server clock: a headset interruption cannot pause the other player.
            if (IsOnline) countdownRemaining = remoteClient == null ? 0 : (float)remoteClient.CountdownRemaining;
            else if (pauseReasons.Count == 0) countdownRemaining = Mathf.Max(0, countdownRemaining - Mathf.Clamp(seconds, 0, .25f));
            int number = Mathf.Clamp(Mathf.CeilToInt(countdownRemaining), 0, StartCountdownSeconds);
            if (number == CountdownNumber) return;
            CountdownNumber = number;
            if (Match != null) Match.Paused = IsPaused;
            CountdownChanged?.Invoke(number);
            Changed?.Invoke();
        }
    }
}
