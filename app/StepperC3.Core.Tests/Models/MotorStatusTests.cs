using StepperC3.Core.Models;

namespace StepperC3.Core.Tests.Models;

public class MotorStatusTests
{
    [Fact]
    public void TryParse_ValidStatusLine_ReturnsMotorStatus()
    {
        var line = "STATUS 2 pos=1500 tgt=3000 state=MOVING en=1 flip=0 step=16 cur=800mA spd=1000hz accel=5000";
        var status = MotorStatus.TryParse(line);

        Assert.NotNull(status);
        Assert.Equal(2, status.MotorId);
        Assert.Equal(1500, status.Position);
        Assert.Equal(3000, status.Target);
        Assert.Equal("MOVING", status.State);
        Assert.True(status.IsEnabled);
        Assert.False(status.IsFlipped);
        Assert.Equal(16, status.Microsteps);
        Assert.Equal(800, status.CurrentMA);
        Assert.Equal(1000, status.SpeedHz);
        Assert.Equal(5000, status.Acceleration);
    }

    [Fact]
    public void TryParse_IdleStatus_ParsesCorrectly()
    {
        var line = "STATUS 0 pos=0 tgt=0 state=IDLE en=1 flip=1 step=32 cur=400mA spd=500hz accel=2000";
        var status = MotorStatus.TryParse(line);

        Assert.NotNull(status);
        Assert.Equal(0, status.MotorId);
        Assert.Equal(0, status.Position);
        Assert.Equal("IDLE", status.State);
        Assert.True(status.IsFlipped);
        Assert.Equal(32, status.Microsteps);
    }

    [Fact]
    public void TryParse_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(MotorStatus.TryParse(null!));
        Assert.Null(MotorStatus.TryParse(""));
        Assert.Null(MotorStatus.TryParse("   "));
    }

    [Fact]
    public void TryParse_NonStatusLine_ReturnsNull()
    {
        Assert.Null(MotorStatus.TryParse("OK"));
        Assert.Null(MotorStatus.TryParse("ERROR: unknown command"));
    }

    [Fact]
    public void TryParse_TooShort_ReturnsNull()
    {
        Assert.Null(MotorStatus.TryParse("STATUS 0"));
    }

    [Fact]
    public void ToString_ReturnsReadableString()
    {
        var status = new MotorStatus
        {
            MotorId = 1,
            Position = 100,
            Target = 200,
            State = "MOVING",
            IsEnabled = true
        };

        var str = status.ToString();
        Assert.Contains("Motor 1", str);
        Assert.Contains("pos=100", str);
        Assert.Contains("state=MOVING", str);
    }
}
