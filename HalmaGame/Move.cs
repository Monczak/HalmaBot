using System.Text;

namespace HalmaBot;

public readonly struct Move(Coord fromSquare, Coord[] steps)
{
    public Coord FromSquare { get; } = fromSquare;
    public Coord[] Steps { get; } = steps;

    public Coord FinalSquare
    {
        get
        {
            var c = FromSquare;
            foreach (var step in steps)
                c += step;
            return c;
        }
    }

    public bool IsJump
    {
        get
        {
            foreach (var step in steps)
                if (Math.Abs(step.X) == 2 || Math.Abs(step.Y) == 2)
                    return true;
            return false;
        }
    }

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

    public static Move FromString(string moveStr)
    {
        if (moveStr.StartsWith('J'))
            moveStr = moveStr[1..];

        var squares = moveStr.Split(">");
        var fromSquare = new Coord(squares[0][0] - 'a', int.Parse(squares[0][1..]) - 1);
        var steps = new List<Coord>();
        var currentSquare = fromSquare;
        for (var i = 1; i < squares.Length; i++)
        {
            var square = new Coord(squares[i][0] - 'a', int.Parse(squares[i][1..]) - 1);
            var step = square - currentSquare;
            steps.Add(step);
            
            currentSquare = square;
        }
        
        var move = new Move(fromSquare, steps.ToArray());
        return move;
    }
}