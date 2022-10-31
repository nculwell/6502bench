public struct Size
{
    public int W { get; }
    public int H { get; }
    public Size(int w, int h)
    {
        if (w < 0)
            throw new ArgumentException($"Width must be >= 0 ({w}).");
        if (h < 0)
            throw new ArgumentException($"Height must be >= 0 ({h}).");
        W = w;
        H = h;
    }
    public static Size operator +(Size a, Size b)
    {
        return new Size(a.W + b.W, a.H + b.H);
    }
    public static Size operator -(Size a, Size b)
    {
        return new Size(a.W - b.W, a.H - b.H);
    }
    public static Size operator *(Size a, Size b)
    {
        return new Size(a.W * b.W, a.H * b.H);
    }
    public static Size operator /(Size a, Size b)
    {
        return new Size(a.W / b.W, a.H / b.H);
    }
    public static Size operator /(Size a, int divisor)
    {
        return new Size(a.W / divisor, a.H / divisor);
    }
}