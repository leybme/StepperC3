using System.IO.Ports;

namespace StepperC3.Core.Services;

/// <summary>
/// Abstraction for serial port communication with the motor chain.
/// Enables testability by allowing mock implementations.
/// </summary>
public interface IMotorConnection : IDisposable
{
    /// <summary>Whether the connection is currently open.</summary>
    bool IsConnected { get; }

    /// <summary>Opens the serial connection.</summary>
    void Connect();

    /// <summary>Closes the serial connection.</summary>
    void Disconnect();

    /// <summary>Sends a command string to the motor chain.</summary>
    Task SendCommandAsync(string command, CancellationToken ct = default);

    /// <summary>Reads the next line from the serial port.</summary>
    Task<string?> ReadLineAsync(CancellationToken ct = default);

    /// <summary>Lists available serial port names on the system.</summary>
    static string[] GetAvailablePorts() => SerialPort.GetPortNames();
}

/// <summary>
/// Serial port connection to the ESP32-C3 motor chain.
/// Uses 115200 baud, 8E1 (even parity) matching the firmware configuration.
/// </summary>
public sealed class MotorChainConnection : IMotorConnection
{
    private SerialPort? _port;
    private readonly string _portName;
    private readonly int _baudRate;

    public MotorChainConnection(string portName, int baudRate = 115200)
    {
        _portName = portName;
        _baudRate = baudRate;
    }

    public bool IsConnected => _port?.IsOpen ?? false;

    public void Connect()
    {
        if (_port?.IsOpen == true) return;

        _port = new SerialPort(_portName, _baudRate, Parity.Even, 8, StopBits.One)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000,
            NewLine = "\n"
        };
        _port.Open();
    }

    public void Disconnect()
    {
        if (_port?.IsOpen == true)
        {
            _port.Close();
        }
        _port?.Dispose();
        _port = null;
    }

    public async Task SendCommandAsync(string command, CancellationToken ct = default)
    {
        if (_port is null || !_port.IsOpen)
            throw new InvalidOperationException("Serial port is not connected.");

        await Task.Run(() => _port.WriteLine(command), ct);
    }

    public async Task<string?> ReadLineAsync(CancellationToken ct = default)
    {
        if (_port is null || !_port.IsOpen)
            throw new InvalidOperationException("Serial port is not connected.");

        try
        {
            return await Task.Run(() => _port.ReadLine(), ct);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
