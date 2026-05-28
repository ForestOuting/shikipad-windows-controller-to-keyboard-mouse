#pragma once

#include <array>
#include <cstdint>

enum class PhysicalKey : std::uint8_t {
    None = 0,

    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    Num0, Num1, Num2, Num3, Num4, Num5, Num6, Num7, Num8, Num9,

    Minus,
    Equals,
    LeftBracket,
    RightBracket,
    Backslash,
    Semicolon,
    Apostrophe,
    Comma,
    Period,
    Slash,
    Grave,

    Space,
    Backspace,
    Enter,
    Tab,
    Escape,

    ArrowUp,
    ArrowDown,
    ArrowLeft,
    ArrowRight,

    COUNT
};

enum class Layer : std::uint8_t {
    Base = 0,
    L1,
    R1,
    L2,
    R2,
    L1R1,
    L2R2,
    Reserved,

    COUNT
};

enum class ActionButton : std::uint8_t {
    Up = 0,
    Right = 1,
    Square = 2,
    Triangle = 3,
    Left = 4,
    Down = 5,
    Cross = 6,
    Circle = 7,

    COUNT = 8
};

struct ActiveModifiers {
    bool shift = false;
    bool ctrl = false;
    bool alt = false;
    bool win = false;
};

class MappingEngine {
public:
    MappingEngine();

    Layer resolveLayer(bool l1, bool r1, bool l2, bool r2) const;
    PhysicalKey lookupKey(Layer layer, ActionButton action) const;
    const std::array<PhysicalKey, 8>& getLayerTable(Layer layer) const;

    static const char* keyName(PhysicalKey key);
    static const char* layerName(Layer layer);
    static const char* actionName(ActionButton action);

private:
    void initMappingTables();

    std::array<std::array<PhysicalKey, 8>, 7> m_tables{};
};
