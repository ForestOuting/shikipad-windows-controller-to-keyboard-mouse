#include "Logger.h"

#include <chrono>
#include <cstdarg>
#include <cstdio>
#include <filesystem>
#include <iomanip>
#include <sstream>

Logger& Logger::instance() {
    static Logger logger;
    return logger;
}

Logger::~Logger() {
    shutdown();
}

void Logger::init(const std::string& logDir, const std::string& filename) {
    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_initialized) {
        return;
    }

    std::filesystem::create_directories(logDir);
    const std::string path = logDir + "/" + filename;
    m_file.open(path, std::ios::app | std::ios::out);
    if (!m_file.is_open()) {
        std::fprintf(stderr, "Logger: failed to open %s\n", path.c_str());
    }
    m_initialized = true;
}

void Logger::shutdown() {
    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_file.is_open()) {
        m_file.flush();
        m_file.close();
    }
    m_initialized = false;
}

const char* Logger::levelStr(LogLevel level) const {
    switch (level) {
        case LogLevel::Debug: return "DEBUG";
        case LogLevel::Info: return "INFO ";
        case LogLevel::Warn: return "WARN ";
        case LogLevel::Error: return "ERROR";
        default: return "?????";
    }
}

std::string Logger::timestamp() const {
    using namespace std::chrono;

    const auto now = system_clock::now();
    const auto time = system_clock::to_time_t(now);
    const auto ms = duration_cast<milliseconds>(now.time_since_epoch()) % 1000;

    std::tm localTime{};
    localtime_s(&localTime, &time);

    std::ostringstream output;
    output << std::put_time(&localTime, "%Y-%m-%dT%H:%M:%S")
           << '.' << std::setfill('0') << std::setw(3) << ms.count();
    return output.str();
}

void Logger::log(LogLevel level, const char* fmt, ...) {
    if (level < m_minLevel) {
        return;
    }

    char message[2048]{};
    va_list args;
    va_start(args, fmt);
    std::vsnprintf(message, sizeof(message), fmt, args);
    va_end(args);

    const std::string ts = timestamp();

    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_file.is_open()) {
        m_file << '[' << ts << "] [" << levelStr(level) << "] " << message << '\n';
        m_file.flush();
    }

    if (m_consoleOutput) {
        std::printf("[%s] [%s] %s\n", ts.c_str(), levelStr(level), message);
    }
}
