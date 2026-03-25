#pragma once
#include <stdint.h>

// ─── Board Preferences ────────────────────────────────────────────────────────
// Persistent board settings stored in NVS (ESP32 Preferences / flash).
// Call board_prefs_load() once at boot before any tasks start.

// Board ID (0-7): identifies this node in the motor chain.
extern uint8_t g_board_id;

// Load all board preferences from NVS.
// Returns the board ID; all other globals are also populated.
uint8_t board_prefs_load();

// Save board ID to NVS.
void board_prefs_save_id(uint8_t id);
