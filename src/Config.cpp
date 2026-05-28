#include "Config.h"

#include "Logger.h"

#include <nlohmann/json.hpp>

#include <exception>
#include <filesystem>
#include <fstream>
#include <cmath>
#include <type_traits>

using json = nlohmann::json;

Config Config::load(const std::string& path) {
    Config cfg;

    if (!std::filesystem::exists(path)) {
        LOG_INFO("Config file '%s' not found; writing defaults", path.c_str());
        cfg.save(path);
        return cfg;
    }

    try {
        std::ifstream input(path);
        json data = json::parse(input);
        bool shouldSaveMigratedConfig = data.contains("mouseDeadzone") ||
                                        !data.contains("rightStickDeadzone") ||
                                        !data.contains("rightStickCurve") ||
                                        !data.contains("rightStickCurveExponent") ||
                                        !data.contains("rightStickEpsilon") ||
                                        !data.contains("leftStickEnterDeadzone") ||
                                        !data.contains("leftStickExitDeadzone");

        auto get = [&](const char* key, auto& value) {
            if (data.contains(key)) {
                value = data.at(key).get<std::remove_reference_t<decltype(value)>>();
            }
        };

        get("enabled", cfg.enabled);
        get("mouseSensitivity", cfg.mouseSensitivity);
        get("mouseMaxSpeed", cfg.mouseMaxSpeed);
        get("rightStickDeadzone", cfg.rightStickDeadzone);
        get("rightStickCurve", cfg.rightStickCurve);
        get("rightStickCurveExponent", cfg.rightStickCurveExponent);
        get("rightStickEpsilon", cfg.rightStickEpsilon);
        get("leftStickEnterDeadzone", cfg.leftStickEnterDeadzone);
        get("leftStickExitDeadzone", cfg.leftStickExitDeadzone);
        get("triggerPressThreshold", cfg.triggerPressThreshold);
        get("triggerReleaseThreshold", cfg.triggerReleaseThreshold);
        get("touchpadSwipeThreshold", cfg.touchpadSwipeThreshold);
        get("touchpadMaxSwipeMs", cfg.touchpadMaxSwipeMs);
        get("repeatDelayMs", cfg.repeatDelayMs);
        get("repeatIntervalMs", cfg.repeatIntervalMs);
        get("useScanCode", cfg.useScanCode);
        get("scrollSlowIntervalMs", cfg.scrollSlowIntervalMs);
        get("scrollFastIntervalMs", cfg.scrollFastIntervalMs);
        get("r3FreezeMs", cfg.r3FreezeMs);

        if (cfg.rightStickDeadzone != 0.0f) {
            LOG_INFO("Migrating rightStickDeadzone from %.6f to 0.0", static_cast<double>(cfg.rightStickDeadzone));
            cfg.rightStickDeadzone = 0.0f;
            shouldSaveMigratedConfig = true;
        }

        if (cfg.rightStickCurve != "power") {
            LOG_WARN("Unsupported rightStickCurve '%s'; using power", cfg.rightStickCurve.c_str());
            cfg.rightStickCurve = "power";
            shouldSaveMigratedConfig = true;
        }

        if (!std::isfinite(cfg.rightStickCurveExponent) || cfg.rightStickCurveExponent <= 0.0f) {
            LOG_WARN("Invalid rightStickCurveExponent; using 2.2");
            cfg.rightStickCurveExponent = 2.2f;
            shouldSaveMigratedConfig = true;
        }

        if (!std::isfinite(cfg.rightStickEpsilon) || cfg.rightStickEpsilon <= 0.0f || cfg.rightStickEpsilon > 0.01f) {
            LOG_WARN("Invalid rightStickEpsilon; using 0.002");
            cfg.rightStickEpsilon = 0.002f;
            shouldSaveMigratedConfig = true;
        }

        if (std::fabs(cfg.leftStickEnterDeadzone - 0.30f) < 0.000001f) {
            LOG_INFO("Migrating leftStickEnterDeadzone from 0.30 to 0.50");
            cfg.leftStickEnterDeadzone = 0.50f;
            shouldSaveMigratedConfig = true;
        }

        if (std::fabs(cfg.leftStickExitDeadzone - 0.20f) < 0.000001f) {
            LOG_INFO("Migrating leftStickExitDeadzone from 0.20 to 0.35");
            cfg.leftStickExitDeadzone = 0.35f;
            shouldSaveMigratedConfig = true;
        }

        LOG_INFO("Config loaded from '%s'", path.c_str());
        if (shouldSaveMigratedConfig) {
            cfg.save(path);
            LOG_INFO("Config migrated to smooth right-stick power curve");
        }
    } catch (const std::exception& ex) {
        LOG_ERROR("Failed to load config '%s': %s; using defaults", path.c_str(), ex.what());
    }

    return cfg;
}

void Config::save(const std::string& path) const {
    json data = {
        {"enabled", enabled},
        {"mouseSensitivity", mouseSensitivity},
        {"mouseMaxSpeed", mouseMaxSpeed},
        {"rightStickDeadzone", rightStickDeadzone},
        {"rightStickCurve", rightStickCurve},
        {"rightStickCurveExponent", rightStickCurveExponent},
        {"rightStickEpsilon", rightStickEpsilon},
        {"leftStickEnterDeadzone", leftStickEnterDeadzone},
        {"leftStickExitDeadzone", leftStickExitDeadzone},
        {"triggerPressThreshold", triggerPressThreshold},
        {"triggerReleaseThreshold", triggerReleaseThreshold},
        {"touchpadSwipeThreshold", touchpadSwipeThreshold},
        {"touchpadMaxSwipeMs", touchpadMaxSwipeMs},
        {"repeatDelayMs", repeatDelayMs},
        {"repeatIntervalMs", repeatIntervalMs},
        {"useScanCode", useScanCode},
        {"scrollSlowIntervalMs", scrollSlowIntervalMs},
        {"scrollFastIntervalMs", scrollFastIntervalMs},
        {"r3FreezeMs", r3FreezeMs},
    };

    try {
        std::ofstream output(path);
        output << data.dump(2) << '\n';
        LOG_INFO("Config defaults written to '%s'", path.c_str());
    } catch (const std::exception& ex) {
        LOG_ERROR("Failed to write config '%s': %s", path.c_str(), ex.what());
    }
}
