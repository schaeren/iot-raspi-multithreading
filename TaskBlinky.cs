using System;
using System.Device.Gpio;

namespace Iot.Raspi.MultiThreading
{
    // TaskBlinky controls the blinking of one LED according to the delay read from StatusPool. The 
    // Start() method creates a thread which reads the delay time (half period time in ms) from
    // StatusPool using a delegate, switches the LED on or off and sleeps for the given delay, all
    // this is executed in an endless loop.
    public class TaskBlinky
    {
        // Delegate type for method to be called for setting delay of corresponding LED
        public delegate TimeSpan GetDelayDelegate();
        // GPIO output pin used for LED
        private int ledPin;

        // Delegate with method to be called for getting delay of corresponding LED
        private GetDelayDelegate getDelay;
        // State of the LES (on/off).
        private bool isLedOn = false;
        // The thread executing the Loop()
        private Thread? thread;

        // Ctor, private -> use static method Start().
        private TaskBlinky(int ledPin, GetDelayDelegate getDelay)
        {
            this.ledPin = ledPin;
            this.getDelay = getDelay;
        }

        // Create and start thread.
        // Parameters:
        // threadName - thread name
        // ledPin     - GPIO output pin used for LED
        // getDelay   - delegate to be used for getting the delay time (half period time) in ms.
        public static TaskBlinky Start(string threadName, int ledPin, GetDelayDelegate getDelay)
        {
            var instance = new TaskBlinky(ledPin, getDelay);
            instance.thread = new Thread(new ThreadStart(instance.Loop));
            instance.thread.Name = threadName;
            instance.thread.Start();
            return instance;
        }

        // Method executed as thread.
        private async void Loop()
        {
            try 
            {
                await AsyncLogger.WriteInfoAsync($"Started, LED pin: {ledPin}.");
                using var controller = new GpioController();
                controller.OpenPin(ledPin, PinMode.Output);
                while (true)
                { 
                    // Get delay from StatusPool
                    var delay = getDelay();
                    if (delay < TimeSpan.MaxValue)
                    {
                        isLedOn = !isLedOn;
                        // Update output for LED
                        controller.Write(ledPin, isLedOn ? PinValue.High : PinValue.Low);
                        // Sleep for given time read from StatusPool
                        Thread.Sleep(getDelay());
                    }
                    else
                    {
                        // delay == TimeSpan.MaxValue means don't blink, LED is swiched off 
                        controller.Write(ledPin, PinValue.Low);
                        Thread.Sleep(TimeSpan.FromMilliseconds(10));
                    }
                }
            }
            catch (Exception ex)
            {
                await AsyncLogger.WriteInfoAsync($"Failed. Exception: {ex.Message}.");
            }
        }
    }
}