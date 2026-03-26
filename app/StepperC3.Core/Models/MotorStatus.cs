namespace StepperC3.Core.Models;

/// <summary>
/// Represents the status of a single motor in the daisy chain, 
/// parsed from the firmware STATUS response.
/// </summary>
public class MotorStatus
{
    /// <summary>Board ID (0-7).</summary>
    public int MotorId { get; set; }

    /// <summary>Current position in steps.</summary>
    public long Position { get; set; }

    /// <summary>Target position in steps.</summary>
    public long Target { get; set; }

    /// <summary>Current motor state (IDLE, MOVING, HOMING, STALLED, DISABLED, ERROR).</summary>
    public string State { get; set; } = "IDLE";

    /// <summary>Whether the driver is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Whether direction polarity is flipped.</summary>
    public bool IsFlipped { get; set; }

    /// <summary>Current microstep resolution.</summary>
    public int Microsteps { get; set; }

    /// <summary>RMS current in milliamps.</summary>
    public int CurrentMA { get; set; }

    /// <summary>Speed in steps/second.</summary>
    public int SpeedHz { get; set; }

    /// <summary>Acceleration in steps/s².</summary>
    public int Acceleration { get; set; }

    /// <summary>
    /// Parses a firmware STATUS response line into a MotorStatus object.
    /// Format: STATUS id pos=N tgt=N state=S en=0/1 flip=0/1 step=N cur=NmA spd=Nhz accel=N
    /// </summary>
    public static MotorStatus? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("STATUS"))
            return null;

        try
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 10) return null;

            var status = new MotorStatus();

            // parts[0] = "STATUS"
            // parts[1] = id
            if (int.TryParse(parts[1], out var id))
                status.MotorId = id;

            foreach (var part in parts.Skip(2))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;

                var key = kv[0];
                var value = kv[1];

                switch (key)
                {
                    case "pos":
                        if (long.TryParse(value, out var pos)) status.Position = pos;
                        break;
                    case "tgt":
                        if (long.TryParse(value, out var tgt)) status.Target = tgt;
                        break;
                    case "state":
                        status.State = value;
                        break;
                    case "en":
                        status.IsEnabled = value == "1";
                        break;
                    case "flip":
                        status.IsFlipped = value == "1";
                        break;
                    case "step":
                        if (int.TryParse(value, out var step)) status.Microsteps = step;
                        break;
                    case "cur":
                        if (int.TryParse(value.Replace("mA", ""), out var cur))
                            status.CurrentMA = cur;
                        break;
                    case "spd":
                        if (int.TryParse(value.Replace("hz", ""), out var spd))
                            status.SpeedHz = spd;
                        break;
                    case "accel":
                        if (int.TryParse(value, out var accel)) status.Acceleration = accel;
                        break;
                }
            }

            return status;
        }
        catch
        {
            return null;
        }
    }

    public override string ToString() =>
        $"Motor {MotorId}: pos={Position} tgt={Target} state={State} en={IsEnabled} " +
        $"flip={IsFlipped} step={Microsteps} cur={CurrentMA}mA spd={SpeedHz}hz accel={Acceleration}";
}
