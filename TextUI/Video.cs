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

    private Size _charSizePx;
    private Size _windowSizePx;
    private Size _textDimCh;
    private Size _textOffsetPx;

    // Keep track of all loaded textures so they can be destroyed.
    private IList<IntPtr> _textures = new List<IntPtr>();

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
        _font = TTF_OpenFont(FontPath, 8);
        if (_font == IntPtr.Zero)
            throw new SdlTtfException("TTF_OpenFont");
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