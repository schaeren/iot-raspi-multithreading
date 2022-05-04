using Iot.Device.CharacterLcd;

namespace Iot.Raspi.MultiThreading
{
    // TaskLcd displays the current delays for the red, yellow and green LEDs on a Liquid Cristal 
    // Display (LCD).
    // The Start() method creates a thread which initializes the LCD of type HD44780 with 2 lines, 
    // each with 16 characters. 
    // After that the thread waits for an EventWaitHandle, which is signaled by StatusPool each 
    // time a delay has been changed. After receiving the signal the delays are read from StatusPool 
    // and displayed on the LCD. All this is executed in an endless loop.
    public class TaskLcd
    {
        // Delegate type for method to be called for getting delays (delays used for LED blinking)
        public delegate TimeSpan GetDelayDelegate();
 
        // GPIO input pin connected with LCD's RegisterSelect
        private const int pinRegisterSelect = 12;
        // GPIO input pin connected with LCD's Enable
        private const int pinEnable = 6;
        // GPIO pins connected to LCD's D4..D7
        private int[] pinsData = new int[] { 20, 26, 16, 19 };

        // Delegate with method to be called for getting delay of red LED
        private GetDelayDelegate getDelayRed;
        // Delegate with method to be called for getting delay of yellow LED
        private GetDelayDelegate getDelayYellow;
        // Delegate with method to be called for getting delay of green LED
        private GetDelayDelegate getDelayGreen;
        // EventWaitHandle siganled by StatusPool each time when a delay has changed
        private EventWaitHandle delayChangedEvent;
        // The thread executing the Loop()
        private Thread? thread;

        // Ctor, private -> use static method Start().
        private TaskLcd(
            GetDelayDelegate getDelayRed, 
            GetDelayDelegate getDelayYellow, 
            GetDelayDelegate getDelayGreen, 
            EventWaitHandle delayChangedEvent)
        {
            this.getDelayRed = getDelayRed;
            this.getDelayYellow = getDelayYellow;
            this.getDelayGreen = getDelayGreen;
            this.delayChangedEvent = delayChangedEvent;
        }

        // Create and start thread.
        // Parameters:
        // getDelayRed       - delegate with method to be called for getting delay of red LED
        // getDelayYellow    - delegate with method to be called for getting delay of yellow LED
        // getDelayGreen     - delegate with method to be called for getting delay of green LED
        // delayChangedEvent - EventWaitHandle siganled by StatusPool each time when a delay has changed
        public static TaskLcd Start(
            GetDelayDelegate getDelayRed, 
            GetDelayDelegate getDelayYellow, 
            GetDelayDelegate getDelayGreen, 
            EventWaitHandle delayChangedEvent)
        {
            var instance = new TaskLcd(getDelayRed, getDelayYellow, getDelayGreen, delayChangedEvent);
            instance.thread = new Thread(instance.Loop);
            instance.thread.Name = instance.GetType().Name;
            instance.thread.Start();
            return instance;
        }

        // Method executed as thread.
        private async void Loop()
        {
            try 
            {
                await AsyncLogger.WriteInfoAsync($"Started.");

                var size = new SixLabors.ImageSharp.Size(16, 2);
                var lcdInterface = LcdInterface.CreateGpio(pinRegisterSelect, pinEnable, pinsData);
                using Hd44780 lcd = new Hd44780(size, lcdInterface);
                lcd.Clear();
                Thread.Sleep(500);
                lcd.Write("red yellow green");
                while (true)
                {
                    // Wait for next change in StatusPool
                    delayChangedEvent.WaitOne();
                    // Get delays from StatusPool, convert to ms and write to LCD
                    var delayRed    = DelayToMs(getDelayRed());
                    var delayYellow = DelayToMs(getDelayYellow());
                    var delayGreen  = DelayToMs(getDelayGreen());
                    var line = $"{delayRed}  {delayYellow}  {delayGreen}";
                    lcd.SetCursorPosition(0, 1);
                    lcd.Write(line);
                }
            }
            catch (Exception ex)
            {
                await AsyncLogger.WriteInfoAsync($"Failed. Exception: {ex.Message}.");
            }
        }

        // Convert TimeSpan (delay) to a 4-digit value representing milliseconds. 
        // delay mast not be greater than 9.999 seconds.
        // delay == TimeSpan.MaxValue is converted to "   -" (no bliking, LED is off).
        private string DelayToMs(TimeSpan delay)
        {
            if (delay == TimeSpan.MaxValue) { return "   -"; }
            var ms = (int) delay.TotalMilliseconds;
            return $"{ms:0000}";
        }
    }
}