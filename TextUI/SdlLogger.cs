using SDL2;
using static SDL2.SDL;
using static SDL2.SDL.SDL_LogPriority;
using static SDL2.SDL.SDL_LogCategory;

public class SdlLogger<T> : ILogger<T>
{
    private const int LogCategory = (int)SDL_LOG_CATEGORY_APPLICATION;

    public IDisposable BeginScope<TState>(TState state)
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        var sdlLogPriority = ToSdlLogPriority(logLevel);
        var activePriority = SDL_LogGetPriority(LogCategory);
        return (int)sdlLogPriority >= (int)activePriority;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var sdlLogPriority = ToSdlLogPriority(logLevel);
        string message = formatter(state, exception);
        SDL_LogMessage(LogCategory, sdlLogPriority, message);
    }

    private SDL_LogPriority ToSdlLogPriority(LogLevel logLevel)
    {
        switch (logLevel)
        {
            case LogLevel.Trace: return SDL_LOG_PRIORITY_VERBOSE;
            case LogLevel.Debug: return SDL_LOG_PRIORITY_DEBUG;
            case LogLevel.Information: return SDL_LOG_PRIORITY_INFO;
            case LogLevel.Warning: return SDL_LOG_PRIORITY_WARN;
            case LogLevel.Error: return SDL_LOG_PRIORITY_ERROR;
            case LogLevel.Critical: return SDL_LOG_PRIORITY_CRITICAL;
            default:
                throw new Exception("Unexpected LogLevel: " + logLevel);
        }
    }

}