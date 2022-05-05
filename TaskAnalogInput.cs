using System;
using System.Device.Gpio;
using System.Device.Spi;
using Iot.Device.Adc;

namespace Iot.Raspi.MultiThreading
{
    // TaskAnalogInput implements the input from a potentiometer used to control the blinking frequency
    // of a LED. The Start() method creates a thread which reads in the analog value (voltage from
    // potentiometer) using an Analog-Digital-Converter (ADC), maps the value to the range 
    // minDelay...minDelay (25...1000 ms) and updates the delay time (half period time) for the 
    // corresponding LED in the StatusPool using a delegate, All this is executed in an endless loop. 
    // Remark: The ADC chip is connected to the Raspberry Pi via SPI (Serial Peripheral Interface), 
    // the chip supports 8 analog input channels with 10-bit resolution.
    public class TaskAnalogInput
    {
        // Delegate type for method to be called for setting delay of corresponding LED
        public delegate void SetDelayDelegate(TimeSpan delay);
        // This interval time defines how often the ADC input is to be read out.
        private readonly TimeSpan adcReadIntervalTime = TimeSpan.FromMilliseconds(100);
        // Minimum difference of the analog value so that the delegate is called to update the StatusPool.
        private const int minADCDiff = 2; // Min. difference read from ADC so that delay change is written logQueue in ms
        // The analog value (0..1023) is mapped to a range minDelay..maxDelay configured here.
        private readonly TimeSpan minDelay = TimeSpan.FromMilliseconds(25);
        // The analog value (0..1023) is mapped to a range minDelay..maxDelay configured here.
        private readonly TimeSpan maxDelay = TimeSpan.FromMilliseconds(1000);

        // Analog input channel to be used.
        private int analogInputChannel;
        // Delegate with method to be called for setting delay of corresponding LED
        private SetDelayDelegate setDelay;
        // Set to true as soon as SPI and ADC are initialized.
        private static bool isInitialized = false;
        // The Serial Peripheral Interface (used to access ADC)
        private static SpiDevice? spi = null;
        // The Analog-Digital Converter (ADC), we use the 10-bit 8-channel ADC MCP3008
        private static Mcp3008? adc = null; 
        // Lock used during initalization of SPI and ADC. 
        private static object adcLock = new object();
        // The thread executing the Loop()
        private Thread? thread;

        // Ctor, private -> use static method Start().
        private TaskAnalogInput(int analogInputChannel, SetDelayDelegate setDelay)
        {
            this.analogInputChannel = analogInputChannel;
            this.setDelay = setDelay;
        }

        // Create and start thread.
        // Parameters:
        // threadName         - thread name
        // analogInputChannel - analog input channel to be used (0...7)
        // setDelay           - delegate to be used for setting the LEDs delay time (half period time) in ms.
        public static TaskAnalogInput Start(string threadName, int analogInputChannel, SetDelayDelegate setDelay)
        {
            var instance = new TaskAnalogInput(analogInputChannel, setDelay);
            instance.thread  = new Thread(instance.Loop);
            instance.thread.Name = threadName;
            instance.thread.Start();
            return instance;
        }

        // Method executed as thread.
        private async void Loop()
        {
            try
            {
                await AsyncLogger.WriteInfoAsync($"Started, Analog input: {analogInputChannel}.");
                InitADC();
                int lastADCValue = -1;
                while (true && adc != null)
                { 
                    // Read value from ADC
                    int adcValue = ReadADC(analogInputChannel);
                    if (Math.Abs(adcValue - lastADCValue) >= minADCDiff)
                    {
                        lastADCValue = adcValue;
                        var halfIntervalTime = TimeSpan.FromMilliseconds(MapRange(adcValue, 0, 1023, 25, 1000));
                        // Set delay in StatusPool
                        setDelay(halfIntervalTime);
                        await AsyncLogger.WriteInfoAsync($"ADC value={adcValue} -> delay={halfIntervalTime}");
                    }
                    Thread.Sleep(adcReadIntervalTime);
                }
            }
            catch (Exception ex)
            {
                await AsyncLogger.WriteInfoAsync($"Failed. Exception: {ex.Message}.");
            }
            finally
            {
                UnloadADC();
            }
        }

        // Initialize Serial Peripheral Interface (SPI) and Analog-Digital Converter (ADC).
        private void InitADC()
        {
            // The lock and isInitialized ensure that SPI and ADC are initialized only once, 
            // even if several threads call this method. 
            lock(adcLock) 
            {
                if (!isInitialized)
                {
                    AsyncLogger.WriteInfo("Initializing ADC ...");
                    var spiSettings = new SpiConnectionSettings(0, 0);
                    spi = SpiDevice.Create(spiSettings);
                    adc = new Mcp3008(spi);
                    isInitialized = true;
                    AsyncLogger.WriteInfo("ADC is initialized.");
                }
            }
        }

        // Read value from Analog-Digital-Converter (ADC).
        // Parameters:
        // channel - ADC channel to read 0..7
        // The ADC has a resolution of 10 bits, so the return value is in the range 0..1023.
        private int ReadADC(int channel)
        {
            int value = 0;
            lock(adcLock)
            {
                if (isInitialized && adc != null)
                {
                    value = adc.Read(channel);
                }
                else
                {
                    AsyncLogger.WriteInfo($"Failed to read from ADC, not initialized.");
                }
            }
            return value;
        }

        // Unload / de-initialized ADC and SPI.
        private void UnloadADC()
        {
            lock(adcLock)
            {
                if (!isInitialized)
                {
                    if (adc != null)
                    {
                        adc.Dispose();
                    }
                    if (spi != null)
                    {
                        spi.Dispose();
                    }
                    isInitialized = false;
                }
            }
        }
 
        // Map value in range fromMin..fromMax to range toMin..toMax
        // Example: MapRange(20,10,30,0,100) -> 50
        private int MapRange(int value, int fromMin, int fromMax, int toMin, int toMax)
        {
            return (value - fromMin) * (toMax - toMin) / (fromMax - fromMin) + toMin;
        }
   }
}