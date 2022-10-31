using System.Runtime.InteropServices;
using SDL2;
using static SDL2.SDL;
using static SDL2.SDL_ttf;

public class Video : IDisposable
{

    private const string FontPath = "font/Hack-Regular.ttf";

    private bool disposedValue = false;

    private IntPtr _window;
    private IntPtr _renderer;
    private IntPtr _font;

    // Single character dimensions in terms of pixels.
    private Size _charSizePx;
    // Window dimensions in terms of pixels.
    private Size _windowSizePx;
    // Text display dimensions in terms of character count.
    private Size _textDimCh;
    // Offset from window origin to text display origin, in pixels.
    private Size _textOffsetPx;

    // Keep track of all loaded textures so they can be destroyed.
    private IList<IntPtr> _textures = new List<IntPtr>();
    // Texture containing font glyphs rendered in grid with transparent background.
    private IntPtr _fontTexture;
    private Size _glyphTextureDimCh;

    public Video()
    {
    }

    public Size TextDimensions { get; private set; }

    public void Init(string windowTitle, int windowWidth, int windowHeight)
    {
        InitSdl(windowTitle, windowWidth, windowHeight);
        InitTtf();
        CalculateTextDimensions();
    }

    private void CalculateTextDimensions()
    {
        CalculateWindowSize();
        CalculateCharacterSize();
        _textDimCh = _windowSizePx / _charSizePx;
        var textExtentPx = _textDimCh * _charSizePx;
        _textOffsetPx = (_windowSizePx - textExtentPx) / 2;
    }

    public void CalculateWindowSize()
    {
        SDL_GetWindowSize(_window, out int w, out int h);
        _windowSizePx = new Size(w, h);
    }

    private void CalculateCharacterSize()
    {
        var black = new SDL_Color() { r = 0, g = 0, b = 0, a = 255 };
        int vSkip = TTF_FontLineSkip(_font);
        var mSurface = TTF_RenderText_Solid(_font, "M", black);
        var mmSurface = TTF_RenderText_Solid(_font, "MM", black);
        var m = Marshal.PtrToStructure<SDL_Surface>(mSurface);
        var mm = Marshal.PtrToStructure<SDL_Surface>(mmSurface);
        int hSkip = mm.w - m.w;
        _charSizePx = new Size(hSkip, vSkip);
    }

    private void InitTtf()
    {
        if (0 != TTF_Init())
            throw new SdlTtfException("TTF_Init");
        SetFontSize(8);
    }

    private void SetFontSize(int pointSize)
    {
        _font = TTF_OpenFont(FontPath, pointSize);
        if (_font == IntPtr.Zero)
            throw new SdlTtfException($"TTF_OpenFont couldn't open font '{FontPath}':{pointSize}");
        CalculateTextDimensions();
        RenderFontGlyphsToTexture();
    }

    private void RenderFontGlyphsToTexture()
    {
        var foreground = new SDL_Color() { r = 255, g = 255, b = 255, a = 255 };
        int glyphMax = 255;
        int glyphCount = glyphMax + 1 - 32;
        int textureW = 512;
        int textureH = 16;
        int textCols = textureW / _charSizePx.W;
        int textRows = glyphCount / textCols;
        if (glyphCount % textCols > 0)
            textRows++;
        _glyphTextureDimCh = new Size(textCols, textRows);
        int textHeightPx = textRows * _charSizePx.H;
        while (textureH < textHeightPx)
            textureH *= 2;
        var surface = CreateSurface(textureW, textureH);
        int x = 0;
        int y = 0;
        SDL_Rect srcRect = new SDL_Rect() { x = 0, y = 0, w = _charSizePx.W, h = _charSizePx.H };
        SDL_Rect dstRect = new SDL_Rect() { x = 0, y = 0, w = _charSizePx.W, h = _charSizePx.H };
        for (ushort i = 32; i < glyphMax; i++)
        {
            var glyph = TTF_RenderGlyph_Blended(_font, i, foreground);
            dstRect.x = x;
            dstRect.y = y;
            SDL_BlitSurface(glyph, ref srcRect, surface, ref dstRect);
            if ((i - 32) % textCols == textCols - 1)
            {
                x = 0;
                y += _charSizePx.H;
            }
            else
            {
                x += _charSizePx.W;
            }
        }
        _fontTexture = SDL_CreateTextureFromSurface(_renderer, surface);
    }

    IntPtr CreateSurface(int w, int h)
    {
        return SDL_CreateRGBSurface(0, w, h, 32, 0, 0, 0, 0);
    }

    private void InitSdl(string windowTitle, int windowWidth, int windowHeight)
    {
        SDL_SetHint(SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING, "1");
        uint flags = SDL_INIT_VIDEO | SDL_INIT_TIMER;
        if (0 != SDL_Init(flags))
            throw new SdlException("SDL_Init");
        InitWindow(windowTitle, windowWidth, windowHeight);
    }

    private void InitWindow(string windowTitle, int windowWidth, int windowHeight)
    {
        _window = SDL_CreateWindow(windowTitle,
            SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED,
            windowWidth, windowHeight,
            SDL_WindowFlags.SDL_WINDOW_SHOWN);
        if (_window == IntPtr.Zero)
            throw new SdlException("SDL_CreateWindow");
        var screen = SDL_GetWindowSurface(_window);
        if (screen == IntPtr.Zero)
            throw new SdlException("SDL_GetWindowSurface");
        _renderer = SDL_CreateRenderer(_window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        if (_renderer == IntPtr.Zero)
            throw new SdlException("SDL_CreateRenderer");
    }

    private void DestroyWindow()
    {
        SDL_DestroyRenderer(_renderer);
        SDL_DestroyWindow(_window);
    }

    private void DestroyTextures()
    {
        foreach (var texture in _textures)
        {
            SDL_DestroyTexture(texture);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            DestroyTextures();
            DestroyWindow();
            // set large fields to null
            disposedValue = true;
        }
    }

    public void DrawFrame(Action drawingCode)
    {
        SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL_RenderClear(_renderer);
        drawingCode();
        SDL_RenderPresent(_renderer);
    }

    public void DrawFrame(char[,] display)
    {
        var white = new SDL_Color() { r = 255, g = 255, b = 255, a = 255 };
        SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL_RenderClear(_renderer);
        int y = _textOffsetPx.H;
        for (int row = 0; row < display.GetLength(0); row++)
        {
            int x = _textOffsetPx.W;
            for (int col = 0; col < display.GetLength(1); col++)
            {
                char c = display[row, col];
                RenderGlyph(c, x, y);
                x += _charSizePx.W;
            }
            y += _charSizePx.H;
        }
        SDL_RenderPresent(_renderer);
    }

    private void RenderGlyph(char c, int x, int y)
    {
        ushort cc = c;
        var src = new SDL_Rect()
        {
            x = cc % _glyphTextureDimCh.W,
            y = cc / _glyphTextureDimCh.H,
            w = _charSizePx.W,
            h = _charSizePx.H,
        };
        var dst = new SDL_Rect()
        {
            x = x,
            y = y,
            w = _charSizePx.W,
            h = _charSizePx.H,
        };
        SDL_RenderCopy(_renderer, _fontTexture, ref src, ref dst);
    }

    ~Video()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}