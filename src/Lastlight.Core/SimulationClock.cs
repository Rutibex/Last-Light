namespace Lastlight.Core;

public sealed class SimulationClock
{
    private double accumulator;
    public bool Paused { get; set; } = true;
    public int Speed { get; private set; } = 1;
    public int LastFrameTicks { get; private set; }
    public void SetSpeed(int speed)
    {
        if (speed is not (1 or 3 or 6)) throw new ArgumentOutOfRangeException(nameof(speed));
        Speed = speed; Paused = false;
    }
    public void Reset() { accumulator = 0; LastFrameTicks = 0; Paused = true; Speed = 1; }
    public void Advance(World world, Definitions definitions, double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        LastFrameTicks = 0;
        if (Paused) { accumulator = 0; return; }
        accumulator += Math.Min(seconds, .25) * Speed;
        const double duration = 1.0 / World.TickRate;
        while (accumulator + 1e-10 >= duration && LastFrameTicks < 45)
        {
            world.Step(definitions); accumulator -= duration; LastFrameTicks++;
        }
        // Slow down under load instead of freezing input to repay an unbounded debt.
        accumulator = Math.Min(accumulator, duration);
    }
}
