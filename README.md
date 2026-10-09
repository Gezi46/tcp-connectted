# Modbus TCP 数据采集程序（C# / .NET）

基于 **C# + NModbus** 实现的 Modbus TCP 主站程序，对接 **iFA Evolution（汇川）平台上的 EVO523 控制器（仿真）**，
周期性采集保持寄存器与线圈状态，并写回线圈实现双向通信。

## 功能

- Modbus TCP 主站连接，支持 IP / 端口 / 从站号配置
- 周期轮询读取保持寄存器（`ReadHoldingRegisters`）与线圈（`ReadCoils`）
- 写回线圈实现双向通信（`WriteSingleCoil`）
- 寄存器原始值翻译为工艺阶段（静止 / 放料 / 搅拌），替代裸数据打印
- **分层异常处理**：单次通信失败仅告警，不让进程崩溃退出
- **断线自动重连**：整体重建连接对象 + 退避等待，恢复后继续采集
- **Ctrl+C 优雅退出**：中断主循环并释放连接资源，不依赖强杀进程

## 技术栈

| 项 | 说明 |
|---|---|
| 语言 / 框架 | C# / .NET 10 |
| 通信库 | [NModbus](https://www.nuget.org/packages/NModbus) 3.0.83 |
| 协议 | Modbus TCP（默认端口 502） |
| 下位机 | iFA Evolution + EVO523（IEC 61131-3 仿真） |
| 开发环境 | Visual Studio |

## 运行

```bash
dotnet run
```

启动后按 `Ctrl+C` 退出。

## 配置项

参数集中在 `Program.cs` 顶部的配置区，修改后重新编译即可：

| 常量 | 默认值 | 说明 |
|---|---|---|
| `PlcIp` | `192.168.100.88` | 从站 IP 地址 |
| `PlcPort` | `502` | Modbus TCP 端口 |
| `SlaveId` | `1` | 从站号 |
| `CoilStart` / `CoilCount` | `10000` / `8` | 线圈起始地址与读取数量 |
| `RegStart` / `RegCount` | `0` / `3` | 保持寄存器起始地址与读取数量 |
| `StageRegIndex` | `2` | 工艺阶段状态字所在下标 |
| `PollIntervalMs` | `200` | 采集周期（ms） |
| `ReadTimeoutMs` / `WriteTimeoutMs` | `1000` | 读写超时（ms） |
| `ReconnectMs` | `2000` | 断线后重连等待（ms） |

## 地址说明

**EVO 系列从 0 编址**，`%MW0` 即 Modbus 地址 `0`，**不要按西门子习惯减 1**。

## 说明与边界

- 本项目对接的是 **iFA Evolution 平台上的 EVO523 控制器仿真环境**，不是现场真实设备。
- 下位机侧使用 IEC 61131-3 编写程序模拟「放料 → 搅拌 → 静止」各阶段动作，作为可控数据源用于上位机联调。
- 真机上还需处理的屏蔽双绞线、终端电阻、共模干扰等现场因素，本项目未涉及。
