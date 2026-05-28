#pragma once

#include <string>

struct Config {
    bool enabled = true;

    float mouseSensitivity = 1.0f;
    float mouseMaxSpeed = 28.0f;
    float rightStickDeadzone = 0.0f;
    std::string rightStickCurve = "power";
    float rightStickCurveExponent = 2.2f;
    float rightStickEpsilon = 0.002f;

    float leftStickEnterDeadzone = 0.50f;
    float leftStickExitDeadzone = 0.35f;

    float triggerPressThreshold = 0.35f;
    float triggerReleaseThreshold = 0.25f;

    float touchpadSwipeThreshold = 0.22f;
    int touchpadMaxSwipeMs = 600;

    int repeatDelayMs = 180;
    int repeatIntervalMs = 20;

    bool useScanCode = true;

    int scrollSlowIntervalMs = 100;
    int scrollFastIntervalMs = 20;

    int r3FreezeMs = 60;

    static Config load(const std::string& path = "padcoder.json");
    void save(const std::string& path = "padcoder.json") const;
};
