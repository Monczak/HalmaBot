namespace HalmaBot;

public struct Coord
{
    public int X { get; set; }
    public int Y { get; set; }

    public Coord(int x, int y)
    {
        X = x;
        Y = y;
    }

    public static Coord operator +(Coord c1, Coord c2)
    {
        return new Coord(c1.X + c2.X, c1.Y + c2.Y);
    }
    
    public static Coord operator -(Coord c1, Coord c2)
    {
        return new Coord(c1.X - c2.X, c1.Y - c2.Y);
    }

    public static Coord operator /(Coord c, int x)
    {
        return new Coord(c.X / x, c.Y / x);
    }

    public static bool operator ==(Coord c1, Coord c2) => c1.Equals(c2);
    public static bool operator !=(Coord c1, Coord c2) => !c1.Equals(c2);

    public override bool Equals(object? obj)
    {
        if (obj is not Coord c)
            return false;
        return c.X == X && c.Y == Y;
    }

    public override int GetHashCode()
    {
        return X.GetHashCode() ^ Y.GetHashCode();
    }

    public override string ToString() => $"{"abcdefghijklmnop"[X]}{Y + 1}";
}