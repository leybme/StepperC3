#include "board_prefs.h"
#include <Preferences.h>

uint8_t g_board_id = 0;

static const char *NVS_NS = "stepper";

uint8_t board_prefs_load()
{
    Preferences prefs;
    prefs.begin(NVS_NS, /*readOnly=*/true);
    g_board_id = prefs.getUChar("board_id", 0);
    prefs.end();
    return g_board_id;
}

void board_prefs_save_id(uint8_t id)
{
    g_board_id = id;
    Preferences prefs;
    prefs.begin(NVS_NS, /*readOnly=*/false);
    prefs.putUChar("board_id", id);
    prefs.end();
}
