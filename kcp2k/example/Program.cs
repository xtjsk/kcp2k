// See https://aka.ms/new-console-template for more information

using kcp2k.kcp;

namespace kcp2k.example
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // greeting
            Console.WriteLine("kcp example");


            // common config
            const ushort port = 3100;
            var config = new KcpConfig(
                // force NoDelay and minimum interval.
                // this way UpdateSeveralTimes() doesn't need to wait very long and
                // tests run a lot faster.
                NoDelay: true,
                // not all platforms support DualMode.
                // run tests without it so they work on all platforms.
                DualMode: false,
                Interval: 1, // 1ms so at interval code at least runs.
                Timeout: 2000,

                // large window sizes so large messages are flushed with very few
                // update calls. otherwise tests take too long.
                SendWindowSize: Kcp.WND_SND * 1000,
                ReceiveWindowSize: Kcp.WND_RCV * 1000,

                // congestion window _heavily_ restricts send/recv window sizes
                // sending a max sized message would require thousands of updates.
                CongestionWindow: false,

                // maximum retransmit attempts until dead_link detected
                // default * 2 to check if configuration works
                MaxRetransmits: Kcp.DEADLINK * 2,
                IsReliablePing: false
            );

            // create server
            var server = new KcpServer(
                (connectionId) => { Log.Info($"server on connected: {connectionId}"); },
                (connectionId, message, channel) =>
                {
                    Log.Info(
                        $"server on data: {connectionId} {channel} {BitConverter.ToString(message.Array, message.Offset, message.Count)}");
                },
                (connectionId) => { Log.Info($"server on disconnected: {connectionId}"); },
                (connectionId, error, reason) => { Log.Warning($"server on error: {connectionId} {error} {reason}"); },
                config
            );

            // create client
            var client = new KcpClient(
                () => { Log.Info("client on connected"); },
                (message, channel) =>
                {
                    Log.Info(
                        $"client on data: {channel} {BitConverter.ToString(message.Array, message.Offset, message.Count)}");
                },
                () => { Log.Info("client on disconnected"); },
                (error, reason) => { Log.Warning($"client on error: {error} {reason}"); },
                config
            );

            // start server
            server.Start(port);

            // connect client
            client.Connect("127.0.0.1", port);
            UpdateSeveralTimes(5, server, client, config);

            // send client to server
            client.Send(new byte[] { 0x01, 0x02 }, KcpChannel.Reliable);
            UpdateSeveralTimes(10, server, client, config);

            // send server to client
            var firstConnectionId = server.connections.Keys.First();
            server.Send(firstConnectionId, new byte[] { 0x03, 0x04 }, KcpChannel.Reliable);
            UpdateSeveralTimes(10, server, client, config);
        }

        // convenience function
        static void UpdateSeveralTimes(int amount, KcpServer server, KcpClient client, KcpConfig config)
        {
            // update serveral times to avoid flaky tests.
            // => need to update at 120 times for default maxed sized messages
            //    where it requires 120+ fragments.
            // => need to update even more often for 2x default max sized
            for (var i = 0; i < amount; ++i)
            {
                client.Tick();
                server.Tick();
                // update 'interval' milliseconds.
                // the lower the interval, the faster the tests will run.
                Thread.Sleep((int)config.Interval);
            }
        }
    }
}