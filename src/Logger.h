#pragma once

#include <fstream>
#include <mutex>
#include <string>

enum class LogLevel {
    Debug,
    Info,
    Warn,
    Error
};

class Logger {
public:
    static Logger& instance();

    void init(const std::string& logDir = "logs", const std::string& filename = "padcoder.log");
    void shutdown();
    void log(LogLevel level, const char* fmt, ...);

    void setConsoleOutput(bool enabled) { m_consoleOutput = enabled; }
    void setMinLevel(LogLevel level) { m_minLevel = level; }

private:
    Logger() = default;
    ~Logger();
    Logger(const Logger&) = delete;
    Logger& operator=(const Logger&) = delete;

    const char* levelStr(LogLevel level) const;
    std::string timestamp() const;

    std::mutex m_mutex;
    std::ofstream m_file;
    bool m_consoleOutput = false;
    LogLevel m_minLevel = LogLevel::Info;
    bool m_initialized = false;
};

#define LOG_DEBUG(...) Logger::instance().log(LogLevel::Debug, __VA_ARGS__)
#define LOG_INFO(...) Logger::instance().log(LogLevel::Info, __VA_ARGS__)
#define LOG_WARN(...) Logger::instance().log(LogLevel::Warn, __VA_ARGS__)
#define LOG_ERROR(...) Logger::instance().log(LogLevel::Error, __VA_ARGS__)
