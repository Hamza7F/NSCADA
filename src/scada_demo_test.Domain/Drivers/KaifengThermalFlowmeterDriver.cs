namespace scada_demo_test.Domain.Drivers;

/// <summary>
/// Kaifeng Thermal Mass Flowmeter
/// ------------------------------
/// Function Code 0x03 (Read Holding Registers), IEEE-754 float32, HighWordFirst.
///   Window 0: start 1, qty 2 -> Instantaneous Flow Rate (Nm³/h)
///   Window 1: start 3, qty 2 -> Accumulated Totalizer (Nm³)
///   CorroborationWindow: start 3, qty 2 (Totalizer)
/// </summary>
public class KaifengThermalFlowmeterDriver : ISensorDriver
{
    private static readonly IReadOnlyList<SensorReadWindow> Windows = new[]
    {
        new SensorReadWindow(0x03, 1, 2), // Flow Rate (Nm³/h) regs 1-2
        new SensorReadWindow(0x03, 3, 2)  // Totalizer (Nm³) regs 3-4
    };

    private static readonly SensorReadWindow ProofWindow = new(0x03, 3, 2);

    public string DriverKey => "KAIFENG_FLOWMETER";
    public string SimpleName => "kaifeng_thermal";
    public string DisplayName => "Kaifeng Thermal Mass Flowmeter";
    public string Description => "Thermal mass flowmeter, IEEE-754 32-bit float pairs (FC03 holding registers 1/3, high-word-first)";

    public int DefaultSlaveAddress => 3;
    public int DefaultPollIntervalSeconds => 3;
    public ushort StartRegister => 1;
    public ushort RegisterQuantity => 2;
    public byte FunctionCode => 0x03;

    public string UnitPrimary => "Nm³/h";
    public string UnitSecondary => "Nm³";
    public string PrimaryColumnName => "InstantaneousFlowRate";
    public string SecondaryColumnName => "AccumulatedTotalizer";

    public IReadOnlyList<SensorReadWindow> ReadWindows => Windows;
    public SensorReadWindow? CorroborationWindow => ProofWindow;

    public ParsedTelemetry ParseData(byte[] rawModbusPayload)
    {
        if (rawModbusPayload is null || rawModbusPayload.Length < 4)
        {
            return new ParsedTelemetry { Success = false, ErrorCode = "INVALID_PAYLOAD" };
        }

        var flowRate = ModbusValueCodec.ReadFloat32HighWordFirst(rawModbusPayload, 0);
        var totalizer = rawModbusPayload.Length >= 8
            ? ModbusValueCodec.ReadFloat32HighWordFirst(rawModbusPayload, 4)
            : 0.0;

        return new ParsedTelemetry
        {
            Success = true,
            PrimaryValue = Math.Round(flowRate, 3),
            SecondaryValue = Math.Round(totalizer, 3)
        };
    }

    public WindowTelemetry ParseWindow(int windowIndex, byte[] rawModbusPayload)
    {
        if (rawModbusPayload is null || rawModbusPayload.Length < 4)
        {
            return new WindowTelemetry();
        }

        var val = ModbusValueCodec.ReadFloat32HighWordFirst(rawModbusPayload, 0);
        return windowIndex switch
        {
            0 => new WindowTelemetry { PrimaryValue = Math.Round(val, 3) },
            1 => new WindowTelemetry { SecondaryValue = Math.Round(val, 3) },
            _ => new WindowTelemetry()
        };
    }

    public bool ValidatePayloadStructure(byte[] rawPayload) => rawPayload != null && (rawPayload.Length == 4 || rawPayload.Length >= 8);

    public bool ValidateValueBoundaries(byte[] rawPayload)
    {
        if (rawPayload == null || rawPayload.Length < 4) return false;
        var f1 = ModbusValueCodec.ReadFloat32HighWordFirst(rawPayload, 0);
        if (!double.IsFinite(f1) || Math.Abs(f1) > 1e7) return false;
        if (rawPayload.Length >= 8)
        {
            var f2 = ModbusValueCodec.ReadFloat32HighWordFirst(rawPayload, 4);
            if (!double.IsFinite(f2) || Math.Abs(f2) > 1e12) return false;
        }
        return true;
    }
}
