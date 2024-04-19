using System.Text;

namespace HalmaBot;

public readonly struct Move(Coord fromSquare, Coord[] steps)
{
    public Coord FromSquare { get; } = fromSquare;
    public Coord[] Steps { get; } = steps;

    public Coord FinalSquare => Steps.Aggregate(FromSquare, (current, step) => current + step);

    public bool IsJump => Steps.Any(step => Math.Abs(step.X) == 2 || Math.Abs(step.Y) == 2);

    public override string ToString()
    {
        var square = FromSquare;
        var builder = new StringBuilder(IsJump ? "J" : "");
        builder.Append(square);
        foreach (var step in Steps)
        {
            square += step;
            builder.Append($">{square}");
        }

        return builder.ToString();
    }
}