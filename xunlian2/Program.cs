// ==========================================================================
//  Modbus TCP 数据采集程序
//
//  平台：iFA Evolution / EVO523（Modbus TCP 从站，地址从 0 起编）
//  依赖：NModbus4 2.1.0
//
//  注意：EVO 从 0 编址，%MW0 即地址 0，不要按西门子习惯减 1。
// ==========================================================================

using System.Net.Sockets;
using System.Threading;
using NModbus;

namespace xunlian2
{
    internal class Program
    {
        // ---------------- 配置区：集中在此，后续可挪进配置文件 ----------------
        private const string PlcIp = "192.168.100.88";
        private const int PlcPort = 502;
        private const byte SlaveId = 1;

        private const ushort CoilStart = 10000;   // 线圈起始地址
        private const ushort CoilCount = 8;

        private const ushort RegStart = 0;        // 保持寄存器起始地址
        private const ushort RegCount = 3;
        private const int StageRegIndex = 2;      // 工艺阶段状态字所在下标

        private const int PollIntervalMs = 200;   // 采集周期
        private const int ReadTimeoutMs = 1000;
        private const int WriteTimeoutMs = 1000;
        private const int ReconnectMs = 2000;     // 断线后等待多久重连

        // volatile：Ctrl+C 回调线程改了它，主循环能立刻看到
        private static volatile bool _running = true;

        private static void Main()
        {
            Console.WriteLine("Modbus TCP 采集程序已启动，按 Ctrl+C 退出。");

            // 优雅退出：拦截 Ctrl+C，不让系统直接杀进程，交由主循环收尾
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _running = false;
            };

            // 外层循环：连接 -> 采集 -> 断了就重连，直到用户退出
            while (_running)
            {
                TcpClient? client = null;

                try
                {
                    // ---------- 1. 建立连接 ----------
                    var tcp = new TcpClient();
                    tcp.Connect(PlcIp, PlcPort);
                    client = tcp;                       // 交给 finally 统一释放

                    var master = new ModbusFactory().CreateMaster(tcp);
                    master.Transport.ReadTimeout = ReadTimeoutMs;
                    master.Transport.WriteTimeout = WriteTimeoutMs;

                    Console.WriteLine($"[INFO] 已连接 {PlcIp}:{PlcPort}（从站 {SlaveId}）");

                    // ---------- 2. 内层循环：连接正常时持续采集 ----------
                    while (_running)
                    {
                        try
                        {
                            bool[] coils = master.ReadCoils(SlaveId, CoilStart, CoilCount);
                            ushort[] regs = master.ReadHoldingRegisters(SlaveId, RegStart, RegCount);

                            // 数据整理：把寄存器原始值翻译成工艺阶段
                            int stage = regs[StageRegIndex];
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss}] 阶段 = {StageName(stage),-6} 线圈[0] = {coils[0]}");

                            // 下发：写回线圈
                            // 真实项目建议改为「命令字 + 回执计数」握手，而不是每轮重复写同一值
                            master.WriteSingleCoil(SlaveId, CoilStart, true);

                            Thread.Sleep(PollIntervalMs);
                        }
                        catch (Exception ex)
                        {
                            // 单次通信失败：只告警，跳出内层交给外层重连，不让进程崩溃
                            Console.WriteLine($"[WARN] 通信失败，准备重连：{ex.Message}");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 连接失败：{ex.Message}");
                }
                finally
                {
                    // 无论正常退出还是异常，连接都要释放
                    try { client?.Close(); } catch { /* 释放失败不影响退出流程 */ }
                }

                // ---------- 3. 退避重连 ----------
                // 分片等待，保证等待期间按 Ctrl+C 也能立刻中断
                if (_running)
                {
                    Console.WriteLine($"[INFO] {ReconnectMs / 1000} 秒后重连……");
                    for (int i = 0; i < ReconnectMs / 100 && _running; i++)
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            Console.WriteLine("[INFO] 程序已退出。");
        }

        // 把阶段状态字翻译成工艺阶段名称
        private static string StageName(int stage) => stage switch
        {
            0 => "静止",
            1 => "放料",
            2 => "搅拌",
            _ => $"未知({stage})"
        };
    }
}
