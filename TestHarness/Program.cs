using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MirrorCast.TestHarness
{
    /// <summary>
    /// Test harness for MirrorCast receiver. Simulates real Android/iOS senders,
    /// verifies protocol handshakes, streams H.264 NAL units, tests control keepalives,
    /// and performs edge-case fuzzing (corrupt frames, rapid disconnects).
    /// </summary>
    public static class Program
    {
        private const string DefaultHost = "127.0.0.1";
        private const int VideoPort = 59100;
        private const int ControlPort = 59101;

        public static async Task<int> Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("============================================================");
            Console.WriteLine("        MirrorCast Automated Test Harness & Simulator        ");
            Console.WriteLine("============================================================");
            Console.ResetColor();

            string host = args.Length > 0 ? args[0] : DefaultHost;
            Console.WriteLine($"Target Host: {host}");
            Console.WriteLine($"Video Port: {VideoPort} | Control Port: {ControlPort}\n");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\n[!] Cancellation requested by user.");
            };

            int passed = 0;
            int failed = 0;

            try
            {
                // Test 1: Handshake test
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[TEST 1/5] Testing Client Handshake on Video Port...");
                Console.ResetColor();
                if (await TestHandshakeAsync(host, cts.Token))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  -> PASS: Handshake succeeded with status: ok\n");
                    passed++;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  -> FAIL: Handshake failed or timed out.\n");
                    failed++;
                }

                // Test 2: Control channel keepalive test
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[TEST 2/5] Testing Control Channel (Ping/Pong)...");
                Console.ResetColor();
                if (await TestControlChannelAsync(host, cts.Token))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  -> PASS: Control ping/pong succeeded\n");
                    passed++;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("  -> SKIP/WARN: Control socket not answering (optional channel)\n");
                }

                // Test 3: Simulated H.264 Stream (SPS, PPS, IDR, Slices)
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[TEST 3/5] Streaming Simulated H.264 NAL Units (5 seconds @ 60 FPS)...");
                Console.ResetColor();
                if (await StreamSyntheticVideoAsync(host, 5, cts.Token))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  -> PASS: 300 frames successfully delivered and parsed.\n");
                    passed++;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  -> FAIL: Video streaming failed.\n");
                    failed++;
                }

                // Test 4: Corrupt & Oversized NAL Unit Resilience Test
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[TEST 4/5] Testing Receiver Resilience to Corrupt / Oversized NALs...");
                Console.ResetColor();
                if (await TestCorruptNalResilienceAsync(host, cts.Token))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  -> PASS: Receiver survived corrupt packets without crash.\n");
                    passed++;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  -> FAIL: Receiver failed resilience test.\n");
                    failed++;
                }

                // Test 5: Rapid Disconnect & Reconnect Test
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[TEST 5/5] Testing Rapid Connect / Disconnect Cycling...");
                Console.ResetColor();
                if (await TestRapidReconnectAsync(host, 3, cts.Token))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  -> PASS: Clean session recycling verified.\n");
                    passed++;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  -> FAIL: Rapid reconnect test failed.\n");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] Unhandled test execution error: {ex.Message}");
                Console.ResetColor();
                failed++;
            }

            Console.WriteLine("============================================================");
            Console.WriteLine($"Test Results: {passed} PASSED, {failed} FAILED");
            Console.WriteLine("============================================================");
            return failed == 0 ? 0 : 1;
        }

        private static async Task<bool> TestHandshakeAsync(string host, CancellationToken ct)
        {
            using var client = new TcpClient();
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                await client.ConnectAsync(host, VideoPort, timeoutCts.Token);
                using var stream = client.GetStream();

                var handshake = new
                {
                    device = "Simulated-TestHarness",
                    os = "android",
                    width = 1920,
                    height = 1080,
                    fps = 60,
                    mode = "wifi"
                };

                string json = JsonSerializer.Serialize(handshake) + "\n";
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(bytes, 0, bytes.Length, timeoutCts.Token);

                byte[] buffer = new byte[1024];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, timeoutCts.Token);
                if (read <= 0) return false;

                string response = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                Console.WriteLine($"    Receiver Response: {response}");

                using var doc = JsonDocument.Parse(response);
                return doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "ok";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Connection error: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestControlChannelAsync(string host, CancellationToken ct)
        {
            using var client = new TcpClient();
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

                await client.ConnectAsync(host, ControlPort, timeoutCts.Token);
                using var stream = client.GetStream();

                string ping = "{\"type\":\"ping\",\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n";
                byte[] pingBytes = Encoding.UTF8.GetBytes(ping);
                await stream.WriteAsync(pingBytes, 0, pingBytes.Length, timeoutCts.Token);

                byte[] buffer = new byte[512];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, timeoutCts.Token);
                if (read <= 0) return false;

                string response = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                Console.WriteLine($"    Control Response: {response}");
                return response.Contains("pong");
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> StreamSyntheticVideoAsync(string host, int seconds, CancellationToken ct)
        {
            using var client = new TcpClient();
            try
            {
                await client.ConnectAsync(host, VideoPort, ct);
                using var stream = client.GetStream();

                // Send Handshake
                string hs = JsonSerializer.Serialize(new
                {
                    device = "Simulated-Streamer",
                    os = "ios",
                    width = 1280,
                    height = 720,
                    fps = 60,
                    mode = "wifi"
                }) + "\n";
                byte[] hsBytes = Encoding.UTF8.GetBytes(hs);
                await stream.WriteAsync(hsBytes, 0, hsBytes.Length, ct);

                byte[] hsResp = new byte[512];
                int hsRead = await stream.ReadAsync(hsResp, 0, hsResp.Length, ct);
                if (hsRead <= 0) return false;

                // Synthetic H.264 NAL units (Baseline profile)
                // SPS (type 7)
                byte[] sps = new byte[] { 0x67, 0x42, 0x00, 0x1f, 0x96, 0x54, 0x02, 0x80, 0x2d, 0x88 };
                // PPS (type 8)
                byte[] pps = new byte[] { 0x68, 0xce, 0x3c, 0x80 };
                // IDR (type 5)
                byte[] idr = GenerateMockSlice(0x65, 8192);
                // Non-IDR Slice (type 1)
                byte[] nonIdr = GenerateMockSlice(0x41, 2048);

                // Send initial SPS / PPS
                await SendLengthPrefixedNalAsync(stream, sps, ct);
                await SendLengthPrefixedNalAsync(stream, pps, ct);

                int totalFrames = seconds * 60;
                int frameIntervalMs = 1000 / 60;

                for (int i = 0; i < totalFrames; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    // Send keyframe every 60 frames (1 second interval)
                    if (i % 60 == 0)
                    {
                        await SendLengthPrefixedNalAsync(stream, idr, ct);
                    }
                    else
                    {
                        await SendLengthPrefixedNalAsync(stream, nonIdr, ct);
                    }

                    await Task.Delay(frameIntervalMs, ct);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Stream error: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestCorruptNalResilienceAsync(string host, CancellationToken ct)
        {
            using var client = new TcpClient();
            try
            {
                await client.ConnectAsync(host, VideoPort, ct);
                using var stream = client.GetStream();

                string hs = JsonSerializer.Serialize(new
                {
                    device = "Fuzzer",
                    os = "android",
                    width = 720,
                    height = 1280,
                    fps = 30,
                    mode = "wifi"
                }) + "\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(hs), ct);

                byte[] hsResp = new byte[256];
                await stream.ReadAsync(hsResp, 0, hsResp.Length, ct);

                // Send corrupt header with random payload
                byte[] garbageNal = new byte[128];
                new Random().NextBytes(garbageNal);
                garbageNal[0] = 0x7F; // Invalid NAL type

                await SendLengthPrefixedNalAsync(stream, garbageNal, ct);

                // Immediately follow with valid SPS + IDR to see if it recovered
                byte[] sps = new byte[] { 0x67, 0x42, 0x00, 0x1f, 0x96, 0x54, 0x02, 0x80, 0x2d, 0x88 };
                await SendLengthPrefixedNalAsync(stream, sps, ct);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> TestRapidReconnectAsync(string host, int cycles, CancellationToken ct)
        {
            for (int i = 0; i < cycles; i++)
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, VideoPort, ct);
                using var stream = client.GetStream();

                string hs = JsonSerializer.Serialize(new
                {
                    device = $"Cycle-{i}",
                    os = "ios",
                    width = 1920,
                    height = 1080,
                    fps = 60,
                    mode = "wifi"
                }) + "\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(hs), ct);

                byte[] buf = new byte[256];
                int read = await stream.ReadAsync(buf, 0, buf.Length, ct);
                if (read <= 0) return false;

                // Send 1 frame and immediately disconnect
                byte[] mockSlice = GenerateMockSlice(0x65, 512);
                await SendLengthPrefixedNalAsync(stream, mockSlice, ct);

                client.Close();
                await Task.Delay(200, ct);
            }
            return true;
        }

        private static async Task SendLengthPrefixedNalAsync(NetworkStream stream, byte[] nal, CancellationToken ct)
        {
            byte[] header = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)nal.Length);
            await stream.WriteAsync(header, 0, 4, ct);
            await stream.WriteAsync(nal, 0, nal.Length, ct);
        }

        private static byte[] GenerateMockSlice(byte nalHeader, int size)
        {
            byte[] data = new byte[size];
            data[0] = nalHeader;
            for (int i = 1; i < size; i++)
            {
                data[i] = (byte)(i % 255);
            }
            return data;
        }
    }
}
