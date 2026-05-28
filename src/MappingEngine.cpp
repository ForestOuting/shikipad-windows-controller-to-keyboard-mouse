#include "MappingEngine.h"

MappingEngine::MappingEngine() {
    initMappingTables();
}

void MappingEngine::initMappingTables() {
    using PK = PhysicalKey;

    m_tables[static_cast<int>(Layer::Base)] = {{
        PK::ArrowUp, PK::ArrowRight, PK::Space, PK::Backspace,
        PK::ArrowLeft, PK::ArrowDown, PK::Enter, PK::Tab
    }};

    m_tables[static_cast<int>(Layer::L1)] = {{
        PK::S, PK::R, PK::D, PK::G,
        PK::L, PK::C, PK::Y, PK::Z
    }};

    m_tables[static_cast<int>(Layer::R1)] = {{
        PK::I, PK::N, PK::E, PK::A,
        PK::O, PK::T, PK::H, PK::U
    }};

    m_tables[static_cast<int>(Layer::L2)] = {{
        PK::K, PK::V, PK::Num1, PK::Num2,
        PK::Num3, PK::Num4, PK::Num5, PK::Num6
    }};

    m_tables[static_cast<int>(Layer::R2)] = {{
        PK::M, PK::W, PK::J, PK::X,
        PK::Q, PK::F, PK::P, PK::B
    }};

    m_tables[static_cast<int>(Layer::L1R1)] = {{
        PK::Num7, PK::Num8, PK::Num9, PK::Num0,
        PK::Minus, PK::Equals, PK::Comma, PK::Period
    }};

    m_tables[static_cast<int>(Layer::L2R2)] = {{
        PK::Apostrophe, PK::Slash, PK::Semicolon, PK::LeftBracket,
        PK::RightBracket, PK::Backslash, PK::Grave, PK::None
    }};
}

Layer MappingEngine::resolveLayer(bool l1, bool r1, bool l2, bool r2) const {
    if (r1 && r2 && !l1 && !l2) {
        return Layer::L1R1;
    }
    if (l1 && l2 && !r1 && !r2) {
        return Layer::L2R2;
    }

    if (l1 && !r1 && !l2 && !r2) {
        return Layer::L1;
    }
    if (r1 && !l1 && !l2 && !r2) {
        return Layer::R1;
    }
    if (l2 && !l1 && !r1 && !r2) {
        return Layer::L2;
    }
    if (r2 && !l1 && !r1 && !l2) {
        return Layer::R2;
    }

    if (!l1 && !r1 && !l2 && !r2) {
        return Layer::Base;
    }

    return Layer::Reserved;
}

PhysicalKey MappingEngine::lookupKey(Layer layer, ActionButton action) const {
    if (layer == Layer::Reserved) {
        return PhysicalKey::None;
    }

    const int layerIndex = static_cast<int>(layer);
    const int actionIndex = static_cast<int>(action);
    if (layerIndex < 0 || layerIndex >= static_cast<int>(m_tables.size()) ||
        actionIndex < 0 || actionIndex >= 8) {
        return PhysicalKey::None;
    }

    return m_tables[layerIndex][actionIndex];
}

const std::array<PhysicalKey, 8>& MappingEngine::getLayerTable(Layer layer) const {
    static constexpr std::array<PhysicalKey, 8> empty = {
        PhysicalKey::None, PhysicalKey::None, PhysicalKey::None, PhysicalKey::None,
        PhysicalKey::None, PhysicalKey::None, PhysicalKey::None, PhysicalKey::None
    };

    const int layerIndex = static_cast<int>(layer);
    if (layerIndex < 0 || layerIndex >= static_cast<int>(m_tables.size())) {
        return empty;
    }

    return m_tables[layerIndex];
}

const char* MappingEngine::keyName(PhysicalKey key) {
    switch (key) {
        case PhysicalKey::None: return "None";
        case PhysicalKey::A: return "A";
        case PhysicalKey::B: return "B";
        case PhysicalKey::C: return "C";
        case PhysicalKey::D: return "D";
        case PhysicalKey::E: return "E";
        case PhysicalKey::F: return "F";
        case PhysicalKey::G: return "G";
        case PhysicalKey::H: return "H";
        case PhysicalKey::I: return "I";
        case PhysicalKey::J: return "J";
        case PhysicalKey::K: return "K";
        case PhysicalKey::L: return "L";
        case PhysicalKey::M: return "M";
        case PhysicalKey::N: return "N";
        case PhysicalKey::O: return "O";
        case PhysicalKey::P: return "P";
        case PhysicalKey::Q: return "Q";
        case PhysicalKey::R: return "R";
        case PhysicalKey::S: return "S";
        case PhysicalKey::T: return "T";
        case PhysicalKey::U: return "U";
        case PhysicalKey::V: return "V";
        case PhysicalKey::W: return "W";
        case PhysicalKey::X: return "X";
        case PhysicalKey::Y: return "Y";
        case PhysicalKey::Z: return "Z";
        case PhysicalKey::Num0: return "0";
        case PhysicalKey::Num1: return "1";
        case PhysicalKey::Num2: return "2";
        case PhysicalKey::Num3: return "3";
        case PhysicalKey::Num4: return "4";
        case PhysicalKey::Num5: return "5";
        case PhysicalKey::Num6: return "6";
        case PhysicalKey::Num7: return "7";
        case PhysicalKey::Num8: return "8";
        case PhysicalKey::Num9: return "9";
        case PhysicalKey::Minus: return "Minus";
        case PhysicalKey::Equals: return "Equals";
        case PhysicalKey::LeftBracket: return "LeftBracket";
        case PhysicalKey::RightBracket: return "RightBracket";
        case PhysicalKey::Backslash: return "Backslash";
        case PhysicalKey::Semicolon: return "Semicolon";
        case PhysicalKey::Apostrophe: return "Apostrophe";
        case PhysicalKey::Comma: return "Comma";
        case PhysicalKey::Period: return "Period";
        case PhysicalKey::Slash: return "Slash";
        case PhysicalKey::Grave: return "Grave";
        case PhysicalKey::Space: return "Space";
        case PhysicalKey::Backspace: return "Backspace";
        case PhysicalKey::Enter: return "Enter";
        case PhysicalKey::Tab: return "Tab";
        case PhysicalKey::Escape: return "Escape";
        case PhysicalKey::ArrowUp: return "ArrowUp";
        case PhysicalKey::ArrowDown: return "ArrowDown";
        case PhysicalKey::ArrowLeft: return "ArrowLeft";
        case PhysicalKey::ArrowRight: return "ArrowRight";
        default: return "Unknown";
    }
}

const char* MappingEngine::layerName(Layer layer) {
    switch (layer) {
        case Layer::Base: return "Base";
        case Layer::L1: return "L1";
        case Layer::R1: return "R1";
        case Layer::L2: return "L2";
        case Layer::R2: return "R2";
        case Layer::L1R1: return "R1+R2";
        case Layer::L2R2: return "L1+L2";
        case Layer::Reserved: return "Reserved";
        default: return "Unknown";
    }
}

const char* MappingEngine::actionName(ActionButton action) {
    switch (action) {
        case ActionButton::Up: return "Up";
        case ActionButton::Right: return "Right";
        case ActionButton::Square: return "Square";
        case ActionButton::Triangle: return "Triangle";
        case ActionButton::Down: return "Down";
        case ActionButton::Cross: return "Cross";
        case ActionButton::Left: return "Left";
        case ActionButton::Circle: return "Circle";
        default: return "Unknown";
    }
}
