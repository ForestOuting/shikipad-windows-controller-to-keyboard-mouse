#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

#include "MappingEngine.h"

#include <unordered_map>
#include <vector>

struct VKMapping {
    WORD vk = 0;
    WORD scanCode = 0;
    bool extended = false;
};

enum class HeldModifier {
    Shift,
    Ctrl,
    Alt,
    Win
};

class InputInjector {
public:
    InputInjector();

    void setUseScanCode(bool use) { m_useScanCode = use; }

    void keyTap(PhysicalKey key, bool shift = false, bool ctrl = false, bool alt = false, bool win = false);
    void keyDown(PhysicalKey key);
    void keyUp(PhysicalKey key);
    void modifierDown(HeldModifier modifier);
    void modifierUp(HeldModifier modifier);
    void mouseMove(int dx, int dy);
    void mouseButtonDown(int button);
    void mouseButtonUp(int button);
    void mouseWheel(int delta);
    void releaseAll();

    const VKMapping* getMapping(PhysicalKey key) const;

private:
    struct ModInfo {
        WORD vk = 0;
        WORD scanCode = 0;
        bool extended = false;
    };

    void initKeyMappings();
    void initModifiers();
    void addMapping(PhysicalKey key, WORD vk, bool extended = false);
    void addKeyInput(std::vector<INPUT>& inputs, WORD vk, WORD scanCode, bool extended, bool up) const;
    const ModInfo& modifierInfo(HeldModifier modifier) const;

    std::unordered_map<PhysicalKey, VKMapping> m_keyMappings;
    bool m_useScanCode = true;
    HKL m_keyboardLayout = nullptr;

    ModInfo m_shift;
    ModInfo m_ctrl;
    ModInfo m_alt;
    ModInfo m_win;
};
