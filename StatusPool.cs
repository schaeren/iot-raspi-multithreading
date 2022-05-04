namespace Iot.Raspi.MultiThreading
{
    // The StatusPool holds the current delay values for the red, yellow and green LED. The delay 
    // value is equal to the half period time used for blinking.
    // Access to delay values is protected by mutex, even this isn't neccessary in this case, because
    // integer variables can be read or written in an atomic operations (without interruption by 
    // another thread).
    // Each time a value is changed, an EventWaitHandle is set to signal TaskLcd that a delay value 
    // has been changed.
    public class StatusPool
    {
        // Colors of the 3 LEDs.
        private enum Color {Red, Yellow, Green};

        // Delay time for red LED. This half-period time determines the flashing frequency for the LED.
        private TimeSpan delayRed;
        // Delay time for yellow LED. This half-period time determines the flashing frequency for the LED.
        private TimeSpan delayYellow;
        // Delay time for green LED. This half-period time determines the flashing frequency for the LED.
        private TimeSpan delayGreen;
        // Mutex used to protect access to delay times.
        private Mutex mutex = new Mutex();
        // This EventWaitHandle is set each time a delay has been changed. Used to inform TaskLcd about changes.
        private EventWaitHandle delayChangedEvent;

        // Ctor.
        // parameters:
        // delayRed          - initial delay for red LED
        // delayYellow       - initial delay for yellow LED
        // delayGreen        - initial delay for green LED
        // delayChangedEvent - EventWaitHandle to be signaled upon changes of delay times.
        public StatusPool(TimeSpan delayRed, TimeSpan delayYellow, TimeSpan delayGreen, EventWaitHandle delayChangedEvent)
        {
            this.delayRed = delayRed;
            this.delayYellow = delayYellow;
            this.delayGreen = delayGreen;
            this.delayChangedEvent = delayChangedEvent;
        }

        public TimeSpan GetDelayRed() { return GetDelay(Color.Red); }
        public void SetDelayRed(TimeSpan delay) { SetDelay(Color.Red, delay); }

        public TimeSpan GetDelayYellow() { return GetDelay(Color.Yellow); }
        public void SetDelayYellow(TimeSpan delay) { SetDelay(Color.Yellow, delay); }

        public TimeSpan GetDelayGreen() { return GetDelay(Color.Green); }
        public void SetDelayGreen(TimeSpan delay) { SetDelay(Color.Green, delay); }

        private void SetDelay (Color color, TimeSpan delay)
        {
            mutex.WaitOne();
            bool delayChanged = false;
            switch (color)
            {
                case Color.Red:    if (delay != delayRed)    { delayRed    = delay; delayChanged = true; } break;
                case Color.Yellow: if (delay != delayYellow) { delayYellow = delay; delayChanged = true; } break;
                case Color.Green:  if (delay != delayGreen)  { delayGreen  = delay; delayChanged = true; } break;
            }
            mutex.ReleaseMutex();
            if (delayChanged)
            {
                delayChangedEvent.Set();
            }
        }

        private TimeSpan GetDelay (Color color)
        {
            TimeSpan delay;
            mutex.WaitOne();
            switch (color)
            {
                case Color.Red:    delay = delayRed;    break;
                case Color.Yellow: delay = delayYellow; break;
                case Color.Green:  delay = delayGreen;  break;
                default: delay = TimeSpan.FromSeconds(1); break;
            }
            mutex.ReleaseMutex();
            return delay;
        }
    }    
}
