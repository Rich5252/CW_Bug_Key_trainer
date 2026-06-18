using System;

namespace CwTrainer.Serial
{
    /// <summary>
    /// A single debounced key transition, as reported by the ESP32 over CDC serial.
    /// Wire format: "DOWN,<timestamp_us>\r\n" or "UP,<timestamp_us>\r\n"
    ///
    /// timestamp_us is the ESP32's esp_timer_get_time() value AT the moment the
    /// debounce decision was committed - this is the device's own monotonic
    /// microsecond clock, NOT the time the PC received the byte. Durations
    /// computed by differencing two KeyEvent.TimestampUs values are therefore
    /// immune to USB/serial transport jitter - only the *notification* of an
    /// edge is delayed by transport, not the measurement itself.
    /// </summary>
    public readonly struct KeyEvent
    {
        public bool KeyDown { get; }
        public long TimestampUs { get; }

        public KeyEvent(bool keyDown, long timestampUs)
        {
            KeyDown = keyDown;
            TimestampUs = timestampUs;
        }

        public override string ToString() =>
            $"{(KeyDown ? "DOWN" : "UP")},{TimestampUs}";
    }

    /// <summary>
    /// A completed mark (key-down period) or space (key-up period), derived by
    /// pairing consecutive KeyEvents. This is the unit the rest of the trainer's
    /// analysis (dit/dah ratio, WPM, jitter) should consume.
    /// </summary>
    public readonly struct Element
    {
        /// <summary>True = this was a mark (key was down), false = a space (key was up).</summary>
        public bool IsMark { get; }

        /// <summary>Duration in milliseconds, computed from ESP32 timestamp deltas.</summary>
        public double DurationMs { get; }

        /// <summary>Wall-clock time the element STARTED, in PC local time - for session timelines/UI display only, not for timing analysis.</summary>
        public DateTime StartedAt { get; }

        public Element(bool isMark, double durationMs, DateTime startedAt)
        {
            IsMark = isMark;
            DurationMs = durationMs;
            StartedAt = startedAt;
        }

        public override string ToString() =>
            $"{(IsMark ? "MARK" : "SPACE")} {DurationMs:F1}ms";
    }
}
