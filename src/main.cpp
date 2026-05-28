#define SDL_MAIN_HANDLED

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <timeapi.h>

#include <SDL3/SDL.h>

#include "Config.h"
#include "InputInjector.h"
#include "Logger.h"
#include "MappingEngine.h"

#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <exception>
#include <filesystem>
#include <string>
#include <utility>
#include <vector>

namespace {

constexpr double Pi = 3.14159265358979323846;
constexpr int WheelUpDelta = 1;
constexpr int WheelDownDelta = -1;

std::atomic_bool g_running = true;

BOOL WINAPI consoleHandler(DWORD type) {
    if (type == CTRL_C_EVENT || type == CTRL_CLOSE_EVENT || type == CTRL_BREAK_EVENT ||
        type == CTRL_SHUTDOWN_EVENT) {
        g_running = false;
        return TRUE;
    }
    return FALSE;
}

double qpcSeconds() {
    static LARGE_INTEGER frequency = [] {
        LARGE_INTEGER value{};
        QueryPerformanceFrequency(&value);
        return value;
    }();

    LARGE_INTEGER now{};
    QueryPerformanceCounter(&now);
    return static_cast<double>(now.QuadPart) / static_cast<double>(frequency.QuadPart);
}

double nowMs() {
    return qpcSeconds() * 1000.0;
}

float normalizeStick(Sint16 value) {
    if (value < 0) {
        return std::max(-1.0f, static_cast<float>(value) / 32768.0f);
    }
    return std::min(1.0f, static_cast<float>(value) / 32767.0f);
}

float normalizeTrigger(Sint16 value) {
    if (value < 0) {
        return std::clamp((static_cast<float>(value) + 32768.0f) / 65535.0f, 0.0f, 1.0f);
    }
    return std::clamp(static_cast<float>(value) / 32767.0f, 0.0f, 1.0f);
}

bool isRepeatable(PhysicalKey key) {
    return key == PhysicalKey::Backspace ||
           key == PhysicalKey::ArrowUp ||
           key == PhysicalKey::ArrowDown ||
           key == PhysicalKey::ArrowLeft ||
           key == PhysicalKey::ArrowRight;
}

enum class StickDirection {
    None,
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft
};

StickDirection sectorFor(float x, float y) {
    const double angle = std::atan2(-static_cast<double>(y), static_cast<double>(x));
    int sector = static_cast<int>(std::floor((angle + Pi / 8.0) / (Pi / 4.0)));
    sector = (sector % 8 + 8) % 8;

    switch (sector) {
        case 0: return StickDirection::Right;
        case 1: return StickDirection::UpRight;
        case 2: return StickDirection::Up;
        case 3: return StickDirection::UpLeft;
        case 4: return StickDirection::Left;
        case 5: return StickDirection::DownLeft;
        case 6: return StickDirection::Down;
        case 7: return StickDirection::DownRight;
        default: return StickDirection::None;
    }
}

ActiveModifiers modifiersFor(StickDirection direction) {
    ActiveModifiers mods;
    switch (direction) {
        case StickDirection::Right:
            mods.win = true;
            break;
        case StickDirection::DownRight:
            mods.alt = true;
            break;
        case StickDirection::DownLeft:
            mods.ctrl = true;
            break;
        case StickDirection::Left:
            mods.shift = true;
            break;
        default:
            break;
    }
    return mods;
}

ActionButton actionForButton(SDL_GamepadButton button, bool& isAction) {
    isAction = true;
    switch (button) {
        case SDL_GAMEPAD_BUTTON_DPAD_UP: return ActionButton::Up;
        case SDL_GAMEPAD_BUTTON_DPAD_RIGHT: return ActionButton::Right;
        case SDL_GAMEPAD_BUTTON_WEST: return ActionButton::Square;
        case SDL_GAMEPAD_BUTTON_NORTH: return ActionButton::Triangle;
        case SDL_GAMEPAD_BUTTON_DPAD_LEFT: return ActionButton::Left;
        case SDL_GAMEPAD_BUTTON_DPAD_DOWN: return ActionButton::Down;
        case SDL_GAMEPAD_BUTTON_SOUTH: return ActionButton::Cross;
        case SDL_GAMEPAD_BUTTON_EAST: return ActionButton::Circle;
        default:
            isAction = false;
            return ActionButton::Up;
    }
}

std::string joinArgs(int argc, char** argv) {
    std::string result;
    for (int i = 1; i < argc; ++i) {
        if (!result.empty()) {
            result += ' ';
        }
        result += argv[i];
    }
    return result;
}

bool hasArg(int argc, char** argv, const char* arg) {
    for (int i = 1; i < argc; ++i) {
        if (std::string(argv[i]) == arg) {
            return true;
        }
    }
    return false;
}

void logMouseSettings(const Config& config) {
    LOG_INFO("mouse settings: rightStickDeadzone = %.1f, rightStickCurve = %s, rightStickCurveExponent = %g, rightStickEpsilon = %.3f, neutralCalibration = enabled, mouseMaxSpeed = %g, mouseSensitivity = %g",
             static_cast<double>(config.rightStickDeadzone),
             config.rightStickCurve.c_str(),
             static_cast<double>(config.rightStickCurveExponent),
             static_cast<double>(config.rightStickEpsilon),
             static_cast<double>(config.mouseMaxSpeed),
             static_cast<double>(config.mouseSensitivity));
}

struct ButtonHold {
    bool down = false;
    bool repeatable = false;
    bool keyIsDown = false;
    PhysicalKey key = PhysicalKey::None;
    ActiveModifiers mods;
    double nextRepeatMs = 0.0;
    bool repeatBlockedLogged = false;
};

const char* stickDirectionName(StickDirection dir) {
    switch (dir) {
        case StickDirection::None: return "None";
        case StickDirection::Up: return "Up";
        case StickDirection::UpRight: return "UpRight";
        case StickDirection::Right: return "Right";
        case StickDirection::DownRight: return "DownRight";
        case StickDirection::Down: return "Down";
        case StickDirection::DownLeft: return "DownLeft";
        case StickDirection::Left: return "Left";
        case StickDirection::UpLeft: return "UpLeft";
        default: return "Unknown";
    }
}

struct TriggerState {
    bool pressed = false;
};

struct TouchpadState {
    bool available = false;
    bool tracking = false;
    float startX = 0.0f;
    float startY = 0.0f;
    float lastX = 0.0f;
    float lastY = 0.0f;
    double startMs = 0.0;
};

class PadCoderApp {
public:
    explicit PadCoderApp(Config config, bool debugAltTab)
        : m_config(config), m_debugAltTab(debugAltTab) {
        m_injector.setUseScanCode(m_config.useScanCode);
        m_enabled = m_config.enabled;
        if (std::filesystem::exists(m_configPath)) {
            m_configWriteTime = std::filesystem::last_write_time(m_configPath);
        }
    }

    int run() {
        LOG_INFO("PadCoder startup");
        logMouseSettings(m_config);

        if (!SDL_Init(SDL_INIT_GAMEPAD | SDL_INIT_EVENTS)) {
            LOG_ERROR("SDL_Init failed: %s", SDL_GetError());
            return 1;
        }

        timeBeginPeriod(1);
        SetConsoleCtrlHandler(consoleHandler, TRUE);

        openFirstController();

        double previous = qpcSeconds();
        while (g_running) {
            SDL_Event event{};
            while (SDL_PollEvent(&event)) {
                handleEvent(event);
            }

            const double current = qpcSeconds();
            const double deltaSec = std::max(0.0, current - previous);
            previous = current;

            maybeReloadConfig();
            updateEmergencyDisable();

            if (m_gamepad != nullptr && m_enabled) {
                updateTriggers();
                updateLeftStick();
                updateActionButtons();
                updateMouseButtons();
                updateRightStick(deltaSec);
                updateTouchpad();
            } else {
                releaseHeldActionKeys();
                setHeldStickModifiers({});
                m_leftDirection = StickDirection::None;
                m_leftStickHeldDirection = StickDirection::None;
            m_scrollNextMs = 0.0;
        }

            Sleep(1);
        }

        shutdown();
        return 0;
    }

private:
    void openFirstController() {
        if (m_gamepad != nullptr) {
            return;
        }

        int count = 0;
        SDL_JoystickID* ids = SDL_GetGamepads(&count);
        if (ids != nullptr) {
            for (int i = 0; i < count && m_gamepad == nullptr; ++i) {
                openController(ids[i]);
            }
            SDL_free(ids);
        }
    }

    void openController(SDL_JoystickID id) {
        if (m_gamepad != nullptr) {
            return;
        }

        SDL_Gamepad* gamepad = SDL_OpenGamepad(id);
        if (gamepad == nullptr) {
            LOG_ERROR("Failed to open controller: %s", SDL_GetError());
            return;
        }

        m_gamepad = gamepad;
        m_gamepadId = id;
        m_rightNeutralX = normalizeStick(SDL_GetGamepadAxis(m_gamepad, SDL_GAMEPAD_AXIS_RIGHTX));
        m_rightNeutralY = normalizeStick(SDL_GetGamepadAxis(m_gamepad, SDL_GAMEPAD_AXIS_RIGHTY));
        m_touchpad.available = SDL_GetNumGamepadTouchpads(m_gamepad) > 0;

        const char* name = SDL_GetGamepadName(m_gamepad);
        LOG_INFO("Controller connected: %s", name != nullptr ? name : "unknown controller");
        if (m_touchpad.available) {
            LOG_INFO("Touchpad available");
        } else {
            LOG_WARN("Touchpad unavailable; continuing without touchpad swipes");
        }
    }

    void closeController() {
        if (m_gamepad == nullptr) {
            return;
        }

        releaseHeldActionKeys();
        m_injector.releaseAll();
        SDL_CloseGamepad(m_gamepad);
        m_gamepad = nullptr;
        m_gamepadId = 0;
        m_touchpad = {};
        m_leftDirection = StickDirection::None;
        m_leftStickHeldDirection = StickDirection::None;
        m_heldStickMods = {};
        m_leftMouseDown = false;
        m_rightMouseDown = false;
        for (auto& hold : m_holds) {
            hold = {};
        }
        LOG_INFO("Controller disconnected");
    }

    void handleEvent(const SDL_Event& event) {
        if (event.type == SDL_EVENT_QUIT) {
            g_running = false;
            return;
        }

        if (event.type == SDL_EVENT_GAMEPAD_ADDED) {
            openController(event.gdevice.which);
            return;
        }

        if (event.type == SDL_EVENT_GAMEPAD_REMOVED && event.gdevice.which == m_gamepadId) {
            closeController();
        }
    }

    bool button(SDL_GamepadButton button) const {
        return m_gamepad != nullptr && SDL_GetGamepadButton(m_gamepad, button);
    }

    float axis(SDL_GamepadAxis axis) const {
        return m_gamepad != nullptr ? normalizeStick(SDL_GetGamepadAxis(m_gamepad, axis)) : 0.0f;
    }

    float triggerAxis(SDL_GamepadAxis axis) const {
        return m_gamepad != nullptr ? normalizeTrigger(SDL_GetGamepadAxis(m_gamepad, axis)) : 0.0f;
    }

    void updateTriggers() {
        updateTrigger(m_l2, triggerAxis(SDL_GAMEPAD_AXIS_LEFT_TRIGGER));
        updateTrigger(m_r2, triggerAxis(SDL_GAMEPAD_AXIS_RIGHT_TRIGGER));
    }

    void updateTrigger(TriggerState& trigger, float value) const {
        if (!trigger.pressed && value > m_config.triggerPressThreshold) {
            trigger.pressed = true;
        } else if (trigger.pressed && value < m_config.triggerReleaseThreshold) {
            trigger.pressed = false;
        }
    }

    void updateLeftStick() {
        const float x = axis(SDL_GAMEPAD_AXIS_LEFTX);
        const float y = axis(SDL_GAMEPAD_AXIS_LEFTY);
        const float radius = std::sqrt(x * x + y * y);
        const StickDirection previous = m_leftDirection;
        StickDirection next = previous;

        if (previous == StickDirection::None) {
            if (radius < m_config.leftStickEnterDeadzone) {
                return;
            }
            next = sectorFor(x, y);
        } else if (radius < m_config.leftStickExitDeadzone) {
            next = StickDirection::None;
        } else {
            next = previous;
        }

        if (next != previous) {
            if (m_debugAltTab) {
                LOG_INFO("LeftStick leave %s", stickDirectionName(previous));
                LOG_INFO("LeftStick enter %s", stickDirectionName(next));
            }
            m_leftDirection = next;
            updateHeldModifierDirection(m_leftDirection);
            m_scrollNextMs = 0.0;
            if (m_leftDirection == StickDirection::UpLeft) {
                m_injector.keyTap(PhysicalKey::Escape);
            }
        }

        updateScroll(radius);
    }

    void updateScroll(float radius) {
        if (m_leftDirection != StickDirection::Up && m_leftDirection != StickDirection::Down) {
            m_scrollNextMs = 0.0;
            return;
        }

        const double currentMs = nowMs();
        if (m_scrollNextMs > currentMs) {
            return;
        }

        const int wheelDelta = m_leftDirection == StickDirection::Up ? WheelUpDelta : WheelDownDelta;
        m_injector.mouseWheel(wheelDelta);

        const float normalized = std::clamp(
            (radius - m_config.leftStickEnterDeadzone) / (1.0f - m_config.leftStickEnterDeadzone),
            0.0f,
            1.0f);
        const double interval =
            static_cast<double>(m_config.scrollSlowIntervalMs) +
            (static_cast<double>(m_config.scrollFastIntervalMs - m_config.scrollSlowIntervalMs) * normalized);
        m_scrollNextMs = currentMs + std::max(1.0, interval);
    }

    void updateHeldModifierDirection(StickDirection direction) {
        const StickDirection heldDirection = isHeldModifierDirection(direction) ? direction : StickDirection::None;
        if (heldDirection == m_leftStickHeldDirection) {
            return;
        }

        setHeldStickModifiers({});
        m_leftStickHeldDirection = StickDirection::None;

        if (heldDirection != StickDirection::None) {
            setHeldStickModifiers(modifiersFor(heldDirection));
            m_leftStickHeldDirection = heldDirection;
        }
    }

    void debugLogHeldKeys() const {
        if (!m_debugAltTab) return;
        std::string held = "Held keys:";
        if (m_heldStickMods.alt) held += " Alt";
        if (m_heldStickMods.ctrl) held += " Ctrl";
        if (m_heldStickMods.shift) held += " Shift";
        if (m_heldStickMods.win) held += " Win";
        for (const auto& hold : m_holds) {
            if (hold.down && hold.keyIsDown) {
                held += " ";
                held += MappingEngine::keyName(hold.key);
            }
        }
        if (held == "Held keys:") held = "Held keys: None";
        LOG_INFO("%s", held.c_str());
    }

    void setHeldStickModifiers(const ActiveModifiers& desired) {
        if (sameModifiers(m_heldStickMods, desired)) {
            return;
        }

        if (m_debugAltTab) {
            if (desired.alt && !m_heldStickMods.alt) LOG_INFO("Alt down");
            if (!desired.alt && m_heldStickMods.alt) LOG_INFO("Alt up");
            if (desired.ctrl && !m_heldStickMods.ctrl) LOG_INFO("Ctrl down");
            if (!desired.ctrl && m_heldStickMods.ctrl) LOG_INFO("Ctrl up");
            if (desired.shift && !m_heldStickMods.shift) LOG_INFO("Shift down");
            if (!desired.shift && m_heldStickMods.shift) LOG_INFO("Shift up");
            if (desired.win && !m_heldStickMods.win) LOG_INFO("Win down");
            if (!desired.win && m_heldStickMods.win) LOG_INFO("Win up");
        }

        releaseStickModifiers(m_heldStickMods);
        pressStickModifiers(desired);
        m_heldStickMods = desired;
        
        debugLogHeldKeys();
    }

    void releaseStickModifiers(const ActiveModifiers& mods) {
        if (mods.shift) {
            m_injector.modifierUp(HeldModifier::Shift);
        }
        if (mods.ctrl) {
            m_injector.modifierUp(HeldModifier::Ctrl);
        }
        if (mods.alt) {
            m_injector.modifierUp(HeldModifier::Alt);
        }
        if (mods.win) {
            m_injector.modifierUp(HeldModifier::Win);
        }
    }

    void pressStickModifiers(const ActiveModifiers& mods) {
        if (mods.ctrl) {
            m_injector.modifierDown(HeldModifier::Ctrl);
        }
        if (mods.shift) {
            m_injector.modifierDown(HeldModifier::Shift);
        }
        if (mods.alt) {
            m_injector.modifierDown(HeldModifier::Alt);
        }
        if (mods.win) {
            m_injector.modifierDown(HeldModifier::Win);
        }
    }

    static bool sameModifiers(const ActiveModifiers& a, const ActiveModifiers& b) {
        return a.shift == b.shift && a.ctrl == b.ctrl && a.alt == b.alt && a.win == b.win;
    }

    static bool isHeldModifierDirection(StickDirection direction) {
        return direction == StickDirection::Right ||
               direction == StickDirection::DownRight ||
               direction == StickDirection::DownLeft ||
               direction == StickDirection::Left;
    }

    void updateActionButtons() {
        const bool l1 = button(SDL_GAMEPAD_BUTTON_LEFT_SHOULDER);
        const bool r1 = button(SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER);
        const Layer layer = m_mapping.resolveLayer(l1, r1, m_l2.pressed, m_r2.pressed);
        const double currentMs = nowMs();

        for (int i = 0; i < static_cast<int>(m_actionButtons.size()); ++i) {
            const SDL_GamepadButton gamepadButton = m_actionButtons[i];
            const bool down = button(gamepadButton);
            ButtonHold& hold = m_holds[i];

            bool isAction = false;
            const ActionButton action = actionForButton(gamepadButton, isAction);
            if (!isAction) {
                continue;
            }

            if (down && !hold.down) {
                const PhysicalKey key = m_mapping.lookupKey(layer, action);
                
                if (m_debugAltTab && gamepadButton == SDL_GAMEPAD_BUTTON_EAST) {
                    LOG_INFO("Circle down");
                    LOG_INFO("Circle resolved to %s", MappingEngine::keyName(key));
                }

                if (key != PhysicalKey::None) {
                    m_injector.keyTap(key);
                    if (m_debugAltTab && key == PhysicalKey::Tab) {
                        LOG_INFO("Tab down");
                        LOG_INFO("Tab up");
                    }
                }

                hold.down = true;
                hold.key = key;
                hold.mods = {};
                hold.repeatable = isRepeatable(key);
                hold.nextRepeatMs = currentMs + static_cast<double>(m_config.repeatDelayMs);
                hold.keyIsDown = false;
                hold.repeatBlockedLogged = false;

                debugLogHeldKeys();
            } else if (!down && hold.down) {
                if (m_debugAltTab && gamepadButton == SDL_GAMEPAD_BUTTON_EAST) {
                    LOG_INFO("Circle up");
                }
                
                if (hold.keyIsDown) {
                    m_injector.keyUp(hold.key);
                }
                hold = {};
                
                debugLogHeldKeys();
            } else if (down && hold.down) {
                if (hold.key == PhysicalKey::Tab && m_debugAltTab && currentMs >= hold.nextRepeatMs) {
                    if (!hold.repeatBlockedLogged) {
                        LOG_INFO("Tab repeat blocked");
                        hold.repeatBlockedLogged = true;
                    }
                }
                if (hold.repeatable && currentMs >= hold.nextRepeatMs) {
                    m_injector.keyTap(hold.key);
                    if (m_debugAltTab) {
                        LOG_INFO("Key repeat event: %s", MappingEngine::keyName(hold.key));
                    }
                    hold.nextRepeatMs = currentMs + static_cast<double>(m_config.repeatIntervalMs);
                }
            }
        }
    }

    void releaseHeldActionKeys() {
        for (auto& hold : m_holds) {
            if (hold.keyIsDown) {
                m_injector.keyUp(hold.key);
            }
            hold = {};
        }
    }

    void updateMouseButtons() {
        const bool l3 = button(SDL_GAMEPAD_BUTTON_LEFT_STICK);
        if (l3 && !m_leftMouseDown) {
            m_injector.mouseButtonDown(0);
            m_leftMouseDown = true;
        } else if (!l3 && m_leftMouseDown) {
            m_injector.mouseButtonUp(0);
            m_leftMouseDown = false;
        }

        const bool r3 = button(SDL_GAMEPAD_BUTTON_RIGHT_STICK);
        if (r3 && !m_rightMouseDown) {
            m_injector.mouseButtonDown(1);
            m_rightMouseDown = true;
            m_mouseFreezeUntilMs = nowMs() + static_cast<double>(m_config.r3FreezeMs);
        } else if (!r3 && m_rightMouseDown) {
            m_injector.mouseButtonUp(1);
            m_rightMouseDown = false;
        }
    }

    void updateRightStick(double deltaSec) {
        const float rawX = axis(SDL_GAMEPAD_AXIS_RIGHTX);
        const float rawY = axis(SDL_GAMEPAD_AXIS_RIGHTY);
        const float calibratedX = rawX - m_rightNeutralX;
        const float calibratedY = rawY - m_rightNeutralY;

        if (std::fabs(calibratedX) < 0.03f && std::fabs(calibratedY) < 0.03f) {
            m_rightNeutralX = m_rightNeutralX * 0.995f + rawX * 0.005f;
            m_rightNeutralY = m_rightNeutralY * 0.995f + rawY * 0.005f;
        }

        if (nowMs() < m_mouseFreezeUntilMs) {
            return;
        }

        const double speed = static_cast<double>(m_config.mouseSensitivity) *
                             static_cast<double>(m_config.mouseMaxSpeed) *
                             deltaSec *
                             120.0;
        const double x = static_cast<double>(calibratedX);
        const double y = static_cast<double>(calibratedY);
        const double actualRadius = std::sqrt(x * x + y * y);
        const double radius = std::clamp(actualRadius, 0.0, 1.0);
        if (radius <= static_cast<double>(m_config.rightStickEpsilon)) {
            return;
        }

        const double dirX = x / actualRadius;
        const double dirY = y / actualRadius;
        const double speedRatio = std::pow(radius, static_cast<double>(m_config.rightStickCurveExponent));
        const double dx = dirX * speedRatio * speed;
        const double dy = dirY * speedRatio * speed;
        if (std::fabs(dx) + std::fabs(dy) < 0.000001) {
            return;
        }

        m_mouseAccumX += dx;
        m_mouseAccumY += dy;

        const int moveX = static_cast<int>(m_mouseAccumX);
        const int moveY = static_cast<int>(m_mouseAccumY);
        if (moveX != 0 || moveY != 0) {
            m_injector.mouseMove(moveX, moveY);
            m_mouseAccumX -= static_cast<double>(moveX);
            m_mouseAccumY -= static_cast<double>(moveY);
        }
    }

    void updateTouchpad() {
        if (!m_touchpad.available || SDL_GetNumGamepadTouchpads(m_gamepad) <= 0) {
            return;
        }

        bool down = false;
        float x = 0.0f;
        float y = 0.0f;
        float pressure = 0.0f;
        if (!SDL_GetGamepadTouchpadFinger(m_gamepad, 0, 0, &down, &x, &y, &pressure)) {
            if (m_touchpad.tracking) {
                finishTouchpadSwipe(m_touchpad.lastX, m_touchpad.lastY);
            }
            return;
        }

        const double currentMs = nowMs();
        if (down && !m_touchpad.tracking) {
            m_touchpad.tracking = true;
            m_touchpad.startX = x;
            m_touchpad.startY = y;
            m_touchpad.lastX = x;
            m_touchpad.lastY = y;
            m_touchpad.startMs = currentMs;
            return;
        }

        if (down && m_touchpad.tracking) {
            m_touchpad.lastX = x;
            m_touchpad.lastY = y;
        } else if (!down && m_touchpad.tracking) {
            finishTouchpadSwipe(m_touchpad.lastX, m_touchpad.lastY);
        }
    }

    void finishTouchpadSwipe(float endX, float endY) {
        const double elapsedMs = nowMs() - m_touchpad.startMs;
        const float dx = endX - m_touchpad.startX;
        const float dy = endY - m_touchpad.startY;
        m_touchpad.tracking = false;

        if (elapsedMs <= static_cast<double>(m_config.touchpadMaxSwipeMs)) {
            handleSwipe(dx, dy);
        }
    }

    void handleSwipe(float dx, float dy) {
        const float absX = std::fabs(dx);
        const float absY = std::fabs(dy);
        const float threshold = m_config.touchpadSwipeThreshold;

        if (absX >= threshold && absX >= absY * 1.5f) {
            if (dx > 0.0f) {
                m_injector.keyTap(PhysicalKey::Tab, false, true);
            } else {
                m_injector.keyTap(PhysicalKey::Tab, true, true);
            }
        } else if (absY >= threshold && absY >= absX * 1.5f) {
            if (dy > 0.0f) {
                m_injector.keyTap(PhysicalKey::Tab, false, false, true);
            } else {
                m_injector.keyTap(PhysicalKey::Tab, true, false, true);
            }
        }
    }

    void updateEmergencyDisable() {
        if (m_gamepad == nullptr) {
            m_disableHoldStartMs = 0.0;
            m_disableToggleArmed = true;
            return;
        }

        const bool held = button(SDL_GAMEPAD_BUTTON_BACK) && button(SDL_GAMEPAD_BUTTON_START);
        const double currentMs = nowMs();
        if (!held) {
            m_disableHoldStartMs = 0.0;
            m_disableToggleArmed = true;
            return;
        }

        if (m_disableHoldStartMs <= 0.0) {
            m_disableHoldStartMs = currentMs;
            return;
        }

        if (m_disableToggleArmed && currentMs - m_disableHoldStartMs >= 2000.0) {
            m_enabled = !m_enabled;
            m_config.enabled = m_enabled;
            m_disableToggleArmed = false;
            if (!m_enabled) {
                m_injector.releaseAll();
            }
            LOG_INFO("PadCoder %s", m_enabled ? "enabled" : "disabled");
        }
    }

    void maybeReloadConfig() {
        try {
            if (!std::filesystem::exists(m_configPath)) {
                return;
            }

            const auto writeTime = std::filesystem::last_write_time(m_configPath);
            if (writeTime == m_configWriteTime) {
                return;
            }

            const bool wasEnabled = m_enabled;
            m_config = Config::load(m_configPath);
            m_configWriteTime = writeTime;
            m_enabled = m_config.enabled;
            m_injector.setUseScanCode(m_config.useScanCode);
            if (wasEnabled && !m_enabled) {
                m_injector.releaseAll();
            }
            LOG_INFO("Config reloaded");
            logMouseSettings(m_config);
        } catch (const std::exception& ex) {
            LOG_ERROR("Config reload failed: %s", ex.what());
        }
    }

    void shutdown() {
        LOG_INFO("PadCoder shutdown");
        m_injector.releaseAll();
        closeController();
        timeEndPeriod(1);
        SDL_Quit();
    }

    Config m_config;
    const std::string m_configPath = "padcoder.json";
    std::filesystem::file_time_type m_configWriteTime{};
    bool m_enabled = true;

    InputInjector m_injector;
    MappingEngine m_mapping;

    SDL_Gamepad* m_gamepad = nullptr;
    SDL_JoystickID m_gamepadId = 0;

    TriggerState m_l2;
    TriggerState m_r2;
    StickDirection m_leftDirection = StickDirection::None;
    StickDirection m_leftStickHeldDirection = StickDirection::None;
    ActiveModifiers m_heldStickMods;
    double m_scrollNextMs = 0.0;

    float m_rightNeutralX = 0.0f;
    float m_rightNeutralY = 0.0f;
    double m_mouseAccumX = 0.0;
    double m_mouseAccumY = 0.0;
    double m_mouseFreezeUntilMs = 0.0;

    bool m_leftMouseDown = false;
    bool m_rightMouseDown = false;

    TouchpadState m_touchpad;

    double m_disableHoldStartMs = 0.0;
    bool m_disableToggleArmed = true;

    const std::array<SDL_GamepadButton, 8> m_actionButtons = {
        SDL_GAMEPAD_BUTTON_DPAD_UP,
        SDL_GAMEPAD_BUTTON_DPAD_RIGHT,
        SDL_GAMEPAD_BUTTON_WEST,
        SDL_GAMEPAD_BUTTON_NORTH,
        SDL_GAMEPAD_BUTTON_DPAD_LEFT,
        SDL_GAMEPAD_BUTTON_DPAD_DOWN,
        SDL_GAMEPAD_BUTTON_SOUTH,
        SDL_GAMEPAD_BUTTON_EAST,
    };
    std::array<ButtonHold, 8> m_holds{};
    bool m_debugAltTab = false;
};

void tapSequence(InputInjector& injector, const std::vector<std::pair<PhysicalKey, bool>>& sequence) {
    for (const auto& [key, shift] : sequence) {
        injector.keyTap(key, shift);
        Sleep(25);
    }
}

void runSelfTest(const Config& config) {
    std::printf("PadCoder self-test starts in 2 seconds. Focus Notepad now.\n");
    Sleep(2000);

    InputInjector injector;
    injector.setUseScanCode(config.useScanCode);
    tapSequence(injector, {
        {PhysicalKey::A, false},
        {PhysicalKey::I, false},
        {PhysicalKey::N, false},
        {PhysicalKey::T, false},
        {PhysicalKey::S, false},
        {PhysicalKey::Num1, false},
        {PhysicalKey::Num2, false},
        {PhysicalKey::Num9, false},
        {PhysicalKey::Num0, false},
        {PhysicalKey::Comma, false},
        {PhysicalKey::Period, false},
        {PhysicalKey::Minus, false},
        {PhysicalKey::Equals, false},
        {PhysicalKey::Slash, false},
        {PhysicalKey::Semicolon, false},
        {PhysicalKey::Apostrophe, false},
        {PhysicalKey::LeftBracket, false},
        {PhysicalKey::RightBracket, false},
        {PhysicalKey::Backslash, false},
        {PhysicalKey::Grave, false},
    });
    injector.releaseAll();
    LOG_INFO("Self-test complete");
}

void runShiftTest(const Config& config) {
    std::printf("PadCoder shift-test starts in 2 seconds. Focus Notepad now.\n");
    Sleep(2000);

    InputInjector injector;
    injector.setUseScanCode(config.useScanCode);
    tapSequence(injector, {
        {PhysicalKey::A, true},
        {PhysicalKey::Num1, true},
        {PhysicalKey::Num9, true},
        {PhysicalKey::Num0, true},
        {PhysicalKey::Comma, true},
        {PhysicalKey::Period, true},
        {PhysicalKey::Minus, true},
        {PhysicalKey::Equals, true},
        {PhysicalKey::Slash, true},
        {PhysicalKey::Semicolon, true},
        {PhysicalKey::Apostrophe, true},
        {PhysicalKey::LeftBracket, true},
        {PhysicalKey::RightBracket, true},
        {PhysicalKey::Backslash, true},
        {PhysicalKey::Grave, true},
    });
    injector.releaseAll();
    LOG_INFO("Shift-test complete");
}

void printLayerTest() {
    MappingEngine mapping;
    const std::array<Layer, 7> layers = {
        Layer::Base, Layer::L1, Layer::R1, Layer::L2, Layer::R2, Layer::L1R1, Layer::L2R2
    };

    std::printf("PadCoder mapping table\n");
    LOG_INFO("PadCoder mapping table");
    for (Layer layer : layers) {
        std::printf("\n[%s]\n", MappingEngine::layerName(layer));
        LOG_INFO("[%s]", MappingEngine::layerName(layer));
        for (int i = 0; i < 8; ++i) {
            const auto action = static_cast<ActionButton>(i);
            const PhysicalKey key = mapping.lookupKey(layer, action);
            std::printf("  %-9s -> %s\n", MappingEngine::actionName(action), MappingEngine::keyName(key));
            LOG_INFO("  %s -> %s", MappingEngine::actionName(action), MappingEngine::keyName(key));
        }
    }

    std::printf("\nReserved combinations: L1+R1, L2+R2, L1+R2, R1+L2, and unsupported combos output None.\n");
    const auto printResolutionCheck = [&](const char* label, bool l1, bool r1, bool l2, bool r2, ActionButton action) {
        const Layer layer = mapping.resolveLayer(l1, r1, l2, r2);
        const PhysicalKey key = mapping.lookupKey(layer, action);
        std::printf("%s -> %s\n", label, MappingEngine::keyName(key));
        LOG_INFO("%s -> %s", label, MappingEngine::keyName(key));
    };

    std::printf("\nResolution checks\n");
    printResolutionCheck("R1+R2 + Square", false, true, false, true, ActionButton::Square);
    printResolutionCheck("R1+R2 + Triangle", false, true, false, true, ActionButton::Triangle);
    printResolutionCheck("R1+R2 + Left", false, true, false, true, ActionButton::Left);
    printResolutionCheck("L1+L2 + Up", true, false, true, false, ActionButton::Up);
    printResolutionCheck("L1+L2 + Square", true, false, true, false, ActionButton::Square);
    printResolutionCheck("L1+R1 + Square", true, true, false, false, ActionButton::Square);
    printResolutionCheck("L2+R2 + Square", false, false, true, true, ActionButton::Square);
    LOG_INFO("Layer-test complete");
}

} // namespace

int main(int argc, char** argv) {
    Logger::instance().init("logs");
    Logger::instance().setConsoleOutput(hasArg(argc, argv, "--layer-test"));
    Logger::instance().setMinLevel(LogLevel::Info);

    LOG_INFO("Command line: %s", joinArgs(argc, argv).c_str());

    Config config = Config::load();

    if (hasArg(argc, argv, "--layer-test")) {
        printLayerTest();
        Logger::instance().shutdown();
        return 0;
    }

    if (hasArg(argc, argv, "--shift-test")) {
        runShiftTest(config);
        Logger::instance().shutdown();
        return 0;
    }

    if (hasArg(argc, argv, "--test")) {
        runSelfTest(config);
        Logger::instance().shutdown();
        return 0;
    }

    bool debugAltTab = hasArg(argc, argv, "--debug-alt-tab");
    PadCoderApp app(config, debugAltTab);
    const int result = app.run();
    Logger::instance().shutdown();
    return result;
}
