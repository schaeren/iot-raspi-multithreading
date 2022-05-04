using System;
using System.Threading.Channels;

namespace Iot.Raspi.MultiThreading
{
    class Program
    {
        // EventWaitHandle used by StatusPool to signal TaskLcd each time when a delay has changed
        private static EventWaitHandle delayChangedEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        static void Main(string[] margs)
        {
            try
            {
                var statusPool = new StatusPool(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.MaxValue, delayChangedEvent);

                var taskAsyncLogger = AsyncLogger.Start();
                var taskLcs = TaskLcd.Start(statusPool.GetDelayRed, statusPool.GetDelayYellow, statusPool.GetDelayGreen, delayChangedEvent);

                var taskBlinkyRed    = TaskBlinky.Start("LED red",    17, statusPool.GetDelayRed);
                var taskBlinkyYellow = TaskBlinky.Start("LED yellow", 27, statusPool.GetDelayYellow);
                var taskBlinkyGreen  = TaskBlinky.Start("LED green",  22, statusPool.GetDelayGreen);
                
                var taskADC1 = TaskAnalogIn.Start("ADC 1", 0, delay => statusPool.SetDelayRed(delay));
                var taskADC2 = TaskAnalogIn.Start("ADC 2", 1, delay => statusPool.SetDelayYellow(delay));
                var taskDigitalIn = TaskDigitalIn.Start(delay => statusPool.SetDelayGreen(delay));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Main() failed. Exception: {ex.Message}.");
            }
        }
    }
}
