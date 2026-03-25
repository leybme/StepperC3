#include "usb_task.h"
#include "motor_ctrl.h"
#include "board_prefs.h"
#include "uart_task.h"
#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <string.h>
#include <stdlib.h>

// ─── USB CDC Command Protocol ─────────────────────────────────────────────────
//
//  Format (ASCII, newline-terminated):
//    <id> <COMMAND> [arg1] [arg2]
//
//  The board ID is always the FIRST token.
//  If id == this board's ID  → execute command locally.
//  If id != this board's ID  → forward raw line to UART OUT (downstream).
//
//  Special commands (no ID prefix, always local):
//    SETID  <new_id>   Set this board's ID (0-7, saved to NVS)
//    HELP              Print command reference
//
//  COMMAND     ARGS          DESCRIPTION
//  ──────────  ────────────  ──────────────────────────────────────────────
//  MOVETO      <pos>         Absolute move to step position
//  MOVE        <steps>       Relative move (+/− steps)
//  GOHOME                    Return to position 0
//  FINDHOME                  Run homing sequence (stall detection)
//  SETPOS      <pos>         Redefine current position (no motion)
//  FLIPDIR                   Toggle direction polarity
//  SETSTEP     <ustep>       Set microsteps (1/2/4/8/16/32/64/256)
//  SETCUR      <mA>          Set RMS current (mA)
    //  SETSPD <hz>          Set cruise speed in steps/second
//  SETACCEL <steps/s²>  Set acceleration (0 → 100)
//  ENABLE                    Enable stepper driver output
//  DISABLE                   Disable stepper driver output
//  STOP                      Emergency stop, clear queue
//  STATUS                    Query full status
//
//  Responses:
//    OK <COMMAND> <id>
//    STATUS <id> pos=<n> tgt=<n> state=<s> en=<0/1> flip=<0/1>
//           step=<n> cur=<n>mA spd=<n>us accel=<n>
//    ERR <reason>
// ─────────────────────────────────────────────────────────────────────────────

static void send_ok(const char *cmd, int id = -1) {
    if (id >= 0) Serial.printf("OK %s %d\r\n", cmd, id);
    else         Serial.printf("OK %s\r\n", cmd);
}

static void send_err(const char *reason) {
    Serial.printf("ERR %s\r\n", reason);
}

static const char *state_str(MotorState s) {
    switch (s) {
        case MotorState::MS_IDLE:     return "IDLE";
        case MotorState::MS_MOVING:   return "MOVING";
        case MotorState::MS_HOMING:   return "HOMING";
        case MotorState::MS_STALLED:  return "STALLED";
        case MotorState::MS_DISABLED: return "DISABLED";
        case MotorState::MS_ERROR:    return "ERROR";
        default:                   return "?";
    }
}

void process_cmd(char *line) {
    // Save raw line before strtok mutates it (needed for forwarding)
    static char raw[256];
    strncpy(raw, line, sizeof(raw) - 2);
    raw[sizeof(raw) - 2] = '\0';
    // Ensure forwarded line ends with \n
    size_t rawlen = strlen(raw);
    if (rawlen == 0 || raw[rawlen - 1] != '\n') { raw[rawlen++] = '\n'; raw[rawlen] = '\0'; }

    char *tok[8] = {};
    int   n      = 0;
    char *p      = strtok(line, " \t\r\n");
    while (p && n < 8) { tok[n++] = p; p = strtok(nullptr, " \t\r\n"); }
    if (n == 0) return;

    // ── HELP (no ID prefix) ───────────────────────────────────────────────────
    if (strcasecmp(tok[0], "HELP") == 0) {
        Serial.print(
            "\r\nFormat: <id> <COMMAND> [args]\r\n"
            "  ID matches this board → execute locally\r\n"
            "  ID does not match     → forwarded downstream\r\n"
            "\r\nLocal-only (no ID prefix):\r\n"
            "  SETID  <start_id>     Set this board to <start_id>, forward SETID <start_id+1>\r\n"
            "                        downstream — auto-numbers the whole chain in one command.\r\n"
            "  HELP                  This message\r\n"
            "\r\nCommands (prefix with board ID):\r\n"
            "  <id> MOVETO   <pos>   Absolute move (steps)\r\n"
            "  <id> MOVE     <steps> Relative move (+/-)\r\n"
            "  <id> GOHOME          Return to position 0\r\n"
            "  <id> FINDHOME        Run homing sequence\r\n"
            "  <id> SETPOS   <pos>  Redefine current position (no motion)\r\n"
            "  <id> FLIPDIR         Toggle direction polarity\r\n"
            "  <id> SETSTEP  <us>   Microsteps: 1/2/4/8/16/32/64/256\r\n"
            "  <id> SETCUR   <mA>   RMS current (mA)\r\n"
            "  <id> SETSPD   <hz>  Speed in steps/second (higher = faster)\r\n"
            "  <id> SETACCEL <n>   Acceleration steps/s\u00b2 (0 \u2192 100)\r\n"
            "  <id> ENABLE          Enable driver\r\n"
            "  <id> DISABLE         Disable driver\r\n"
            "  <id> STOP            Emergency stop\r\n"
            "  <id> STATUS          Query status\r\n"
        );
        Serial.printf("This Board ID: %d\r\n", g_board_id);
        return;
    }

    // ── SETID (no ID prefix) ──────────────────────────────────────────────────
    // Auto-chain: sets this board to <new_id>, then forwards SETID <new_id+1>
    // downstream so each board in the chain gets a unique sequential ID.
    // Example: send "SETID 0" to the first board → board0=0, board1=1, board2=2 …
    if (strcasecmp(tok[0], "SETID") == 0) {
        if (n < 2) { send_err("SETID needs <new_id>"); return; }
        uint8_t id = (uint8_t)atoi(tok[1]);
        if (id > 7) { send_err("id must be 0-7"); return; }
        board_prefs_save_id(id);
        motor_setID(id);
        send_ok("SETID", id);
        // Forward SETID <id+1> to the next board in the chain
        if (id < 7) {
            char fwd[16];
            int  flen = snprintf(fwd, sizeof(fwd), "SETID %d\n", id + 1);
            uart_forward((const uint8_t *)fwd, (size_t)flen);
        }
        return;
    }

    // ── All other commands: <id> <COMMAND> [args...] ──────────────────────────
    if (n < 2) { send_err("format: <id> <COMMAND> [args]"); return; }
    uint8_t id  = (uint8_t)atoi(tok[0]);
    const char *cmd = tok[1];

    // ID mismatch → forward raw line downstream
    if (id != g_board_id) {
        uart_forward((const uint8_t *)raw, rawlen);
        return;
    }

    // ── STATUS ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "STATUS") == 0) {
        MotorStatus st;
        if (!motor_getStatus(id, st)) { send_err("wrong id"); return; }
        Serial.printf(
            "STATUS %d pos=%ld tgt=%ld state=%s en=%d flip=%d"
            " step=%u cur=%umA spd=%luhz accel=%lu\r\n",
            st.id, (long)st.position, (long)st.target, state_str(st.state),
            (int)st.enabled, (int)st.dirFlipped,
            st.microsteps, st.currentMA,
            (unsigned long)st.speedHz, (unsigned long)st.accelHz);
        return;
    }

    // ── MOVETO ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "MOVETO") == 0) {
        if (n < 3) { send_err("MOVETO needs <pos>"); return; }
        motor_moveTo(id, atol(tok[2]));
        send_ok("MOVETO", id); return;
    }

    // ── MOVE ──────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "MOVE") == 0) {
        if (n < 3) { send_err("MOVE needs <steps>"); return; }
        motor_move(id, atol(tok[2]));
        send_ok("MOVE", id); return;
    }

    // ── GOHOME ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "GOHOME") == 0) {
        motor_goHome(id); send_ok("GOHOME", id); return;
    }

    // ── FINDHOME ──────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "FINDHOME") == 0) {
        motor_findHome(id); send_ok("FINDHOME", id); return;
    }

    // ── STOP ──────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "STOP") == 0) {
        motor_stop(id); send_ok("STOP", id); return;
    }

    // ── SETPOS ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETPOS") == 0) {
        if (n < 3) { send_err("SETPOS needs <pos>"); return; }
        motor_setPosition(id, atol(tok[2]));
        send_ok("SETPOS", id); return;
    }

    // ── FLIPDIR ───────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "FLIPDIR") == 0) {
        motor_flipDir(id); send_ok("FLIPDIR", id); return;
    }

    // ── SETSTEP ───────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETSTEP") == 0) {
        if (n < 3) { send_err("SETSTEP needs <ustep>"); return; }
        motor_setStepSize(id, (uint16_t)atoi(tok[2]));
        send_ok("SETSTEP", id); return;
    }

    // ── SETCUR ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETCUR") == 0) {
        if (n < 3) { send_err("SETCUR needs <mA>"); return; }
        motor_setCurrent(id, (uint16_t)atoi(tok[2]));
        send_ok("SETCUR", id); return;
    }

    // ── SETSPD ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETSPD") == 0) {
        if (n < 3) { send_err("SETSPD needs <hz>"); return; }
        motor_setSpeed(id, (uint32_t)atol(tok[2]));
        send_ok("SETSPD", id); return;
    }

    // ── SETACCEL ──────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETACCEL") == 0) {
        if (n < 3) { send_err("SETACCEL needs <steps/s2>"); return; }
        motor_setAccel(id, (uint32_t)atol(tok[2]));
        send_ok("SETACCEL", id); return;
    }

    // ── ENABLE / DISABLE ──────────────────────────────────────────────────────
    if (strcasecmp(cmd, "ENABLE") == 0)  { motor_enable(id, true);  send_ok("ENABLE",  id); return; }
    if (strcasecmp(cmd, "DISABLE") == 0) { motor_enable(id, false); send_ok("DISABLE", id); return; }

    send_err("unknown command — send HELP");
}

// ─── FreeRTOS USB task ───────────────────────────────────────────────────────
static void usbTask(void *) {
    static char buf[256];
    uint16_t    idx = 0;

    for (;;) {
        while (Serial.available()) {
            char c = (char)Serial.read();
            if (idx < sizeof(buf) - 1) buf[idx++] = c;
            if (c == '\n') {
                buf[idx] = '\0';
                process_cmd(buf);
                idx = 0;
            }
        }
        vTaskDelay(pdMS_TO_TICKS(10));
    }
}

void usb_task_start() {
    xTaskCreate(usbTask, "usb", 4096, nullptr, 2, nullptr);
}
