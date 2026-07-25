using System;
using System.Globalization;

namespace CwTrainer.Serial
{
    // NOTE: KeyEvent and Element themselves live in KeyEvent.cs - this file only
    // adds the shared parser and the transport-agnostic interface, so both the
    // serial handler and any other transport (UDP, named pipe, ...) produce and
    // consume the exact same KeyEvent type with no duplication or renaming.

    /// <summary>
    /// Parses the wire format shared by every transport: "DOWN,<int64>" or "UP,<int64>".
    /// Kept transport-agnostic so serial, named-pipe, and UDP sources all parse identically.
    /// </summary>
    public static class KeyEventLineParser
    {
        public static bool TryParse(string line, out KeyEvent evt)
        {
            evt = default;
            if (string.IsNullOrEmpty(line)) return false;

            int commaIndex = line.IndexOf(',');
            if (commaIndex < 0) return false;

            string keyword = line.Substring(0, commaIndex);
            string numberPart = line.Substring(commaIndex + 1);

            bool keyDown;
            if (keyword == "DOWN") keyDown = true;
            else if (keyword == "UP") keyDown = false;
            else return false;

            if (!long.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestampUs))
                return false;

            evt = new KeyEvent(keyDown, timestampUs);
            return true;
        }
    }

    /// <summary>
    /// Implemented by any transport that can deliver key-down/key-up timing events
    /// (serial port, named pipe, UDP multicast, ...). Lets the analyzer subscribe to
    /// "key events" without caring how they arrived, and makes swapping or running
    /// multiple sources side-by-side a one-line change at the wiring point.
    /// </summary>
    public interface IKeyEventSource
    {
        event EventHandler<KeyEvent> KeyEventReceived;
    }
}