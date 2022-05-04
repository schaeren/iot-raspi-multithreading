using System.Threading.Channels;
using System.Device.Gpio;

namespace Iot.Raspi.MultiThreading
{
    // TaskDigitalIn implements the input from 3 buttons used to control the blinking frequency of the green 
    // LED.
    // The 3 buttons for 'stop blinking', 'slow blinking' and 'fast blinking' are read in and debounced by a 
    // callback (~ interrupt handler). The callback also determines a command (stop, slow, fast) depending 
    // on the button pressed and sends it via Channel (System.Threading.Channels.Channel<T>) to a thread 
    // (method Loop()). 
    // Loop() receives the command and updates the delay time (half period time) for the green LED in the 
    // StatusPool using a delegate.
    public class TaskDigitalIn
    {
        // Delegate type for method to be called for setting delay of green LED
        public delegate void SetDelayDelegate(TimeSpan delay);
        // Enum type used for commands allowed in commandChannel
        private enum ECommand {Stop, Slow, Fast, Undefined};
        // GPIO input pin for button 'stop blinking' of green LED
        private const int pinButtonStop = 18;
        // GPIO input pin for button 'slow blicking' of green LED
        private const int pinButtonSlow = 23;
        // GPIO input pin for button 'fast blinking' of green LED
        private const int pinButtonFast = 24;
        // Max capacity for commandChannel, i.e. max number of commands (ECommand) in queue
        private const int commandChannelCapacity = 3;
        // Delay for 'slow blinking' of green LED (1 Hz)
        private readonly TimeSpan slowDelay = TimeSpan.FromMilliseconds(500);
        // Delay for 'fast blinking' of green LED (0.1 Hz)
        private readonly TimeSpan fastDelay = TimeSpan.FromMilliseconds(50);


        // .NET channel used to send commands (ECommand) from button callbacks to thread loop
        private Channel<ECommand>? commandChannel;
        // Delegate with method to be called for setting delay of green LED
        private SetDelayDelegate setDelay;
        // Delay used for debouncing buttons
        private readonly TimeSpan debounceDuration = TimeSpan.FromMilliseconds(20);
        // Last time a button change was detected (used for debouncing, too)
        private DateTime lastButtonStateChangeTime = DateTime.MinValue;
        // Previous state of button 'stop blinking' of green LED
        private PinValue previousStopButtonState = PinValue.High;
        // Previous state of button 'slow blicking' of green LED
        private PinValue previousSlowButtonState = PinValue.High;
        // Previous state of button 'fast blicking' of green LED
        private PinValue previoustFastButtonState = PinValue.High;
        // GPIO controller used to access I/O pins
        private GpioController? controller;
        // The thread executing the Loop()
        private Thread? thread;

        // Ctor, private -> use static method Start().
        private TaskDigitalIn(SetDelayDelegate setDelay)
        {
            this.setDelay = setDelay;
        }

        // Create and start thread.
        // Parameters:
        // setDelay - delegate to be used for setting the LEDs delay time (half period time) in ms.
        public static TaskDigitalIn Start(SetDelayDelegate setDelay)
        {
            var instance = new TaskDigitalIn(setDelay);
            instance.thread  = new Thread(instance.Loop);
            instance.thread.Name = instance.GetType().Name;
            instance.thread.Start();
            return instance;
        }

        // Method executed as thread.
        private async void Loop()
        {
            try
            {
                await AsyncLogger.WriteInfoAsync($"Started, Digital inputs: .");
                controller = new GpioController();
                commandChannel = Channel.CreateBounded<ECommand>(commandChannelCapacity);
                controller.OpenPin(pinButtonStop, PinMode.InputPullUp);
                controller.OpenPin(pinButtonSlow, PinMode.InputPullUp);
                controller.OpenPin(pinButtonFast, PinMode.InputPullUp);

                controller.RegisterCallbackForPinValueChangedEvent(pinButtonStop, PinEventTypes.Falling, ButtonChanged);
                controller.RegisterCallbackForPinValueChangedEvent(pinButtonSlow, PinEventTypes.Falling, ButtonChanged);
                controller.RegisterCallbackForPinValueChangedEvent(pinButtonFast, PinEventTypes.Falling, ButtonChanged);
                controller.RegisterCallbackForPinValueChangedEvent(pinButtonStop, PinEventTypes.Rising, ButtonChanged);
                controller.RegisterCallbackForPinValueChangedEvent(pinButtonSlow, PinEventTypes.Rising, ButtonChanged);
                controller.RegisterCallbackForPinValueChangedEvent(pinButtonFast, PinEventTypes.Rising, ButtonChanged);

                if (commandChannel == null) { throw new Exception("'TaskDigitalIn.Loop()' failed. commandChannel not initialized."); }
                while (true)
                { 
                    // Receive command (ECommand) from button callback method.
                    var command = await commandChannel.Reader.ReadAsync();
                    // Set delay in StatusPool
                    switch (command)
                    {
                        case ECommand.Stop:
                            setDelay(TimeSpan.MaxValue);
                            break;
                        case ECommand.Slow:
                            setDelay(slowDelay);
                            break;
                        case ECommand.Fast:
                            setDelay(fastDelay);
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await AsyncLogger.WriteInfoAsync($"Failed. Exception: {ex.Message}.");
            }
            finally
            {
                controller?.Dispose();
            }
        }

        // Callback method for button changes.
        // Depending on the button pressed a command (stop, slow, fast) is sent to the thread Loop().
        // Remark: Buttons are debounced 'by software' in this method.
        private async void ButtonChanged(object sender, PinValueChangedEventArgs eventArgs)
        {
            var now = DateTime.Now;
            if (controller == null) { throw new Exception("'TaskDigitalIn.ButtonChanged()' failed. controller not initialized."); }
            var stopButtonState = controller.Read(pinButtonStop);
            var slowButtonState = controller.Read(pinButtonSlow);
            var fastButtonState = controller.Read(pinButtonFast);
            if (   (stopButtonState != previousStopButtonState ||
                    slowButtonState != previousSlowButtonState ||
                    fastButtonState != previoustFastButtonState) 
                && (now - lastButtonStateChangeTime > debounceDuration)   )
            {
                previousStopButtonState = stopButtonState;
                previousSlowButtonState = slowButtonState;
                previoustFastButtonState = fastButtonState;
                lastButtonStateChangeTime = now;

                ECommand command = ECommand.Undefined;
                if (stopButtonState == PinValue.Low)
                {
                    command = ECommand.Stop;
                }
                else if (slowButtonState == PinValue.Low)
                {
                    command = ECommand.Slow;
                }
                else if (fastButtonState == PinValue.Low)
                {
                    command = ECommand.Fast;
                }
                if (command != ECommand.Undefined)
                {
                    if (commandChannel == null) { throw new Exception("'TaskDigitalIn.ButtonChanged()' failed. commandChannel not initialized."); }
                    // Send command (ECommand) to thread Loop()
                    await commandChannel.Writer.WriteAsync(command);
                    await AsyncLogger.WriteInfoAsync($"Button {command} pressed.");
                }
            }
        }
    }
}