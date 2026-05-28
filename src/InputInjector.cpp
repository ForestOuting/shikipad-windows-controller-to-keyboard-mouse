#include "InputInjector.h"

#include "Logger.h"

#include <array>
#include <cstddef>
#include <cstdint>

InputInjector::InputInjector() {
    m_keyboardLayout = GetKeyboardLayout(0);
    initKeyMappings();
    initModifiers();
}

void InputInjector::addMapping(PhysicalKey key, WORD vk, bool extended) {
    VKMapping mapping;
    mapping.vk = vk;
    mapping.extended = extended;

    const UINT raw = MapVirtualKeyExW(vk, MAPVK_VK_TO_VSC_EX, m_keyboardLayout);
    mapping.scanCode = static_cast<WORD>(raw & 0xFF);
    if ((raw & 0xFF00U) != 0) {
        mapping.extended = true;
    }

    m_keyMappings[key] = mapping;
}

void InputInjector::initKeyMappings() {
    addMapping(PhysicalKey::A, 'A');
    addMapping(PhysicalKey::B, 'B');
    addMapping(PhysicalKey::C, 'C');
    addMapping(PhysicalKey::D, 'D');
    addMapping(PhysicalKey::E, 'E');
    addMapping(PhysicalKey::F, 'F');
    addMapping(PhysicalKey::G, 'G');
    addMapping(PhysicalKey::H, 'H');
    addMapping(PhysicalKey::I, 'I');
    addMapping(PhysicalKey::J, 'J');
    addMapping(PhysicalKey::K, 'K');
    addMapping(PhysicalKey::L, 'L');
    addMapping(PhysicalKey::M, 'M');
    addMapping(PhysicalKey::N, 'N');
    addMapping(PhysicalKey::O, 'O');
    addMapping(PhysicalKey::P, 'P');
    addMapping(PhysicalKey::Q, 'Q');
    addMapping(PhysicalKey::R, 'R');
    addMapping(PhysicalKey::S, 'S');
    addMapping(PhysicalKey::T, 'T');
    addMapping(PhysicalKey::U, 'U');
    addMapping(PhysicalKey::V, 'V');
    addMapping(PhysicalKey::W, 'W');
    addMapping(PhysicalKey::X, 'X');
    addMapping(PhysicalKey::Y, 'Y');
    addMapping(PhysicalKey::Z, 'Z');

    addMapping(PhysicalKey::Num0, '0');
    addMapping(PhysicalKey::Num1, '1');
    addMapping(PhysicalKey::Num2, '2');
    addMapping(PhysicalKey::Num3, '3');
    addMapping(PhysicalKey::Num4, '4');
    addMapping(PhysicalKey::Num5, '5');
    addMapping(PhysicalKey::Num6, '6');
    addMapping(PhysicalKey::Num7, '7');
    addMapping(PhysicalKey::Num8, '8');
    addMapping(PhysicalKey::Num9, '9');

    addMapping(PhysicalKey::Minus, VK_OEM_MINUS);
    addMapping(PhysicalKey::Equals, VK_OEM_PLUS);
    addMapping(PhysicalKey::LeftBracket, VK_OEM_4);
    addMapping(PhysicalKey::RightBracket, VK_OEM_6);
    addMapping(PhysicalKey::Backslash, VK_OEM_5);
    addMapping(PhysicalKey::Semicolon, VK_OEM_1);
    addMapping(PhysicalKey::Apostrophe, VK_OEM_7);
    addMapping(PhysicalKey::Comma, VK_OEM_COMMA);
    addMapping(PhysicalKey::Period, VK_OEM_PERIOD);
    addMapping(PhysicalKey::Slash, VK_OEM_2);
    addMapping(PhysicalKey::Grave, VK_OEM_3);

    addMapping(PhysicalKey::Space, VK_SPACE);
    addMapping(PhysicalKey::Backspace, VK_BACK);
    addMapping(PhysicalKey::Enter, VK_RETURN);
    addMapping(PhysicalKey::Tab, VK_TAB);
    addMapping(PhysicalKey::Escape, VK_ESCAPE);

    addMapping(PhysicalKey::ArrowUp, VK_UP, true);
    addMapping(PhysicalKey::ArrowDown, VK_DOWN, true);
    addMapping(PhysicalKey::ArrowLeft, VK_LEFT, true);
    addMapping(PhysicalKey::ArrowRight, VK_RIGHT, true);
}

void InputInjector::initModifiers() {
    auto resolve = [this](WORD vk) {
        ModInfo info;
        info.vk = vk;
        const UINT raw = MapVirtualKeyExW(vk, MAPVK_VK_TO_VSC_EX, m_keyboardLayout);
        info.scanCode = static_cast<WORD>(raw & 0xFF);
        info.extended = (raw & 0xFF00U) != 0;
        return info;
    };

    m_shift = resolve(VK_LSHIFT);
    m_ctrl = resolve(VK_LCONTROL);
    m_alt = resolve(VK_LMENU);
    m_win = resolve(VK_LWIN);
    m_win.extended = true;
}

void InputInjector::addKeyInput(std::vector<INPUT>& inputs, WORD vk, WORD scanCode, bool extended, bool up) const {
    INPUT input{};
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = m_useScanCode ? 0 : vk;
    input.ki.wScan = scanCode;
    input.ki.dwFlags = up ? KEYEVENTF_KEYUP : 0;
    if (m_useScanCode) {
        input.ki.dwFlags |= KEYEVENTF_SCANCODE;
    }
    if (extended) {
        input.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
    }
    inputs.push_back(input);
}

void InputInjector::keyTap(PhysicalKey key, bool shift, bool ctrl, bool alt, bool win) {
    if (key == PhysicalKey::None) {
        return;
    }

    const auto found = m_keyMappings.find(key);
    if (found == m_keyMappings.end()) {
        LOG_ERROR("No injection mapping for key '%s'", MappingEngine::keyName(key));
        return;
    }

    const VKMapping& target = found->second;
    std::vector<INPUT> inputs;
    inputs.reserve(10);

    if (shift) {
        addKeyInput(inputs, m_shift.vk, m_shift.scanCode, m_shift.extended, false);
    }
    if (ctrl) {
        addKeyInput(inputs, m_ctrl.vk, m_ctrl.scanCode, m_ctrl.extended, false);
    }
    if (alt) {
        addKeyInput(inputs, m_alt.vk, m_alt.scanCode, m_alt.extended, false);
    }
    if (win) {
        addKeyInput(inputs, m_win.vk, m_win.scanCode, m_win.extended, false);
    }

    addKeyInput(inputs, target.vk, target.scanCode, target.extended, false);
    addKeyInput(inputs, target.vk, target.scanCode, target.extended, true);

    if (win) {
        addKeyInput(inputs, m_win.vk, m_win.scanCode, m_win.extended, true);
    }
    if (alt) {
        addKeyInput(inputs, m_alt.vk, m_alt.scanCode, m_alt.extended, true);
    }
    if (ctrl) {
        addKeyInput(inputs, m_ctrl.vk, m_ctrl.scanCode, m_ctrl.extended, true);
    }
    if (shift) {
        addKeyInput(inputs, m_shift.vk, m_shift.scanCode, m_shift.extended, true);
    }

    if (!inputs.empty()) {
        const UINT sent = SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
        if (static_cast<std::size_t>(sent) != inputs.size()) {
            LOG_ERROR("SendInput sent %u of %zu keyboard inputs", sent, inputs.size());
        }
    }
}

void InputInjector::keyDown(PhysicalKey key) {
    const auto found = m_keyMappings.find(key);
    if (key == PhysicalKey::None || found == m_keyMappings.end()) {
        return;
    }

    std::vector<INPUT> inputs;
    const VKMapping& target = found->second;
    addKeyInput(inputs, target.vk, target.scanCode, target.extended, false);
    SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
}

void InputInjector::keyUp(PhysicalKey key) {
    const auto found = m_keyMappings.find(key);
    if (key == PhysicalKey::None || found == m_keyMappings.end()) {
        return;
    }

    std::vector<INPUT> inputs;
    const VKMapping& target = found->second;
    addKeyInput(inputs, target.vk, target.scanCode, target.extended, true);
    SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
}

const InputInjector::ModInfo& InputInjector::modifierInfo(HeldModifier modifier) const {
    switch (modifier) {
        case HeldModifier::Shift: return m_shift;
        case HeldModifier::Ctrl: return m_ctrl;
        case HeldModifier::Alt: return m_alt;
        case HeldModifier::Win: return m_win;
        default: return m_shift;
    }
}

void InputInjector::modifierDown(HeldModifier modifier) {
    const ModInfo& info = modifierInfo(modifier);
    std::vector<INPUT> inputs;
    addKeyInput(inputs, info.vk, info.scanCode, info.extended, false);
    SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
}

void InputInjector::modifierUp(HeldModifier modifier) {
    const ModInfo& info = modifierInfo(modifier);
    std::vector<INPUT> inputs;
    addKeyInput(inputs, info.vk, info.scanCode, info.extended, true);
    SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
}

void InputInjector::mouseMove(int dx, int dy) {
    if (dx == 0 && dy == 0) {
        return;
    }

    INPUT input{};
    input.type = INPUT_MOUSE;
    input.mi.dwFlags = MOUSEEVENTF_MOVE;
    input.mi.dx = dx;
    input.mi.dy = dy;
    SendInput(1, &input, sizeof(INPUT));
}

void InputInjector::mouseButtonDown(int button) {
    INPUT input{};
    input.type = INPUT_MOUSE;
    input.mi.dwFlags = button == 0 ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_RIGHTDOWN;
    SendInput(1, &input, sizeof(INPUT));
}

void InputInjector::mouseButtonUp(int button) {
    INPUT input{};
    input.type = INPUT_MOUSE;
    input.mi.dwFlags = button == 0 ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_RIGHTUP;
    SendInput(1, &input, sizeof(INPUT));
}

void InputInjector::mouseWheel(int delta) {
    if (delta == 0) {
        return;
    }

    INPUT input{};
    input.type = INPUT_MOUSE;
    input.mi.dwFlags = MOUSEEVENTF_WHEEL;
    input.mi.mouseData = static_cast<DWORD>(delta * WHEEL_DELTA);
    SendInput(1, &input, sizeof(INPUT));
}

void InputInjector::releaseAll() {
    std::vector<INPUT> inputs;
    inputs.reserve(6);

    addKeyInput(inputs, m_shift.vk, m_shift.scanCode, m_shift.extended, true);
    addKeyInput(inputs, m_ctrl.vk, m_ctrl.scanCode, m_ctrl.extended, true);
    addKeyInput(inputs, m_alt.vk, m_alt.scanCode, m_alt.extended, true);
    addKeyInput(inputs, m_win.vk, m_win.scanCode, m_win.extended, true);

    INPUT left{};
    left.type = INPUT_MOUSE;
    left.mi.dwFlags = MOUSEEVENTF_LEFTUP;
    inputs.push_back(left);

    INPUT right{};
    right.type = INPUT_MOUSE;
    right.mi.dwFlags = MOUSEEVENTF_RIGHTUP;
    inputs.push_back(right);

    SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
}

const VKMapping* InputInjector::getMapping(PhysicalKey key) const {
    const auto found = m_keyMappings.find(key);
    if (found == m_keyMappings.end()) {
        return nullptr;
    }
    return &found->second;
}
