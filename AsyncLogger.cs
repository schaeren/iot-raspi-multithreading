using System.Threading.Channels;

namespace Iot.Raspi.MultiThreading
{
    // AsyncLogger provides a logger whose output (to the console) is asynchronous. 
    // That means the logger methods WriteInfoAsync() and WriteInfo() write the message 
    // into a .NET channel (logChannel). 
    // The Loop() method, which runs as an independent thread, reads from this channel 
    // and writes the messages to the console output.
    public class AsyncLogger
    {
        // Maximum number of (unprocessed) log entries in the channel
        private const int logChannelCapacity = 10;

        // .NET channel used to send log messages to the thread loop
        private static Channel<string>? logChannel;
        // The thread executing the Loop()
        private Thread? thread;

        // Ctor, private -> use static method Start().
        private AsyncLogger() 
        {
            logChannel = Channel.CreateBounded<string>(logChannelCapacity);
        }

        // Create and start thread.
        public static AsyncLogger Start()
        {
            var instance = new AsyncLogger();
            instance.thread  = new Thread(instance.Loop);
            instance.thread.Name = instance.GetType().Name;
            instance.thread.Start();
            return instance;
        }

        // Write messgae to log asynchronously.
        // If the queue (channel) with unwritten messages is full, the calling thread is suspended until 
        // space is available in the queue.
        public static async Task WriteInfoAsync(string message, CancellationToken cancellationToken = default)
        {
            try
            {
                message = FormatMessage(message);
                if (logChannel != null)
                {
                    await logChannel.Writer.WriteAsync(message, cancellationToken);
                }
                else
                {
                    Console.WriteLine($"'AsyncLogger.WriteInfo()' failed. Not initialized. Message: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"'AsyncLogger.WriteInfo()' failed. Message: {message}, Exception: {ex.Message}.");
            }
        }

        // Write messgae to log synchronously.
        // If the queue (channel) with unwritten messages is full, the message is written directly to 
        // the console output - this can lead to a confusion with other log messages!
        public static void WriteInfo(string message)
        {
            try
            {
                message = FormatMessage(message);
                if (logChannel != null)
                {
                    if (!logChannel.Writer.TryWrite(message))
                    {
                        Console.WriteLine($"'AsyncLogger.WriteInfo()' failed to write to channel synchronously. Message: {message}");
                    }
                }
                else
                {
                    Console.WriteLine($"'AsyncLogger.WriteInfo()' failed. Not initialized. Message: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"'AsyncLogger.WriteInfo()' failed. Message: {message}, Exception: {ex.Message}.");
            }

        }

        // Method executed as thread.
        private async void Loop()
        {
            try 
            {
                Console.WriteLine(FormatMessage("Started."));
                if (logChannel == null) { throw new Exception("'AsyncLogger.Loop()' failed. logChannel not initialized."); }
                while (true)
                { 
                    var msg = await logChannel.Reader.ReadAsync();
                    Console.WriteLine(msg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed. Exception: {ex.Message}.");
            }
        }

        private static string FormatMessage(string message)
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            var thread = Thread.CurrentThread;
            return $"{now.ToString("HH:mm:ss.fff")} - {thread.Name} ({thread.ManagedThreadId}) - {message}";
        }
    }
}